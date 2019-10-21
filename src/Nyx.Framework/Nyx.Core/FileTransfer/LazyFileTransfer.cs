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
using System.IO;
using System.Linq;
using Nyx.Core.Config;
using Nyx.Core.Logging;
using Nyx.Core.Threading;

namespace Nyx.Core.FileTransfer
{
    public class LazyFileTransfer : IFileTransferManager
    {
        private readonly IConfigManager _config;
        private string _extractPath = @"%TEMP%\NyxFiles\";
        private readonly ILogger<LazyFileTransfer> _logger;
        private readonly SerialAsyncTasker _taskQueue;

        public LazyFileTransfer(IConfigManager config, 
            ILogger<LazyFileTransfer> logger)
        {
            _config = config;
            _logger = logger;
            _config.WhenConfigChanges.Subscribe(ReloadConfig);
            ReloadConfig(null);
            _taskQueue = new SerialAsyncTasker();
        }

        private void ReloadConfig(ConfigChanged message)
        {
            _extractPath = Environment.ExpandEnvironmentVariables(_config.Get("borg", "depot", _extractPath));
        }

        public bool TransferFiles(INyxMessage message, INyxBorg sender)
        {
            if (message.Has("fileTransfer"))
                return true;
            _taskQueue.QueueTask(()=> AsyncLoadToMessage(message, sender));
            return false;
        }

        public bool ExtractFiles(INyxMessage message, INyxBorg nyxBorg)
        {
            if (message.Has("fileExtract"))
                return true;
            _taskQueue.QueueTask(()=> AsyncExtractFiles(message, nyxBorg));
            return false;
        }

        public bool SeedFiles(INyxMessage message, INyxHub nyxHub)
        {
            // Do nothing just pass the file
            return true;
        }

        public ulong MaxFileSize => 100048;

        private void AsyncLoadToMessage(INyxMessage message, INyxBorg sender)
        {
            if (message.Has("fileTransfer"))
            {
                _logger.Warn("File serialization is already in progress for message {0}.", message);
                return;
            }
            message["fileTransfer"] = "1";
            foreach (var nyxFile in message.Files)
            {
                if (!File.Exists(nyxFile.Path)) break;
                var filename = Path.GetFileName(nyxFile.Path);
                _logger.Debug("Converting file for message transfer.");
                var file = Convert.ToBase64String(File.ReadAllBytes(nyxFile.Path));
                message[string.Format("file_{0}", filename)] = file;
                try
                {
                    if (nyxFile.DeleteOnTransfer) File.Delete(nyxFile.Path);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error deleting file.", ex);
                }
            }
            sender.SendMessage(message, true);
        }

        private void AsyncExtractFiles(INyxMessage message, INyxBorg nyxBorg)
        {
            if (message.Has("fileExtract"))
            {
                _logger.Warn("File extration is already in progress for message {0}.", message);
                return;
            }
            message["fileExtract"] = "1";
            var files = message.Elements.Where(s => s.Key.StartsWith("file_")).Select(s => s.Key).ToList();
            message.Files.Clear();
            var fullExtractPath = Path.Combine(_extractPath, nyxBorg.NodeId);
            foreach (var file in files)
            {
                var path = Path.Combine(fullExtractPath, file.Substring(5));
                _logger.Debug("Extracting file {0} to {1}", file, path);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                catch (Exception ex)
                {
                    _logger.Error("Error creating directory for extraction.", ex);
                }
                File.WriteAllBytes(path, Convert.FromBase64String(message[file]));
                message.Remove(file);
                message.Files.Add(new NyxFile { Name=file.Substring(5), Path = path, TransferType = GetType().Name});
            }
            nyxBorg.SendMessage(message.AsInternal());
        }
    }
}