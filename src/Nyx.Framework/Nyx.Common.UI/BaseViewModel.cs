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
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nyx.Common.UI
{
    public abstract class BaseViewModel : IDisposable, INotifyPropertyChanged
    {
        private bool _isDirty;
        private bool _checkDirty;
        private volatile bool _disposed;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { propertyChanged += value; }
            remove { propertyChanged -= value; }
        }

        public void DisableDirtyFlag()
        {
            _checkDirty = false;
        }

        public void EnableDirtyFlag()
        {
            _checkDirty = true;
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set { if(_checkDirty) SetProperty(ref _isDirty, value);}
        }

        // ReSharper disable once InconsistentNaming
        private event PropertyChangedEventHandler propertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var eventHandler = propertyChanged;
            eventHandler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void SetProperty<TProperty>(ref TProperty field, TProperty value, [CallerMemberName]string name="")
        {
            ThrowIfDisposed();
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
            IsDirty = true;
        }

        protected void SetProperty(Func<bool> equal, Action action, [CallerMemberName] string propertyName="")
        {
            ThrowIfDisposed();
            if (equal()) return;
            action();
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyNames">The property names.</param>
        protected void OnPropertyChanged(params string[] propertyNames)
        {
            if (propertyNames == null)
            {
                throw new ArgumentNullException("propertyNames");
            }

            foreach (string propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Gets the when property changed observable event. Occurs when a property value changes.
        /// </summary>
        /// <value>
        /// The when property changed observable event.
        /// </value>
        public IObservable<string> WhenPropertyChanged
        {
            get
            {
                ThrowIfDisposed();
                return Observable
                    .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                        h => propertyChanged += h,
                        h => propertyChanged -= h)
                    .Select(x => x.EventArgs.PropertyName);
            }
        }

        private void ThrowIfDisposed()
        {
            if(_disposed)
                throw new ObjectDisposedException("BaseViewModel");
        }

        public virtual void Dispose()
        {
            _disposed = true;
        }
    }
}