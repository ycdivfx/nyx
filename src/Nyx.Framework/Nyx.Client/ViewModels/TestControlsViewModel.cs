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
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Nyx.Core;
using Nyx.Core.Config;
using Nyx.Core.Extensions;
using Nyx.Core.Messaging;
using Nyx.Core.Plugins;
using ReactiveUI;

namespace Nyx.Client.ViewModels
{
    public class TestControlsViewModel : ReactiveObject, ITestControlsViewModel
    {
        private string _connectionIp;
        private string _actionText;
        private long _pingCount = 10;
        private string _actionTarget = "global";
        private bool _isConnected;
        private string _actionSource;

        public TestControlsViewModel(
            INyxBorg borg,
            IConfigManager config
            )
        {
            PingCommand = ReactiveCommand.Create<object, Unit>(_ =>
            {
                for (int i = 0; i < PingCount; i++)
                {
                    borg.SendMessage(NyxMessage.Create("nyx", BasicHubAction.Ping, borg.NodeId));
                }
                return Unit.Default;
            });

            SendActionCommand = ReactiveCommand.Create<object, Unit>(_ =>
            {
                borg.SendMessage(NyxMessage.Create(ActionTarget, ActionText, string.IsNullOrWhiteSpace(ActionSource) ? borg.NodeId : ActionSource));
                return Unit.Default;
            });

            ConnectCommand = ReactiveCommand.Create<object, Unit>(_ =>
            {
                borg.Connect(ConnectionIp);
                return Unit.Default;
            });

            _connectionIp = config.Get("borg_hubIp", "127.0.0.1");

            config.WhenConfigChanges
                .Throttle(TimeSpan.FromMilliseconds(200), ThreadPoolScheduler.Instance)
                .Where(k => k.Keys.Contains("borg_hubIp"))
                .Select(k => k.Sender.Get("borg_hubIp", "127.0.0.1"))
                .DistinctUntilChanged()
                .ObserveOnDispatcher()
                .Subscribe(s => ConnectionIp = s);

            borg.ConnectionStatusStream
                .ObserveOnDispatcher()
                .Subscribe(c =>
                {
                    IsConnected = c.HasFlag(ConnectionStatus.Connected);
                });

            ValidActions = PluginManager.Instance
                .GetExtensions()
                .OfType<INyxMessageActions>()
                .SelectMany(_ => _.SupportedActions).ToList();

        }

        public string ConnectionIp
        {
            get { return _connectionIp; }
            set { this.RaiseAndSetIfChanged(ref _connectionIp, value); }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            private set { this.RaiseAndSetIfChanged(ref _isConnected, value);}
        }

        public ReactiveCommand<object, Unit> PingCommand { get; }

        public ReactiveCommand<object, Unit> ConnectCommand { get; }

        public ReactiveCommand<object, Unit> SendActionCommand { get; }

        public string ActionText
        {
            get { return _actionText; }
            set { this.RaiseAndSetIfChanged(ref _actionText, value); }
        }

        public string ActionTarget
        {
            get { return _actionTarget; }
            set { this.RaiseAndSetIfChanged(ref _actionTarget, value); }
        }

        public string ActionSource
        {
            get { return _actionSource; }
            set { this.RaiseAndSetIfChanged(ref _actionSource, value); }
        }

        public long PingCount
        {
            get { return _pingCount; }
            set { this.RaiseAndSetIfChanged(ref _pingCount, value); }
        }

        public IEnumerable<string> ValidActions { get; }
    }
}