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
using NLog;
using Nyx.Common.UI.ViewModels;

namespace Nyx.Common.UI.Designer
{
    public class DesignerLoggerViewModel : BaseViewModel, ILoggerViewModel
    {
        private string _selectedLogLevel;

        public DesignerLoggerViewModel()
        {
            var rnd = new Random();
            LogEvents = new ObservableCollection<LogEventInfo>();
            for (int i = 0; i < 100; i++)
            {
                LogEvents.Add(new LogEventInfo(LogLevel.FromOrdinal(rnd.Next(1,5)), "Test", "Log test " + i));
            }
            LogLevels = new List<string> (new[] {"Trace","Debug","Info", "Warn","Error"});
            _selectedLogLevel = "Trace";
        }

        public ObservableCollection<LogEventInfo> LogEvents { get; }

        public IEnumerable<string> LogLevels { get; }

        public string SelectedLogLevel
        {
            get { return _selectedLogLevel; }
            set { SetProperty(ref _selectedLogLevel, value); }
        }
    }
}
