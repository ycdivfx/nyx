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

namespace Nyx.Core.Logging
{
    public static class LoggerExtensions
    {
        [StringFormatMethod("format")]
        public static void Trace<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Trace, format, args);
        }

        [StringFormatMethod("format")]
        public static void Debug<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Debug, format, args);
        }

        public static void Debug<T>(this ILogger<T> logger, string message, Exception exception)
        {
            logger.Log(LogLevel.Debug, message, exception);
        }

        [StringFormatMethod("format")]
        public static void Info<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Info, format, args);
        }

        public static void Info<T>(this ILogger<T> logger, string format, Exception exception)
        {
            logger.Log(LogLevel.Info, format, exception);
        }

        [StringFormatMethod("format")]
        public static void Warn<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Warn, format, args);
        }

        public static void Warn<T>(this ILogger<T> logger, string message, Exception exception)
        {
            logger.Log(LogLevel.Warn, message, exception);
        }

        [StringFormatMethod("format")]
        public static void Error<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Error, format, args);
        }

        public static void Error<T>(this ILogger<T> logger, string message, Exception exception)
        {
            logger.Log(LogLevel.Error, message, exception);
        }

        [StringFormatMethod("format")]
        public static void Fatal<T>(this ILogger<T> logger, string format, params object[] args)
        {
            logger.Log(LogLevel.Fatal, format, args);
        }

        public static void Fatal<T>(this ILogger<T> logger, string format, Exception exception)
        {
            logger.Log(LogLevel.Fatal, format, exception);
        }

    }
}