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
using System.IO;
using Newtonsoft.Json;
using Nyx.Core.Logging;

namespace Nyx.Core.Config
{
    /// <summary>
    /// Json Configuration
    /// </summary>
    public class JsonConfigManager : NullConfigManager, IDisposable
    {
        public ILogger<JsonConfigManager> Logger { get; set; }
        private string _filename;
        private readonly FileSystemWatcher _watcher = new FileSystemWatcher();
        private bool _saving;
        private static readonly object SyncLock = new object();

        public JsonConfigManager()
        {
            _watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName;
            _watcher.Changed += ConfigDirChanged;
            _watcher.Created += ConfigDirChanged;
        }

        /// <summary>
        /// Reloads config on detected changes
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event args</param>
        private void ConfigDirChanged(object sender, FileSystemEventArgs e)
        {
            if(_saving) return;
            if (e.Name != _filename) return;
            lock (SyncLock) Reload();
        }

        /// <summary>
        /// Sets a config value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="section">Section</param>
        public override void Set<T>(string section, string name, T value)
        {
            base.Set(section, name, value);
            lock (SyncLock) Save();
        }

        /// <summary>
        /// Sets many config values at the same time
        /// </summary>
        /// <param name="items"></param>
        public override void SetMany(IDictionary<string, object> items)
        {
            base.SetMany(items);
            lock(SyncLock) Save();
        }

        /// <summary>
        /// Setups configuration manager
        /// </summary>
        /// <param name="args"></param>
        public override void Setup(params object[] args)
        {
            if (args.Length != 1 || !(args[0] is string)) return;
            _filename = (string) args[0];
            lock (SyncLock)
            {
                Logger?.Trace(string.Format("Setting save path to '{0}'.", _filename));
                Reload();
            }
        }

        /// <summary>
        /// Reloads config.
        /// </summary>
        private void Reload()
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Filter = "*" + Path.GetExtension(_filename);
                _watcher.Path = Path.GetDirectoryName(_filename);
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger?.Error("Error setting watcher.", ex);
            }
            if (!File.Exists(_filename)) return;
            _saving = true;
            try
            {
                Logger?.Trace("Loading from path '{0}'.", _filename);
                var loadDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(_filename));
                SetMany(loadDict);

            }
            catch (Exception ex)
            {
                Logger?.Error(string.Format("Error loading configuration at {0}.",  _filename), ex);
            }
            _saving = false;
        }

        /// <summary>
        /// Saves config.
        /// </summary>
        private void Save()
        {
            if(string.IsNullOrWhiteSpace(_filename)) return;
            // Disable watch while saving
            _saving = true;
            try
            {
                var path = Path.GetDirectoryName(_filename);
                if (path != null) Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Logger?.Error("Error saving configuration.", ex);
            }
            using (var stream = new StreamWriter(_filename))
            {
                try
                {
                    Logger.Trace(string.Format("Saving to path '{0}'.", _filename));
                    JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented }).Serialize(stream, Properties);
                    stream.Flush();
                }
                catch (Exception ex)
                {
                    Logger?.Error("Error saving configuration.", ex);
                }
            }
            // Enable watch after saving
            _saving = false;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposing) return;
            _watcher?.Dispose();
        }
    }
}
