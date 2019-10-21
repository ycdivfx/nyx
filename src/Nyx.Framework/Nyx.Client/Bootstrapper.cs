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
using Autofac;
using Nyx.Common.UI.ViewModels;
using Nyx.Common.UI.Views;
using Nyx.Client.ViewModels;
using Nyx.Client.Views;
using ReactiveUI;

namespace Nyx.Client
{
    public class Bootstrapper : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register<IViewFor<ITestControlsViewModel>>(c => new TestControlsView());
            builder.Register<IViewFor<ILoggerViewModel>>(c => new LoggerView());
            builder.RegisterType<ShellViewModel>().As<IShellViewModel>();
            builder.RegisterType<ConnectionStatusViewModel>().As<IConnectionStatusViewModel>();
            builder.RegisterType<LoggerViewModel>().As<ILoggerViewModel>();
            builder.RegisterType<TestControlsViewModel>().As<ITestControlsViewModel>();
        }
    }
}
