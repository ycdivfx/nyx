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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nyx.Core;
using Nyx.Core.Config;
using Nyx.Core.Extensions;
using Nyx.Core.Logging;
using Nyx.Core.Messaging;

namespace Nyx.Plugin.HubAutodiscovery
{
    /// <summary>
    /// This is a "simple" plugin to handle auto discovery of a hub in an intranet.
    /// It needs to be installed in both Hub and Client for it to work.
    /// </summary>
    public sealed class AutoConnectToHub : INyxService, IDisposable
    {
        private readonly INyxBorg _borg;
        private readonly INyxHub _hub;
        private readonly IConfigManager _config;
        private readonly ILogger<AutoConnectToHub> _logger;
        private AutoResetEvent _resetEvent = new AutoResetEvent(true);
        private volatile bool _stop;
        private bool _runningServer;
        private bool _runningClient;
        private int _hubPort;
        private bool _start;
        private string _hubLastIp;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();
        private readonly CancellationTokenSource _cancelationToken;

        public string Name => "Hub Discovery";

        public AutoConnectToHub(IEnumerable<INyxNode> nodes,
            IConfigManager config,
            ILogger<AutoConnectToHub> logger)
        {
            var nyxNodes = nodes as IList<INyxNode> ?? nodes.ToList();
            _borg = nyxNodes.FirstOrDefault(e => e is INyxBorg) as INyxBorg;
            _hub = nyxNodes.FirstOrDefault(e => e is INyxHub) as INyxHub;
            _config = config;
            _logger = logger;
            ReloadConfig(null);
            _logger.Info("Auto Discovery loaded.");
            _cancelationToken = new CancellationTokenSource();
        }

        private void ReloadConfig(ConfigChanged keys)
        {
            _hubPort = _config.Get("hub", "port", 4015);
            _hubLastIp = _config.Get("borg", "hubIp", "127.0.0.1");
            _start = _config.Get("hubdiscovery", "enabled", true);
        }

        private void ConnectionChanged(ConnectionStatus status)
        {
            Interlocked.MemoryBarrier();
            if ((status.HasFlag(ConnectionStatus.Disconnected) ||
                status.HasFlag(ConnectionStatus.Offline)) && _start && _borg != null)
                _resetEvent.Set();
        }

        public void Start()
        {
            _logger.Trace("Starting....");
            if (_borg != null)
                _disposable.Add(_borg.ConnectionStatusStream.Distinct().ObserveOnPool().Subscribe(ConnectionChanged));
            _disposable.Add(_config.WhenConfigChanges.ObserveOnPool().Subscribe(ReloadConfig));

            if (_start && _borg != null && !_runningClient)
                Task.Factory.StartNew(FindValidHub, _cancelationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (_start && _hub != null && !_runningServer)
                Task.Run(AutodiscoveryServer, _cancelationToken.Token);
        }

        private async Task AutodiscoveryServer()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Hub Auto Responder";
            var hello = Encoding.UTF8.GetBytes("ACK");
            _logger.Debug($"Starting autodiscover helper server at {IPAddress.Any}:{_hubPort + 10}....");
            _runningServer = true;
            while (!_stop)
            {
                var server = new UdpClient(new IPEndPoint(IPAddress.Any, _hubPort + 10));
                try
                {
                    var msg = await server.ReceiveAsync();
                    var decoded = Encoding.UTF8.GetString(msg.Buffer);
                    _logger.Debug($"Discovered by {msg.RemoteEndPoint}....");
                    await server.SendAsync(hello, hello.Length, msg.RemoteEndPoint);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _logger.Trace($"Error on discovery: {e}");
                }
                finally
                {
                    server.Close();
                }
            }
            _runningServer = false;
        }

        private void FindValidHub()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Borg Hub Finder";
            _runningClient = true;
            _stop = false;
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Interlocked.MemoryBarrier();
            // First try the last ip.
            if (_borg.IsStarted && !string.IsNullOrWhiteSpace(_hubLastIp))
            {
                var s = _borg.ConnectionStatusStream.SkipWhile(c => c.HasFlag(ConnectionStatus.Connecting)).Latest().First();
                if (s.HasFlag(ConnectionStatus.Online) && s.HasFlag(ConnectionStatus.Disconnected))
                {
                    _logger.Trace($"Trying hub ip {_hubLastIp} ...");
                    if (!_borg.Connect(_hubLastIp, _hubPort)) _logger.Trace("Last hub ip failed...");
                }
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            while (!_stop)
            {
                try
                {
                    BroadcastSearchPacker().Wait(_cancelationToken.Token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.Trace(ex.ToString());
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            }
            _runningClient = false;
        }

        private async Task BroadcastSearchPacker()
        {
            var data = Encoding.ASCII.GetBytes("hello");
            var client = new UdpClient
            {
                EnableBroadcast = true,
                Client = { ReceiveTimeout = 1000 }
            };
            var serverEndpoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), _hubPort + 10);
            try
            {
                //_logger.Trace($"Looking for Hub at {serverEndpoint}");
                client.Send(data, data.Length, serverEndpoint);
                client.Receive(ref serverEndpoint);
                _logger.Trace($"Got response from {serverEndpoint.Address}");
                var status =
                    await
                        _borg.ConnectionStatusStream.ObserveOnPool()
                            .RepeatLastValueDuringSilence(TimeSpan.FromSeconds(1))
                            .FirstAsync().Timeout(TimeSpan.FromSeconds(2)).Catch(Observable.Return(ConnectionStatus.Disconnected));
                if (status.HasFlag(ConnectionStatus.Connected) && status.HasFlag(ConnectionStatus.Online))
                {
                    _logger.Trace("Connected to hub, disabling hub discovery agent...");
                    _resetEvent.Reset();
                    _resetEvent.WaitOne();
                }
                _logger.Trace($"Found hub at {serverEndpoint.Address}...");
                if (_borg.Connect(serverEndpoint.Address.ToString(), _hubPort))
                {
                    if (status.HasFlag(ConnectionStatus.Connected))
                    {
                        _resetEvent.Reset();
                        _resetEvent.WaitOne();
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (SocketException)
            {
                //_logger.Trace("HUb discovery timeout...");
            }
            catch (Exception ex)
            {
                _logger.Trace($"HUb discovery error: {ex}");
            }
            finally
            {
                client.Close();
            }
        }

        public void Stop()
        {
            _logger.Trace("HUb discovery stopped...");
            _cancelationToken.Cancel(false);
            _stop = true;
            _resetEvent.Set();
            _disposable?.Dispose();
        }

        public void Dispose()
        {
            if (_resetEvent == null) return;
            _resetEvent.Dispose();
            _resetEvent = null;
            _disposable?.Dispose();
        }
    }
}
