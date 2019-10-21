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
using Nyx.Core.Logging;

namespace Nyx.Core.Plugins
{

    public class BasicHubAction : BaseHubAction
    {
        private readonly ILogger<BasicHubAction> _logger;
        public const string NyxId = "nyx";
        public const string Register = "core.register";
        public const string Callback = "core.callback";
        public const string Ping = "core.ping";
        public const string Pong = "core.pong";
        private readonly INyxHub _hub;
        
        public BasicHubAction(ILogger<BasicHubAction> logger, INyxNode hub)
        {
            _logger = logger;
            _hub = hub as INyxHub;
            _name = "basic";
            _supportedActions.AddRange(new[]
            {
                Register, Ping
            });
        }

        public override bool ProcessMessage(INyxMessage msg)
        {
            if (_hub == null) return true;
            switch (msg.Action)
            {
                case Register:
                    _logger.Info("Registered node {0}", StringExtensions.Trimmer(msg.Source));
                    _hub.BroadcastMessage(new NyxMessage
                    {
                        Target = msg.Source,
                        Action = Callback,
                        Source = NyxId
                    }.Set("event","registered")
                    );
                    break;
                case Ping:
                    _logger.Info("Node {0} pinged.", StringExtensions.Trimmer(msg.Source));
                    msg.Reply(Pong).BroadcastMessage(_hub);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
