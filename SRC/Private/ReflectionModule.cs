/********************************************************************************
* ReflectionModule.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Solti.Utils.Eventing.Internals
{
    using Abstractions;
    using Primitives;
    using Proxy.Generators;

    using static Properties.Resources;

    /// <summary>
    /// Module holding the reflection related stuffs
    /// </summary>
    public class ReflectionModule
    {
        /// <summary>
        /// Creates the factory function that is responsible for creating proxy instances around the given <typeparamref name="TView"/>
        /// </summary>
        /// <remarks>The default implementation is using the ProxyGen.NET library</remarks>
        public virtual Func<TView, IView> CreateInterceptorFactory<TView, IView>() where TView : ViewBase, IView, new() where IView : class
        {
            ConstructorInfo ctor = ProxyGenerator<IView, ViewInterceptor<TView, IView>>.GetGeneratedType().GetConstructors().Single();

            ParameterExpression view = Expression.Parameter(typeof(TView), nameof(view));

            return Expression.Lambda<Func<TView, IView>>
            (
                Expression.New(ctor, view),
                view
            ).Compile();
        }

        /// <summary>
        /// Creates the event processor dictionary for the given <typeparamref name="TView"/>
        /// </summary>
        public virtual IReadOnlyDictionary<string, Action<TView, string, JsonSerializerOptions>> CreateEventProcessorsDict<TView>() where TView : ViewBase
        {
            Dictionary<string, FutureDelegate<Action<TView, string, JsonSerializerOptions>>> processors = [];

            DelegateCompiler compiler = new();

            foreach (MethodInfo method in typeof(TView).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                EventAttribute? evtAttr = method.GetCustomAttribute<EventAttribute>();
                if (evtAttr is null)
                    continue;

                if (processors.ContainsKey(evtAttr.Name))
                    throw new InvalidOperationException(string.Format(DUPLICATE_EVENT_ID, evtAttr.Name));

                IReadOnlyList<Type> argTypes = method
                    .GetParameters()
                    .Select(static p => p.ParameterType)
                    .ToList();

                MultiTypeArrayConverter multiTypeArrayConverter = new(argTypes);

                ParameterExpression
                    self = Expression.Parameter(typeof(TView), nameof(self)),
                    args = Expression.Parameter(typeof(string), nameof(args)),
                    opts = Expression.Parameter(typeof(JsonSerializerOptions), nameof(opts)),
                    argsArray = Expression.Variable(typeof(object?[]), nameof(argsArray));

                processors.Add
                (
                    evtAttr.Name,
                    compiler.Register
                    (
                        //
                        // (view, args) =>
                        // {
                        //     object[] argsAr = DeserializeMultiTypeArray(args);
                        //     view.Method((T1) argsAr[0], (T2) argsAr[1]);
                        // }
                        //

                        Expression.Lambda<Action<TView, string, JsonSerializerOptions>>
                        (
                            Expression.Block
                            (
                                variables: [argsArray],
                                Expression.Assign
                                (
                                    argsArray,
                                    Expression.Invoke
                                    (
                                        Expression.Constant
                                        (
                                            (Func<string, JsonSerializerOptions, object?[]>) DeserializeMultiTypeArray
                                        ),
                                        args,
                                        opts
                                    )
                                ),
                                Expression.Call
                                (
                                    self,
                                    method,
                                    argTypes.Select
                                    (
                                        (t, i) => Expression.Convert
                                        (
                                            Expression.ArrayAccess(argsArray, Expression.Constant(i)),
                                            t
                                        )
                                    )
                                )
                            ),
                            parameters: [self, args, opts]
                        )
                    )
                );

                object?[] DeserializeMultiTypeArray(string s, JsonSerializerOptions opts)
                {
                    opts = new(opts);
                    opts.Converters.Add(multiTypeArrayConverter);

                    return JsonSerializer.Deserialize<object?[]>(s, opts)!;
                }
            }

            compiler.Compile();
            return processors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.Value);
        }
    }
}
