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
using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using Nyx.Core.Extensions;

namespace Nyx.Core.Plugins
{
    /// <summary>
    ///     Plugin manager.
    /// </summary>
    public sealed class PluginManager
    {
        private readonly Logger _logger;
        private IEnumerable<INyxService> _plugins;
        private IEnumerable<INyxMessageFilter> _filters;
        private IEnumerable<IBorgAction> _borgActions;
        private IEnumerable<IHubAction> _hubActions;
        private List<INyxExtension> _extensions;
        private Dictionary<string, ExtensionAction> _actions;
        public static PluginManager Instance { get; private set; }

        /// <summary>
        ///     Removed logger.
        /// </summary>
        public PluginManager()
        {
            _logger = LogManager.GetCurrentClassLogger();
            if (Instance != null)
            {
                throw new InvalidOperationException("Core was not properly build.");
            }
            Instance = this;
        }

        /// <summary>
        /// Inits the pluginmanager. Called on autofac registration.
        /// </summary>
        /// <paramCount name="context"></paramCount>
        internal void Init(IComponentContext context)
        {
            // We only one "entry" point now to Nyx.
            _extensions = context.Resolve<IEnumerable<INyxExtension>>().ToList();
            // Remove non shared instances... they should not be here
            _extensions.RemoveAll(e => e.GetType().IsNonSharedExtension());
            _plugins = _extensions.Where(e => e.GetType().IsAssignableTo<INyxService>()).Cast<INyxService>().Distinct();
            _filters = _extensions.Where(e => e.GetType().IsAssignableTo<INyxMessageFilter>()).Cast<INyxMessageFilter>().Distinct();
            _borgActions = _extensions.Where(e => e.GetType().IsAssignableTo<IBorgAction>()).Cast<IBorgAction>().Distinct();
            _hubActions = _extensions.Where(e => e.GetType().IsAssignableTo<IHubAction>()).Cast<IHubAction>().Distinct();
            BuildActions();
        }

        private void BuildActions()
        {
            _actions = new Dictionary<string, ExtensionAction>();
            foreach (var extension in _extensions)
            {
                var methods = FindValidMethods(extension);
                foreach (var info in methods)
                {
                    var actionAttr = info.GetCustomAttribute<ExtensionActionAttribute>();
                    if (_actions.ContainsKey(actionAttr.Name))
                    {
                        _logger.Error("Duplicate action found in {0}, {1}. Skipping.", extension, actionAttr.Name);
                        continue;
                    }
                    _actions.Add(actionAttr.Name, new ExtensionAction(extension, info));
                }
            }
        }

        private IEnumerable<MethodInfo> FindValidMethods(INyxExtension extension)
        {
            var methods = extension.GetType().GetMethods();
            return from method in methods
                let attr = method.GetCustomAttribute<ExtensionActionAttribute>()
                where attr != null
                select method;
        }

        /// <summary>
        /// Easy way to filter our collections.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <paramCount name="plugins"></paramCount>
        /// <returns></returns>
        private IEnumerable<T> Get<T>(IEnumerable<object> plugins) where T : class
        {
            // First try to get the concrete class
            var objects = plugins as IList<object> ?? plugins.ToList();
            var result = objects.Where(o => o.GetType() == typeof(T));
            // If failing returns the first subclass of it.
            if(!result.Any())
                result = objects.Where(o => o.GetType().IsAssignableTo<T>());
            return result.Cast<T>();
        }

        /// <summary>
        /// Call start on all plugins.
        /// </summary>
        public void StartAllPlugins()
        {
            foreach (var plugin in _plugins)
            {
                try
                {
                    _logger.Trace("Starting plugin {0}", plugin.GetType().Name);
                    plugin.Start();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error starting plugin {plugin.GetType().Name}.");
                }
            }
        }

        /// <summary>
        /// Call stop on all plugins.
        /// </summary>
        public void StopAllPlugins()
        {
            foreach (var plugin in _plugins)
            {
                try
                {
                    _logger.Trace("Stopping plugin {0}", plugin.GetType().Name);
                    plugin.Stop();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error stopping plugin {plugin.GetType().Name}.");
                    throw;
                }
            }
        }

