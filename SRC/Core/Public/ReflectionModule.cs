/********************************************************************************
* ReflectionModule.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private static readonly PropertyInfo
            FFlowId = PropertyInfoExtractor.Extract<TView, string>(static v => v.FlowId),
            FOwnerRepo = PropertyInfoExtractor.Extract<TView, object>(static v => v.OwnerRepository);

        private sealed class ViewInterceptor : IInterceptor
        {
            private static IReadOnlyDictionary<IntPtr, Func<ViewInterceptor, object?[], object?>> ConcreteImplementations { get; } = new Dictionary<IntPtr, Func<ViewInterceptor, object?[], object?>>
            {
                {PropertyInfoExtractor.Extract<IEventfulView, bool>(static v => v.EventingDisabled).GetMethod.MethodHandle.Value, static (v, _) => v.EventingDisabled},
                {PropertyInfoExtractor.Extract<IEventfulView, bool>(static v => v.EventingDisabled).SetMethod.MethodHandle.Value, static (v, args) => { v.EventingDisabled = (bool) args[0]!; return null; } }
            };

            public bool EventingDisabled { get; private set; }

            public void Intercept(IInvocation invocation)
            {
                //
                // Ensure the view is not disposed (regardless we have an eventized method or not)
                //

                TView view = (TView) invocation.Proxy;
                view.CheckDisposed();

                //
                // If we have a concerete implementation then use that
                //

                if (ConcreteImplementations.TryGetValue(invocation.Method.MethodHandle.Value, out Func<ViewInterceptor, object?[], object?> impl))
                {
                    invocation.ReturnValue = impl(this, invocation.Arguments);
                    return;
                }

                //
                // Call the target method
                //

                invocation.Proceed();

                //
                // Persist the state
                //

                EventAttribute? evtAttr = invocation.MethodInvocationTarget.GetCustomAttribute<EventAttribute>();
                if (evtAttr is not null && !EventingDisabled)
                    view.OwnerRepository.Persist(view, evtAttr.Id, invocation.Arguments);
            }
        }

        private sealed class MyProxyGenerator : ProxyGenerator
        {
            //
            // CreateClassProxy() uses Activator.CreateInstance() which is... uhm... slow?
            //

            public Type CreateProxyClass<T>() => CreateClassProxyType(typeof(T), [typeof(IEventfulView)], ProxyGenerationOptions.Default);
        }

        private static FutureDelegate<Func<string, IViewRepository<TView>, TView>> CreateInterceptorFactory(DelegateCompiler compiler) 
        {
            MyProxyGenerator proxyGenerator = new();
            Type t = proxyGenerator.CreateProxyClass<TView>();

            ConstructorInfo ctor = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, [typeof(IInterceptor[])], null);
            Debug.Assert(ctor is not null, "View constructor not found");

            ParameterExpression
                flowId = Expression.Parameter(typeof(string), nameof(flowId)),
                ownerRepo = Expression.Parameter(typeof(IViewRepository<TView>), nameof(ownerRepo));

            return compiler.Register
            (
                Expression.Lambda<Func<string, IViewRepository<TView>, TView>>
                (
                    Expression.MemberInit
                    (
                        Expression.New
                        (
                            ctor,
                            Expression.NewArrayInit
                            (
                                typeof(IInterceptor),
                                Expression.New(typeof(ViewInterceptor))
                            )
                        ),
                        Expression.Bind(FFlowId, flowId),
                        Expression.Bind(FOwnerRepo, ownerRepo)
                    ),
                    flowId,
                    ownerRepo
                )
            );
        }

        private static IReadOnlyDictionary<string, FutureDelegate<Action<TView, string, ISerializer>>> CreateEventProcessorsDict(DelegateCompiler compiler)
        {
            Type viewType = typeof(TView);

            if (viewType.IsSealed)
                throw new InvalidOperationException(ERR_CANNOT_BE_INTERCEPTED);

            Dictionary<string, FutureDelegate<Action<TView, string, ISerializer>>> processors = [];
          
            foreach (MethodInfo method in viewType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                EventAttribute? evtAttr = method.GetCustomAttribute<EventAttribute>();
                if (evtAttr is null)
                    continue;

                if (!method.IsVirtual)
                    throw new InvalidOperationException(Format(ERR_NOT_VIRTUAL, method.Name));

                if (processors.ContainsKey(evtAttr.Id))
                    throw new InvalidOperationException(Format(ERR_DUPLICATE_EVENT_ID, evtAttr.Id));

                IReadOnlyList<Type> argTypes = method
                    .GetParameters()
                    .Select(static p => p.ParameterType)
                    .ToList();
                if (method.ReturnType != typeof(void) || argTypes.Any(static t => t.IsByRef))
                    throw new InvalidOperationException(Format(ERR_HAS_RETVAL, method.Name));

                ParameterExpression
                    self = Expression.Parameter(viewType, nameof(self)),
                    args = Expression.Parameter(typeof(string), nameof(args)),
                    serializer = Expression.Parameter(typeof(ISerializer), nameof(serializer)),
                    argsArray = Expression.Variable(typeof(object?[]), nameof(argsArray));

                processors.Add
                (
                    evtAttr.Id,
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

            FutureDelegate<Func<string, IViewRepository<TView>, TView>> ctor = CreateInterceptorFactory(compiler);

            compiler.Compile();

            EventProcessors = processors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.Value);
            CreateRawView = ctor.Value;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Action<TView, string, ISerializer>> EventProcessors { get; }

        /// <inheritdoc/>
        public Func<string, IViewRepository<TView>, TView> CreateRawView { get; }
    }
}
