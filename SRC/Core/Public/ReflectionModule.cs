/********************************************************************************
* ReflectionModule.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Castle.DynamicProxy;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;
    using Primitives;
    using Primitives.Patterns;

    using static Properties.Resources;

    /// <summary>
    /// Module holding the reflection related stuffs
    /// </summary>
    public sealed class ReflectionModule<TView>: Singleton<ReflectionModule<TView>>, IReflectionModule<TView> where TView : ViewBase
    {
        #region Private
        private static readonly MethodInfo FDeserializeMultiTypeArray = MethodInfoExtractor.Extract<ISerializer>(static s => s.Deserialize(null!, null!));

        private sealed class ViewInterceptor : IInterceptor, IEventfulViewConfig
        {
            public bool EventingDisabled { get; set; }

            public void Intercept(IInvocation invocation)
            {
                //
                // Ensure the view is not disposed (regardless we have an eventized method or not)
                //

                TView view = (TView) invocation.Proxy;
                view.CheckDisposed();

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
            private ProxyGenerationOptions Options { get; }

            private MyProxyGenerator()
            {
                Options = new ProxyGenerationOptions();
                Options.AdditionalAttributes.Add
                (
                    new CustomAttributeInfo
                    (
                        typeof(GeneratedCodeAttribute).GetConstructors().Single(),
                        [nameof(ReflectionModule<TView>), null]
                    )
                );
            }

            //
            // CreateClassProxy() uses Activator.CreateInstance() which is... uhm... slow?
            //

            public Type CreateProxyClass<T>() => CreateClassProxyType(typeof(T), [], Options);

            public static MyProxyGenerator Instance { get; } = new();
        }

        private static FutureDelegate<CreateRawViewDelegate<TView>> CreateInterceptorFactory(DelegateCompiler compiler) 
        {
            ParameterExpression
                flowId = Expression.Parameter(typeof(string), nameof(flowId)),
                ownerRepo = Expression.Parameter(typeof(IViewRepository<TView>), nameof(ownerRepo)),
                config = Expression.Parameter(typeof(IEventfulViewConfig).MakeByRefType(), nameof(config)),
                interceptor = Expression.Variable(typeof(ViewInterceptor), nameof(interceptor));

            return compiler.Register
            (
                //
                // (flowId, repo, out config) =>
                // {
                //     ViewInterceptor interceptor = new();
                //     config = interceptor;
                //     return new ProxyView([interceptor], flowId, repo);
                // }
                //

                Expression.Lambda<CreateRawViewDelegate<TView>>
                (
                    Expression.Block
                    (
                        typeof(TView),
                        variables: [interceptor],
                        Expression.Assign(interceptor, Expression.New(typeof(ViewInterceptor))),
                        Expression.Assign(config, interceptor),
                        Expression.New
                        (
                            MyProxyGenerator
                                .Instance
                                .CreateProxyClass<TView>()
                                .GetConstructor
                                (
                                    BindingFlags.Public | BindingFlags.Instance,
                                    null,
                                    [typeof(IInterceptor[]), typeof(string), typeof(IViewRepository)],
                                    null
                                ) ?? throw new InvalidOperationException(ERR_NO_COMPATIBLE_CTOR),
                            Expression.NewArrayInit(typeof(IInterceptor), interceptor),
                            flowId,
                            ownerRepo
                        )
                    ),
                    flowId,
                    ownerRepo,
                    config
                )
            );
        }

        private static IReadOnlyDictionary<string, FutureDelegate<ProcessEventDelegate<TView>>> CreateEventProcessorsDict(DelegateCompiler compiler)
        {
            Type viewType = typeof(TView);

            if (viewType.IsSealed)
                throw new InvalidOperationException(ERR_CANNOT_BE_INTERCEPTED);

            Dictionary<string, FutureDelegate<ProcessEventDelegate<TView>>> processors = [];
          
            foreach (MethodInfo method in viewType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                EventAttribute? evtAttr = method.GetCustomAttribute<EventAttribute>();
                if (evtAttr is null)
                    continue;

                if (!method.IsVirtual)
                    throw new InvalidOperationException(ERR_NOT_VIRTUAL).WithData(("method", method.Name));

                if (processors.ContainsKey(evtAttr.Id))
                    throw new InvalidOperationException(ERR_DUPLICATE_EVENT_ID).WithData(("id", evtAttr.Id));

                IReadOnlyList<Type> argTypes = method
                    .GetParameters()
                    .Select(static p => p.ParameterType)
                    .ToList();
                if (method.ReturnType != typeof(void) || argTypes.Any(static t => t.IsByRef))
                    throw new InvalidOperationException(ERR_HAS_RETVAL).WithData(("method", method.Name));

                ParameterExpression
                    view = Expression.Parameter(viewType, nameof(view)),
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

                        Expression.Lambda<ProcessEventDelegate<TView>>
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
                                    view,
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
                            parameters: [view, args, serializer]
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

            IReadOnlyDictionary<string, FutureDelegate<ProcessEventDelegate<TView>>> processors = CreateEventProcessorsDict(compiler);

            FutureDelegate<CreateRawViewDelegate<TView>> ctor = CreateInterceptorFactory(compiler);

            compiler.Compile();

            EventProcessors = processors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.Value);
            CreateRawView = ctor.Value;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, ProcessEventDelegate<TView>> EventProcessors { get; }

        /// <inheritdoc/>
        public CreateRawViewDelegate<TView> CreateRawView { get; }
    }
}
