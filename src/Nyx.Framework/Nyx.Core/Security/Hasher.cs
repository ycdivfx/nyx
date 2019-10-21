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
using System.Security.Cryptography;
using System.Text;

namespace Nyx.Core.Security
{
    internal class Hasher
    {
        public Hasher(EnumHashSize hashSize)
        {
            HashSize = hashSize;
        }

        public EnumHashSize HashSize { get; set; }

        private List<byte> GetSaltedValue(string value)
        {
            var saltBytes = new List<byte>(ComputeHashToBytes(GetSalt()));
            var saltedValue = new List<byte>(ComputeHashToBytes(string.Concat(value, Convert.ToBase64String(saltBytes.ToArray()))));
            saltedValue.AddRange(saltBytes);
            return saltedValue;
        }

        private string GetSalt()
        {
            int num;
            switch (HashSize)
            {
                case EnumHashSize.SHA1:
                {
                    num = 20;
                    break;
                }
                case EnumHashSize.SHA256:
                {
                    num = 32;
                    break;
                }
                case EnumHashSize.SHA384:
                {
                    num = 48;
                    break;
                }
                case EnumHashSize.SHA512:
                {
                    num = 64;
                    break;
                }
                default:
                {
                    throw new NotImplementedException();
                }
            }
            var random = new Random();
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < num; i++)
            {
                var num1 = random.Next(0, 62);
                if (num1 >= 10)
                {
                    num1 = (num1 >= 36 ? num1 + 61 : num1 + 55);
                }
                else
                {
                    num1 = num1 + 48;
                }
                stringBuilder.Append(Convert.ToChar((char) num1));
            }
            return stringBuilder.ToString();
        }

        public string ComputeHash(string value)
        {
            var nums = new List<byte>(ComputeHashToBytes(value));
            return Convert.ToBase64String(nums.ToArray());
        }

        public IList<byte> ComputeHashToBytes(string value)
        {
            HashAlgorithm sHA1Managed;
            IList<byte> nums;
            var nums1 = new List<byte>(Encoding.Unicode.GetBytes(value));
            switch (HashSize)
            {
                case EnumHashSize.SHA1:
                case EnumHashSize.FOLD32:
                case EnumHashSize.FOLD16:
                {
                    sHA1Managed = new SHA1Managed();
                    break;
                }
                case EnumHashSize.SHA256:
                {
                    sHA1Managed = new SHA256Managed();
                    break;
                }
                case EnumHashSize.SHA384:
                {
                    sHA1Managed = new SHA384Managed();
                    break;
                }
                case EnumHashSize.SHA512:
                {
                    sHA1Managed = new SHA512Managed();
                    break;
                }
                default:
                {
                    throw new NotImplementedException();
                }
            }
            using (sHA1Managed)
            {
                var nums2 = new List<byte>(sHA1Managed.ComputeHash(nums1.ToArray()));
                int item;
                switch (HashSize)
                {
                    case EnumHashSize.FOLD32:
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            item = 0;
                            for (var j = 0; j < 5; j++)
                            {
                                item = item + nums2[j*4 + i];
                            }
                            nums2[i] = (byte) (item%256);
                        }
                        nums2.RemoveRange(4, nums2.Count - 4);
                        break;
                    }
                    case EnumHashSize.FOLD16:
                    {
                        for (var k = 0; k < 2; k++)
                        {
                            item = 0;
                            for (var l = 0; l < 10; l++)
                            {
                                item = item + nums2[l*2 + k];
                            }
                            nums2[k] = (byte) (item%256);
                        }
                        nums2.RemoveRange(2, nums2.Count - 2);
                        break;
                    }
                }
                nums = nums2;
            }
            return nums;
        }

        public string ComputeHashWithSalt(string value)
        {
            return Convert.ToBase64String(GetSaltedValue(value).ToArray());
        }

        public bool IsHashWithSaltMatch(string value, string saltedHash)
        {
            var nums = new List<byte>(Convert.FromBase64String(saltedHash));
            return Convert.ToBase64String(
                (new List<byte>(
                    ComputeHashToBytes(string.Concat(value,
                        Convert.ToBase64String((new List<byte>(nums.GetRange(nums.Count/2, nums.Count/2))).ToArray())))))
                    .ToArray()) == Convert.ToBase64String(nums.GetRange(0, nums.Count/2).ToArray());
        }
    }
}