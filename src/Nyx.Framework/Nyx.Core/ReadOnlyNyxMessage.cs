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
using Nyx.Core.FileTransfer;
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    public sealed class ReadOnlyNyxMessage : INyxMessage
    {
        private readonly string _target;
        private readonly string _source;
        private readonly string _action;

        public ReadOnlyNyxMessage(INyxMessage message)
        {
            Id = message.Id;
            _target = message.Target;
            _source = message.Source;
            _action = message.Action;
            Direction = message.Direction;
            Elements = new ReadOnlyDictionary<string, object>((IDictionary<string, object>) message.Elements);
            Files = new ReadOnlyCollection<IFile>(message.Files);
        }

        public object Clone()
        {
            return new ReadOnlyNyxMessage(this);
        }

        public Guid Id { get; }

        public string Target
        {
            get { return _target; }
            set { throw new InvalidOperationException("ReadOnly message."); }
        }

        public string Source
        {
            get { return _source; }
            set { throw new InvalidOperationException("ReadOnly message."); }
        }

        public string Action
        {
            get { return _action; }
            set { throw new InvalidOperationException("ReadOnly message."); }
        }

        public MessageDirection Direction { get; }

        public IList<IFile> Files { get; }

        public string this[string key]
        {
            get { return Elements.ContainsKey(key) ? Elements[key].ToString() : null; }
            set { throw new InvalidOperationException("ReadOnly message."); }
        }

        public IReadOnlyDictionary<string, object> Elements { get; }

        public INyxMessage Set(string key, object val)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key)
        {
            var defaultData = default(T);
            if (!Elements.ContainsKey(key)) return defaultData;
            var o = Elements[key];
            if (o is string && typeof(T) != typeof(string))
            {
                // Cast to string any type.
                var c = TypeDescriptor.GetConverter(typeof(T));
                try
                {
                    o = c.ConvertFromInvariantString(o.ToString());
                }
                catch (Exception)
                {
                    o = defaultData;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                // Serializer makes number long type, so convert them to int in a safe way.
                o = Convert.ToInt32(o);
            }
            else if (typeof(T).IsEnum)
            {
                // Proper cast to enum
                o = Enum.ToObject(typeof(T), o);
            }
            try
            {
                return (T)o;
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("[Nyx] NyxMessage.GetData error. {0}", e));
                return defaultData;
            }
        }

        public bool TryGet<T>(string key, out T prop)
        {
            prop = default(T);
            if (!Elements.ContainsKey(key)) return false;
            prop = Get<T>(key);
            return true;
        }

        public bool Has(string key)
        {
            return Elements.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return false;
        }

        public void ClearData()
        {
        }

        public INyxMessage AddFile(string path, bool deleteOnTransfer = false)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Format("ReadOnlyNyxMessage I:{0} S:{1} T:{2} D:{3} FC:{4}", this.ShortId(), Source, Target, Direction, Files.Count);
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Files.Clear();
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
}
