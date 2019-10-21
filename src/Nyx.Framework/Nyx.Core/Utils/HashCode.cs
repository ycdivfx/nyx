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
using System.Collections.Generic;
using System.Linq;

namespace Nyx.Core.Utils
{
    public struct HashCode
    {
        private readonly int _value;
        private HashCode(int value)
        {
            _value = value;
        }
        public static implicit operator int (HashCode hashCode)
        {
            return hashCode._value;
        }
        public static HashCode Of<T>(T item)
        {
            return new HashCode(0).And(item);
        }
        public HashCode And<T>(T item)
        {
            unchecked
            {
                int itemHashCode = GetHashCode(item);
                return new HashCode(CombineHashCodes(_value, itemHashCode));
            }
        }
        public HashCode AndEach<T>(IEnumerable<T> items)
        {
            unchecked
            {
                int itemsHashCode = items.Select(GetHashCode).Aggregate(CombineHashCodes);
                return new HashCode(CombineHashCodes(_value, itemsHashCode));
            }
        }
        private static int CombineHashCodes(int h1, int h2)
        {
            // Code copied from System.Tuple so it must be the best way to combine hash codes or at least a good one.
            return ((h1 << 5) + h1) ^ h2;
        }
        private static int GetHashCode<T>(T item)
        {
            return item?.GetHashCode() ?? 0;
        }
    }
}
