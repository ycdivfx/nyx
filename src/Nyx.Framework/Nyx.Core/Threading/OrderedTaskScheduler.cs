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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;

namespace Nyx.Core.Threading
{
    /// <summary>
    ///     Orderer task scheduler. We can use this to make task run one at a time in order since ThreadPool can't make this.
    /// </summary>
    public class OrderedTaskScheduler : LocalScheduler
    {
        private readonly Queue<Action> _tasks = new Queue<Action>();
        private int _runningTaskCount;
        private readonly int _maxQueue = -1;
        private static readonly object SyncLock = new object();

        public static OrderedTaskScheduler NewInstance => new OrderedTaskScheduler();

        public OrderedTaskScheduler()
        {
        }

        public OrderedTaskScheduler(int maxQueue)
        {
            _maxQueue = maxQueue;
        }

        public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            var d = new SingleAssignmentDisposable();
            lock (SyncLock)
            {
                if (_maxQueue > 0 && _tasks.Count > _maxQueue) return Disposable.Empty;
                _tasks.Enqueue(() =>
                {
                    if (d.IsDisposed)
                        return;
                    d.Disposable = action(this, state);
                });
            }
            ProcessTaskQueue();
            return d;
        }

        private void ProcessTaskQueue()
        {
            lock (SyncLock)
            {
                if (_runningTaskCount != 0 || _tasks.Count == 0) return;
                var action = _tasks.Dequeue();
                if (action == null) return;
                QueueUserWorkItem(action);
            }
        }

        private void QueueUserWorkItem(Action action)
        {
            Action completionTask = () =>
            {
                action();
                OnTaskCompleted();
            };
            _runningTaskCount++;
            ThreadPool.QueueUserWorkItem(_ => completionTask());
        }

        private void OnTaskCompleted()
        {
            lock (SyncLock)
            {
                if (--_runningTaskCount == 0)
                {
                    ProcessTaskQueue();
                }
            }
        }
    }
}
