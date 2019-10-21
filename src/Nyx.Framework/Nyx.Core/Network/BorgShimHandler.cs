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
    internal class BorgShimHandler : IShimHandler, IDisposable
    {
        private readonly ISubject<INyxMessage> _messageSubject;
        private readonly string _address;
        private readonly int _port;
        private NetMQPoller _poller;
        private SubscriberSocket _subscriberSocket;
        private readonly Logger _logger;

        public static BorgShimHandler Create(ISubject<INyxMessage> messageSubject,
            string address, int port)
        {
            return new BorgShimHandler(messageSubject, address, port);
        }

        protected BorgShimHandler(ISubject<INyxMessage> messageSubject, string address, int port)
        {
            _messageSubject = messageSubject;
            _address = address;
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

            _subscriberSocket = new SubscriberSocket();
            _subscriberSocket.Options.ReceiveHighWatermark = 1000;
            _subscriberSocket.Options.Linger = TimeSpan.FromSeconds(2);
            _subscriberSocket.Options.TcpKeepalive = true;
            _subscriberSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
            _subscriberSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

#if MONITOR
            var monitor = new NetMQMonitor(_subscriberSocket, "inproc://#monitor", SocketEvents.All);
            _ =
                Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor, "Connected")
                .Select(e => new { Event = "Connected", e })
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor, nameof(monitor.Listening)).Select(e => new { Event = "Listening", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor, nameof(monitor.Accepted)).Select(e => new { Event = "Accepted", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor, nameof(monitor.Closed)).Select(e => new { Event = "Closed", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(monitor, nameof(monitor.Disconnected)).Select(e => new { Event = "Disconnected", e }))
                .Do(e => _logger.Info("Monitor socket: {0}, {1}", e.Event, e.e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor, nameof(monitor.AcceptFailed))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor, nameof(monitor.ConnectDelayed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor, nameof(monitor.CloseFailed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(monitor, nameof(monitor.BindFailed)))
                .Do(e => _logger.Error("Monitor error socket: {0}, {1}", e.EventArgs.ErrorCode, e.EventArgs.Address))
                .Subscribe();
            _ = Observable.FromEventPattern<NetMQMonitorIntervalEventArgs>(monitor, nameof(monitor.ConnectRetried))
                .Do(e => _logger.Info("Monitor retry socket: {0}, {1}", e.EventArgs.Interval, e.EventArgs.Address))
                .Subscribe();
            monitor.AttachToPoller(_poller);
#endif

            _ = Observable.FromEventPattern<NetMQSocketEventArgs>(
                e => _subscriberSocket.ReceiveReady += e, e => _subscriberSocket.ReceiveReady -= e)
                .Select(e => e.EventArgs)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(ReceivedMessage);

            _poller.Add(_subscriberSocket);

            _subscriberSocket.Connect($"tcp://{_address}:{_port + 1}");

            shim.SignalOK();

            try
            {
                _poller.Run();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Fatal error on NetMQ poller.");
            }

            // Cleanup stuff after stopping
            _poller.Remove(_subscriberSocket);
            _poller.Remove(shim);
#if MONITOR
            if (monitor != null)
            {
                monitor.DetachFromPoller();
                monitor.Dispose();
                monitor = null;
            }
#endif
            _poller.Dispose();
        }

        private void ReceivedMessage(NetMQSocketEventArgs e)
        {
            try
            {
                var msg = e.Socket.ReceiveMultipartMessage();
                if (msg.First.ConvertToString() == Heartbeat.CoreHearbeatTopic)
                    return;
                // Ignore messages that have less than 3 frames or no empty second frame.
                if (msg.FrameCount < 3) return;
                if (msg[1].MessageSize != 0) return;
                var data = msg[2].ConvertToString();
                var nyxMsg = new NyxMessage().FromDefault(data);
                _messageSubject.OnNext(nyxMsg);
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Error receiving message on BorgShim.");
            }
        }

        private void OnShimReady(NetMQSocketEventArgs args)
        {
            try
            {
                var msg = args.Socket.ReceiveMultipartMessage();
                var cmd = msg.First.ConvertToString();
                if (cmd == NetMQActor.EndShimMessage)
                {
                    _poller?.Stop();
                    return;
                }
                if (msg.FrameCount <= 1) return;
                switch (cmd)
                {
                    case "subscribe":
                        _logger.Debug("Adding node to group {0}", msg[1].ConvertToString());
                        _subscriberSocket?.Subscribe(msg[1].ConvertToString());
                        break;
                    case "unsubscribe":
                        _logger.Debug("Removing node from group {0}", msg[1].ConvertToString());
                        _subscriberSocket?.Unsubscribe(msg[1].ConvertToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Error on BorgShim.");
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
                    _subscriberSocket?.Dispose();
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
