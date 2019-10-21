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
using System.Linq;
using System.Reflection;
using Nyx.Core.Reflection;

namespace Nyx.Core.Extensions
{
    public static class ExtensionHelpers
    {
        /// <summary>
        /// Gets all the non-shared extensions on a given assembly.
        /// </summary>
        /// <param name="assembly">Assembly to check.</param>
        /// <returns>Types that are non-shared extensions.</returns>
        public static Type[] GetNonSharedExtensions(Assembly assembly)
        {
            var types = assembly.GetTypes().Where(t => t.IsAssignableTo<INyxExtension>() && !t.IsAbstract).Where(t =>
            {
                var a = t.GetCustomAttribute<ExtensionAttribute>();
                return a != null && a.LifeCycle == ExtensionLifeCycle.NonShared;
            });
            return types.ToArray();
        }

        /// <summary>
        /// Gets all the shared extensions on a given assembly.
        /// </summary>
        /// <param name="assembly">Assembly to check.</param>
        /// <returns>Types that are shared extensions.</returns>
        public static Type[] GetSharedExtensions(Assembly assembly)
        {
            var types = assembly.GetTypes().Where(t => t.IsAssignableTo<INyxExtension>() && !t.IsAbstract).Where(t =>
            {
                var a = t.GetCustomAttribute<ExtensionAttribute>();
                return a == null || a.LifeCycle != ExtensionLifeCycle.NonShared;
            });
            return types.ToArray();
        }

        /// <summary>
        /// Returns if a type is an extension and is a non-shared instance.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if is a shared <see cref="INyxExtension"/></returns>
        public static bool IsNonSharedExtension(this Type type)
        {
            if (!(type.IsAssignableTo<INyxExtension>()) || type.IsAbstract) return false;
            var a = type.GetCustomAttribute<ExtensionAttribute>();
            return a != null && a.LifeCycle == ExtensionLifeCycle.NonShared;
        }

        /// <summary>
        /// Returns if a type is an extension and is a shared instance.
        /// All extensions are shared by default.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if is a shared <see cref="INyxExtension"/></returns>
        public static bool IsSharedExtension(this Type type)
        {
            if (!(type.IsAssignableTo<INyxExtension>()) || type.IsAbstract) return false;
            var a = type.GetCustomAttribute<ExtensionAttribute>();
            return a == null || a.LifeCycle != ExtensionLifeCycle.NonShared;
        }
    }
}
