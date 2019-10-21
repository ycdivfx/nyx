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
using Nyx.Core.Messaging;

namespace Nyx.Core.Logging
{
    /// <summary>
    /// 
    /// </summary>
    public class LogEntryMessage : IMessage
    {
        public LogLevel Level { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }
        public object[] PropertyValues { get; private set; }
        public string FormatMessage { get { return string.Format(Message, PropertyValues); } }

        public LogEntryMessage(LogLevel logLevel, string message, Exception exception, object[] propertyValues)
        {
            Timestamp = DateTime.Now;
            Level = logLevel;
            Message = message;
            Exception = exception;
            PropertyValues = propertyValues;
        }
    }
}
