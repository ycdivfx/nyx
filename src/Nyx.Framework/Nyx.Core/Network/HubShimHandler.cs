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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NLog;

namespace Nyx.Core.Network
{
    internal class HubShimHandler : IShimHandler, IDisposable
    {
        private readonly ISubject<Tuple<byte[], INyxMessage>> _messageSubject;
        private readonly int _port;
        private NetMQPoller _poller;
        private PublisherSocket _publisherSocket;
        private RouterSocket _responseSocket;
        private readonly Logger _logger;

        public HubShimHandler(ISubject<Tuple<byte[], INyxMessage>> messageSubject, int port)
        {
            _messageSubject = messageSubject;
            _port = port;
            _logger = LogManager.GetCurrentClassLogger();
        }

        void IShimHandler.Run(PairSocket shim)
        {
            _poller = new NetMQPoller();

            // Listen to actor dispose
            _ = Observable.FromEventPattern<NetMQSocketEventArgs>(e => shim.ReceiveReady += e, e => shim.ReceiveReady -= e)
                .Select(e => e.EventArgs)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(OnShimReady);

            _poller.Add(shim);

            _publisherSocket = new PublisherSocket();
            _responseSocket = new RouterSocket();
#if DEBUG
            var monitor1 = new NetMQMonitor(_publisherSocket, "inproc://#monitor1", SocketEvents.All);
            _ = Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor1, "Connected")
                .Select(e => new { Event = "Connected", e })
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor1, nameof(monitor1.Listening)).Select(e => new { Event = "Listening", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor1, nameof(monitor1.Accepted)).Select(e => new { Event = "Accepted", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor1, nameof(monitor1.Closed)).Select(e => new { Event = "Closed", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor1, nameof(monitor1.Disconnected)).Select(e => new { Event = "Disconnected", e }))
                .Do(e => _logger.Info("Monitor socket: {0}, {1}", e.Event, e.e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor1, nameof(monitor1.AcceptFailed))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor1, nameof(monitor1.ConnectDelayed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor1, nameof(monitor1.CloseFailed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor1, nameof(monitor1.BindFailed)))
                .Do(e => _logger.Error("Monitor error socket: {0}, {1}", e.EventArgs.ErrorCode, e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorIntervalEventArgs>(monitor1, nameof(monitor1.ConnectRetried))
                .Do(e => _logger.Info("Monitor retry socket: {0}, {1}", e.EventArgs.Interval, e.EventArgs.Address))
                .Subscribe();
            monitor1.AttachToPoller(_poller);

            var monitor2 = new NetMQMonitor(_responseSocket, "inproc://#monitor2", SocketEvents.All);
            _ = Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor2, "Connected")
                .Select(e => new { Event = "Connected", e })
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor2, nameof(monitor2.Listening)).Select(e => new { Event = "Listening", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor2, nameof(monitor2.Accepted)).Select(e => new { Event = "Accepted", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor2, nameof(monitor2.Closed)).Select(e => new { Event = "Closed", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor2, nameof(monitor2.Disconnected)).Select(e => new { Event = "Disconnected", e }))
                .Do(e => _logger.Info("Monitor socket: {0}, {1}", e.Event, e.e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor2, nameof(monitor2.AcceptFailed))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor2, nameof(monitor2.ConnectDelayed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor2, nameof(monitor2.CloseFailed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor2, nameof(monitor2.BindFailed)))
                .Do(e => _logger.Error("Monitor error socket: {0}, {1}", e.EventArgs.ErrorCode, e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorIntervalEventArgs>(monitor2, nameof(monitor2.ConnectRetried))
                .Do(e => _logger.Info("Monitor retry socket: {0}, {1}", e.EventArgs.Interval, e.EventArgs.Address))
                .Subscribe();
            monitor2.AttachToPoller(_poller);

#endif

            // To avoid crashes if the queue builds up too fast.
            _publisherSocket.Options.SendHighWatermark = 1000;
            _publisherSocket.Options.TcpKeepalive = true;
            _publisherSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
            _publisherSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);
            // Number of network hops to jump, default is 1, which means local network.
            //_publisherSocket.Options.MulticastHops = 100;
            _publisherSocket.Bind($"tcp://*:{_port + 1}");
            _poller.Add(_publisherSocket);

            _responseSocket.Bind($"tcp://*:{_port}");
            _ = Observable.FromEventPattern<NetMQSocketEventArgs>(
                e => _responseSocket.ReceiveReady += e, e => _responseSocket.ReceiveReady -= e)
                .Select(e => e.EventArgs)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(e =>
                {
                    try
                    {
                        var msg = e.Socket.ReceiveMultipartMessage();
                        // Ignore messages that have less than 3 frames or no empty second frame.
                        var address = msg.First();
                        var data = msg.Last().ConvertToString();
                        var nyxMsg = new NyxMessage().FromDefault(data);
                        _messageSubject.OnNext(new Tuple<byte[], INyxMessage>(address.ToByteArray(), nyxMsg));
                    }
                    catch (Exception ex)
                    {
                        _messageSubject.OnError(ex);
                    }
                });

            _poller.Add(_responseSocket);

            var heartbeatTimer = new NetMQTimer(TimeSpan.FromSeconds(2));
            _ = Observable.FromEventPattern<NetMQTimerEventArgs>(e => heartbeatTimer.Elapsed += e, e => heartbeatTimer.Elapsed -= e)
                .Select(e => e.EventArgs)
                .Subscribe(OnTimeoutElapsed);
            _poller.Add(heartbeatTimer);

            heartbeatTimer.Reset();
            shim.SignalOK();

            _poller.Run();

            if(monitor1 != null)
            {
                monitor1.DetachFromPoller();
                monitor2.DetachFromPoller();
            }
            _poller.Remove(_responseSocket);
            _poller.Remove(_publisherSocket);
            _poller.Remove(heartbeatTimer);
            _poller.Remove(shim);
            _poller.Dispose();
        }

        private void OnTimeoutElapsed(NetMQTimerEventArgs args)
        {
            _publisherSocket.SendFrame(Heartbeat.CoreHearbeatTopic);
        }

        private void OnShimReady(NetMQSocketEventArgs args)
        {
            var msg = args.Socket.ReceiveMultipartMessage();
            var cmd = msg.First.ConvertToString();
            if (cmd == NetMQActor.EndShimMessage)
            {
                _poller.Stop();
                return;
            }
            if (msg.FrameCount <= 1) return;
            if (cmd == "broadcast" && msg.FrameCount == 3)
            {
                var nmqMsg = new NetMQMessage();
                nmqMsg.Append(msg[1].ConvertToString());
                nmqMsg.AppendEmptyFrame();
                nmqMsg.Append(msg[2].ConvertToString());
                _publisherSocket.SendMultipartMessage(nmqMsg);
            }
            else if (cmd == "response")
            {
                var nmqMsg = new NetMQMessage();
                for (var i = 1; i < msg.FrameCount; i++)
                    nmqMsg.Append(msg[i]);
                _responseSocket.SendMultipartMessage(nmqMsg);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _poller?.Dispose();
                    _responseSocket?.Dispose();
                    _publisherSocket?.Dispose();

                }

                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
