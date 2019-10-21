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
using System.Linq.Expressions;

namespace Nyx.Common.UI
{
    public class TypeExtensions<T>
    {
        /// <exception cref="Exception"><c>Exception</c>.</exception> 
        public static string GetProperty<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body.NodeType == ExpressionType.MemberAccess)
                return ((MemberExpression) expression.Body).Member.Name;
            if ((expression.Body.NodeType == ExpressionType.Convert) && (expression.Body.Type == typeof(object)))
            {
                return ((MemberExpression)((UnaryExpression)expression.Body).Operand).Member.Name;
            }

            throw new Exception(string.Format("Invalid expression type: Expected ExpressionType.MemberAccess, Found {0}", expression.Body.NodeType));
        }
    }
}