        /// <summary>
        ///     Get the first <see cref="INyxExtension"/> of type <typeparamref name="T"/> found, 
        /// or a new instance if its a non-shared extension.
        /// </summary>
        /// <typeparam name="T">Type implementing <see cref="INyxExtension"/>.</typeparam>
        /// <returns><see cref="INyxExtension"/> of type <typeparamref name="T"/>.</returns>
        public T Get<T>() where T : class, INyxExtension
        {
            return typeof (T).IsNonSharedExtension() ? NyxBoot.Container.Resolve<T>() : Get<T>(_extensions).FirstOrDefault();
        }

        /// <summary>
        ///     Get the first <see cref="INyxExtension"/> of type <typeparamref name="T"/> matching the predicate.
        /// </summary>
        /// <typeparam name="T">Type implementing <see cref="INyxExtension"/>.</typeparam>
        /// <paramCount name="predicate">Search predicate.</paramCount>
        /// <returns><see cref="INyxExtension"/> of type <typeparamref name="T"/>.</returns>
        public T Get<T>(Func<INyxExtension, bool> predicate) where T : class, INyxExtension
        {
            if (typeof (T).IsNonSharedExtension())
            {
                var result = NyxBoot.Container.Resolve<T>();
                if (predicate(result)) return result;
            }
            return Get<T>(_extensions).FirstOrDefault(predicate) as T;
        }

        public T GetPlugin<T>() where T : class, INyxService
        {
            return Get<T>(_plugins).FirstOrDefault();
        }

        /// <summary>
        /// Gets a Nyx message filter of the given type.
        /// <seealso cref="INyxMessageFilter"/>
        /// </summary>
        /// <typeparam name="T">A type that inherits from INyxMessageFilter</typeparam>
        /// <returns>null if not found.</returns>
        public T GetFilter<T>() where T : class, INyxMessageFilter
        {
            return Get<T>(_filters).FirstOrDefault();
        }

        /// <summary>
        /// Gets an action processor of the given type.
        /// <seealso cref="IBorgAction"/>
        /// </summary>
        /// <typeparam name="T">A type that inherits from IBorgAction.</typeparam>
        /// <returns></returns>
        public T GetAction<T>() where T : class, IBorgAction
        {
            return Get<T>(_borgActions).FirstOrDefault();
        }

        /// <summary>
        /// Gets an extension of the given type.
        /// <seealso cref="INyxExtension"/>
        /// </summary>
        /// <typeparam name="T">A type that inherits from INyxExtension.</typeparam>
        /// <returns>The extension</returns>
        public T GetExtension<T>() where T : class, INyxExtension
        {
            return Get<T>(_extensions).FirstOrDefault();
        }

        /// <summary>
        /// Gets all plugins registered in the Nyx system.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<INyxExtension> GetExtensions() 
        {
            return _extensions;
        }

        /// <summary>
        /// Gets all messafe filters-
        /// </summary>
        /// <returns></returns>
        public IEnumerable<INyxMessageFilter> GetFilters()
        {
            return Get<INyxMessageFilter>(_filters);
        }

        /// <summary>
        /// Gets all messages filters that  fit the predicate.
        /// </summary>
        /// <paramCount name="func">Predicate function.</paramCount>
        /// <returns></returns>
        public IEnumerable<INyxMessageFilter> GetFilters(Func<INyxMessageFilter, bool> func)
        {
            return Get<INyxMessageFilter>(_filters).Where(func);
        }

        /// <summary>
        /// Gets all the Actions than can be cast to the given type.
        /// <example>
        /// var actions = _plugman.GetActions&lt;IHubAction&gt;().Where(p => p.SupportedActions.Contains(msg.Action.ToLowerInvariant()))
        /// </example>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> GetActions<T>() where T : class, INyxMessageActions
        {
            if (typeof (T).IsAssignableTo<IBorgAction>()) return Get<T>(_borgActions);
            return typeof(T).IsAssignableTo<IHubAction>() ? Get<T>(_hubActions) : new List<T>();
        }

