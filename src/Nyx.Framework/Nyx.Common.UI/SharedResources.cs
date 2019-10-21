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
using System.Reflection;
using System.Windows;

namespace Nyx.Common.UI
{
    public static class SharedResources
    {
        #region MergedDictionaries

        /// <summary>
        ///     Looks for resources on Themes folder, of the calling library/app.
        /// </summary>
        public static readonly DependencyProperty MergedDictionariesProperty = DependencyProperty.RegisterAttached("MergedDictionaries",
                typeof(string), typeof(SharedResources), new FrameworkPropertyMetadata(null,OnMergedDictionariesChanged));

        public static string GetMergedDictionaries(DependencyObject d)
        {
            return (string)d.GetValue(MergedDictionariesProperty);
        }

        public static void SetMergedDictionaries(DependencyObject d, string value)
        {
            d.SetValue(MergedDictionariesProperty, value);
        }

        private static void OnMergedDictionariesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewValue as string)) return;
            foreach (var dictionaryName in ((string) e.NewValue).Split(';'))
            {
                var dictionary = GetResourceDictionary(dictionaryName);
                if (dictionary == null) continue;
                var element = d as FrameworkElement;
                if (element != null)
                    element.Resources.MergedDictionaries.Add(dictionary);
                else
                    (d as FrameworkContentElement)?.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        #endregion

        private static ResourceDictionary GetResourceDictionary(string dictionaryName)
        {
            ResourceDictionary result = null;
            if (SharedDictionaries.ContainsKey(dictionaryName))
                result = SharedDictionaries[dictionaryName].Target as ResourceDictionary;
            if (result != null) return result;

            var assemblyName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().ManifestModule.Name);
            result = Application.LoadComponent(new Uri(string.Format("/{0};component/Themes/{1}.xaml", assemblyName, dictionaryName), UriKind.Relative)) as ResourceDictionary;
            SharedDictionaries[dictionaryName] = new WeakReference(result);
            return result;
        }

        private static readonly Dictionary<string, WeakReference> SharedDictionaries= new Dictionary<string, WeakReference>();
    }
}