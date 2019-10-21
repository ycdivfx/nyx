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

namespace Nyx.Core.Config
{
    public interface IConfigManager
    {
        /// <summary>
        ///     Observes config changes. This is a Hot observable, never completes.
        /// </summary>
        IObservable<ConfigChanged> WhenConfigChanges { get; }

        /// <summary>
        /// Sets a config value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="section"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void Set<T>(string section, string name, T value);

        void Set<T>(string name, T value);

        /// <summary>
        /// Sets many config values at the same time
        /// </summary>
        /// <param name="items"></param>
        void SetMany(IDictionary<string, object> items);

        /// <summary>
        ///     Gets a value from the given section and property.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="section"></param>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        T Get<T>(string section, string name, T defaultValue = default(T));

        /// <summary>
        ///     Gets a value from the given section and property.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        T Get<T>(string name, T defaultValue = default(T));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        IDictionary<string, object> GetMany(string section);

        /// <summary>
        ///     Setups ConfigManager.
        /// </summary>
        /// <param name="args"></param>
        void Setup(params object[] args);
    }
}