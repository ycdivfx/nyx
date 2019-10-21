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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NLog;
using NLog.Config;
using NLog.Layouts;
using Nyx.Common.UI.Logger;
using Nyx.Core.Config;
using ReactiveUI;

namespace Nyx.Common.UI.ViewModels
{
    public class LoggerViewModel : ReactiveObject, ILoggerViewModel
    {
        private ObservableCollection<LogEventInfo> internalLogEvents { get; }
        public ObservableCollection<LogEventInfo> LogEvents { get; }
        private string _selectedLogLevel;

        public LoggerViewModel(IConfigManager config)
        {
            _selectedLogLevel = config.Get("enslaver", "debug", "Trace");
            var memoryTarget = LogManager.Configuration.AllTargets.FirstOrDefault(t => t.Name == "m1") as ObservableTarget;
            if (memoryTarget == null)
            {
                memoryTarget = new ObservableTarget
                {
                    Layout = new SimpleLayout("${longdate}|${level:uppercase=true}|${logger}|${message}")
                };
                LogManager.Configuration.AddTarget("m1", memoryTarget);
                var rule = new LoggingRule("*", LogLevel.Trace, memoryTarget);
                LogManager.Configuration.LoggingRules.Add(rule);
                LogManager.ReconfigExistingLoggers();
            }

            internalLogEvents = new ObservableCollection<LogEventInfo>();
            LogEvents = new ObservableCollection<LogEventInfo>();

            this.ObservableForProperty(_ => _.SelectedLogLevel)
                .Select(_ => _.Value)
                .DistinctUntilChanged()
                .ObserveOnUI()
                .Subscribe(_ => FilterLogs());

            memoryTarget.WhenLogWrite
                .ObserveOnUI()
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Subscribe(l =>
                {
                    if (internalLogEvents.Count > 2000) internalLogEvents.Clear();
                    internalLogEvents.Add(l);
                    if (l.Level.Ordinal >= LogLevel.FromString(SelectedLogLevel).Ordinal)
                    {
                        if (LogEvents.Count > 2000) LogEvents.Clear();
                        LogEvents.Add(l);
                    }
                });

            config.WhenConfigChanges
                .Throttle(TimeSpan.FromMilliseconds(200), ThreadPoolScheduler.Instance)
                .Where(p => p.Keys.Contains("enslaver_debug"))
                .DistinctUntilChanged()
                .ObserveOnUI()
                .Subscribe(p =>
                {
                    SelectedLogLevel = config.Get("enslaver", "debug", SelectedLogLevel);
                });

            LogLevels = new List<string>(new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal", "Off" });
            FilterLogs();
        }

        private void FilterLogs()
        {
            LogEvents.Clear();
            foreach (var source in internalLogEvents.Where(e => e.Level.Ordinal >= LogLevel.FromString(SelectedLogLevel).Ordinal))
            {
                LogEvents.Add(source);
            }
        }

        public IEnumerable<string> LogLevels { get; }

        public string SelectedLogLevel
        {
            get { return _selectedLogLevel; }
            set { this.RaiseAndSetIfChanged(ref _selectedLogLevel, value); }
        }
    }
}