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
    [Flags]
    public enum MessageDirection
    {
        In,
        Out
    }

    public enum MessageCondition
    {
        Sent,
        Block,
        Filtered,
        Received,
        Failed,
        Timeout
    }

    /// <summary>
    /// MessageStatus works as a wrapper for the MessageBus
    /// </summary>
    public class MessageStatus
    {
        public INyxNode Sender { get; set; }
        public INyxMessage Message { get; private set; }
        public bool IsHubReply { get; private set; }
        public MessageCondition Status { get; private set; }
        public string Description { get; private set; }

        public MessageStatus(INyxNode sender, INyxMessage msg, MessageCondition condition) :
            this(sender, msg, condition, string.Empty, false)
        {
        }

        public MessageStatus(INyxNode sender, INyxMessage msg, MessageCondition condition, string description) :
            this(sender, msg, condition, description, false)
        {
        }

        public MessageStatus(INyxNode sender, INyxMessage msg, MessageCondition condition, bool hubReply) :
            this(sender, msg, condition, string.Empty, hubReply)
        {
        }

        public MessageStatus(INyxNode sender, INyxMessage msg, MessageCondition condition, string description, bool hubReply)
        {
            Sender = sender;
            Message = msg;
            Status = condition;
            IsHubReply = hubReply;
            Description = description;
        }

        public override string ToString()
        {
            return $"M:{Message} S:{Status} O:{Sender} D:{Description}";
        }
    }
}
