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

namespace Nyx.Core.Extensions
{
    /// <summary>
    /// This is the main way to extend Nyx Framework.
    /// Actions processing is done by classes derived of this interface.
    /// </summary>
    public interface INyxMessageActions : INyxExtension
    {
        /// <summary>
        /// List of supported message actions.
        /// Acts as a quick filter.
        /// </summary>
        IEnumerable<string> SupportedActions { get; }

        /// <summary>
        /// This is called for processing a message after filtering actions.
        /// </summary>
        /// <param name="message">Message received</param>
        /// <returns></returns>
        bool ProcessMessage(INyxMessage message);
    }
}