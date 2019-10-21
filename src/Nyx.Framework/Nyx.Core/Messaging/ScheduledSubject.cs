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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Threading;

namespace Nyx.Core.Messaging
{
    public class ScheduledSubject<T> : ISubject<T>
    {
        public ScheduledSubject(IScheduler scheduler, IObserver<T> defaultObserver = null,
            ISubject<T> defaultSubject = null)
        {
            _scheduler = scheduler;
            _defaultObserver = defaultObserver;
            _subject = defaultSubject ?? new Subject<T>();

            if (defaultObserver != null)
            {
                _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(_defaultObserver);
            }
        }

        private readonly IObserver<T> _defaultObserver;
        private readonly IScheduler _scheduler;
        private readonly ISubject<T> _subject;
        private int _observerRefCount = 0;
        private IDisposable _defaultObserverSub = Disposable.Empty;

        public void Dispose()
        {
            var subject = _subject as IDisposable;
            subject?.Dispose();
        }

        public void OnCompleted()
        {
            _subject.OnCompleted();
        }

        public void OnNext(T value)
        {
            _subject.OnNext(value);
        }

        public void OnError(Exception error)
        {
            _subject.OnError(error);
        }


        public IDisposable Subscribe(IObserver<T> observer)
        {
            Interlocked.Exchange(ref _defaultObserverSub, Disposable.Empty).Dispose();

            Interlocked.Increment(ref _observerRefCount);

            return new CompositeDisposable(
                _subject.ObserveOn(_scheduler).Subscribe(observer),
                Disposable.Create(() => {
                    if (Interlocked.Decrement(ref _observerRefCount) <= 0 && _defaultObserver != null)
                    {
                        _defaultObserverSub = _subject.ObserveOn(_scheduler).Subscribe(_defaultObserver);
                    }
                }));
        }
    }
}