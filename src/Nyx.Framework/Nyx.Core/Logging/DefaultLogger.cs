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
using JetBrains.Annotations;
using NLog;

namespace Nyx.Core.Logging
{
    /// <summary>
    /// Default logger. It uses NLog internally.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DefaultLogger<T> : ILogger<T>
    {
        private readonly Logger _log;

        public DefaultLogger()
        {
            _log = LogManager.GetLogger(typeof(T).FullName);
        }

        public virtual void Log(LogLevel logLevel, [NotNull] string format, object[] args)
        {
            _log.Log(typeof(T), LogEventInfo.Create(NLog.LogLevel.FromOrdinal(((int)logLevel)), _log.Name, null, format, args));
        }

        public virtual void Log(LogLevel logLevel, string message, Exception exception)
        {
            _log.Log(typeof(T), LogEventInfo.Create(NLog.LogLevel.FromOrdinal(((int)logLevel)), _log.Name, message, exception));
        }
    }
}
