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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Nyx.Core.Messaging;

namespace Nyx.Core.Network
{
    public sealed class Heartbeat : IDisposable
    {
        private readonly NetMQContext _context;
        private string _address;
        private int _port;
        private int _requiresInitialisation = 1;
        private static volatile Heartbeat _instance;
        private static readonly object SyncLock = new object();
        private NetMQActor _actor; 
        private readonly ISubject<ConnectionStatus> _connectionSubject;

        public static Heartbeat CreateInstance(NetMQContext context, string address, int port)
        {
            if (_instance != null) return _instance;
            lock (SyncLock)
            {
                if (_instance == null) _instance = new Heartbeat(context, address, port);
            }
            return _instance;
        }

        public static Heartbeat Instance => _instance;

        private Heartbeat(NetMQContext context, string address, int port)
        {
            _context = context;
            _address = address;
            _port = port;
            _connectionSubject =
                new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connecting);
            Init();
        }

        public void Setup(string address, int port)
        {
            lock (SyncLock)
            {
                _address = address;
                _port = port;
                _requiresInitialisation = 1;
            }
            Init();
        }

        /// <summary>
        ///     Init or reinit all communications.
        /// </summary>
        public void Init()
        {
            if (Interlocked.CompareExchange(ref _requiresInitialisation, 0, 1) == 0) return;
            _actor?.SendFrame(NetMQActor.EndShimMessage);
            _actor?.Dispose();
            _actor = NetMQActor.Create(_context, new ShimHandler(this, _context, _connectionSubject, _address, _port));
        }

        private class ShimHandler : IShimHandler, IDisposable
        {
            private readonly NetMQContext _context;
            private readonly ISubject<ConnectionStatus> _subject;
            private readonly string _address;
            private readonly int _port;
            private readonly Heartbeat _parent;
            private Poller _poller;
            private readonly CompositeDisposable _disposables;
            private NetMQTimer _timeoutTimer;
            private SubscriberSocket _subscriberSocket;
            private volatile bool _borgConnected;

            public ShimHandler(Heartbeat parent, NetMQContext context, ISubject<ConnectionStatus> subject, string address, int port)
            {
                _parent = parent;
                _context = context;
                _subject = subject;
                _address = address;
                _port = port;
                _disposables = new CompositeDisposable();
            }

            public void Run(PairSocket shim)
            {
                _poller = new Poller();

                _disposables.Add(Observable.FromEventPattern<NetMQSocketEventArgs>(e => shim.ReceiveReady += e, e => shim.ReceiveReady -= e)
                    .Select(e => e.EventArgs)
                    .Subscribe(OnShimReady));

                _poller.AddSocket(shim);
                _timeoutTimer = new NetMQTimer(TimeSpan.FromSeconds(5));

                _disposables.Add(Observable.FromEventPattern<NetMQTimerEventArgs>(e => _timeoutTimer.Elapsed += e, e => _timeoutTimer.Elapsed -= e)
                    .Select(e => e.EventArgs)
                    .ObserveOn(ThreadPoolScheduler.Instance)
                    .Subscribe(OnTimeoutElapsed));

                _poller.AddTimer(_timeoutTimer);

                _subscriberSocket = _context.CreateSubscriberSocket();
                _subscriberSocket.Options.Linger = TimeSpan.Zero;
                _subscriberSocket.Subscribe(CoreHearbeatTopic);
                _subscriberSocket.Connect($"tcp://{_address}:{_port + 1}");
                _subject.OnNext(ConnectionStatus.Connecting);

                _disposables.Add(Observable.FromEventPattern<NetMQSocketEventArgs>(e => _subscriberSocket.ReceiveReady += e, e => _subscriberSocket.ReceiveReady -= e)
                    .Select(e => e.EventArgs)
                    .Where(e => e.Socket.ReceiveFrameString() == CoreHearbeatTopic)
                    .ObserveOn(ThreadPoolScheduler.Instance)
                    .Subscribe(e =>
                    {
                        _timeoutTimer.Reset();
                        Thread.MemoryBarrier();
                        var status = _borgConnected
                            ? (ConnectionStatus.Online | ConnectionStatus.Connected)
                            : (ConnectionStatus.Online | ConnectionStatus.Disconnected);
                        _subject.OnNext(status);
                    }));

                _poller.AddSocket(_subscriberSocket);
                _disposables.Add(_subscriberSocket);
                _timeoutTimer.Reset();
                shim.SignalOK();

                _disposables.Add(Disposable.Create(() =>
                {
                    _timeoutTimer.Enable = false;
                    _poller.Cancel();
                }));
                _disposables.Add(_subscriberSocket);

                _poller.PollTillCancelled();

                // Cleanup stuff after stopping
                _disposables.Dispose();
            }

            private void OnShimReady(NetMQSocketEventArgs e)
            {
                var msg = e.Socket.ReceiveMultipartMessage();
                var cmd = msg.First.ConvertToString();
                switch (cmd)
                {
                    case NetMQActor.EndShimMessage:
                        Dispose();
                        break;
                    case "connected":
                        Thread.MemoryBarrier();
                        _borgConnected = true;
                        break;
                    case "disconnected":
                        Thread.MemoryBarrier();
                        _borgConnected = false;
                        break;
                }
            }

            private void OnTimeoutElapsed(NetMQTimerEventArgs args)
            {
                _parent._requiresInitialisation = 1;
                var status = _borgConnected
                    ? (ConnectionStatus.Offline | ConnectionStatus.Connected)
                    : (ConnectionStatus.Offline | ConnectionStatus.Disconnected);
                _subject.OnNext(status);
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

        public IObservable<ConnectionStatus> ConnectionStatusStream => _connectionSubject.AsObservable();
        internal const string CoreHearbeatTopic = "$$!core.heartbeat#!";

        public void Connected()
        {
            _actor.SendFrame("connected");
        }

        public void Disconnected()
        {
            _actor.SendFrame("disconnected");
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _actor.Dispose();
        }
    }
}
