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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetMQ;
using NetMQ.Sockets;
using Nyx.Core.Config;
using Nyx.Core.Extensions;
using Nyx.Core.Logging;
using Nyx.Core.Messaging;
using Nyx.Core.Network;
using Nyx.Core.Plugins;
using Nyx.Core.Threading;

namespace Nyx.Core
{
    /// <summary>
    ///     Borgs are the main nodes of the Nyx Framework. They received messages from each other and act on the actions of
    ///     the messages.
    ///     Messages that are received, are first filter and only if they are allowed, they are processed in the actions
    ///     extensions.
    /// </summary>
    public sealed class NyxBorg : INyxBorg, IDisposable
    {
        private static readonly object MessageQueueSync = new object();
        private bool _autoReconnect = true;
        private bool _autostart = true;
        private bool _clearQueueOnFail;
        private bool _dequeueOnFail = true;
        private bool _isConnected;
        private int _keepAliveTimeOut = 1000;
        private string _lastHubIp;
        internal int _port = 4015;
        private bool _queueOnDisconnected;
        private readonly AutoResetEvent _serverEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _messageSendResetEvent = new AutoResetEvent(false);
        private int _timeOut = 3000;
        private int _timeOutRetries = 5;
        private NetMQSocket _reqSocket;
        private Task _serverTask;
        private readonly IConfigManager _config;
        private readonly ILogger<NyxBorg> _logger;
        private readonly Queue<MessageHelper> _messageQueue = new Queue<MessageHelper>();
        private readonly PluginManager _plugman;
        private readonly HashSet<string> _subscribersChannels = new HashSet<string>();
        private string _hostApp;
        private NyxHostMode _hostMode;
        private volatile bool _isStarted;
        private CancellationToken _messageLoopToken;
        private readonly CancellationTokenSource _messageLoopCancelation = new CancellationTokenSource();
        private readonly CancellationTokenSource _serverCancelationSource = new CancellationTokenSource();
        private CancellationToken _serverCancelToken;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly ISubject<MessageStatus> _messageReceived = new Subject<MessageStatus>();
        private readonly ISubject<MessageStatus> _messageReplyReceived = new Subject<MessageStatus>();
        private readonly ISubject<NyxNodeStatus> _nodeStatusSubject = new BehaviorSubject<NyxNodeStatus>(NyxNodeStatus.Stopped);
        private readonly ISubject<ConnectionStatus> _nodeConnectionSubject = new Subject<ConnectionStatus>();
        private NetMQActor _actor;
        private readonly ISubject<INyxMessage> _nyxMessageSubject = new Subject<INyxMessage>();
        private IDisposable _connectionDisposable;
        private volatile bool _hubOnline;
        private readonly SerialAsyncTasker _tasker = new SerialAsyncTasker();
        private bool _multithread;
        private int _maxFailedRetries = 3;

        private class MessageHelper
        {
            public INyxMessage Message { get; set; }

            public int Retries { get; set; } = 0;
        }

        /// <summary>
        ///     Observable for message reception. It returns a message status wrapper.
        /// </summary>
        public IObservable<MessageStatus> InMessageStream => _messageReceived.AsObservable();

        /// <summary>
        ///     Observable for message from the Hub. This is just replies.
        /// </summary>
        public IObservable<MessageStatus> OutMessageStream => _messageReplyReceived.AsObservable();

        /// <summary>
        ///     Observable for connection changes.
        /// </summary>
        public IObservable<ConnectionStatus> ConnectionStatusStream { get; }

        /// <summary>
        ///     Observable for node status changes.
        /// </summary>
        public IObservable<NyxNodeStatus> NodeStatusStream => _nodeStatusSubject.AsObservable();

        /// <summary>
        ///     For unit testing and internal logging.
        /// </summary>
        internal IObservable<INyxMessage> NyxMessageStream => _nyxMessageSubject.AsObservable();

        public long TotalInMessages { get; private set; }

        public long TotalOutMessages { get; private set; }

        /// <summary>
        /// HostApp can only be set once. Other calls just don't do anything.
        /// </summary>
        public string HostApp
        {
            get { return _hostApp; }
            set { if(string.IsNullOrWhiteSpace(_hostApp)) _hostApp = value; }
        }

        public NyxNodeStatus Status { get; private set; }

