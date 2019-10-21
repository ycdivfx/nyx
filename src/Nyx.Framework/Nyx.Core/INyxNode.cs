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
using System.Reactive.Linq;
using Nyx.Core.Plugins;

namespace Nyx.Core
{
    public enum NyxNodeStatus
    {
        Stopped,
        Started,
        Unknown
    }

    public static class NodeStatus
    {
        public static IObservable<NyxNodeStatus> UnknownObservable()
        {
            return Observable.Return(NyxNodeStatus.Unknown);
        }
        public static IObservable<NyxNodeStatus> StartedObservable()
        {
            return Observable.Return(NyxNodeStatus.Started);
        }

        public static IObservable<NyxNodeStatus> StoppedObservable()
        {
            return Observable.Return(NyxNodeStatus.Stopped);
        }

        public static NyxNodeStatus Stopped =>NyxNodeStatus.Stopped;

        public static NyxNodeStatus Started => NyxNodeStatus.Started;

        public static NyxNodeStatus Unknown => NyxNodeStatus.Unknown;
    }

    /// <summary>
    ///     This is used by <see cref="NodeManager" /> and <see cref="EvidenceManager" />
    /// </summary>
    public enum NyxHostMode
    {
        /// <summary>
        ///     None is for enslaver and clients that don't count for anything.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Workstation is for clients that control other slaves.
        /// </summary>
        Workstation = 1,

        /// <summary>
        ///     No ui or other stuff available they are remotely controlled.
        /// </summary>
        Slave = 2
    }

    /// <summary>
    ///     Represents a node for the NyxFramework.
    ///     There are 2 types of nodes, hub(server) and borg(client).
    /// </summary>
    public interface INyxNode
    {
        /// <summary>
        ///     Total messages received on this node.
        /// </summary>
        long TotalInMessages { get; }

        /// <summary>
        ///     Total messages sent out of the node.
        /// </summary>
        long TotalOutMessages { get; }

        /// <summary>
        ///     Host application where the <see cref="INyxNode" /> is running.
        /// </summary>
        string HostApp { get; set; }

        /// <summary>
        ///     This is the host mode of the application. Only Borgs should really use this.
        ///     <example>
        ///         node.HostMode = NyxHostMode.Slave; //
        ///     </example>
        /// </summary>
        NyxHostMode HostMode { get; set; }

        /// <summary>
        ///     Node status
        /// </summary>
        NyxNodeStatus Status { get; }

        /// <summary>
        ///     Returns a observable for node status changes.
        /// </summary>
        IObservable<NyxNodeStatus> NodeStatusStream { get; }
    }
}