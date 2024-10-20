/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;
    using Primitives;
    using Proxy.Generators;

    using static Properties.Resources;

    /// <summary>
    /// Repository to store view instances
    /// </summary>
    public class ViewRepository<TView, IView>(IEventStore EventStore, IDistributedCache Cache, ILock Lock, ILogger? Logger = null) : IViewRepository<IView> where TView: ViewBase, IView, new() where IView: class
    {
        private static readonly IReadOnlyDictionary<string, Action<TView, string>> FEventProcessors = CreateEventProcessorsDict();

        private static readonly Func<TView, IView> FInterceptorFactory = CreateInterceptorFactory();

        private static Func<TView, IView> CreateInterceptorFactory()
        {
            ConstructorInfo ctor = ProxyGenerator<IView, ViewInterceptor<TView, IView>>.GetGeneratedType().GetConstructors().Single();

            ParameterExpression view = Expression.Parameter(typeof(TView), nameof(view));

            return Expression.Lambda<Func<TView, IView>>
            (
                Expression.New(ctor, view),
                view
            ).Compile();
        }

        private static IReadOnlyDictionary<string, Action<TView, string>> CreateEventProcessorsDict()
        {
            Dictionary<string, FutureDelegate<Action<TView, string>>> processors = [];

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

                        Expression.Lambda<Action<TView, string>>
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
                                            (Func<string, object?[]>) DeserializeMultiTypeArray
                                        )
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
                            parameters: [self, args]
                        )
                    )
                );

                object?[] DeserializeMultiTypeArray(string s)
                {
                    JsonSerializerOptions options = new(SerializerOptions);
                    options.Converters.Add(multiTypeArrayConverter);

                    return JsonSerializer.Deserialize<object?[]>(s, options)!;
                }
            }

            compiler.Compile();
            return processors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.Value);
        }

        /// <summary>
        /// Gets or sets the cache expiraton.
        /// </summary>
        public static TimeSpan CacheEntryExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the JSON serializer options.
        /// </summary>
        public static JsonSerializerOptions SerializerOptions { get; set; } = new()
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        /// <inheritdoc/>
        public void Persist(ViewBase view, string eventId, object?[] args)
        {
            if (view is null)
                throw new ArgumentNullException(nameof(view));

            if (eventId is null)
                throw new ArgumentNullException(nameof(eventId));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            Logger?.LogInformation(new EventId(503, "INSERT_EVENT"), LOG_INSERT_EVENT, eventId, view.FlowId);

            EventStore.SetEvent
            (
                new Event
                (
                    view.FlowId,
                    eventId,
                    DateTime.UtcNow,
                    JsonSerializer.Serialize(args)
                )
            );

            Logger?.LogInformation(new EventId(504, "UPDATE_CACHE"), LOG_UPDATE_CACHE, view.FlowId);

            Cache.SetString
            (
                view.FlowId,
                JsonSerializer.Serialize(view, SerializerOptions),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = CacheEntryExpiration
                }
            );
        }

        object IUntypedViewRepository.Materialize(string flowId) => Materialize(flowId);

        /// <inheritdoc/>
        public IView Materialize(string flowId)
        {
            TView view;

            if (flowId is null)
                throw new ArgumentNullException(nameof(flowId));

            using IDisposable _ = Lock.Lock(flowId);

            //
            // Check if we can grab the view from the cache
            //

            string? cached = Cache.GetString(flowId);
            if (cached is not null)
            {
                Logger?.LogInformation(new EventId(500, "CACHE_ENTRY_FOUND"), LOG_CACHE_ENTRY_FOUND, flowId);

                try
                {
                    view = JsonSerializer.Deserialize<TView>(cached, SerializerOptions)!;
                    view.OwnerRepository = this;

                    return Intercept(view, Logger);
                }
                catch (JsonException) { }

                Logger?.LogWarning(new EventId(300, "LAYOUT_MISMATCH"), LOG_LAYOUT_MISMATCH);
            }

            //
            // Materialize the view by replaying the events
            //

            Logger?.LogInformation(new EventId(501, "REPLAY_EVENTS"), LOG_REPLAY_EVENTS, flowId);

            IList<Event> events = EventStore.QueryEvents(flowId);
            if (events.Count is 0)
                throw new ArgumentException(string.Format(INVALID_FLOW_ID, flowId), nameof(flowId));

            view = new()
            {
                FlowId = flowId,
                OwnerRepository = this
            };

            foreach (Event evt in events.OrderBy(static evt => evt.CreatedUtc))
            {
                if (!FEventProcessors.TryGetValue(evt.EventId, out Action<TView, string> processor))
                    throw new InvalidOperationException(string.Format(INVALID_EVENT_ID, evt.EventId));

                processor(view, evt.Arguments);
            }

            return Intercept(view, Logger);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static IView Intercept(TView view, ILogger? logger)
            {
                logger?.LogInformation(new EventId(502, "CREATE_INTERCEPTOR"), LOG_CREATE_INTERCEPTOR, view.FlowId);
                return FInterceptorFactory(view);
            }
        }
    }
}