        /// <summary>
        /// Gets all the Actions than can be cast to the given type, using a predicate.
        /// <example>
        /// var actions = _plugman.GetActions&lt;IHubAction&gt;(p => p.SupportedActions.Contains(msg.Action.ToLowerInvariant()));
        /// </example>
        /// </summary>
        /// <typeparam name="T">Type of action to get</typeparam>
        /// <paramCount name="func">Predicate</paramCount>
        /// <returns>Actions.</returns>
        public IEnumerable<T> GetActions<T>(Func<T, bool> func) where T : class, INyxMessageActions
        {
            IEnumerable<T> results;
            if (typeof(T).IsAssignableTo<IBorgAction>()) results = Get<T>(_borgActions);
            else results = typeof(T).IsAssignableTo<IHubAction>() ? Get<T>(_hubActions) : new List<T>();
            return results.Where(func);
        }

        /// <summary>
        /// Executes an exposed action of an extension and returns a result.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <paramCount name="name">Action name.</paramCount>
        /// <paramCount name="args">Arguments for the action.</paramCount>
        /// <returns></returns>
        public T ExecuteAction<T>(string name, params object[] args)
        {
            if(!_actions.ContainsKey(name.ToLowerInvariant())) return default(T);
            var action = _actions[name.ToLowerInvariant()];
            if (args.Length != action.Parameters.Length) return default(T);
            return (T) action.Execute(args);
        }

        /// <summary>
        /// Executes an exposed action of an extension
        /// </summary>
        /// <paramCount name="name">Action name.</paramCount>
        /// <paramCount name="args">Arguments for the action.</paramCount>
        public void ExecuteAction(string name, params object[] args)
        {
            if (!_actions.ContainsKey(name.ToLowerInvariant())) return;
            var action = _actions[name.ToLowerInvariant()];
            if (args.Length != action.Parameters.Length) return;
            action.Execute(args);
        }
    }

    /// <summary>
    /// Represent a Action of an extension
    /// </summary>
    public class ExtensionAction
    {
        private readonly INyxExtension _extension;
        private readonly MethodInfo _info;
        private readonly Logger _logger;

        public ExtensionAction(INyxExtension extension, MethodInfo info)
        {
            _extension = extension;
            _info = info;
            _logger = LogManager.GetLogger("PluginManager.ExtensionAction");
        }

        /// <summary>
        /// Wrapper for the method parameters
        /// </summary>
        public Parameter[] Parameters
        {
            get { return _info.GetParameters().Select(p => new Parameter(p)).ToArray(); }
        }

        public bool IsValidRange(int paramCount)
        {
            var maxLenght = _info.GetParameters().Length;
            var minLenght = maxLenght - _info.GetParameters().Count(p => p.HasDefaultValue);
            return paramCount >= minLenght && paramCount <= maxLenght;
        }

        private bool HasDefaults()
        {
            return _info.GetParameters().Count(p => p.HasDefaultValue) > 0;
        }

        /// <summary>
        /// Execute action.
        /// </summary>
        /// <paramCount name="parameters">Action parameters</paramCount>
        /// <returns>Returns null if action is void, or a value.</returns>
        public object Execute(object[] parameters)
        {
            var paramList = new List<object>(parameters);
            object result = null;
            try
            {
                if (!HasDefaults()) result = _info.Invoke(_extension, paramList.ToArray());
                else
                {
                    // Work around for default parameters. We should support Named default parameters.
                    var diff = Parameters.Length - parameters.Length;
                    for (var i = 0; i < diff; i++)
                        paramList.Add(Type.Missing);
                    result = _info.Invoke(_extension, paramList.ToArray());
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing action.");
            }
            return result;
        }
    }

    public class Parameter
    {
        private readonly ParameterInfo _parameterInfo;

        public Parameter(ParameterInfo parameterInfo)
        {
            _parameterInfo = parameterInfo;
        }

        public string Name => _parameterInfo.Name;

        public Type ParameterType => _parameterInfo.ParameterType;
    }
}
