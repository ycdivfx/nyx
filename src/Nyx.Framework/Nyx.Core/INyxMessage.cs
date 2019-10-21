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
using Nyx.Core.FileTransfer;
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    /// <summary>
    /// This is the main class for doing all the data exchange between nodes in the system.
    /// </summary>
    public interface INyxMessage : ICloneable, IDisposable
    {
        /// <summary>
        /// Unique Id of the message
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Target to deliver the message.
        /// </summary>
        string Target { get; set; }

        /// <summary>
        /// Source of the message.
        /// </summary>
        string Source { get; set; }

        /// <summary>
        /// Action to process in the Target.
        /// </summary>
        string Action { get; set;}

        /// <summary>
        /// Direction of the message, controlled by the system.
        /// </summary>
        MessageDirection Direction { get; }

        ///// <summary>
        ///// Property bag for the message.
        ///// </summary>
        //Dictionary<string, object> Data { get; }

        /// <summary>
        /// List of INyxFile to transfer.
        /// </summary>
        IList<IFile> Files { get; }

        /// <summary>
        /// Gets or sets a string value to the data store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string this[string key] { get; set; }

        IReadOnlyDictionary<string, object> Elements { get; }

            /// <summary>
        /// Deprecated, use the Data property directly. INyxMessage.Data.Add(key, val).
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        INyxMessage Set(string key, object val);

        /// <summary>
        /// Gets a Typed data from the Data dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T Get<T>(string key);

        /// <summary>
        /// Tries to get the element for this key.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="key">Key</param>
        /// <param name="prop">Data to get.</param>
        /// <returns></returns>
        bool TryGet<T>(string key, out T prop);

        /// <summary>
        /// Check for the element with this key present.
        /// </summary>
        /// <param name="key">Key to check.</param>
        /// <returns></returns>
        bool Has(string key);

        /// <summary>
        /// Removes a element from the data store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(string key);

        /// <summary>
        /// Clears all the Data. Deprecated. Use INyxMessage.Data.Clear()
        /// </summary>
        void ClearData();

        /// <summary>
        /// Adds file to msg
        /// </summary>
        /// <param name="path"></param>
        /// <param name="deleteOnTransfer">Deletes file after transfer completes.</param>
        /// <returns></returns>
        INyxMessage AddFile(string path, bool deleteOnTransfer=false);
        
    }
}
