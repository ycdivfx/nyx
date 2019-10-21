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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Nyx.Common.UI.Threading;

namespace Nyx.Common.UI
{
    public static class ObservableCollectionHelper
    {
        public static void AddOnUI<T>(this ICollection<T> collection, T item)
        {
            Action<T> addMethod = collection.Add;
            DispatcherHelper.CheckBeginInvokeWithArgsOnUI(addMethod, item);
        }

        public static void ClearOnUI<T>(this ICollection<T> collection)
        {
            Action clear = collection.Clear;
            DispatcherHelper.CheckBeginInvokeOnUI(clear);
        }

        public static void RemoveOnUI<T>(this ICollection<T> collection, T item)
        {
            Func<T, bool> removeMethod = collection.Remove;
            DispatcherHelper.CheckBeginInvokeWithArgsOnUI(removeMethod, item);
        }

        public static ObservableCollection<T> Wrap<T>(IList<T> list)
        {
            var ret = new ObservableCollection<T>(list);

            ret.CollectionChanged += (sender, e) =>
            {
                var collection = (ObservableCollection<T>)sender;
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Reset:
                        list.Clear();
                        foreach (T item in collection)
                            list.Add(item);
                        break;

                    case NotifyCollectionChangedAction.Add:
                        for (int i = 0; i < e.NewItems.Count; i++)
                            list.Insert(e.NewStartingIndex + i, (T)e.NewItems[i]);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < e.OldItems.Count; i++)
                            list.RemoveAt(e.OldStartingIndex);
                        break;

                    case NotifyCollectionChangedAction.Move:
                        list.RemoveAt(e.OldStartingIndex);
                        list.Insert(e.NewStartingIndex, (T)e.NewItems[0]);
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        list[e.NewStartingIndex] = (T)e.NewItems[0];
                        break;
                }
            };
            return ret;
        }
    }
}
