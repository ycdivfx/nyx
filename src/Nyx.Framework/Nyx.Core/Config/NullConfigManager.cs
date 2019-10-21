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
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Nyx.Core.Config
{
    public class NullConfigManager : IConfigManager
    {
        protected Dictionary<string, object> Properties;
        protected Dictionary<string, object> Defaults;
        private readonly ISubject<ConfigChanged> _subjectChangedMessage; 
        private static readonly object SyncLock = new object();

        public NullConfigManager()
        {
            Properties = new Dictionary<string, object>();
            Defaults = new Dictionary<string, object>();
            _subjectChangedMessage = new BehaviorSubject<ConfigChanged>(new ConfigChanged(this, Enumerable.Empty<string>()));
        }

        public IObservable<ConfigChanged> WhenConfigChanges => _subjectChangedMessage.AsObservable();
        /// <summary>
        /// Sets a config value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="section">Section</param>
        public virtual void Set<T>(string section, string name, T value)
        {
            var prop = name;
            if (!string.IsNullOrWhiteSpace(section)) prop = section + "_" + name;
            Properties[prop] = value;
            _subjectChangedMessage.OnNext(new ConfigChanged(this, new[] { prop }));
        }

        /// <summary>
        /// Sets a config using a complete section + name value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public virtual void Set<T>(string name, T value)
        {
            Set(string.Empty, name, value);
        }

        /// <summary>
        /// Sets many config values at the same time
        /// </summary>
        /// <param name="items"></param>
        public virtual void SetMany(IDictionary<string, object> items)
        {
            foreach (var item in items)
                Properties.Add(item.Key, item.Value);
            _subjectChangedMessage.OnNext(new ConfigChanged(this, items.Keys));
        }

        /// <summary>
        /// Gets a property from the configuratio or returns a registered default value if it exists.
        /// If no default exists its added to the defaults. Those are never saved.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="section">Config section</param>
        /// <param name="name">Config property name</param>
        /// <param name="defaultValue">Property default value</param>
        /// <returns></returns>
        public virtual T Get<T>(string section, string name, T defaultValue = default(T))
        {
            var prop = name;
            if (!string.IsNullOrWhiteSpace(section)) prop = section + "_" + name;
            T result = defaultValue;
            try
            {

                // Tries first the normal container
                object value;
                if (Properties.TryGetValue(prop, out value))
                {
                    if (typeof (T).IsEnum)
                        return (T) Enum.ToObject(typeof (T), value);
                    if (typeof (T) == typeof (int))
                        return (T) (object) Convert.ToInt32(value);
                    return (T) value;
                }
                lock (SyncLock)
                {

                    // If failed added the default to the defaults.
                    if (!Defaults.ContainsKey(prop))
                        Defaults.Add(prop, defaultValue);

                    // Returns the default so no different defaults are used.
                    // Defaults never get saved.
                    if (typeof (T).IsEnum)
                        result = (T) Enum.ToObject(typeof (T), Defaults[prop]);
                    else
                        result = (T) Defaults[prop];
                }
            }
            catch (Exception ex)
            {
                Debug.Write(string.Format("NullConfigManager: Error - Section:{0} Name:{1}\n{2}", section, name, ex));
            } 
            return result;
        }

        /// <summary>
        /// Gets a property from the configuratio or returns a registered default value if it exists.
        /// If no default exists its added to the defaults. Those are never saved.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="name">Config property name with section included. Format is section_prop.</param>
        /// <param name="defaultValue">Property default value</param>
        /// <returns></returns>
        public virtual T Get<T>(string name, T defaultValue = default(T))
        {
            return Get(string.Empty, name, defaultValue);
        }

        public virtual IDictionary<string, object> GetMany(string section)
        {
            var result = Properties.Where(k => k.Key.StartsWith(section)).ToDictionary(property => property.Key, property => property.Value);
            return result;
        }

        /// <summary>
        /// Setups configuration manager
        /// </summary>
        /// <param name="args"></param>
        public virtual void Setup(params object[] args)
        {
            
        }
    }
}
