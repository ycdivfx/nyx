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
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Nyx.Common.UI
{
    /// <summary>
    /// Selects the last add item.
    /// </summary>
    public class LastItemBehavior
    {
        static readonly Dictionary<ListBox, Capture> Associations = new Dictionary<ListBox, Capture>();

        public static bool GetScrollOnNewItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollOnNewItemProperty);
        }

        public static void SetScrollOnNewItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        public static readonly DependencyProperty ScrollOnNewItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOnNewItem",
                typeof(bool),
                typeof(LastItemBehavior),
                new UIPropertyMetadata(false, OnScrollOnNewItemChanged));

        public static void OnScrollOnNewItemChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var listBox = d as ListBox;
            if (listBox == null) return;
            bool oldValue = (bool)e.OldValue, newValue = (bool)e.NewValue;
            if (newValue == oldValue) return;
            if (newValue)
            {
                listBox.Loaded += Control_Loaded;
                listBox.Unloaded += Control_Unloaded;
            }
            else
            {
                listBox.Loaded -= Control_Loaded;
                listBox.Unloaded -= Control_Unloaded;
                if (Associations.ContainsKey(listBox))
                    Associations[listBox].Dispose();
            }
        }

        static void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (Associations.ContainsKey(listBox))
                Associations[listBox].Dispose();
            listBox.Unloaded -= Control_Unloaded;
        }

        static void Control_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            var incc = listBox.Items as INotifyCollectionChanged;
            if (incc == null) return;
            listBox.Loaded -= Control_Loaded;
            Associations[listBox] = new Capture(listBox);
        }

        class Capture : IDisposable
        {
            private ListBox ListBox { get; }
            private INotifyCollectionChanged NotifyCollectionChanged { get; }

            public Capture(ListBox listBox)
            {
                ListBox = listBox;
                NotifyCollectionChanged = listBox.ItemsSource as INotifyCollectionChanged;
                if (NotifyCollectionChanged != null)
                {
                    NotifyCollectionChanged.CollectionChanged += incc_CollectionChanged;
                }
            }

            void incc_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action != NotifyCollectionChangedAction.Add) return;
                ListBox.ScrollIntoView(e.NewItems[0]);
                ListBox.SelectedItem = e.NewItems[0];
            }

            public void Dispose()
            {
                if (NotifyCollectionChanged != null)
                    NotifyCollectionChanged.CollectionChanged -= incc_CollectionChanged;
            }
        }
    }
}
