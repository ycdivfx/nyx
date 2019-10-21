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

namespace Nyx.Core.Extensions
{
    public enum ExtensionLifeCycle
    {
        Shared,
        NonShared
    }

    [Flags]
    public enum ExtensionRegistration
    {
        Interfaces,
        Self
    }

    /// <summary>
    /// Extension attribute for meta info on a extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [Serializable]
    public class ExtensionAttribute : Attribute
    {
        public string Name { get; private set; }
        public ExtensionLifeCycle LifeCycle { get; private set; }
        public ExtensionRegistration Registration { get; private set; }

        public ExtensionAttribute(string name, ExtensionLifeCycle lifeCycle) : this(name, lifeCycle, ExtensionRegistration.Interfaces|ExtensionRegistration.Self)
        {
        }

        public ExtensionAttribute(string name) : this(name, ExtensionLifeCycle.Shared, ExtensionRegistration.Interfaces|ExtensionRegistration.Self)
        {
        }

        public ExtensionAttribute(string name, ExtensionLifeCycle lifeCycle, ExtensionRegistration registration)
        {
            Name = name;
            LifeCycle = lifeCycle;
            Registration = registration;
        }
    }
}
