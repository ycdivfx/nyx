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
using JetBrains.Annotations;
using NetMQ;
using Newtonsoft.Json;
using Nyx.Core.Messaging;
using Nyx.Core.Serialization;

namespace Nyx.Core
{
    public static class NyxMessageExtensions
    {
        private const string CoreParamsOriginalMessageId = "core.params.original.message.id";
        private static readonly JsonConverter[] JsonConverters = { new InterfacesConverter() };

        /// <summary>
        /// Converts a NyxMessage to json format
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static string ToJson(this INyxMessage msg)
        {
            return JsonConvert.SerializeObject(msg);
        }

        public static string ToDefault(this INyxMessage msg)
        {
            return msg.ToJson();
        }

        /// <summary>
        /// Converts a NyxMessage to Json and then to a NetMQMessage
        /// </summary>
        /// <param name="msg">Nyx message</param>
        /// <returns>Converted NetMQMessage</returns>
        public static NetMQMessage ToJsonMessage(this INyxMessage msg)
        {
            var zmsg = new NetMQMessage();
            zmsg.Push(msg.ToJson());
            return zmsg;
        }

        /// <summary>
        /// Converts a NyxMessage to the default type and then to a NetMQMessage.
        /// This should be plugable... for future proof
        /// </summary>
        /// <param name="msg">Nyx message</param>
        /// <returns>Converted NetMQMessage</returns>
        public static NetMQMessage ToNetMQMessage(this INyxMessage msg)
        {
            // TODO: Add plugin support or atleast configuration support, so we can 
            var zmsg = new NetMQMessage();
            zmsg.Push(msg.ToDefault());
            return zmsg;
        }

        public static INyxMessage FromJson(this INyxMessage msg, string message)
        {
            msg = JsonConvert.DeserializeObject<NyxMessage>(message,
                JsonConverters);
            ((NyxMessage)msg).Direction = MessageDirection.In;
            return msg;
        }

        public static INyxMessage FromJson(string message)
        {
            var msg = JsonConvert.DeserializeObject<NyxMessage>(message, 
                JsonConverters);
            msg.Direction = MessageDirection.In;
            return msg;
        }

        public static INyxMessage FromDefault(this INyxMessage msg, string message)
        {
            return msg.FromJson(message);
        }

        public static INyxMessage FromDefault(string message)
        {
            return FromJson(message);
        }

        /// <summary>
        /// Shorthand for sending a message.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        /// <param name="borg">Borg responsable to sending the message.</param>
        /// <param name="skip">Skips the message if hub offline.</param>
        /// <param name="timeout">If bigger than 0 or null, a TimeoutMessage operator is added.</param>
        /// <returns>Returns the obsersavle to track message status.</returns>
        public static IObservable<MessageStatus> SendMessage(this INyxMessage msg, INyxBorg borg, bool skip=false, double? timeout=null)
        {
            return timeout.HasValue && timeout.Value > 0
                ? borg.SendMessage(msg, skip).TimeoutMessage(timeout.Value, msg)
                : borg.SendMessage(msg, skip);
        }

        /// <summary>
        ///     Shorthand for the broadcast method of the hub.
        /// </summary>
        /// <param name="msg">Message to broadcast</param>
        /// <param name="hub">Hub from where the message is sent.</param>
        public static void BroadcastMessage(this INyxMessage msg, INyxHub hub)
        {
            hub.BroadcastMessage(msg);
        }

        public static string ShortId(this INyxMessage msg)
        {
            return msg.Id.ToString("N");
        }

        /// <summary>
        ///     Creates a reply message, by switching the target and source. Also adds info from the previous message.
        /// <remarks>No data from the original message is maitained.</remarks>
        /// </summary>
        /// <param name="msg">Source message</param>
        /// <param name="action">Message action</param>
        /// <returns></returns>
        public static INyxMessage Reply(this INyxMessage msg, string action)
        {
            return new NyxMessage(msg.Source, action, msg.Target).Set(CoreParamsOriginalMessageId, msg.ShortId());
        }

        /// <summary>
        ///     Creates a reply message, by switching the target and source and maitaining the previous data bag.
        /// Also adds info from the previous message.
        /// <remarks>All file data is not transfered.</remarks>
        /// </summary>
        /// <param name="msg">Source message</param>
        /// <param name="action">Message action</param>
        /// <returns></returns>
        public static INyxMessage ReplyWithData(this INyxMessage msg, string action)
        {
            var reply = new NyxMessage(msg.Source, action, msg.Target).Set(CoreParamsOriginalMessageId, msg.ShortId());
            foreach (var element in msg.Elements)
                reply.Set(element.Key, element.Value);
            return reply;
        }

