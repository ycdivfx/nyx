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

namespace Nyx.Core.Messaging
{
    /// <summary>
    /// Simple enum to tell the hub status
    /// </summary>
    [Flags]
    public enum ConnectionStatus
    {
        Connecting = 1,
        Connected = 2,
        Disconnected = 4,
        Online = 8,
        Offline = 16
    }

    /// <summary>
    /// Connection status message
    /// </summary>
    public class ConnectionStatusInfo
    {
        /// <summary>
        /// Node connection status.
        /// </summary>
        public ConnectionStatus Status { get; private set; }

        /// <summary>
        /// Endpoint for the status.
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// Nyx node that send this message.
        /// </summary>
        public INyxNode Node { get; private set; }

        public ConnectionStatusInfo(ConnectionStatus status, string endpoint) : this(status, endpoint, null)
        {
        }

        /// <summary>
        /// Connection status message construtor.
        /// </summary>
        /// <param name="node">Sender node</param>
        /// <param name="endpoint">Endpoint for the connection status.</param>
        /// <param name="status">Node connection status.</param>
        public ConnectionStatusInfo(ConnectionStatus status, string endpoint, INyxNode node)
        {
            Node = node;
            Endpoint = endpoint;
            Status = status;
        }
    }
}
