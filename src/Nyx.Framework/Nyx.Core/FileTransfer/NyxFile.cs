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
using System.Diagnostics;
using System.IO;

namespace Nyx.Core.FileTransfer
{
    /// <summary>
    ///     Nyx file.
    /// </summary>
    public class NyxFile : IFile
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public string TransferType { get; set; }

        public bool DeleteOnTransfer { get; set; }

        public ulong FileSize { get; set; }


        public bool Delete()
        {
            try
            {
                File.Delete(Path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Write("[NyxFile] Error on delete." + ex);
            }
            return false;
        }

        public override string ToString()
        {
            return string.Format("{0} deleteAfter:{1}", Name, DeleteOnTransfer);
        }
    }
}
