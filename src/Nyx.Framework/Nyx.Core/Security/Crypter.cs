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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Nyx.Core.Security
{
    public class Crypter
    {
        private readonly List<byte> _iv;
        private readonly List<byte> _passkey;

        public Crypter(string passkey, string initVector)
        {
            var hasher = new Hasher(EnumHashSize.SHA256);
            var nums = new List<byte>(hasher.ComputeHashToBytes(initVector));
            _passkey = new List<byte>(hasher.ComputeHashToBytes(passkey));
            var num = _passkey.Aggregate(0, (current, num1) => current + num1);
            _iv = new List<byte>(nums.GetRange(num%16, 16));
        }

        private byte[] InitVectorBytes
        {
            get { return _iv.ToArray(); }
        }

        private byte[] PasskeyBytes
        {
            get { return _passkey.ToArray(); }
        }

        public string Decrypt(string value)
        {
            byte[] numArray;
            int num;
            ICryptoTransform cryptoTransform = null;
            MemoryStream memoryStream = null;
            CryptoStream cryptoStream = null;
            var numArray1 = Convert.FromBase64String(value);
            var rijndaelManaged = new RijndaelManaged();
            using (rijndaelManaged)
            {
                try
                {
                    cryptoTransform = rijndaelManaged.CreateDecryptor(PasskeyBytes, InitVectorBytes);
                    memoryStream = new MemoryStream(numArray1);
                    cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Read);
                    numArray = new byte[numArray1.Length];
                    num = cryptoStream.Read(numArray, 0, numArray1.Length);
                    cryptoStream.Close();
                    cryptoStream = null;
                    memoryStream = null;
                }
                finally
                {
                    if (cryptoStream != null)
                    {
                        cryptoStream.Dispose();
                        memoryStream = null;
                    }
                    if (memoryStream != null)
                        memoryStream.Dispose();
                    if (cryptoTransform != null)
                        cryptoTransform.Dispose();
                    rijndaelManaged.Clear();
                }
            }
            var nums = new List<byte>(numArray);
            return Encoding.Unicode.GetString(nums.GetRange(0, num).ToArray());
        }

        public string Encrypt(string value)
        {
            byte[] array;
            ICryptoTransform cryptoTransform = null;
            MemoryStream memoryStream = null;
            CryptoStream cryptoStream = null;
            var bytes = Encoding.Unicode.GetBytes(value);
            var rijndaelManaged = new RijndaelManaged();
            using (rijndaelManaged)
            {
                try
                {
                    cryptoTransform = rijndaelManaged.CreateEncryptor(PasskeyBytes, InitVectorBytes);
                    memoryStream = new MemoryStream();
                    cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write);
                    cryptoStream.Write(bytes, 0, bytes.Length);
                    cryptoStream.FlushFinalBlock();
                    array = memoryStream.ToArray();
                    cryptoStream.Close();
                    cryptoStream = null;
                    memoryStream = null;
                }
                finally
                {
                    if (cryptoStream != null)
                    {
                        cryptoStream.Dispose();
                        memoryStream = null;
                    }
                    if (memoryStream != null)
                    {
                        memoryStream.Dispose();
                    }
                    if (cryptoTransform != null)
                    {
                        cryptoTransform.Dispose();
                    }
                    rijndaelManaged.Clear();
                }
            }
            return Convert.ToBase64String(array);
        }
    }
}