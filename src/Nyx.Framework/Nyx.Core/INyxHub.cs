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
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    /// <summary>
    /// Server for the NyxFramework
    /// </summary>
    public interface INyxHub : INyxNode
    {
        /// <summary>
        /// Port number for the server listener.
        /// There are 2 open ports, one for a REQ-REP (INyxHub.Port) and another for the SUB-PUB (INyxHub.Port+1).
        /// </summary>
        int Port { get; set; }

        /// <summary>
        ///     Observable for message received.
        /// </summary>
        IObservable<MessageStatus> InMessageStream { get; }

        /// <summary>
        /// Broadcast a message to the SUB-PUB channel.
        /// </summary>
        /// <param name="msg">Message to broadcast.</param>
        void BroadcastMessage(INyxMessage msg);

        /// <summary>
        /// Starts the Hub.
        /// </summary>
        IObservable<NyxNodeStatus> Start();

        /// <summary>
        /// Stops the Hub.
        /// </summary>
        IObservable<NyxNodeStatus> Stop();
    }
}
