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
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Nyx.Common.UI.Threading;

namespace Nyx.Common.UI
{
    /// <summary>
    ///     From reactive trader.
    /// https://github.com/AdaptiveConsulting/ReactiveTrader
    /// </summary>
    public static class ObservableExtensions
    {
        // ReSharper disable once InconsistentNaming
        /// <summary>
        ///     Observes on the main ui thread.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IObservable<T> ObserveOnUI<T>(this IObservable<T> source)
        {
            return source.ObserveOn(DispatcherHelper.UIDispatcher);
        }

        public static IObservable<Unit> ObserveProperty(this INotifyPropertyChanged source)
        {
            return Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => (s, e) => h(e),
                h => source.PropertyChanged += h,
                h => source.PropertyChanged -= h)
                .Select(_ => Unit.Default);
        }

        public static IObservable<TProp> ObserveProperty<TSource, TProp>(this TSource source,
            Expression<Func<TSource, TProp>> propertyExpression,
            bool observeInitialValue)
            where TSource : INotifyPropertyChanged
        {
            return Observable.Create<TProp>(o =>
            {
                var propertyName = GetPropertyName(source, propertyExpression);
                var selector = CompiledExpressionHelper<TSource, TProp>.GetFunc(propertyExpression);

                var observable
                    = from evt in Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                        h => (s, e) => h(e),
                        h => source.PropertyChanged += h,
                        h => source.PropertyChanged -= h)
                        where evt.PropertyName == propertyName
                        select selector(source);
                observable = observeInitialValue ? observable.StartWith(selector(source)) : observable;

                return observable.Subscribe(o);
            });
        }

        public static IObservable<TProp> ObserveProperty<TSource, TProp>(this TSource source,
            Expression<Func<TSource, TProp>>
                propertyExpression)
            where TSource : INotifyPropertyChanged
        {
            return ObserveProperty(source, propertyExpression, false);
        }

        public static string GetPropertyName<TSource, TProp>(this TSource source,
            Expression<Func<TSource, TProp>> propertyExpression)
        {
            var memberExpression = CompiledExpressionHelper<TSource, TProp>.GetMemberExpression(propertyExpression);
            return memberExpression.Member.Name;
        }

        private static class CompiledExpressionHelper<TSource, TProp>
        {
            private static readonly Dictionary<string, Func<TSource, TProp>> Funcs =
                new Dictionary<string, Func<TSource, TProp>>();

            public static Func<TSource, TProp> GetFunc(Expression<Func<TSource, TProp>> propertyExpression)
            {
                var memberExpression = GetMemberExpression(propertyExpression);
                var propertyName = memberExpression.Member.Name;
                var key = typeof (TSource).FullName + "." + propertyName;
                Func<TSource, TProp> func;

                if (!Funcs.TryGetValue(key, out func))
                {
                    Funcs[key] = propertyExpression.Compile();
                }
                return Funcs[key];
            }

            public static MemberExpression GetMemberExpression(Expression<Func<TSource, TProp>> propertyExpression)
            {
                MemberExpression memberExpression;

                var unaryExpr = propertyExpression.Body as UnaryExpression;
                if (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert)
                {
                    memberExpression = (MemberExpression) unaryExpr.Operand;
                }
                else
                {
                    memberExpression = (MemberExpression) propertyExpression.Body;
                }
                if (memberExpression.Expression.NodeType != ExpressionType.Parameter &&
                    memberExpression.Expression.NodeType != ExpressionType.Constant)
                {
                    throw new InvalidOperationException(
                        "Getting members not directly on the expression's root object has been disallowed.");
                }
                return memberExpression;
            }
        }
    }
}