﻿/*
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
using System.Collections.Generic;
using Nyx.Core.Extensions;

namespace Nyx.Core.Plugins
{
    public abstract class BaseHubAction : IHubAction
    {
        protected readonly List<string> _supportedActions = new List<string>();
        protected string _name;

        public IEnumerable<string> SupportedActions => _supportedActions;

        public string Name => _name;

        public abstract bool ProcessMessage(INyxMessage msg);
    }
}