        /// <summary>
        ///     Checks if the message is a response to a message.
        /// </summary>
        /// <param name="replyMessage">Possible reply message.</param>
        /// <param name="message">Message to verify against for checking if is a reply.</param>
        /// <returns></returns>
        public static bool IsReplyTo(this INyxMessage replyMessage, INyxMessage message)
        {
            return replyMessage.Has(CoreParamsOriginalMessageId) && replyMessage[CoreParamsOriginalMessageId] == message.ShortId();
        }

        public static MessageStatus Failed(this INyxMessage msg, INyxNode sender, string description)
        {
            return new MessageStatus(sender, msg.AsReadOnly(), MessageCondition.Failed, description);
        }

        public static MessageStatus Timeout(this INyxMessage msg, INyxNode sender, string description)
        {
            return new MessageStatus(sender, msg.AsReadOnly(), MessageCondition.Timeout, description);
        }

        public static IObservable<MessageStatus> Timeout(this INyxMessage msg, Exception ex)
        {
            return Observable.Return(msg.Timeout(null, ex.ToString()));
        }

        public static MessageStatus Filtered(this INyxMessage msg, INyxNode sender, string description)
        {
            return new MessageStatus(sender, msg.AsReadOnly(), MessageCondition.Filtered, description);
        }

        public static MessageStatus SuccessfullReceived(this INyxMessage msg, INyxNode sender)
        {
            return new MessageStatus(sender, msg.AsReadOnly(), MessageCondition.Received);
        }

        public static MessageStatus SuccessfullSent(this INyxMessage msg, INyxNode sender)
        {
            return new MessageStatus(sender, msg.AsReadOnly(), MessageCondition.Sent);
        }

        /// <summary>
        /// Always create a new <see cref="ReadOnlyNyxMessage"/> from a <see cref="INyxMessage"/>./>
        /// </summary>
        /// <param name="msg">Read only <see cref="INyxMessage"/>.</param>
        /// <returns></returns>
        public static INyxMessage ToReadOnly(this INyxMessage msg)
        {
            return new ReadOnlyNyxMessage(msg);
        }

        /// <summary>
        /// Converts <see cref="INyxMessage"/> into a <see cref="ReadOnlyNyxMessage"/> if it isn't read only yet./>
        /// <remarks>Note that this doesn't create a new message every call, only if needed.</remarks>
        /// </summary>
        /// <param name="msg">Read only <see cref="INyxMessage"/>.</param>
        /// <returns></returns>
        public static INyxMessage AsReadOnly([NotNull] this INyxMessage msg)
        {
            if (msg is ReadOnlyNyxMessage) return msg;
            return new ReadOnlyNyxMessage(msg);
        }

        /// <summary>
        ///     Converts a INyxMessage into a internal message.
        ///     This is used to communicate inside borg, it nevers leaves the borg.
        ///     Internal messages are processed by the message filters and message processors, like they were received.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static INyxMessage AsInternal([NotNull] this INyxMessage msg)
        {
            if (msg is InternalNyxMessage) return msg;
            var nmsg = msg as NyxMessage;
            return nmsg == null ? null : new InternalNyxMessage(nmsg) {Direction = MessageDirection.In};
        }

        /// <summary>
        ///     Sends a dummy message to the hub.
        /// </summary>
        /// <param name="sender">The borg to send from</param>
        /// <returns></returns>
        public static IObservable<MessageStatus> SendDummyMessage(this INyxBorg sender)
        {
            return NyxMessage.Create("nyx", "dummy", sender.NodeId).SendMessage(sender, true);
        }

        /// <summary>
        ///     Sends a simple message to the target.
        /// </summary>
        /// <param name="sender">The borg that sends.</param>
        /// <param name="target">The target of the message.</param>
        /// <param name="action">The message action.</param>
        /// <param name="skip">Skips the message if hub offline</param>
        /// <param name="timeout">Timeouts the message.</param>
        /// <returns></returns>
        public static IObservable<MessageStatus> SendMessage(this INyxBorg sender, string target, string action, bool skip = false, double? timeout = null)
        {
            return NyxMessage.Create(target, action, sender.NodeId).SendMessage(sender, skip, timeout);
        }
    }
}
