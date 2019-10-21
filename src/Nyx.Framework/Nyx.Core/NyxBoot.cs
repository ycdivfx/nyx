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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Nyx.Core.Boot;
using Nyx.Core.Extensions;
using Nyx.Core.Plugins;

[assembly: Obfuscation(Exclude = false, Feature = "random seed: ycdivfxNYX")]
namespace Nyx.Core
{
    public static class NyxBoot
    {
        internal static NyxBootHelper NyxBootHelper;

        public static IContainer Container { get; private set; }

        static NyxBoot()
        {
            DefaultExceptionHandler = Observer.Create<Exception>(ex => {
                // NB: If you're seeing this, it means that an
                // ObservableAsPropertyHelper or the CanExecute of a
                // ReactiveCommand ended in an OnError. Instead of silently
                // breaking, ReactiveUI will halt here if a debugger is attached.
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                MainThreadScheduler.Schedule(() => {
                    throw new Exception(
                        "An OnError occurred on an object (usually ObservableAsPropertyHelper) that would break a binding or command. To prevent this, Subscribe to the ThrownExceptions property of your objects",
                        ex);
                });
            });

            MainThreadScheduler = DefaultScheduler.Instance;
            TaskpoolScheduler = TaskPoolScheduler.Default;
        }

        /// <summary>
        /// MainThreadScheduler is the scheduler used to schedule work items that
        /// should be run "on the UI thread". In normal mode, this will be
        /// DispatcherScheduler, and in Unit Test mode this will be Immediate,
        /// to simplify writing common unit tests.
        /// </summary>
        public static IScheduler MainThreadScheduler { get; set; }

        /// <summary>
        /// TaskpoolScheduler is the scheduler used to schedule work items to
        /// run in a background thread. In both modes, this will run on the TPL
        /// Task Pool (or the normal Threadpool on Silverlight).
        /// </summary>
        public static IScheduler TaskpoolScheduler { get; set; }

        public static IObserver<Exception> DefaultExceptionHandler { get; set; }

        public static NyxBootHelper With()
        {
            NyxBootHelper = new NyxBootHelper();
            NyxBootHelper.Builder.RegisterAssemblyTypes(Assembly.GetCallingAssembly())
                .Where(t => t.IsSharedExtension())
                .AsImplementedInterfaces().SingleInstance().AsSelf().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            NyxBootHelper.Builder.RegisterAssemblyTypes(Assembly.GetCallingAssembly())
                .Where(t => t.IsNonSharedExtension())
                .AsImplementedInterfaces().InstancePerDependency().AsSelf().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            var modules = Assembly.GetCallingAssembly().GetTypes()
                                    .Where(p => typeof(IModule).IsAssignableFrom(p)
                                                && !p.IsAbstract)
                                    .Select(p => (IModule)Activator.CreateInstance(p));
            foreach (var module in modules)
            {
                NyxBootHelper.Builder.RegisterModule(module);
            }
            return NyxBootHelper;
        }

        public static NyxBootHelper NyxBorg(this NyxBootHelper @this)
        {
            return NyxBootHelper.NyxNode<NyxBorg>();
        }

        public static NyxBootHelper NyxHub(this NyxBootHelper @this)
        {
            return NyxBootHelper.NyxNode<NyxHub>();
        }

        public static NyxBootHelper NyxNode<T>(this NyxBootHelper @this) where T : INyxNode
        {
            NyxBootHelper.Builder.RegisterType<T>().AsImplementedInterfaces().SingleInstance().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            return NyxBootHelper;
        }

        public static NyxBootHelper And<T>(this NyxBootHelper @this)
        {
            if (typeof(PluginManager) == typeof(T))
                return NyxBootHelper;
            NyxBootHelper.Builder.RegisterType<T>().AsSelf().AsImplementedInterfaces().SingleInstance().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            return NyxBootHelper;
        }

        public static NyxBootHelper AndInstance(this NyxBootHelper @this, object obj)
        {
            if (obj is PluginManager)
                return NyxBootHelper;
            NyxBootHelper.Builder.RegisterInstance(obj).AsImplementedInterfaces().SingleInstance();
            return NyxBootHelper;
        }

        public static NyxBootHelper AndGeneric(this NyxBootHelper @this, Type type)
        {
            if (typeof(PluginManager) == type)
                return NyxBootHelper;
            NyxBootHelper.Builder.RegisterGeneric(type).AsImplementedInterfaces().SingleInstance().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            return NyxBootHelper;
        }

        public static NyxBootHelper AndPlugins(this NyxBootHelper @this, string path, string ext = "*.dll")
        {
            Directory.CreateDirectory(path);
            var pluginAssemblies = Directory.EnumerateFiles(path, ext)
                //.Where(p => !p.ToLowerInvariant().Contains("nyx.core.dll"))
                .Select(Assembly.LoadFile)
                .ToArray();
            NyxBootHelper.Builder.RegisterAssemblyTypes(pluginAssemblies)
                .Where(t => t.IsSharedExtension())
                .AsImplementedInterfaces().SingleInstance().AsSelf().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            NyxBootHelper.Builder.RegisterAssemblyTypes(pluginAssemblies)
                .Where(t => t.IsNonSharedExtension())
                .AsImplementedInterfaces().InstancePerDependency().AsSelf().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            foreach (var module in pluginAssemblies.Select(assembly => assembly.GetTypes()
                .Where(p => typeof(IModule).IsAssignableFrom(p)
                            && !p.IsAbstract)
                .Select(p => (IModule)Activator.CreateInstance(p))).SelectMany(modules =>
                {
                    var enumerable = modules as IModule[] ?? modules.ToArray();
                    return enumerable;
                }))
            {
                NyxBootHelper.Builder.RegisterModule(module);
            }
            return NyxBootHelper;
        }

        public static NyxBootHelper AndModule(this NyxBootHelper @this, IModule module)
        {
            NyxBootHelper.Builder.RegisterModule(module);
            return NyxBootHelper;
        }

        public static void Start(this NyxBootHelper @this, Action<IContainer> postBuildCallback=null)
        {
            if (Container == null)
                Container = NyxBootHelper.Builder.Build();
            else
                NyxBootHelper.Builder.Update(Container);
            postBuildCallback?.Invoke(Container);
            // Bug: Double registration for some reason it calls the construtor again.
            //_container.Resolve<PluginManager>();
            //_container.Resolve<EvidenceManager>();
        }

        public static void Clear()
        {
            Container.Dispose();
            Container = null;
        }
    }
}
