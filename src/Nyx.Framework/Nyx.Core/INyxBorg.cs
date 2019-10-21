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
using Nyx.Core.Messaging;

namespace Nyx.Core
{
    /// <summary>
    /// This is the Client for the NyxFramework.
    /// </summary>
    public interface INyxBorg : INyxNode
    {
        /// <summary>
        ///     List of Groups a Borg belongs.
        ///     Internally this is the topics subscription to the zmq pub-sub model.
        /// </summary>
        IReadOnlyList<string> SubscribersChannels { get; }

        /// <summary>
        ///     Is running
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        ///     Node id
        /// </summary>
        string NodeId { get; set; }

        /// <summary>
        ///     Observable for message received.
        /// </summary>
        IObservable<MessageStatus> InMessageStream { get; }

        /// <summary>
        ///     Observable for connection changes.
        /// </summary>
        IObservable<ConnectionStatus> ConnectionStatusStream { get; }

        /// <summary>
        ///     Observable for message from the Hub. This is just replies.
        /// </summary>
        IObservable<MessageStatus> OutMessageStream { get; }

        /// <summary>
        ///     Start the borg.
        /// </summary>
        IObservable<NyxNodeStatus> Start();

        /// <summary>
        ///     Stops the borg.
        /// </summary>
        IObservable<NyxNodeStatus> Stop();

        /// <summary>
        ///     Connects the borg to the given hub address.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        bool Connect(string ipAddress="", int port = 4015);

        /// <summary>
        ///     Adds the borg to a new group.
        /// </summary>
        /// <param name="topic">Group to add</param>
        void AddToGroup(string topic);

        /// <summary>
        ///     Removes a borg from a group.
        /// </summary>
        /// <param name="topic">Group to remove from</param>
        /// <returns></returns>
        bool RemoveFromGroup(string topic);

        /// <summary>
        ///     Queues a message for sending to the hub for distribution.
        /// </summary>
        /// <param name="msg">Nyx message to send.</param>
        /// <param name="skip">Doesn't queue the message if no connection to the hub.</param>
        /// <returns>An observable for checking when the message is delivered to the Hub.</returns>
        IObservable<MessageStatus> SendMessage(INyxMessage msg, bool skip = false);

        /// <summary>
        ///     Removes all subscriptions.
        /// </summary>
        void ClearSubscriptions();

        /// <summary>
        ///     Add a batch of new groups
        /// </summary>
        /// <param name="groups"></param>
        void AddSubscriptions(IEnumerable<string> groups);
    }
}