        /// <summary>
        ///     Is the server loop running.
        /// </summary>
        public bool IsStarted
        {
            get { return _isStarted; }
            private set { _isStarted = value; }
        }

        /// <summary>
        ///     Host mode for the borg. Could be slave or workstation mode.
        ///     Slave mode, is a headless mode that the user has no direct interaction.
        /// </summary>
        public NyxHostMode HostMode
        {
            get { return _hostMode; }
            set { if(_hostMode == NyxHostMode.None) _hostMode = value; }
        }

        /// <summary>
        ///     Node id.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        ///     This is a readonly list of the channels that this client as subscribed.
        /// </summary>
        public IReadOnlyList<string> SubscribersChannels => _subscribersChannels.ToList();

        /// <summary>
        ///     Protected default construtor for creating a new id.
        /// </summary>
        public NyxBorg(
            ILogger<NyxBorg> logger,
            IConfigManager config,
            PluginManager plugman)
        {
            NodeId = Guid.NewGuid().ToString("N");
            _config = config;
            _logger = logger;
            _config.WhenConfigChanges.ObserveOn(ThreadPoolScheduler.Instance).Subscribe(LoadConfig);
            _plugman = plugman;

            _messageLoopToken = _messageLoopCancelation.Token;
            _serverCancelToken = _serverCancelationSource.Token;

            _disposables.Add(_messageSendResetEvent);
            _disposables.Add(_serverEvent);
            _disposables.Add(_messageLoopCancelation);
            _disposables.Add(_serverCancelationSource);

            LoadConfig(null);
            Heartbeat.CreateInstance(_lastHubIp, _port);
            //_heartbeat = new Heartbeat(_context, _lastHubIp, _port.ToString());
            ConnectionStatusStream = Observable.Create<ConnectionStatus>(o =>
            {
                _logger.Debug("Hearbeat restarted.");
                Heartbeat.Instance.Init();
                return Heartbeat.Instance.ConnectionStatusStream.Subscribe(o);
            }).Repeat().Publish().RefCount();

            _disposables.Add(ConnectionStatusStream
                .ObserveOn(ThreadPoolScheduler.Instance)
                .Subscribe(x => _hubOnline = x.HasFlag(ConnectionStatus.Online)));

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

        /// <summary>
        ///     Dispose resources
        /// </summary>
        public void Dispose()
        {
            _actor?.Dispose();
            _connectionDisposable?.Dispose();
            _reqSocket?.Dispose();
            _disposables?.Dispose();
        }

        /// <summary>
        ///     Starts our server
        /// </summary>
        public IObservable<NyxNodeStatus> Start()
        {
            if (_serverTask != null || _serverTask?.Status == TaskStatus.Running || Status == NyxNodeStatus.Started) return NodeStatus.StartedObservable();
            var result = Observable.Create<NyxNodeStatus>(o =>
            {
                var dispose = NodeStatusStream.Subscribe(n =>
                {
                    if (n != NyxNodeStatus.Started) return;
                    o.OnNext(n);
                    o.OnCompleted();
                });
                return new CompositeDisposable(dispose);
            });
            _serverTask = Task.Factory.StartNew(ServerLoop, _serverCancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            IsStarted = true;
            return result;
        }

        /// <summary>
        ///     Stop our server
        /// </summary>
        public IObservable<NyxNodeStatus> Stop()
        {
            if (_serverTask == null || Status == NyxNodeStatus.Stopped) return NodeStatus.StoppedObservable();
            var result = Observable.Create<NyxNodeStatus>(o =>
            {
                var dispose = NodeStatusStream.Subscribe(n =>
                {
                    if (n != NyxNodeStatus.Stopped) return;
                    o.OnNext(n);
                    o.OnCompleted();
                });
                return new CompositeDisposable(dispose);
            });

            _serverCancelationSource.Cancel();
            _serverEvent.Set();
            return result;
        }

        /// <summary>
        ///     Default connect
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool Connect(string ipAddress="", int port = 4015)
        {
            if (!IsStarted)
            {
                _logger.Error("Connection failed. Server not running.");
                if (_autostart)
                {
                    _logger.Info("AutoStart is on. Server starting.");
                    if (!Start().Select(x => true).Amb(Observable.Return(false).Delay(TimeSpan.FromSeconds(5))).Wait())
                        return false;
                }
                else
                    return false;
            }
            try
            {
                if (_isConnected && _lastHubIp == ipAddress)
                {
                    _logger.Trace("Connection to {0} is active, not reconnecting.", ipAddress);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(ipAddress))
                    ipAddress = "127.0.0.1";

                Heartbeat.Instance.Disconnected();
                _connectionDisposable?.Dispose();

                // Stop previous actor is any.
                try
                {
                    _actor?.SendFrame(NetMQActor.EndShimMessage);
                    //_actor?.Dispose();
                }
                catch(Exception ex)
                {
                    _logger.Error("NetMQ error.", ex);
                }
                _actor = null;
                // Store port and hub address
                _port = port;
                _lastHubIp = ipAddress;

                Heartbeat.Instance.Setup(_lastHubIp, _port);
                // Create a new actor to handle the comunications from hub.
                _actor = NetMQActor.Create(BorgShimHandler.Create(_nyxMessageSubject, ipAddress, port));
                Heartbeat.Instance.Connected();

                CreateServerSocket();
                _config.Set("borg", "hubIp", _lastHubIp);

                // Global channel
                AddToGroup("global");
                // Custom channels
                SubscribersChannels.ForEach(c => _actor.SendMoreFrame("subscribe").SendFrame(c));
                _actor.SendMoreFrame("subscribe").SendFrame(NodeId);
                _isConnected = true;
                _connectionDisposable = Disposable.Create(() => _isConnected = false);
                NyxMessage.Create(BasicHubAction.NyxId, BasicHubAction.Register, NodeId).Set("nodeID", NodeId).SendMessage(this);
            }
            catch (Exception ex)
            {
                _logger.Error("Error connecting...", ex);
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Subscribe to Nyx group
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        public void AddToGroup(string topic)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(topic))
                {
                    _logger.Warn("Topic is empty.");
                    return;
                }
                if (_subscribersChannels.Contains(topic))
                {
                    _logger.Warn($"Topic {topic} already registered.");
                    return;
                }
                if(_subscribersChannels.Add(topic))
                    _actor?.SendMoreFrame("subscribe").SendFrame(topic);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error adding subscription {topic}.", ex);
            }
        }

        public bool RemoveFromGroup(string topic)
        {
            var result = false;
            try
            {
                if (!_subscribersChannels.Contains(topic)) return false;
                _subscribersChannels.Remove(topic);
                _actor?.SendMoreFrame("unsubscribe").SendFrame(topic);
                result = true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error removing subscription to group {topic}.", ex);
            }
            return result;
        }

        /// <summary>
        ///     Queues a message for sending to the hub for distribution.
        /// </summary>
        /// <param name="msg">Nyx message to send.</param>
        /// <param name="skip">Doesn't queue the message if no connection to the hub.</param>
        /// <returns>An observable for checking when the message is delivered to the Hub.</returns>
        public IObservable<MessageStatus> SendMessage(INyxMessage msg, bool skip = false)
        {
            if (msg == null) return Observable.Return(new MessageStatus(this, NyxMessage.EmptyOut, MessageCondition.Failed, "Null message"));
            if (string.IsNullOrWhiteSpace(msg.Source)) msg.Source = NodeId;
            if (string.IsNullOrWhiteSpace(msg.Target))
            {
                _logger.Error("Message validation failed, missing Target.");
                return Observable.Return(new MessageStatus(this, msg, MessageCondition.Failed, "Message validation failed, missing Target."));
            }
            // Processes internal messages like they were external messages.
            if (msg is InternalNyxMessage)
            {
                return Observable.Start(() => ProcessExternalMessages(msg));
            }
            if ((!_isConnected && !_queueOnDisconnected) || (!_hubOnline && skip) || (!_isConnected && skip))
            {
                _logger.Debug("Message missed. Not connected to any hub, or hub offline.");
                return Observable.Return(new MessageStatus(this, msg, MessageCondition.Failed, "Not connected to any hub."));
            }

            if (_messageLoopCancelation.IsCancellationRequested)
            {
                _logger.Error("Message missed. Not connected to any hub, or hub offline.");
                return Observable.Return(new MessageStatus(this, msg, MessageCondition.Failed, "Borg is going down."));
            }

            ((NyxMessage)msg).Direction = MessageDirection.Out;
            var observable = Observable.Create<MessageStatus>(observer =>
            {
                return OutMessageStream.Where(m => m?.Message.Id == msg.Id).Subscribe(n =>
                {
                    observer.OnNext(n);
                    switch (n.Status)
                    {
                        case MessageCondition.Failed:
                        case MessageCondition.Filtered:
                        case MessageCondition.Sent:
                            observer.OnCompleted();
                            break;
                    }
                });
            });
            lock (MessageQueueSync)
                _messageQueue.Enqueue(new MessageHelper {Message = msg});
            _messageSendResetEvent.Set();

            return observable;
        }

        /// <summary>
        ///     Remove all subscriptions
        /// </summary>
        public void ClearSubscriptions()
        {
            SubscribersChannels.ForEach(t => _actor?.SendMoreFrame("unsubscribe").SendFrame(t));
            _subscribersChannels.Clear();
        }

        /// <summary>
        ///     Add a batch of new groups
        /// </summary>
        /// <param name="groups"></param>
        public void AddSubscriptions([NotNull] IEnumerable<string> groups)
        {
            if(groups == null)
                throw new ArgumentNullException(nameof(groups));
            var enumerable = groups as IList<string> ?? groups.ToList();
            enumerable.ForEach(t =>
            {
                if(_subscribersChannels.Add(t))
                    _actor?.SendMoreFrame("subscribe").SendFrame(t);
            });
        }

        private MessageStatus ProcessExternalMessages(INyxMessage msg)
        {
            if(FilterMessage(msg)) return msg.Filtered(this, "Filtered.");
            if (msg.Direction == MessageDirection.In)
                ProcessMessageActions(msg.AsReadOnly());
            _messageReceived.OnNext(msg.SuccessfullReceived(this));
            return msg.SuccessfullSent(this);
        }

        /// <summary>
        ///     Load the configuration, or sets the defaults
        /// </summary>
        /// <param name="configChanged"></param>
        private void LoadConfig(ConfigChanged configChanged)
        {
            _lastHubIp = _config.Get("borg", "hubIp", _lastHubIp);
            _port = _config.Get("hub", "port", _port);
            _timeOut = _config.Get("borg", "timeout", _timeOut);
            _keepAliveTimeOut = _config.Get("borg", "keepAliveTimeOut", _keepAliveTimeOut);
            _timeOutRetries = _config.Get("borg", "timeoutRetries", _timeOutRetries);
            _autoReconnect = _config.Get("borg", "autoReconnect", _autoReconnect);
            _autostart = _config.Get("borg", "autostart", _autostart);
            _dequeueOnFail = _config.Get("borg", "dequeueOnFail", _dequeueOnFail);
            _clearQueueOnFail = _config.Get("borg", "clearQueueOnFail", _clearQueueOnFail);
            _queueOnDisconnected = _config.Get("borg", "queueOnDisconnected", _queueOnDisconnected);
            _multithread = _config.Get("borg", "experimentalmultithread", false);
            _maxFailedRetries = _config.Get("borg", "maxFailedRetries", _maxFailedRetries);
        }

        /// <summary>
        ///     New non blocking queue, using Thread events for smart control.
        /// </summary>
        private void MessageSenderLoop()
        {
            _logger.Debug("Starting dequeue loop...");
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Borg Message Queue";
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            while (!_messageLoopToken.IsCancellationRequested)
            {
                if (_messageQueue.Count == 0)
                    _messageSendResetEvent.WaitOne();
                else if (!_isConnected)
                {
                    if(_messageLoopCancelation.IsCancellationRequested) _messageReplyReceived.OnError(new EndOfStreamException("Message queue is stopping."));
                    _messageLoopToken.ThrowIfCancellationRequested();
                    _messageSendResetEvent.WaitOne(TimeSpan.FromSeconds(_timeOut));
                    continue;
                }
                // Check if we have messages or if our hub is alive
                if (_messageQueue.Count == 0) continue;
                if (_reqSocket == null) continue;
                // Dequeues message if dequeue on fail is on, else leave this to a later stage
                var dequeueOnFail = _dequeueOnFail;
                MessageHelper msg;
                lock (MessageQueueSync) msg = dequeueOnFail ? _messageQueue.Dequeue() : _messageQueue.Peek();

                var readonlyMsg = msg.Message.AsReadOnly();
                _logger.Trace("{0} message {1}", dequeueOnFail ? "Dequeued" : "Peeked", msg.Message);
                var abort = FilterMessage(msg.Message);
                if (abort)
                {
                    _logger.Debug("Message {0} was not sent.", msg.Message);
                    if (!dequeueOnFail || msg.Retries > _maxFailedRetries) lock (MessageQueueSync) _messageQueue.Dequeue();
                    else msg.Retries++;
                    if (_messageQueue.Count == 0) _messageSendResetEvent.Reset();
                    continue;
                }

                // Reply control
                NetMQMessage mqMessage = null;
                var retries = _timeOutRetries;
                var timeout = Debugger.IsAttached ? 120000 : _timeOut;
                try
                {
                    _reqSocket.SendMultipartMessage(msg.Message.ToNetMQMessage());
                    var received = false;
                    while (!received && retries > 0)
                    {
                        received = _reqSocket.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(timeout), ref mqMessage);
                        retries--;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error sending message. REQ-REP Socket state problem.", ex);
                }

                if (mqMessage == null)
                {
                    _logger.Error("Message with id={0}, failed, due to timeout of {1}ms reached, hub maybe busy.", msg.Message.ShortId(), timeout * retries);
                    NotifyMessageFailedOut(readonlyMsg, "No reply or invalid data return by the hub.");
                    // Here we restart all over again...
                    if (!dequeueOnFail && msg.Retries <= _maxFailedRetries)
                    {
                        _logger.Debug("Requeue message with id={0}.", msg.Message.ShortId());
                        msg.Retries++;
                    }
                    else if(msg.Retries > _maxFailedRetries)
                    {
                        if (!dequeueOnFail)
                            lock (MessageQueueSync) _messageQueue.Dequeue();
                    }
                    if (_clearQueueOnFail)
                    {
                        lock (MessageQueueSync) _messageQueue.Clear();
                    }
                    CreateServerSocket();
                    continue;
                }
                // Everything went well lets dequeue if we didn't do that before
                if (!dequeueOnFail)
                    lock (MessageQueueSync) _messageQueue.Dequeue();
                NotifyMessageSentOut(readonlyMsg, true);
                var data = mqMessage.FrameCount > 1 ? mqMessage[2].ConvertToString() : mqMessage.First.ConvertToString();
                _logger.Trace("We did get a reply {0}...", data);
                if (_messageQueue.Count == 0) _messageSendResetEvent.Reset();
            }
            _messageReplyReceived.OnError(new EndOfStreamException("Message queue is stopping."));
            _logger.Debug("Stopping dequeue loop...");
        }

        private void NotifyMessageFailedOut(INyxMessage msg, string description)
        {
            var messageStatus = new MessageStatus(this, msg, MessageCondition.Failed, description);
            _messageReplyReceived.OnNext(messageStatus);
        }

        private void NotifyMessageSentOut(INyxMessage msg, bool isReply=false)
        {
            var messageStatus = new MessageStatus(this, msg, MessageCondition.Sent, isReply);
            _messageReplyReceived.OnNext(messageStatus);
        }

        /// <summary>
        ///     Creates a new RequestSocket
        /// </summary>
        private void CreateServerSocket()
        {
            _reqSocket?.Dispose();
            _reqSocket = new RequestSocket();
            _reqSocket.Options.Linger = TimeSpan.Zero;
            _reqSocket.Connect($"tcp://{_lastHubIp}:{_port}");
        }

        /// <summary>
        ///     Server loop
        /// </summary>
        private void ServerLoop()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Borg Server Loop";

            _logger.Debug("Starting borg server loop...");
            //_disposables.Add(NyxMessageStream.SubscribeOn(ThreadPoolScheduler.Instance).Subscribe(ReceivedMessage));

            _disposables.Add(NyxMessageStream
                .TimeInterval()
                .Do(t => _logger.Info("Last message ({1}) received {0:##0.000}s ago.", t.Interval.TotalSeconds, t.Value.Action))
                .Select(t => new NyxMessage(t.Value as NyxMessage))
                .ObserveOnPool()
                .Subscribe(ReceivedMessage));

            _disposables.Add(OutMessageStream
                .Where(m => m.Status == MessageCondition.Sent)
                .TimeInterval()
                .Do(t => _logger.Info("Last message ({1}) sent {0:##0.000}s ago.", t.Interval.TotalSeconds, t.Value.Message.Action))
                .ObserveOnPool()
                .Subscribe());

            var messageLoopTask = new Task(MessageSenderLoop, _messageLoopToken, TaskCreationOptions.LongRunning);
            messageLoopTask.Start();
            _plugman.StartAllPlugins();
            Status = NyxNodeStatus.Started;
            _nodeStatusSubject.OnNext(NyxNodeStatus.Started);

            // Endless loop until request to stop, then shutdowns
            while (!_serverCancelToken.IsCancellationRequested)
            {
                _serverEvent.WaitOne(6000);
            }
            _logger.Debug("Stopping server loop...");
            var stopTask = Task.Run(() => _plugman.StopAllPlugins(), _messageLoopToken);

            if(!stopTask.Wait(TimeSpan.FromSeconds(45)))
                _logger.Error("Plugins didn't stop in the allocated time.");
            // Dispose the event listener and no "leaks"

            // Flush any message leftovers
            _messageSendResetEvent.Set();
            if (_messageQueue.Count > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(15));
                _messageSendResetEvent.Set();
            }
            _messageLoopCancelation.Cancel();
            _nodeConnectionSubject.OnNext(ConnectionStatus.Disconnected);

