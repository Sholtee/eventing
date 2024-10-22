/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using static System.String;

namespace Solti.Utils.Eventing
{
    using Abstractions;

    using static Properties.Resources;

    /// <summary>
    /// View repository
    /// </summary>
    public class ViewRepository<TView>(IEventStore eventStore, IDistributedLock @lock, ISerializer serializer, IReflectionModule<TView> reflectionModule, IDistributedCache? cache, ILogger? logger) : IViewRepository<TView> where TView: ViewBase, new()
    {
        private readonly string FRepoId = Guid.NewGuid().ToString("D");

        /// <summary>
        /// Creates a new <see cref="ViewRepository{TView}"/> instance
        /// </summary>
        public ViewRepository(IEventStore eventStore, IDistributedLock @lock, IDistributedCache? cache = null, ILogger? logger = null) : this(eventStore, @lock, JsonSerializer.Instance, ReflectionModule<TView>.Instance, cache, logger) { }

        /// <summary>
        /// Gets or sets the cache expiraton.
        /// </summary>
        public TimeSpan CacheEntryExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the lock timeout.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <inheritdoc/>
        public void Persist(TView view, string eventId, object?[] args)
        {
            if (view is null)
                throw new ArgumentNullException(nameof(view));

            if (eventId is null)
                throw new ArgumentNullException(nameof(eventId));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            if (!@lock.IsHeld(view.FlowId, FRepoId))
                throw new InvalidOperationException(NO_LOCK);

            logger?.LogInformation(new EventId(503, "UPDATE_CACHE"), LOG_UPDATE_CACHE, view.FlowId);

            cache?.SetString
            (
                view.FlowId,
                serializer.Serialize(view),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = CacheEntryExpiration
                }
            );

            logger?.LogInformation(new EventId(504, "INSERT_EVENT"), LOG_INSERT_EVENT, eventId, view.FlowId);

            try
            {
                eventStore.SetEvent
                (
                    new Event
                    (
                        view.FlowId,
                        eventId,
                        DateTime.UtcNow,
                        serializer.Serialize(args)
                    )
                );
            }
            catch
            {
                //
                // Drop the cache to prevent inconsistent state
                //

                cache?.Remove(view.FlowId);
                throw;
            }
        }

        /// <inheritdoc/>
        public IDisposable Materialize(string flowId, out TView view)
        {
            if (flowId is null)
                throw new ArgumentNullException(nameof(flowId));

            //
            // Lock the flow
            //

            IDisposable lockInst = @lock.Acquire(flowId, FRepoId, LockTimeout);
            try
            {
                //
                // Check if we can grab the view from the cache
                //

                string? cached = cache?.GetString(flowId);
                if (cached is not null)
                {
                    logger?.LogInformation(new EventId(500, "CACHE_ENTRY_FOUND"), LOG_CACHE_ENTRY_FOUND, flowId);

                    view = serializer.Deserialize(cached, CreateRawView)!;
                    if (view.IsValid)
                        return lockInst;

                    logger?.LogWarning(new EventId(300, "LAYOUT_MISMATCH"), LOG_LAYOUT_MISMATCH);
                }

                //
                // Materialize the view by replaying the events
                //

                logger?.LogInformation(new EventId(501, "REPLAY_EVENTS"), LOG_REPLAY_EVENTS, flowId);

                IList<Event> events = eventStore.QueryEvents(flowId);
                if (events.Count is 0)
                    throw new ArgumentException(Format(INVALID_FLOW_ID, flowId), nameof(flowId));

                view = CreateRawView();

                foreach (Event evt in events.OrderBy(static evt => evt.CreatedUtc))
                {
                    if (!reflectionModule.EventProcessors.TryGetValue(evt.EventId, out Action<TView, string, ISerializer> processor))
                        throw new InvalidOperationException(Format(INVALID_EVENT_ID, evt.EventId));

                    processor(view, evt.Arguments, serializer);
                }

                view.DisableInterception = false;
                return lockInst;
            }
            catch
            {
                lockInst.Dispose();
                throw;
            }

            TView CreateRawView()
            {
                TView view = reflectionModule.CreateRawView(flowId, this);

                //
                // Disable interceptors while deserializing or replying the events
                //

                view.DisableInterception = true;

                return view;
            }
        }

        /// <inheritdoc/>
        public IDisposable Create(string? flowId, out TView view)
        {
            flowId ??= Guid.NewGuid().ToString("D");

            //
            // Lock the flow
            //

            IDisposable lockInst = @lock.Acquire(flowId, FRepoId, LockTimeout);
            try
            {
                if (eventStore.QueryEvents(flowId).Count > 0)
                    throw new ArgumentException(Format(FLOW_ID_ALREADY_EXISTS, flowId), nameof(flowId));

                view = reflectionModule.CreateRawView(flowId, this);
                return lockInst;
            }
            catch
            {
                lockInst.Dispose();
                throw;
            }
        }
    }
}
