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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using Nyx.Core.Utils;

namespace Nyx.Core.Management
{
    public class ComputerInfo : IDisposable
    {
        private string _os;
        // ReSharper disable once InconsistentNaming
        public string OS { get; private set; }
        public long TotalRam { get; private set; }
        public long FreeRam { get; private set; }
        public long CpuSpeed { get; private set; }

        public ComputerInfo()
        {
            Refresh();
            //_disposable = Observable
            //    .Interval(TimeSpan.FromSeconds(30))
            //    .ObserveOnPool()
            //    .Subscribe(o =>
            //    {
            //        Refresh();
            //    });
        }

        public void Refresh()
        {
            var sw = Stopwatch.StartNew();
            OS = GetOS();
            TotalRam = GetTotalMemory();
            FreeRam = GetFreeMemory();
            CpuSpeed = GetCPUSpeed();
            Debug.Write(string.Format("Gathering computer info took {0}ms.", sw.ElapsedMilliseconds));
            sw.Stop();
        }

        // ReSharper disable once InconsistentNaming
        private string GetOS()
        {
            if (_os != "") return _os;
            var stringBuilder = new StringBuilder();
            try
            {
                var managementObjectSearcher =
                    new ManagementObjectSearcher(new ObjectQuery("select * from Win32_OperatingSystem"));
                try
                {
                    foreach (string str in from ManagementObject managementObject in managementObjectSearcher.Get()
                        where managementObject["Caption"] != null
                        select Convert.ToString(managementObject["Caption"])
                        into str
                        select str.Replace("(R)", "")
                        into str
                        select str.Replace("Microsoft", "")
                        into str
                        select Regex.Replace(str, "[^A-Za-z0-9 ]", ""))
                    {
                        stringBuilder.Append(str.Trim());
                        break;
                    }
                }
                finally
                {
                    ((IDisposable) managementObjectSearcher).Dispose();
                }
            }
            catch
            {
                // ignored
            }
            if (stringBuilder.Length == 0)
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    switch (Environment.OSVersion.Version.Major)
                    {
                        case 3:
                        {
                            stringBuilder.Append("Windows NT 3.51");
                            break;
                        }
                        case 4:
                        {
                            stringBuilder.Append("Windows NT 4.0");
                            break;
                        }
                        case 5:
                        {
                            switch (Environment.OSVersion.Version.Minor)
                            {
                                case 0:
                                {
                                    stringBuilder.Append("Windows 2000");
                                    break;
                                }
                                case 1:
                                {
                                    stringBuilder.Append("Windows XP");
                                    break;
                                }
                                case 2:
                                {
                                    stringBuilder.Append("Windows XP64");
                                    break;
                                }
                            }
                            break;
                        }
                        case 6:
                        {
                            switch (Environment.OSVersion.Version.Minor)
                            {
                                case 0:
                                {
                                    stringBuilder.Append("Windows Vista");
                                    break;
                                }
                                case 1:
                                {
                                    stringBuilder.Append("Windows 7");
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            if (stringBuilder.Length == 0)
            {
                stringBuilder.Append("Windows ");
                stringBuilder.Append(Environment.OSVersion.Version.Major);
                stringBuilder.Append(".");
                stringBuilder.Append(Environment.OSVersion.Version.Minor);
            }
            var servicePack = Environment.OSVersion.ServicePack;
            if (string.CompareOrdinal(servicePack, "None") == 0)
            {
                servicePack = "";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 1") == 0)
            {
                servicePack = "SP1";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 2") == 0)
            {
                servicePack = "SP2";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 3") == 0)
            {
                servicePack = "SP3";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 4") == 0)
            {
                servicePack = "SP4";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 5") == 0)
            {
                servicePack = "SP5";
            }
            else if (string.CompareOrdinal(servicePack, "Service Pack 6") == 0)
            {
                servicePack = "SP6";
            }
            if (servicePack.Length > 0)
            {
                stringBuilder.Append(" (");
                stringBuilder.Append(servicePack);
                stringBuilder.Append(")");
            }
            _os = stringBuilder.ToString();
            return _os;
        }

        private long GetTotalMemory()
        {
            long num;
            try
            {
                long num1 = (long) -1;
                bool flag = false;
                ManagementObjectSearcher managementObjectSearcher =
                    new ManagementObjectSearcher(new ObjectQuery("select * from Win32_ComputerSystem"));
                try
                {
                    foreach (
                        var managementObject in
                            managementObjectSearcher.Get()
                                .Cast<ManagementObject>()
                                .Where(managementObject => managementObject["TotalPhysicalMemory"] != null))
                    {
                        num1 = Convert.ToInt64(managementObject["TotalPhysicalMemory"]);
                        flag = true;
                        break;
                    }
                }
                finally
                {
                    ((IDisposable) managementObjectSearcher).Dispose();
                }
                if (flag)
                {
                    num = num1;
                    return num;
                }
            }
            catch
            {
                // ignored
            }
            num = 0;
            return num;
        }

        private long GetFreeMemory()
        {
            long num;
            try
            {
                long num1 = (long) -1;
                bool flag = false;
                ManagementObjectSearcher managementObjectSearcher =
                    new ManagementObjectSearcher(new ObjectQuery("select * from Win32_OperatingSystem"));
                try
                {
                    foreach (
                        var managementObject in
                            managementObjectSearcher.Get()
                                .Cast<ManagementObject>()
                                .Where(managementObject => managementObject["FreePhysicalMemory"] != null))
                    {
                        num1 = Convert.ToInt64(managementObject["FreePhysicalMemory"])*(long) 0x400;
                        flag = true;
                        break;
                    }
                }
                finally
                {
                    ((IDisposable) managementObjectSearcher).Dispose();
                }
                if (flag)
                {
                    num = num1;
                    return num;
                }
            }
            catch
            {
                // ignored
            }
            num = 0;
            return num;
        }

        private long GetCPUSpeed()
        {
            long num = 0;

            try
            {
                var managementObjectSearcher =
                    new ManagementObjectSearcher(new ObjectQuery("select * from Win32_Processor"));
                try
                {
                    foreach (var managementObject in managementObjectSearcher.Get().Cast<ManagementObject>().Where(managementObject => managementObject["CurrentClockSpeed"] != null))
                    {
                        num = Convert.ToInt64(managementObject["CurrentClockSpeed"]);
                        break;
                    }
                }
                finally
                {
                    ((IDisposable) managementObjectSearcher).Dispose();
                }
            }
            catch
            {
                // ignored
            }
            return num;
        }

        #region IDisposable Support
        private bool _disposedValue;
        private readonly IDisposable _disposable;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;
            if (disposing)
            {
                _disposable?.Dispose();          
            }

            _disposedValue = true;
        }


        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}