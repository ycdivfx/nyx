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
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Nyx.Core.Logging;
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    public static class ObservableExtensions
    {
        /// <summary>
        ///     Logs whats happening in the observable to the logger.
        /// </summary>
        /// <typeparam name="T">Observable type.</typeparam>
        /// <typeparam name="TL">Logger type</typeparam>
        /// <param name="source">Observable</param>
        /// <param name="logger">Logger.</param>
        /// <param name="key">Key for adding to the log.</param>
        /// <returns></returns>
        public static IObservable<T> LogTo<T, TL>(this IObservable<T> source, ILogger<TL> logger, string key = null)
        {
            return Observable.Create<T>(observer =>
                source.Materialize().Subscribe(notification =>
                {
                    logger.Debug(NotifyToString(notification, key));
                    notification.Accept(observer);
                }));
        }

        /// <summary>
        ///     Like Catch, but also prints a message and the error to the log.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="logger"></param>
        /// <param name="next">The Observable to replace the current one OnError.</param>
        /// <param name="message">An error message to print.</param>
        /// <returns>The same Observable</returns>
        public static IObservable<T> LogCatchTo<T, TL>(this IObservable<T> source, ILogger<TL> logger,
            IObservable<T> next = null, 
            string message = null)
        {
            next = next ?? Observable.Return(default(T));
            return source.Catch<T, Exception>(ex => {
                logger.Warn(message ?? "", ex);
                return next;
            });
        }

        /// <summary>
        ///     Like Catch, but also prints a message and the error to the log.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="logger"></param>
        /// <param name="next">A Func to create an Observable to replace the current one OnError.</param>
        /// <param name="message">An error message to print.</param>
        /// <returns>The same Observable</returns>
        public static IObservable<T> LogCatchTo<T, TL, TException>(this IObservable<T> source, 
            ILogger<TL> logger,
            Func<TException, IObservable<T>> next,
            string message = null) where TException : Exception
        {
            return source.Catch<T, TException>(ex => {
                logger.Warn(message ?? "", ex);
                return next(ex);
            });
        }

        /// <summary>
        ///     Converts the <see cref="Notification"/> to a string, with the optional key.
        /// </summary>
        /// <typeparam name="T">Type of the notification.</typeparam>
        /// <param name="notification">The notification to convert to string.</param>
        /// <param name="key">Extra key string to add to the output.</param>
        /// <returns></returns>
        public static string NotifyToString<T>(Notification<T> notification, string key = null)
        {
            if (key != null)
                return key + "\t" + notification;

            return notification.ToString();
        }

        /// <summary>
        ///     Timeouts the message and returns a Failed message status.
        /// </summary>
        /// <param name="observable"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static IObservable<MessageStatus> TimeoutMessage(this IObservable<MessageStatus> observable, double timeoutSeconds, INyxMessage msg)
        {
            return observable
                .Amb(Observable.Return(msg.Timeout(null, "Timeout waiting for message.")).Delay(TimeSpan.FromSeconds(timeoutSeconds*2)));
        }

        public static IObservable<MessageStatus> IsReplyTo(this IObservable<MessageStatus> observable, INyxMessage message)
        {
            return observable.Where(m => m.Message?.IsReplyTo(message) ?? false);
        }

        public static IObservable<INyxMessage> IsReplyTo(this IObservable<INyxMessage> observable, INyxMessage message)
        {
            return observable.Where(m => m?.IsReplyTo(message) ?? false);
        }

        public static IObservable<T> RepeatLastValueDuringSilence<T>(this IObservable<T> inner, TimeSpan maxQuietPeriod)
        {
            return inner.Select(x =>
                Observable.Interval(maxQuietPeriod)
                          .Select(_ => x)
                          .StartWith(x)
            ).Switch();
        }

        public static IObservable<T> ObserveOnPool<T>(this IObservable<T> source)
        {
            return source.ObserveOn(ThreadPoolScheduler.Instance);
        }
    }
}
