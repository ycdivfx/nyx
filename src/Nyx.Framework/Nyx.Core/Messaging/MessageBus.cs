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
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Nyx.Core.Messaging
{
    /// <summary>
    /// An implementation of http://msdn.microsoft.com/en-us/library/ff647328.aspx
    /// It should help unknowed components comunicate with each other, could
    /// also be used to event messaging (less error prune to leaks).
    /// All messages are synchronous.
    /// Also grabed from https://github.com/hadouken/hadouken/blob/develop/src/Core/Hadouken.Common/Messaging/MessageBus.cs
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private readonly Dictionary<Tuple<Type, string>, NotAWeakReference> _messageBus =
            new Dictionary<Tuple<Type, string>, NotAWeakReference>();

        private readonly IDictionary<Tuple<Type, string>, IScheduler> _schedulerMappings =
            new Dictionary<Tuple<Type, string>, IScheduler>();

        private readonly Func<IEnumerable<IMessageHandler>> _handlers;
        private readonly IDictionary<Type, IList<object>> _callbacks;

        public static IMessageBus Current { get; private set; }

        /// <summary>
        /// Default construtor
        /// </summary>
        /// <param name="handlers"></param>
        public MessageBus(Func<IEnumerable<IMessageHandler>> handlers)
        {
            _handlers = handlers;
            _callbacks = new Dictionary<Type, IList<object>>();
            Current = this;
        }

        /// <summary>
        /// Registers a scheduler for the type, which may be specified at runtime, and the contract.
        /// </summary>
        /// <remarks>If a scheduler is already registered for the specified runtime and contract, this will overrwrite the existing registration.</remarks>
        /// <typeparam name="T">The type of the message to listen to.</typeparam>
        /// <param name="scheduler">The scheduler on which to post the
        /// notifications for the specified type and contract. CurrentThreadScheduler by default.</param>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        public void RegisterScheduler<T>(IScheduler scheduler, string contract = null)
        {
            _schedulerMappings[new Tuple<Type, string>(typeof(T), contract)] = scheduler;
        }

        /// <summary>
        /// Listen provides an Observable that will fire whenever a Message is
        /// provided for this object via RegisterMessageSource or SendMessage.
        /// </summary>
        /// <typeparam name="T">The type of the message to listen to.</typeparam>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        /// <returns>An Observable representing the notifications posted to the
        /// message bus.</returns>
        public IObservable<T> Listen<T>(string contract = null)
        {
            return SetupSubjectIfNecessary<T>(contract).Skip(1);
        }

        /// <summary>
        /// Listen provides an Observable that will fire whenever a Message is
        /// provided for this object via RegisterMessageSource or SendMessage.
        /// </summary>
        /// <typeparam name="T">The type of the message to listen to.</typeparam>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        /// <returns>An Observable representing the notifications posted to the
        /// message bus.</returns>
        public IObservable<T> ListenIncludeLatest<T>(string contract = null)
        {
            return SetupSubjectIfNecessary<T>(contract);
        }

        /// <summary>
        /// Determines if a particular message Type is registered.
        /// </summary>
        /// <param name="type">The Type of the message to listen to.</param>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        /// <returns>True if messages have been posted for this message Type.</returns>
        public bool IsRegistered(Type type, string contract = null)
        {
            var ret = false;
            WithMessageBus(type, contract, (mb, tuple) => { ret = mb.ContainsKey(tuple) && mb[tuple].IsAlive; });
            return ret;
        }

        /// <summary>
        /// Registers an Observable representing the stream of messages to send.
        /// Another part of the code can then call Listen to retrieve this
        /// Observable.
        /// </summary>
        /// <typeparam name="T">The type of the message to listen to.</typeparam>
        /// <param name="source">An Observable that will be subscribed to, and a
        /// message sent out for each value provided.</param>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        public IDisposable RegisterMessageSource<T>(IObservable<T> source, string contract = null)
        {
            return source.Subscribe(SetupSubjectIfNecessary<T>(contract));
        }

        /// <summary>
        /// Sends a single message using the specified Type and contract.
        /// Consider using RegisterMessageSource instead if you will be sending
        /// messages in response to other changes such as property changes
        /// or events.
        /// </summary>
        /// <typeparam name="T">The type of the message to send.</typeparam>
        /// <param name="message">The actual message to send</param>
        /// <param name="contract">A unique string to distinguish messages with
        /// identical types (i.e. "MyCoolViewModel") - if the message type is
        /// only used for one purpose, leave this as null.</param>
        public void SendMessage<T>(T message, string contract = null)
        {
            SetupSubjectIfNecessary<T>(contract).OnNext(message);
        }

        /// <summary>
        /// Publish a message to the MessageBus
        /// </summary>
        /// <typeparam name="T">A IMessage subclass</typeparam>
        /// <param name="message">The message itself</param>
        public void Publish<T>(T message) where T : class, IMessage
        {
            if (message == null) throw new ArgumentNullException("message");

            var t = typeof (T);

            if (_callbacks.ContainsKey(t))
            {
                var callbacks = _callbacks[t].OfType<Action<T>>().ToList();

                //_logger.Trace(string.Format("Sending {0} to {1} callbacks.", t.Name, callbacks.Count));

                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback(message);
                    }
                    catch (Exception e)
                    {
                        Debug.Write("[NyxMessageBus] " + e);
                    }
                }
            }

            // Find handlers
            var handlerType = typeof (IMessageHandler<T>);
            var handlers = (from handler in _handlers()
                let type = handler.GetType()
                where handlerType.IsAssignableFrom(type)
                select (IMessageHandler<T>) handler).ToList();

            //_logger.Trace(string.Format("Sending {0} to {1} handlers.", t.Name, handlers.Count));

            foreach (var handler in handlers)
            {
                try
                {
                    handler.Handle(message);
                }
                catch (Exception e)
                {
                    Debug.Write("[NyxMessageBus] " + e);
                }
            }
        }

        /// <summary>
        /// Subscribe to a given message.
        /// </summary>
        /// <typeparam name="T">A IMessage subclass</typeparam>
        /// <param name="callback">Action callback</param>
        public void Subscribe<T>(Action<T> callback) where T : IMessage
        {
            var t = typeof (T);

            // Add empty list
            if (!_callbacks.ContainsKey(t)) _callbacks.Add(t, new List<object>());

            // Add callback to list
            _callbacks[t].Add(callback);
        }

        /// <summary>
        /// Unsubscribe to a given message.
        /// </summary>
        /// <typeparam name="T">A IMessage subclass</typeparam>
        /// <param name="callback">Action callback</param>
        public void Unsubscribe<T>(Action<T> callback) where T : IMessage
        {
            var t = typeof (T);

            if (!_callbacks.ContainsKey(t)) return;

            _callbacks[t].Remove(callback);
        }

        private ISubject<T> SetupSubjectIfNecessary<T>(string contract)
        {
            ISubject<T> ret = null;

            WithMessageBus(typeof (T), contract, (mb, tuple) =>
            {
                NotAWeakReference subjRef;
                if (mb.TryGetValue(tuple, out subjRef) && subjRef.IsAlive)
                {
                    ret = (ISubject<T>) subjRef.Target;
                    return;
                }

                ret = new ScheduledSubject<T>(GetScheduler(tuple), null, new BehaviorSubject<T>(default(T)));
                mb[tuple] = new NotAWeakReference(ret);
            });

            return ret;
        }

        private void WithMessageBus(Type type, string contract,
            Action<Dictionary<Tuple<Type, string>, NotAWeakReference>, Tuple<Type, string>> block)
        {
            lock (_messageBus)
            {
                var tuple = new Tuple<Type, string>(type, contract);
                block(_messageBus, tuple);
                if (_messageBus.ContainsKey(tuple) && !_messageBus[tuple].IsAlive)
                {
                    _messageBus.Remove(tuple);
                }
            }
        }

        private IScheduler GetScheduler(Tuple<Type, string> tuple)
        {
            IScheduler scheduler;
            _schedulerMappings.TryGetValue(tuple, out scheduler);
            return scheduler ?? CurrentThreadScheduler.Instance;
        }
    }

    internal class NotAWeakReference
    {
        public NotAWeakReference(object target)
        {
            Target = target;
        }

        public object Target { get; }

        public bool IsAlive => true;
    }
}
