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
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autofac;
using NLog;
using NLog.Config;
using Nyx.Common.UI;
using Nyx.Common.UI.Threading;
using Nyx.Core;
using Nyx.Core.Config;
using Nyx.Core.Logging;
using Nyx.Client.ViewModels;
using Nyx.Client.Views;

namespace Nyx.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : IDisposable
    {
        private INyxBorg _borg;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();
        private readonly Dictionary<string, string> _commandLineArgs = new Dictionary<string, string>();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool createdNew;
            _disposable.Add(new Mutex(true, "NyxEnslaver", out createdNew));
            if (!createdNew)
            {
                MessageBox.Show("Nyx Enslaver is already running!", "Multiple Instances");
                Shutdown();
                return;
            }
            const string pattern = @"(?<argname>[/-]\w+)(?:[:=](?<argvalue>[\d\w._]+))?";
            foreach (var match in e.Args.Select(arg => Regex.Match(arg, pattern)))
            {
                // If match not found, command line args are improperly formed.
                if (!match.Success)
                {
                    MessageBox.Show($"The command line arguments are improperly formed. Use /argname[:argvalue].\n{string.Join(",", e.Args)}");
                    Shutdown();
                    return;
                }

                // Store command line arg and value
                var value = match.Groups["argvalue"].Value;
                _commandLineArgs[match.Groups["argname"].Value.Substring(1)] = value;
            }

            if (!Debugger.IsAttached)
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            ConfigurationItemFactory.Default.Targets.RegisterDefinition("Observable", typeof(Common.UI.Logger.ObservableTarget));
            DispatcherHelper.Initialize();
            InitCore();
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            Debug.WriteLine($"[Nyx Client] Fatal error - {unhandledExceptionEventArgs.ExceptionObject}");
            MessageBox.Show("An fatal error occur in NyxClient.\nIt will close now.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Environment.Exit(-1);
        }

        public async void InitCore()
        {
            var splash = new Splash();
            splash.Show();

            var pluginDir = Path.Combine(Assembly.GetExecutingAssembly().AssemblyDirectory(), "plugins");
            NyxBoot.With()
                .NyxBorg()
                .AndPlugins(pluginDir)
                .Start(RxAppAutofacExtension.UseAutofacDependencyResolver);

            IConfigManager config;
            try
            {
                config = NyxBoot.Container.Resolve<IConfigManager>();
                config.Setup(Path.Combine(GetType().Assembly.AssemblyDirectory(), "nyx.cfg"));
                _borg = NyxBoot.Container.Resolve<INyxBorg>();
                NyxBoot.Container.Resolve<ILogger<App>>();
                _borg.HostApp = "enslaver";
            }
            catch (Exception ex)
            {
                MessageBox.Show("[Nyx] Error building container ...\n" + ex);
                Shutdown();
                return;
            }
            var shellViewModel = NyxBoot.Container.Resolve<IShellViewModel>();
            MainWindow = new MainWindow { Content = new ShellView(shellViewModel) };
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            splash.Close();
            MainWindow.Show();

            // Sets the default hub.
            string hubAddress;
            string hubPort;
            if (_commandLineArgs.TryGetValue("c", out hubAddress))
                config.Set("borg", "hubIp", hubAddress);
            if (_commandLineArgs.TryGetValue("p", out hubPort))
            {
                int port;
                if (int.TryParse(hubPort, out port))
                    config.Set("hub", "port", port);
            }
            await _borg.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Debug.WriteLine("[Nyx.Client] Trying to close...");
            _borg?.Stop()
                .Timeout(TimeSpan.FromSeconds(30))
                .Catch<NyxNodeStatus, Exception>(ex =>
                {
                    LogManager.GetCurrentClassLogger().Error(ex, "Timeout stopping borg.");
                    return Observable.Return(NyxNodeStatus.Stopped);
                }).Wait();
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException;
            base.OnExit(e);
            Debug.WriteLine("[Nyx.Client] Closing...");
            // Give more 10s for the threads to stop.
            Observable.Timer(TimeSpan.FromSeconds(10)).Subscribe(_ => Environment.Exit(0));
            Current.Shutdown();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
