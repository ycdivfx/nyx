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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Nyx.Core.Plugins
{
    public class ParameterResolver
    {
        public object[] Resolve(object requestParameters, Parameter[] targetParameters)
        {
            // Convert to array
            var parameters = ((IEnumerable)requestParameters).Cast<object>().ToArray();

            // If length does not match, throw.
            if (parameters.Length != targetParameters.Length)
            {
                return null;
            }

            var result = new List<object>();

            // Loop through and parse parameters
            for (var i = 0; i < parameters.Length; i++)
            {
                var request = parameters[i];
                var target = targetParameters[i];

                // If the target parameter is `object`, do not try to convert it.
                if (target.ParameterType == typeof(object))
                {
                    result.Add(request);
                }
            }

            return result.ToArray();
        }

    }
}
