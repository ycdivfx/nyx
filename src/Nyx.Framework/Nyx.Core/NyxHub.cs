/*
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 * The Original Code is Copyright (C) 2016 You can do it! VFX & JDBGraphics
 * All rights reserved.
 */
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ;
using Nyx.Core.Config;
using Nyx.Core.Extensions;
using Nyx.Core.Logging;
using Nyx.Core.Messaging;
using Nyx.Core.Network;
using Nyx.Core.Plugins;

namespace Nyx.Core
{
    /// <summary>
    /// The Hub, acts as a router for messages between Borgs/Clients of the Nyx System.
    /// The Hub can also have a mediator effect since it can filter messages and divert or cancel any message,
    /// or even have a high role as a file distributer.
    /// </summary>
    public sealed class NyxHub : INyxHub, IDisposable
    {
        private const string HubConfigMultithread = "hub_multithread.receive";
        private readonly ILogger<NyxHub> _logger;
        private Task _serverWorker;
        private readonly NetMQContext _context;
        private readonly object _syncLock = new object();
        private bool _isStarted;
        private readonly PluginManager _plugman;
        private readonly AutoResetEvent _serverEvent = new AutoResetEvent(false);
        private string _hostApp;
        private volatile bool _receiveMultithread;
        private readonly ISubject<NyxNodeStatus> _nodeStatusSubject = new Subject<NyxNodeStatus>();
        private readonly ISubject<Tuple<byte[], INyxMessage>> _inMessage = new Subject<Tuple<byte[], INyxMessage>>();
        private NetMQActor _hubActor;
        private readonly ISubject<MessageStatus> _inMessageStatus = new Subject<MessageStatus>();
        private readonly ISubject<MessageStatus> _outMessageStatus = new Subject<MessageStatus>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly CancellationTokenSource _serverCancelationSource = new CancellationTokenSource();
        private CancellationToken _serverCancelToken;

        public NyxHostMode HostMode
        {
            get { return NyxHostMode.None; }
            set {  }
        }

        /// <summary>
        /// Nyx Node service status
        /// </summary>
        public NyxNodeStatus Status { get; private set; }

        /// <summary>
        /// Hub running port.
        /// </summary>
        public int Port { get; set; } = 4015;

        public long TotalInMessages
        {
            get; private set; }

        public long TotalOutMessages
        {
            get;
            private set;
        }

        /// <summary>
        /// HostApp can only be set once. Other calls just don't do anything.
        /// </summary>
        public string HostApp
        {
            get { return _hostApp; }
            set { if (string.IsNullOrWhiteSpace(_hostApp)) _hostApp = value; }
        }

        public IObservable<NyxNodeStatus> NodeStatusStream => _nodeStatusSubject.AsObservable();

        public IObservable<MessageStatus> InMessageStream => _inMessageStatus.AsObservable();

        /// <summary>
        /// Just a standin, we will replace this.
        /// </summary>
        internal IObservable<bool> LicenseSubject;

        /// <summary>
        /// Just a simple flag to check if the system is licensed.
        /// </summary>
        public IObservable<bool> LicenseStatus => LicenseSubject?.AsObservable();

        public IObservable<MessageStatus> OutMessageStream => _outMessageStatus.AsObservable();

        internal IObservable<Tuple<byte[], INyxMessage>> NyxMessageStream => _inMessage.AsObservable();
        /// <summary>
        ///     Default construtor used by IoC builder.
        /// </summary>
        /// <param name="context">NetMQ context</param>
        /// <param name="logger">System logger</param>
        /// <param name="plugman">PluginManager</param>
        /// <param name="config"></param>
        public NyxHub(
            NetMQContext context,
            ILogger<NyxHub> logger,
            PluginManager plugman,
            IConfigManager config)
        {
            _context = context;
            _serverCancelToken = _serverCancelationSource.Token;
            config.WhenConfigChanges.Where(m => m.Keys.Contains(HubConfigMultithread)).Subscribe(ConfigReload);
            _receiveMultithread = config.Get(HubConfigMultithread, false);
            _logger = logger;
            _plugman = plugman;
            _disposables.Add(NyxMessageStream.Subscribe(x =>
            {
                if (TotalInMessages == long.MaxValue) TotalInMessages = 0;
                TotalInMessages++;
            }));
            _disposables.Add(OutMessageStream.Subscribe(x =>
            {
                if (TotalInMessages == long.MaxValue) TotalOutMessages = 0;
                TotalOutMessages++;
            }));
        }

        private void ConfigReload(ConfigChanged changes)
        {
            _receiveMultithread = changes.Sender.Get(HubConfigMultithread, false);
        }

