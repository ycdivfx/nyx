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
using Nyx.Core.Messaging;

namespace Nyx.Core.Extensions
{
    /// <summary>
    /// This is the message filter interface.
    /// </summary>
    public interface INyxMessageFilter : INyxExtension
    {
        /// <summary>
        /// Should we filter Incoming or Outgoing messages or both.
        /// </summary>
        MessageDirection Direction { get; }

        /// <summary>
        /// This is a better way to check if is valid or not filter.
        /// </summary>
        /// <param name="node">Nyx node, can be a nyxBorg or a nyxHub or both.</param>
        /// <param name="message">If pass the message, it can do quick checks to avoid calling the AllowMessage.</param>
        /// <returns>True if it can filter.</returns>
        bool CanFilter(INyxNode node, INyxMessage message = null);

        /// <summary>
        /// Pre process a message and stops message execution or changes the message.
        /// </summary>
        /// <param name="message">Message to filter</param>
        /// <param name="sender"></param>
        /// <param name="error">Error message for filtered messages.</param>
        /// <returns>Should we continue</returns>
        bool AllowMessage(INyxMessage message, INyxNode sender, out string error);
    }
}
