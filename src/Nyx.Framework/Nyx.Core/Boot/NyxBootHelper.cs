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
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using Autofac;
using NetMQ;
using Nyx.Core.Config;
using Nyx.Core.Logging;
using Nyx.Core.Messaging;
using Nyx.Core.Plugins;

namespace Nyx.Core.Boot
{
    /// <summary>
    /// Nyx Boot helper.
    /// </summary>
    public class NyxBootHelper
    {
        internal ContainerBuilder Builder;

        internal NyxBootHelper()
        {
            Builder = new ContainerBuilder();
            // TODO: Requires upgrade for fwd 
            Builder.RegisterInstance(NetMQContext.Create()).SingleInstance();

            Builder.RegisterType<MessageBus>().As<IMessageBus>().SingleInstance();
            Builder.RegisterGeneric(typeof(DefaultLogger<>)).As(typeof(ILogger<>));
            Builder.RegisterType<JsonConfigManager>().As<IConfigManager>().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies).SingleInstance();
            Builder.RegisterInstance(new Form()).As<ISynchronizeInvoke>().SingleInstance();
            Builder.RegisterInstance(SynchronizationContext.Current).As<SynchronizationContext>().SingleInstance();
            Builder.RegisterType<PluginManager>().AsSelf().SingleInstance().OnActivated(c => c.Instance.Init(c.Context));
        }
    }
}