        /// <summary>
        /// Broadcast message to the given channel(s)
        /// </summary>
        /// <param name="msg"></param>
        public void BroadcastMessage(INyxMessage msg)
        {
            if (!_isStarted || _hubActor == null)
            {
                _logger.Warn("Hub is not started.");
                return;
            }
            _logger.Debug("Broadcasting message to channel {0}...", StringExtensions.Trimmer(msg.Target));
            ((NyxMessage)msg).Direction = MessageDirection.Out;
            var error = string.Empty;
            try
            {
                var filterFound = _plugman.GetFilters(f => f.Direction.HasFlag(MessageDirection.Out) && f.CanFilter(this)).FirstOrDefault(filter => !filter.AllowMessage(msg, this, out error));
                if(filterFound != null)
                {
                    _logger.Warn("Message not sent. Was filtered by {0}. {1}", filterFound.GetType().Name, error);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error filtering message.", ex);
            }

            _hubActor.SendMoreFrame("broadcast").SendMoreFrame(msg.Target).SendFrame(msg.ToDefault());
            _outMessageStatus.OnNext(msg.AsReadOnly().SuccessfullSent(this));
        }

        /// <summary>
        /// Starts our server
        /// </summary>
        public IObservable<NyxNodeStatus> Start()
        {
            if (_serverWorker != null || _isStarted) return Observable.Return(NyxNodeStatus.Started);
            var result = NodeStatusStream.Where(n => n == NyxNodeStatus.Started).FirstAsync();
            _serverWorker = Task.Factory.StartNew(ServerLoop, _serverCancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return result;
        }

        /// <summary>
        /// Stop our server
        /// </summary>
        public IObservable<NyxNodeStatus> Stop()
        {
            if (_serverWorker == null) return Observable.Return(NyxNodeStatus.Stopped);
            var result = Observable.Create<NyxNodeStatus>(o =>
            {
                var tempDisposable = new CompositeDisposable();
                tempDisposable.Add(NodeStatusStream.Where(n => n == NyxNodeStatus.Stopped).Subscribe(x =>
                {
                    o.OnNext(x);
                    o.OnCompleted();
                    tempDisposable.Dispose();
                }));
                return tempDisposable;
            });
            _serverCancelationSource.Cancel();
            _serverEvent.Set();
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ServerLoop()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Hub Server Loop";

            _disposables.Add(NyxMessageStream.Subscribe(RepSocketOnReceiveReady, x => _logger.Error("Error processing stream.", x)));
            _hubActor?.Dispose();
            _hubActor = NetMQActor.Create(_context, new HubShimHandler(_context, _inMessage, Port));

            _logger.Debug("Starting hub server loop...");
            _isStarted = true;
            _plugman.StartAllPlugins();
            _nodeStatusSubject.OnNext(NyxNodeStatus.Started);
            // Endless loop until request to stop, then shutdowns
            while (!_serverCancelToken.IsCancellationRequested)
            {
                _serverEvent.WaitOne(TimeSpan.FromSeconds(60));
            }
            _plugman.StopAllPlugins();
            Status = NyxNodeStatus.Stopped;
            _nodeStatusSubject.OnNext(Status);
            try
            {
                _hubActor?.SendFrame(NetMQActor.EndShimMessage);
                _hubActor?.Dispose();
            }
            catch (TerminatingException)
            {
                // Ignore and go
            }
            _isStarted = false;
        }

        private void RepSocketOnReceiveReady(Tuple<byte[], INyxMessage> msg)
        {
            // Release the event queue to receive more. Since we know the address in Rep-Router
            if (_receiveMultithread)
                Task.Factory.StartNew(() => RepSocketOnReceiveReadyImpl(msg), TaskCreationOptions.PreferFairness);
            else
                RepSocketOnReceiveReadyImpl(msg);
        }

        /// <summary>
        /// Reply from client.
        /// </summary>
        /// <param name="message"></param>
        private void RepSocketOnReceiveReadyImpl([NotNull] Tuple<byte[], INyxMessage> message)
        {
            try
            {
                // Get the cliendid
                var clientAddress = message.Item1;
                var result = "1";
                var msg = message.Item2;
                var abort = false;
                var error = string.Empty;
                
                // Optimized for the first false found. No need to run all filters.
                // Filters are not message processors so they should only filter to block or do background processing on messages
                // Before they can be used by the system.
                try
                {
                    var filterFound = _plugman.GetFilters(f => f.Direction.HasFlag(MessageDirection.In) && f.CanFilter(this)).FirstOrDefault(filter => msg != null && !filter.AllowMessage(msg, this, out error));
                    if (filterFound != null)
                    {
                        _logger.Warn("Message was filtered by {0}. Reason: {1}", filterFound.GetType().Name, error);
                        result = string.IsNullOrWhiteSpace(error) ? result : error;
                        abort = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error filtering message.", ex);
                }
                
                // Send message and doesn't wait. Always returns the id of the message that we processed
                lock (_syncLock)
                {
                    var responseMsg = new NetMQMessage();
                    responseMsg.Append("response");
                    responseMsg.Append(clientAddress);
                    responseMsg.AppendEmptyFrame();
                    responseMsg.Append(result);
                    _hubActor.SendMultipartMessage(responseMsg);
                }

                if (abort) return;
                _logger.Debug("New message. S:{0} T:{1}", StringExtensions.Trimmer(msg.Source), StringExtensions.Trimmer(msg.Target));

                try
                {
                    var hubPlugins = _plugman.GetActions<IHubAction>().Where(p => p.SupportedActions.Contains(msg.Action.ToLowerInvariant())).ToList();
                    if (hubPlugins.Any())
                    {
                        _logger.Trace("Executing action '{0}'...", msg.Action);
                        foreach (var hubAction in hubPlugins)
                        {
                            hubAction.ProcessMessage(msg.AsReadOnly());
                        }
                    }
                    else
                    {
                        _logger.Info("Direct pass-throught to channel '{0}'...", StringExtensions.Trimmer(msg.Target));
                        BroadcastMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error processing message.", ex);
                }
                _inMessageStatus.OnNext(new MessageStatus(this, msg.AsReadOnly(), MessageCondition.Received));
            }
            catch (Exception ex)
            {
                _logger.Error("Error occured processing nyx message.", ex);
                _inMessageStatus.OnNext(new MessageStatus(this, NyxMessage.EmptyIn, MessageCondition.Failed, ex.ToString()));
            }
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _serverEvent?.Dispose();
        }
    }
}
