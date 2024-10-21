/********************************************************************************
* ViewRepositoryBase.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using static System.String;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;

    using static Properties.Resources;

    /// <summary>
    /// View repository base
    /// </summary>
    public class ViewRepositoryBase<TView, IView, TReflectionModule>(IEventStore EventStore, IDistributedCache Cache, IDistributedLock Lock, ISerializer Serializer, ILogger? Logger) : IViewRepository<IView> where TView: ViewBase, IView, new() where IView: class where TReflectionModule: ReflectionModule, new()
    {
        private static readonly IReadOnlyDictionary<string, Action<TView, string, ISerializer>> FEventProcessors = new TReflectionModule().CreateEventProcessorsDict<TView>();

        private static readonly Func<TView, IView> FInterceptorFactory = new TReflectionModule().CreateInterceptorFactory<TView, IView>();

        private readonly string FRepoId = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the cache expiraton.
        /// </summary>
        public static TimeSpan CacheEntryExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <inheritdoc/>
        public void Persist(ViewBase view, string eventId, object?[] args)
        {
            if (view is null)
                throw new ArgumentNullException(nameof(view));

            if (eventId is null)
                throw new ArgumentNullException(nameof(eventId));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            if (!Lock.IsHeld(view.FlowId, FRepoId))
                throw new InvalidOperationException(NO_LOCK);

            Logger?.LogInformation(new EventId(503, "INSERT_EVENT"), LOG_INSERT_EVENT, eventId, view.FlowId);

            EventStore.SetEvent
            (
                new Event
                (
                    view.FlowId,
                    eventId,
                    DateTime.UtcNow,
                    Serializer.Serialize(args)
                )
            );

            Logger?.LogInformation(new EventId(504, "UPDATE_CACHE"), LOG_UPDATE_CACHE, view.FlowId);

            Cache.SetString
            (
                view.FlowId,
                Serializer.Serialize(view),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = CacheEntryExpiration
                }
            );
        }

        /// <inheritdoc/>
        public IDisposable Materialize(string flowId, out IView view)
        {
            TView concreteView;

            if (flowId is null)
                throw new ArgumentNullException(nameof(flowId));

            //
            // Lock the flow
            //

            IDisposable @lock = Lock.Acquire(flowId, FRepoId);
            try
            {
                //
                // Check if we can grab the view from the cache
                //

                string? cached = Cache.GetString(flowId);
                if (cached is not null)
                {
                    Logger?.LogInformation(new EventId(500, "CACHE_ENTRY_FOUND"), LOG_CACHE_ENTRY_FOUND, flowId);

                    concreteView = Serializer.Deserialize<TView>(cached)!;
                    if (concreteView.IsValid())
                    {
                        concreteView.OwnerRepository = this;
                        view = CreateInterceptor();
                        return @lock;
                    }

                    Logger?.LogWarning(new EventId(300, "LAYOUT_MISMATCH"), LOG_LAYOUT_MISMATCH);
                }

                //
                // Materialize the view by replaying the events
                //

                Logger?.LogInformation(new EventId(501, "REPLAY_EVENTS"), LOG_REPLAY_EVENTS, flowId);

                IList<Event> events = EventStore.QueryEvents(flowId);
                if (events.Count is 0)
                    throw new ArgumentException(Format(INVALID_FLOW_ID, flowId), nameof(flowId));

                concreteView = new()
                {
                    FlowId = flowId,
                    OwnerRepository = this
                };

                foreach (Event evt in events.OrderBy(static evt => evt.CreatedUtc))
                {
                    if (!FEventProcessors.TryGetValue(evt.EventId, out Action<TView, string, ISerializer> processor))
                        throw new InvalidOperationException(Format(INVALID_EVENT_ID, evt.EventId));

                    processor(concreteView, evt.Arguments, Serializer);
                }

                view = CreateInterceptor();
                return @lock;
            }
            catch
            {
                @lock.Dispose();
                throw;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            IView CreateInterceptor()
            {
                Logger?.LogInformation(new EventId(502, "CREATE_INTERCEPTOR"), LOG_CREATE_INTERCEPTOR, concreteView.FlowId);
                return FInterceptorFactory(concreteView);
            }
        }
    }
}
