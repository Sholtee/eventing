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

using Castle.DynamicProxy;

using static System.String;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Primitives;
    using Primitives.Patterns;

    using static Properties.Resources;

    /// <summary>
    /// Module holding the reflection related stuffs
    /// </summary>
    public sealed class ReflectionModule<TView>: Singleton<ReflectionModule<TView>>, IReflectionModule<TView> where TView : ViewBase, new()
    {
        #region Private
        private static readonly MethodInfo FDeserializeMultiTypeArray = MethodInfoExtractor.Extract<ISerializer>(static s => s.Deserialize(null!, null!));

        private sealed class ViewInterceptor : Singleton<ViewInterceptor>, IInterceptor
        {
            public void Intercept(IInvocation invocation)
            {
                invocation.Proceed();

                TView view = (TView) invocation.Proxy;

                if (!view.DisableInterception)
                {
                    EventAttribute? evtAttr = invocation.MethodInvocationTarget.GetCustomAttribute<EventAttribute>();
                    if (evtAttr is not null)
                    {
                        IViewRepository<TView> repo = (IViewRepository<TView>) view.OwnerRepository;
                        repo.Persist(view, evtAttr.Name, invocation.Arguments);
                    }
                }
            }
        }

        private sealed class MyProxyGenerator : ProxyGenerator
        {
            //
            // CreateClassProxy() uses Activator.CreateInstance() which is... uhm... slow?
            //

            public Type CreateProxyClass<T>() => CreateClassProxyType(typeof(T), [], ProxyGenerationOptions.Default);
        }

        private static FutureDelegate<Func<TView>> CreateInterceptorFactory(DelegateCompiler compiler) 
        {
            MyProxyGenerator proxyGenerator = new();
            Type t = proxyGenerator.CreateProxyClass<TView>();

            ConstructorInfo ctor = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, [typeof(IInterceptor[])], null);

            return compiler.Register
            (
                Expression.Lambda<Func<TView>>
                (
                    Expression.New
                    (
                        ctor,
                        Expression.Constant(new IInterceptor[] { ViewInterceptor.Instance })
                    )
                )
            );
        }

        private static IReadOnlyDictionary<string, FutureDelegate<Action<TView, string, ISerializer>>> CreateEventProcessorsDict(DelegateCompiler compiler)
        {
            Type viewType = typeof(TView);

            if (viewType.IsSealed)
                throw new InvalidOperationException(CANNOT_BE_INTERCEPTED);

            Dictionary<string, FutureDelegate<Action<TView, string, ISerializer>>> processors = [];
          
            foreach (MethodInfo method in viewType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                EventAttribute? evtAttr = method.GetCustomAttribute<EventAttribute>();
                if (evtAttr is null)
                    continue;

                if (!method.IsVirtual)
                    throw new InvalidOperationException(Format(NOT_VIRTUAL, method.Name));

                if (processors.ContainsKey(evtAttr.Name))
                    throw new InvalidOperationException(Format(DUPLICATE_EVENT_ID, evtAttr.Name));

                IReadOnlyList<Type> argTypes = method
                    .GetParameters()
                    .Select(static p => p.ParameterType)
                    .ToList();

                ParameterExpression
                    self = Expression.Parameter(viewType, nameof(self)),
                    args = Expression.Parameter(typeof(string), nameof(args)),
                    serializer = Expression.Parameter(typeof(ISerializer), nameof(serializer)),
                    argsArray = Expression.Variable(typeof(object?[]), nameof(argsArray));

                processors.Add
                (
                    evtAttr.Name,
                    compiler.Register
                    (
                        //
                        // (view, args, serializer) =>
                        // {
                        //     object[] argsAr = serializer.DeserializeMultiTypeArray(args);
                        //     view.Method((T1) argsAr[0], (T2) argsAr[1]);
                        // }
                        //

                        Expression.Lambda<Action<TView, string, ISerializer>>
                        (
                            Expression.Block
                            (
                                variables: [argsArray],
                                Expression.Assign
                                (
                                    argsArray,
                                    Expression.Call(serializer, FDeserializeMultiTypeArray, args, Expression.Constant(argTypes))
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
                            parameters: [self, args, serializer]
                        )
                    )
                );
            }
                
            return processors;
        }
        #endregion

        /// <summary>
        /// Creates a new <see cref="ReflectionModule{TView}"/> instance
        /// </summary>
        public ReflectionModule()
        {
            DelegateCompiler compiler = new();

            IReadOnlyDictionary<string, FutureDelegate<Action<TView, string, ISerializer>>> processors = CreateEventProcessorsDict(compiler);

            FutureDelegate<Func<TView>> ctor = CreateInterceptorFactory(compiler);

            compiler.Compile();

            EventProcessors = processors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.Value);
            CreateRawView = ctor.Value;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Action<TView, string, ISerializer>> EventProcessors { get; }

        /// <inheritdoc/>
        public Func<TView> CreateRawView { get; }
    }
}
