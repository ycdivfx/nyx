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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nyx.Common.UI.Rules;

namespace Nyx.Common.UI
{
    public class NotifyDataErrorInfo<T> : BaseViewModel, INotifyDataErrorInfo
        where T : NotifyDataErrorInfo<T>
    {
        #region Fields
        private const string HasErrorsPropertyName = "HasErrors";
        private static readonly RuleCollection<T> _rules = new RuleCollection<T>();
        private Dictionary<string, List<object>> _errors;
        #endregion
        #region Public Events
        /// <summary>
        /// Occurs when the validation errors have changed for a property or for the entire object.
        /// </summary>
        event EventHandler<DataErrorsChangedEventArgs> INotifyDataErrorInfo.ErrorsChanged
        {
            add { errorsChanged += value; }
            remove { errorsChanged -= value; }
        }
        #endregion
        #region Private Events
        /// <summary>
        /// Occurs when the validation errors have changed for a property or for the entire object.
        /// </summary>
        private event EventHandler<DataErrorsChangedEventArgs> errorsChanged;
        #endregion
        #region Public Properties
        /// <summary>
        /// Gets the when errors changed observable event. Occurs when the validation errors have changed for a property or for the entire object.
        /// </summary>
        /// <value>
        /// The when errors changed observable event.
        /// </value>
        public IObservable<string> WhenErrorsChanged
        {
            get
            {
                return Observable
                    .FromEventPattern<DataErrorsChangedEventArgs>(
                        h => errorsChanged += h,
                        h => errorsChanged -= h)
                    .Select(x => x.EventArgs.PropertyName);
            }
        }
        /// <summary>
        /// Gets a value indicating whether the object has validation errors.
        /// </summary>
        /// <value><c>true</c> if this instance has errors, otherwise <c>false</c>.</value>
        public virtual bool HasErrors
        {
            get
            {
                InitializeErrors();
                return _errors.Count > 0;
            }
        }
        #endregion
        #region Protected Properties
        /// <summary>
        /// Gets the rules which provide the errors.
        /// </summary>
        /// <value>The rules this instance must satisfy.</value>
        protected static RuleCollection<T> Rules => _rules;

        /// <summary>
        /// Gets the validation errors for the entire object.
        /// </summary>
        /// <returns>A collection of errors.</returns>
        public IEnumerable GetErrors()
        {
            return GetErrors(null);
        }
        /// <summary>
        /// Gets the validation errors for a specified property or for the entire object.
        /// </summary>
        /// <param name="propertyName">Name of the property to retrieve errors for. <c>null</c> to
        /// retrieve all errors for this instance.</param>
        /// <returns>A collection of errors.</returns>
        public IEnumerable GetErrors(string propertyName)
        {
            Debug.Assert(
                string.IsNullOrEmpty(propertyName) ||
                (GetType().GetRuntimeProperty(propertyName) != null),
                "Check that the property name exists for this instance.");
            InitializeErrors();
            IEnumerable result;
            if (string.IsNullOrEmpty(propertyName))
            {
                List<object> allErrors = new List<object>();
                foreach (KeyValuePair<string, List<object>> keyValuePair in _errors)
                {
                    allErrors.AddRange(keyValuePair.Value);
                }
                result = allErrors;
            }
            else
            {
                if (_errors.ContainsKey(propertyName))
                {
                    result = _errors[propertyName];
                }
                else
                {
                    result = new List<object>();
                }
            }
            return result;
        }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (string.IsNullOrEmpty(propertyName))
            {
                ApplyRules();
            }
            else
            {
                ApplyRules(propertyName);
            }
            base.OnPropertyChanged(HasErrorsPropertyName);
        }
        /// <summary>
        /// Called when the errors have changed.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void OnErrorsChanged([CallerMemberName] string propertyName = null)
        {
            Debug.Assert(
                string.IsNullOrEmpty(propertyName) ||
                (GetType().GetRuntimeProperty(propertyName) != null),
                "Check that the property name exists for this instance.");
            EventHandler<DataErrorsChangedEventArgs> eventHandler = errorsChanged;
            eventHandler?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Applies all rules to this instance.
        /// </summary>
        private void ApplyRules()
        {
            InitializeErrors();
            foreach (var propertyName in _rules.Select(x => x.PropertyName))
            {
                ApplyRules(propertyName);
            }
        }
        /// <summary>
        /// Applies the rules to this instance for the specified property.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void ApplyRules(string propertyName)
        {
            InitializeErrors();
            List<object> propertyErrors = _rules.Apply((T)this, propertyName).ToList();
            if (propertyErrors.Count > 0)
            {
                if (_errors.ContainsKey(propertyName))
                {
                    _errors[propertyName].Clear();
                }
                else
                {
                    _errors[propertyName] = new List<object>();
                }
                _errors[propertyName].AddRange(propertyErrors);
                OnErrorsChanged(propertyName);
            }
            else if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                OnErrorsChanged(propertyName);
            }
        }
        /// <summary>
        /// Initializes the errors and applies the rules if not initialized.
        /// </summary>
        private void InitializeErrors()
        {
            if (_errors != null) return;
            _errors = new Dictionary<string, List<object>>();
            ApplyRules();
        }
        #endregion
    }
}
