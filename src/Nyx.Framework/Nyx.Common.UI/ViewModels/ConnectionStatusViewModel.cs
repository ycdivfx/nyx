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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Nyx.Core;
using ReactiveUI;

namespace Nyx.Common.UI.ViewModels
{
    public class ConnectionStatusViewModel : ReactiveObject, IConnectionStatusViewModel
    {
        private string _status;
        private long _totalSent;
        private long _totalReceived;
        private double _networkSpeed;
        private string _nyxVersion;

        public ConnectionStatusViewModel(INyxBorg borg) : this(borg, null)
        { }

        public ConnectionStatusViewModel(
            INyxBorg borg,
            IEnumerable<Lazy<IStatusViewModel>> models)
        {
            var borg1 = borg;

            borg1.InMessageStream
                .ObserveOnUI()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Subscribe(m => TotalReceived++);

            borg1.OutMessageStream
                .ObserveOnUI()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Subscribe(m => TotalSent++);

            borg1.ConnectionStatusStream
                .ObserveOnUI()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Subscribe(m => Status = m.ToString());

            NyxVersion = borg1.GetType().Assembly.GetName().Version.ToString();

            StatusViewModels = models?.Select(m => m.Value).ToList() ?? new List<IStatusViewModel>();
        }

        public IEnumerable<IStatusViewModel> StatusViewModels { get; set; }

        public string Status
        {
            get { return _status; }
            private set { this.RaiseAndSetIfChanged(ref _status, value); }
        }

        public long TotalSent
        {
            get { return _totalSent; }
            private set { this.RaiseAndSetIfChanged(ref _totalSent, value); }
        }

        public long TotalReceived
        {
            get { return _totalReceived; }
            private set { this.RaiseAndSetIfChanged(ref _totalReceived, value); }
        }

        public double NetworkSpeed
        {
            get { return _networkSpeed; }
            private set { this.RaiseAndSetIfChanged(ref _networkSpeed, value); }
        }

        public string NyxVersion
        {
            get { return _nyxVersion; }
            private set { this.RaiseAndSetIfChanged(ref _nyxVersion, value); }
        }
    }

    public interface IStatusViewModel
    {
    }
}