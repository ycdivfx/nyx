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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nyx.Client
{
    internal class AssemblyBypasser
    {
        private static bool _registered = false;
        private static IEnumerable<string> _folders;
        private static bool _debug = true;

        public static void RegisterBootloader(IEnumerable<string> folders)
        {
            if (_registered) return;
            _folders = folders;
            _registered = true;
            var tmp = ConfigurationManager.AppSettings["loader_debug"];
            if(tmp != null)
                bool.TryParse(tmp, out _debug);
            tmp = ConfigurationManager.AppSettings["loader_paths"];
            if (tmp != null) _folders = tmp.Split(';').ToList();
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var t in currentAssemblies.Where(t => t.FullName == args.Name))
                return t;

            Assembly assm = null;
            foreach (var folder in _folders)
            {
                if(_debug) Debug.Write($"[Nyx.Bootloader] Loading from '{folder}'...");
                if (_debug) Debug.Write($"[Nyx.Bootloader] Trying to load assembly {args.Name} for {args.RequestingAssembly?.GetName().Name}.");
                assm = FindAssembliesInDirectory(args.Name, folder);
                if (_debug) Debug.WriteLine($" - {(assm != null ? "found" : "not found")}.");
                if(assm != null) break;
            }
            return assm;
        }

        private static Assembly FindAssembliesInDirectory(string assemblyName, string directory)
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                Assembly assm;

                if (TryLoadAssemblyFromFile(file, assemblyName, out assm))
                    return assm;
            }

            return null;
        }

        private static bool TryLoadAssemblyFromFile(string file, string assemblyName, out Assembly assm)
        {
            try
            {
                // Convert the filename into an absolute file name for 
                // use with LoadFile. 
                file = new FileInfo(file).FullName;
                var fullnameSplit = assemblyName.Split(',');
                if (AssemblyName.GetAssemblyName(file).Name == fullnameSplit[0])
                {
                    if (_debug) Debug.Write($" trying '{file}' ");
                    if (fullnameSplit.Length > 1)
                    {
                        var ver = fullnameSplit[1].Split('=')[1];
                        if (_debug) Debug.Write($" with version '{AssemblyName.GetAssemblyName(file).Version}' ");
                        if (AssemblyName.GetAssemblyName(file).Version >= new Version(ver))
                        {
                            assm = Assembly.LoadFile(file);
                            return true;
                        }
                    }
                    else
                    {
                        assm = Assembly.LoadFile(file);
                        return true;
                    }
                }
            }
            catch
            {
                /* Do Nothing */
            }
            assm = null;
            return false;
        }
    }
}
