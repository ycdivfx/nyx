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
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading;
using Autofac;
using Nyx.Core.Reflection;
using Nyx.Core;
using Nyx.Core.Config;
using Nyx.Core.Logging;
using System.Windows.Forms;

namespace Nyx.Hub.Console
{
    public class Server
    {
        private static readonly CompositeDisposable Disposable = new CompositeDisposable();
        
        static void Main()
        {
            Disposable.Add(new Mutex(true, "NyxHub", out bool createdNew));
            if (!createdNew)
            {
                MessageBox.Show("Nyx Hub is already running!", "Multiple Instances");
                return;
            }
            using (var container = BuildContainer())
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                var logger = container.Resolve<ILogger<Server>>();
                var config = container.Resolve<IConfigManager>();
                config.Setup(Path.Combine(Assembly.GetExecutingAssembly().AssemblyDirectory(), "nyx_hub.cfg"));
                logger.Info("Init Hub v{0}", assemblyName.Version);
                var hub = container.Resolve<INyxHub>();
                logger.Info("Starting Hub...");
                hub.Start();
                var runServer = true;
                System.Console.CancelKeyPress += (o, e) =>
                {
                    e.Cancel = true;
                    hub.Stop();
                    runServer = false;
                };
                while (runServer)
                {
                    try
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
                    {
                        logger.Fatal("Exception caught.", ex);
                        hub.Stop();
                        break;
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }
                logger.Info("Stopped Hub.");
                Disposable.Dispose();
            }
        }

        private static IContainer BuildContainer()
        {
            var pluginDir = Path.Combine(Assembly.GetExecutingAssembly().AssemblyDirectory(), "plugins");

            NyxBoot.With()
                .NyxHub()
                .AndPlugins(pluginDir)
                .Start();
            
            return NyxBoot.Container;
        }
    }
}
