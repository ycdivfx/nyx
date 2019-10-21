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
using System.Threading;

namespace Nyx.Core.Threading
{

    /// <summary>
    /// From http://stackoverflow.com/questions/3670914/serial-task-executor-is-this-thread-safe
    /// </summary>
    public sealed class SerialAsyncTasker
    {
        private readonly Queue<Action> _tasks = new Queue<Action>();
        private volatile bool _taskExecuting;

        /// <summary>
        /// Queue a new task for asynchronous execution on the thread pool.
        /// </summary>
        /// <param name="task">Task to execute</param>
        public void QueueTask(Action task)
        {
            if (task == null) throw new ArgumentNullException("task");

            lock (_tasks)
            {
                bool isFirstTask = (_tasks.Count == 0);
                _tasks.Enqueue(task);

                //Only start executing the task if this is the first task
                //Additional tasks will be executed normally as part of sequencing
                if (isFirstTask && !_taskExecuting)
                    RunNextTask();
            }
        }

        /// <summary>
        /// Clear all queued tasks.  Any task currently executing will continue to execute.
        /// </summary>
        public void Clear()
        {
            lock (_tasks)
            {
                _tasks.Clear();
            }
        }

        /// <summary>
        /// Wait until all currently queued tasks have completed executing.
        /// If no tasks are queued, this method will return immediately.
        /// This method does not prevent the race condition of a second thread 
        /// queueing a task while one thread is entering the wait;
        /// if this is required, it must be synchronized externally.
        /// </summary>
        public void WaitUntilAllComplete()
        {
            lock (_tasks)
            {
                while (_tasks.Count > 0 || _taskExecuting)
                    Monitor.Wait(_tasks);
            }
        }

        private void RunTask(Object state)
        {
            var task = (Action) state;
            task();
            _taskExecuting = false;
            RunNextTask();
        }

        private void RunNextTask()
        {
            lock (_tasks)
            {
                if (_tasks.Count > 0)
                {
                    _taskExecuting = true;
                    var task = _tasks.Dequeue();
                    ThreadPool.QueueUserWorkItem(RunTask, task);
                }
                else
                {
                    //If anybody is waiting for tasks to be complete, let them know
                    Monitor.PulseAll(_tasks);
                }
            }
        }
    }
}