            Status = NyxNodeStatus.Stopped;
            _nodeStatusSubject.OnNext(Status);

            _actor?.Dispose();
            _connectionDisposable?.Dispose();
            _reqSocket?.Dispose();
            IsStarted = false;
        }

        /// <summary>
        ///     Override this for handling the SubSocket receive
        /// </summary>
        /// <param name="msg"></param>
        private void ReceivedMessage(INyxMessage msg)
        {
            var data = string.Empty;
            try
            {
                _logger.Trace("Received message...");
                _logger.Trace("Message source: {0}", msg.Source);

                if (FilterMessage(msg)) return;

                // Run message processing in another thread
                var readOnly = msg.AsReadOnly();
                void processMessages() => ProcessMessageActions(readOnly);

                if (_multithread)
                    _tasker.QueueTask(processMessages);
                else
                    processMessages();

                // Notify
                _messageReceived.OnNext(msg.SuccessfullReceived(this));
            }

            catch (Exception ex)
            {
                _logger.Error($"Exception caught in message {data}.", ex);
            }
        }

        private bool FilterMessage(INyxMessage msg)
        {
            var abort = false;
            try
            {
                var error = string.Empty;
                var filters = _plugman.GetFilters(f => f.Direction.HasFlag(msg.Direction) && f.CanFilter(this)).ToList();
                if (!filters.Any()) return true;
                var sw = Stopwatch.StartNew();
                var filterFound = filters.FirstOrDefault(filter => msg != null && !filter.AllowMessage(msg, this, out error));
                if (filterFound != null)
                {
                    if(error != string.Empty)
                        _logger.Warn("Message {0} was filtered by {1}. {2}", msg, filterFound.GetType().Name, error);
                    var messageStatus = msg.Filtered(this, $"Message was filtered by {filterFound.GetType().Name}.");
                    if(!(msg is InternalNyxMessage) && msg.Direction == MessageDirection.In)
                        _messageReceived.OnNext(messageStatus);
                    if(!(msg is InternalNyxMessage) && msg.Direction == MessageDirection.Out)
                        _messageReplyReceived.OnNext(msg.Filtered(this, error));
                    abort = true;
                }
                _logger.Trace("Message filter took {0}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error("Error processing filter.", ex);
            }
            return abort;
        }

        private void ProcessMessageActions(INyxMessage msg)
        {
            try
            {
                var action = msg.Action;
                var borgPlugins = _plugman.GetActions<IBorgAction>(p => p.SupportedActions.Contains(action)).ToList();
                if (borgPlugins.Count == 0) return;
                var sw = Stopwatch.StartNew();
                _logger.Trace("Plugins with action {0}: {1} in {2}", msg.Action, borgPlugins.Count(), _plugman.GetActions<IBorgAction>().Count());
                void forAction(IBorgAction borgPlug)
                {
                    try
                    {
                        _logger.Debug("Running action {0} for message {1} on processor {2}", msg.Action, msg, borgPlug.GetType().Name);
                        borgPlug.ProcessMessage(msg.AsReadOnly());
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error running action on {borgPlug.GetType().Name}", ex);
                    }
                }
                borgPlugins.ForEach(forAction);
                _logger.Trace("Message actions took {0}ms", sw.ElapsedMilliseconds);
                sw.Stop();
            }
            catch (Exception ex)
            {

                _logger.Error("Error processing actions.", ex);
            }
        }
    }
}