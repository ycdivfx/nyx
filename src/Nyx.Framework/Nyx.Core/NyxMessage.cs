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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Nyx.Core.FileTransfer;
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    /// <summary>
    ///     Nyx message class, used for hub and borgs communications.
    /// </summary>
    public class NyxMessage : INyxMessage
    {
        [JsonProperty("Id")]
        private Guid _id;

        /// <summary>
        ///     Nyx default construtor
        /// </summary>
        public NyxMessage()
        {
            Data = new Dictionary<string, object>();
            Files = new List<IFile>();
            Id = Guid.NewGuid();
            Direction = MessageDirection.Out;
        }

        /// <summary>
        ///     Copy constructor.
        /// </summary>
        /// <param name="refMessage"></param>
        public NyxMessage(NyxMessage refMessage)
        {
            Id = refMessage.Id;
            Data = new Dictionary<string, object>(refMessage.Data);
            Files = new List<IFile>(refMessage.Files);
            Action = refMessage.Action;
            Target = refMessage.Target;
            Source = refMessage.Source;
        }

        public NyxMessage(string target, string action) : this(target, action, string.Empty)
        {
        }

        /// <summary>
        ///     Nyx construtor
        /// </summary>
        /// <param name="target"></param>
        /// <param name="action"></param>
        /// <param name="source"></param>
        public NyxMessage(string target, string action, string source) : this()
        {
            Target = target;
            Action = action;
            Source = source;
            Files = new List<IFile>();
        }

        [JsonProperty("Data", ItemTypeNameHandling = TypeNameHandling.All)]
        private Dictionary<string, object> Data { get; }

        [JsonIgnore]
        public Guid Id
        {
            get { return _id; }
            internal set { _id = value; }
        }

        public string Target { get; set; }
        public string Source { get; set; }
        public string Action { get; set; }

        [JsonIgnore]
        public MessageDirection Direction { get; internal set; }

        public IList<IFile> Files { get; }

            /// <summary>
        ///     Read only view of the Data
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, object> Elements => new ReadOnlyDictionary<string, object>(Data);

        public static INyxMessage EmptyIn => new NyxMessage {Direction = MessageDirection.In}.AsReadOnly();

        public static INyxMessage EmptyOut => new NyxMessage { Direction = MessageDirection.Out }.AsReadOnly();

        /// <summary>
        ///     Add data to the message
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public INyxMessage Set(string key, object val)
        {
            Data[key] = val;
            return this;
        }

        /// <summary>
        /// Gets a Typed data from the Data dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            var defaultData = default(T);
            if (!Data.ContainsKey(key)) return defaultData;
            var o = Data[key];
            if (o is string && typeof (T) != typeof (string))
            {
                // Cast to string any type.
                var c = TypeDescriptor.GetConverter(typeof (T));
                try
                {
                    o = c.ConvertFromInvariantString(o.ToString());
                }
                catch (Exception)
                {
                    o = defaultData;
                }
            }
            else if (typeof (T) == typeof (int))
            {
                // Serializer makes number long type, so convert them to int in a safe way.
                o = Convert.ToInt32(o);
            }
            else if (typeof (T).IsEnum)
            {
                // Proper cast to enum
                o = Enum.ToObject(typeof (T), o);
            }
            try
            {
                return (T) o;
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("[Nyx] NyxMessage.GetData error. {0}", e));
                return defaultData;
            }
        }

        /// <summary>
        ///     Tries to get the element for this key.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="key">Key</param>
        /// <param name="prop">Data to get.</param>
        /// <returns></returns>
        public bool TryGet<T>(string key, out T prop)
        {
            prop = default(T);
            if (!Data.ContainsKey(key)) return false;
            prop = Get<T>(key);
            return true;
        }

        /// <summary>
        ///     Check for the element with this key present.
        /// </summary>
        /// <param name="key">Key to check.</param>
        /// <returns></returns>
        public bool Has(string key)
        {
            return Data.ContainsKey(key);
        }

        /// <summary>
        ///     Removes a element from the data store.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return Data.Remove(key);
        }

        /// <summary>
        ///     Adds a new file to the message
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <param name="deleteOnTransfer">Deletes file on transfer finish.</param>
        /// <returns></returns>
        public INyxMessage AddFile(string path, bool deleteOnTransfer = false)
        {
            if (!File.Exists(path)) return this;
            var filename = Path.GetFileName(path);
            Files.Add(new NyxFile {Name = filename, Path = path, DeleteOnTransfer = deleteOnTransfer});
            return this;
        }

        /// <summary>
        ///     Clears data
        /// </summary>
        public void ClearData()
        {
            Data.Clear();
        }

        public string this[string key]
        {
            get
            {
                return Convert.ToString(Data[key], CultureInfo.InvariantCulture);
            }
            set { Data[key] = value; }
        }

        /// <summary>
        ///     Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>
        ///     A new object that is a copy of this instance.
        /// </returns>
        public object Clone()
        {
            return new NyxMessage(this);
        }

        public override string ToString()
        {
            return string.Format("NyxMessage A:{5} I:{0} S:{1} T:{2} D:{3} FC:{4}", this.ShortId(), Source, Target, Direction, Files.Count, Action);
        }

        /// <summary>
        ///     Helper to create a NyxMessage
        /// </summary>
        /// nx.SendMessage  msg
        /// <param name="target"></param>
        /// <param name="action"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static NyxMessage Create(string target, string action, string source)
        {
            return new NyxMessage(target, action, source);
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Files.Clear();
                    Data.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources. 
        // ~NyxMessage() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }

    /// <summary>
    ///     This is just a helper class. Messages of this type are used for internal communication.
    ///     They never leave a <see cref="INyxNode"/>.
    /// </summary>
    public sealed class InternalNyxMessage : NyxMessage
    {
        /// <summary>
        /// The only construtor only accepts a true NyxMessage.
        /// </summary>
        /// <param name="msg"></param>
        public InternalNyxMessage(NyxMessage msg) : base(msg)
        {
            
        }

        public override string ToString()
        {
            return string.Format("InternalNyxMessage I:{0} S:{1} T:{2} D:{3} FC:{4}", this.ShortId(), Source, Target, Direction, Files.Count);
        }

    }
}