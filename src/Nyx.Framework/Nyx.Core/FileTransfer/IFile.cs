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
using System.Runtime.Serialization;

namespace Nyx.Core.FileTransfer
{
    /// <summary>
    /// This represents a file to be transfered, by the Nyx.
    /// Normally this is done by a FileTransferManager.
    /// </summary>
    public interface IFile
    {
        /// <summary>
        /// Name of the file, can be used as a key to access the file.
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// Full path to the file.
        /// </summary>
        string Path { get; set; }
        /// <summary>
        /// Gets the type used to transfer this file.
        /// </summary>
        string TransferType { get; set; }
        /// <summary>
        /// Deletes file after being transfer.
        /// </summary>
        bool DeleteOnTransfer { get; set; }

        [IgnoreDataMember]
        ulong FileSize { get; set; }

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <returns></returns>
        bool Delete();
    }
}
