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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Proxy.Generators;

    /// <summary>
    /// Repository to store view instances
    /// </summary>
    public class ViewRepository<TView, IView>(IDistributedCache Cache, ILogger? Logger, IEventStore EventStore, ILock Lock, ISerializer Serializer): IViewRepository<IView> where TView: ViewBase, IView, new() where IView: class
    {
        private static readonly IReadOnlyDictionary<string, Action<TView, object?[]>> FEventProcessors = null!;

        private static Func<object?[], IView>? FInterceptorFactory;

        private static readonly object FLock = new();

        /// <summary>
        /// Gets or sets the cache expiraton.
        /// </summary>
        public static TimeSpan CacheEntryExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets the singleton interceptor factory.
        /// </summary>
        protected Func<object?[], IView> InterceptorFactory
        {
            get
            {
                if (FInterceptorFactory is null)
                    lock (FLock)
                        FInterceptorFactory ??= CreateInterceptorFactory();
                return FInterceptorFactory;
            }
        }

        /// <summary>
        /// Applies the <see cref="InterceptorType"/> against the given <paramref name="view"/>
        /// </summary>
        protected virtual IView Intercept(TView view)
        {
            Logger?.LogInformation(new EventId(502, "CREATE_INTERCEPTOR"), "Creating interceptor for view: {0}", view.FlowId);

            return InterceptorFactory([view]);
        }

        /// <summary>
        /// Creates the interceptor factory to be used for dispatching events.
        /// </summary>
        protected virtual Func<object?[], IView> CreateInterceptorFactory()
        {
            ConstructorInfo ctor = ProxyGenerator<IView, ViewInterceptor<TView, IView>>.GetGeneratedType().GetConstructors().Single();

            ParameterExpression args = Expression.Parameter(typeof(object?[]), nameof(args));

            return Expression.Lambda<Func<object?[], IView>>
            (
                Expression.New
                (
                    ctor,
                    ctor.GetParameters().Select
                    (
                        (p, i) => Expression.Convert
                        (
                            Expression.ArrayAccess(args, Expression.Constant(i)),
                            p.ParameterType
                        )
                    )
                ),
                args
            ).Compile();
        }

        /// <inheritdoc/>
        public virtual void Persist(ViewBase view, string eventId, object?[] args)
        {
            if (view is null)
                throw new ArgumentNullException(nameof(view));

            if (eventId is null)
                throw new ArgumentNullException(nameof(eventId));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            EventStore.SetEvent(new Event(view.FlowId, eventId, DateTime.UtcNow, Serializer.Serialize(args)));

            Cache.SetString
            (
                view.FlowId,
                Serializer.Serialize(view.AsDict()),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = CacheEntryExpiration
                }
            );
        }

        object IUntypedViewRepository.Materialize(string flowId) => Materialize(flowId);

        /// <inheritdoc/>
        public virtual IView Materialize(string flowId)
        {
            if (flowId is null)
                throw new ArgumentNullException(nameof(flowId));

            using IDisposable _ = Lock.Lock(flowId);

            TView view = new()
            {
                FlowId = flowId,
                OwnerRepository = this
            };

            string? cached = Cache.GetString(flowId);
            if (cached is not null)
            {
                Logger?.LogInformation(new EventId(500, "CACHE_ENTRY_FOUND"), "Found cache entry for view: {0}", flowId);

                if (!view.FromDict(Serializer.Deserialize<Dictionary<string, object?>>(cached)!))
                    return Intercept(view);
                    
                Logger?.LogWarning(new EventId(300, "LAYOUT_MISMATCH"), "View layout changed since the last update. Skip retrieving view from cache");
            }

            Logger?.LogInformation(new EventId(501, "REPLAY_EVENTS"), "Replaying events for view: {0}", flowId);

            IList<Event> events = EventStore.QueryEvents(flowId);
            if (events.Count is 0)
                throw new ArgumentException("Invalid flow id", nameof(flowId));  // TODO: move to a resource

            foreach (Event evt in events.OrderBy(static evt => evt.CreatedUtc))
            {
                if (!FEventProcessors.TryGetValue(evt.EventId, out Action<TView, object?[]> processor))
                    throw new InvalidOperationException($"Invalid event id provided: {evt.EventId}");  // TODO: move to a resource

                processor(view, Serializer.Deserialize<object?[]>(evt.Arguments)!);
            }

            return Intercept(view);
        }
    }
}
