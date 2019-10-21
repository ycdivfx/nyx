﻿/*
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
using Poller = NetMQ.Poller;

namespace Nyx.Core.Network
{
    internal class BorgShimHandler : IShimHandler, IDisposable
    {
        private readonly NetMQContext _context;
        private readonly ISubject<INyxMessage> _messageSubject;
        private readonly string _address;
        private readonly int _port;
        private readonly CompositeDisposable _disposables;
        private Poller _poller;
        private SubscriberSocket _subscriberSocket;
        private readonly Logger _logger;
        private NetMQMonitor _monitor;

        public static BorgShimHandler Create(NetMQContext context, ISubject<INyxMessage> messageSubject,
            string address, int port)
        {
            return new BorgShimHandler(context, messageSubject, address, port);
        }

        protected BorgShimHandler(NetMQContext context, ISubject<INyxMessage> messageSubject, string address, int port)
        {
            _context = context;
            _messageSubject = messageSubject;
            _address = address;
            _port = port;
            _disposables = new CompositeDisposable();
            _logger = LogManager.GetCurrentClassLogger();
        }

        void IShimHandler.Run(PairSocket shim)
        {
            _poller = new Poller();

            // Listen to actor dispose
            _disposables.Add(Observable.FromEventPattern<NetMQSocketEventArgs>(e => shim.ReceiveReady += e, e => shim.ReceiveReady -= e)
                .Select(e => e.EventArgs)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(OnShimReady));

            _poller.AddSocket(shim);

            _subscriberSocket = _context.CreateSubscriberSocket();
            _subscriberSocket.Options.ReceiveHighWatermark = 1000;
            _subscriberSocket.Options.ReceiveTimeout = TimeSpan.MaxValue;
            _subscriberSocket.Options.Linger = TimeSpan.FromSeconds(2);
            _subscriberSocket.Options.TcpKeepalive = true;
            _subscriberSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
            _subscriberSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

            //SetupMonitor();

            _disposables.Add(Observable.FromEventPattern<NetMQSocketEventArgs>(
                e => _subscriberSocket.ReceiveReady += e, e => _subscriberSocket.ReceiveReady -= e)
                .Select(e => e.EventArgs)
                .ObserveOn(Scheduler.CurrentThread)
                .Subscribe(ReceivedMessage));

            _poller.AddSocket(_subscriberSocket);
            _disposables.Add(_subscriberSocket);

            _subscriberSocket.Connect($"tcp://{_address}:{_port + 1}");

            shim.SignalOK();

            _disposables.Add(Disposable.Create(() => _poller?.Cancel()));
            _disposables.Add(_subscriberSocket);

            try
            {
                _poller.PollTillCancelled();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Fatal error on NetMQ poller.");
            }

            _disposables.Dispose();
        }

        private void SetupMonitor()
        {
            _monitor = new NetMQMonitor(_context, _subscriberSocket, "inproc://#monitor", SocketEvents.All);
            _disposables.Add(
                Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(_monitor, "Connected")
                .Select(e => new { Event = "Connected", e })
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(_monitor, nameof(_monitor.Listening)).Select(e => new { Event = "Listening", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(_monitor, nameof(_monitor.Accepted)).Select(e => new { Event = "Accepted", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(_monitor, nameof(_monitor.Closed)).Select(e => new { Event = "Closed", e }))
                .Merge(Observable.FromEventPattern<NetMQMonitorSocketEventArgs>(_monitor, nameof(_monitor.Disconnected)).Select(e => new { Event = "Disconnected", e }))
                .Do(e => _logger.Info("Monitor socket: {0}, {1}", e.Event, e.e.EventArgs.Address))
                .Subscribe()
                );
            _disposables.Add(
                Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(_monitor, nameof(_monitor.AcceptFailed))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(_monitor, nameof(_monitor.ConnectDelayed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(_monitor, nameof(_monitor.CloseFailed)))
                .Merge(Observable.FromEventPattern<NetMQMonitorErrorEventArgs>(_monitor, nameof(_monitor.BindFailed)))
                .Do(e => _logger.Error("Monitor error socket: {0}, {1}", e.EventArgs.ErrorCode, e.EventArgs.Address))
                .Subscribe()
                );
            _disposables.Add(
                Observable.FromEventPattern<NetMQMonitorIntervalEventArgs>(_monitor, nameof(_monitor.ConnectRetried))
                .Do(e => _logger.Info("Monitor retry socket: {0}, {1}", e.EventArgs.Interval, e.EventArgs.Address))
                .Subscribe()
                );
            _monitor.AttachToPoller(_poller);

            _disposables.Add(_monitor);
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
                    Dispose();
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if(!_disposables.IsDisposed)
                _disposables.Dispose();
        }
    }
}