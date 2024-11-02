/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;

    using static Properties.Resources;

    /// <summary>
    /// View repository
    /// </summary>
    public class ViewRepository<TView>: IViewRepository<TView> where TView: ViewBase, new()
    {
        internal const string SCHEMA_INIT_LOCK_NAME = "SCHEMA_INIT_LOCK";

        private static string CreateGuid() => Guid.NewGuid().ToString("D");

        /// <summary>
        /// Creates a new <see cref="ViewRepository{TView}"/> instance
        /// </summary>
        public ViewRepository(IEventStore eventStore, IDistributedLock @lock, ISerializer? serializer = null, IReflectionModule<TView>? reflectionModule = null, IDistributedCache? cache = null, ILogger? logger = null)
        {
            EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            Lock = @lock ?? throw new ArgumentNullException(nameof(@lock));

            Serializer = serializer ?? JsonSerializer.Instance;
            ReflectionModule = reflectionModule ?? ReflectionModule<TView>.Instance;

            Logger = logger;
            Cache = cache;
            if (Cache is null)
                Logger?.LogWarning(new EventId(300, "CACHING_DISABLED"), LOG_CACHING_DISABLED);
   
            RepoId = CreateGuid();

            if (!eventStore.SchemaInitialized)
            {
                Lock.Acquire(SCHEMA_INIT_LOCK_NAME, RepoId, LockTimeout);
                try
                {
                    if (!eventStore.SchemaInitialized)
                    {
                        Logger?.LogInformation(new EventId(500, "INIT_SCHEMA"), LOG_INIT_SCHEMA);
                        eventStore.InitSchema();
                        Logger?.LogInformation(new EventId(501, "SCHEMA_INITIALIZED"), LOG_SCHEMA_INIT_DONE);
                    }
                }
                finally
                {
                    Lock.Release(SCHEMA_INIT_LOCK_NAME, RepoId);
                }
            }
        }

        /// <summary>
        /// The underlying data store.
        /// </summary>
        public IEventStore EventStore { get; }

        /// <summary>
        /// Global lcok to be used
        /// </summary>
        public IDistributedLock Lock { get; }

        /// <summary>
        /// Global cache to be used.
        /// </summary>
        public IDistributedCache? Cache { get; }

        /// <summary>
        /// Logger to be used
        /// </summary>
        public ILogger? Logger { get; }

        /// <summary>
        /// Serializer to be used.
        /// </summary>
        public ISerializer Serializer { get; }

        /// <summary>
        /// Module responsible for reflection related task-
        /// </summary>
        public IReflectionModule<TView> ReflectionModule { get; }

        /// <summary>
        /// The unique id of this repository.
        /// </summary>
        public string RepoId { get; }

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

            if (!Lock.IsHeld(view.FlowId, RepoId))
                throw new InvalidOperationException(ERR_NO_LOCK);

            Logger?.LogInformation(new EventId(502, "UPDATE_CACHE"), LOG_UPDATE_CACHE, view.FlowId);

            Cache?.Set
            (
                view.FlowId,
                Serializer.Serialize(view.ToDict()),
                CacheEntryExpiration,
                DistributedCacheInsertionFlags.AllowOverwrite
            );

            Logger?.LogInformation(new EventId(503, "INSERT_EVENT"), LOG_INSERT_EVENT, eventId, view.FlowId);

            try
            {
                EventStore.SetEvent
                (
                    new Event
                    {
                        FlowId = view.FlowId,
                        EventId = eventId,
                        Arguments = Serializer.Serialize(args)
                    }
                );
            }
            catch
            {
                //
                // Drop the cache to prevent inconsistent state
                //

                Cache?.Remove(view.FlowId);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Close(string flowId) => Lock.Release(flowId ?? throw new ArgumentNullException(nameof(flowId)), RepoId);

        /// <inheritdoc/>
        public TView Materialize(string flowId)
        {
            //
            // Lock the flow
            //

            Lock.Acquire(flowId ?? throw new ArgumentNullException(nameof(flowId)), RepoId, LockTimeout);
            try
            {
                TView view = ReflectionModule.CreateRawView(flowId, this, out IEventfulViewConfig viewConfig);

                //
                // Disable interceptors while deserializing or replaying the events
                //

                viewConfig.EventingDisabled = true;

                //
                // Check if we can grab the view from the cache
                //

                string? cached = Cache?.Get(flowId);
                if (cached is not null)
                {
                    Logger?.LogInformation(new EventId(504, "CACHE_ENTRY_FOUND"), LOG_CACHE_ENTRY_FOUND, flowId);

                    if (Serializer.Deserialize<object>(cached) is not IDictionary<string, object?> cacheItem || !view.FromDict(cacheItem))
                    {
                        Logger?.LogError(new EventId(200, "LAYOUT_MISMATCH"), LOG_LAYOUT_MISMATCH, flowId);
                        throw new InvalidOperationException(ERR_LAYOUT_MISMATCH).WithArgs((nameof(flowId), flowId));
                    }
                }

                //
                // Materialize the view by replaying the events
                //

                else
                {
                    Logger?.LogInformation(new EventId(505, "REPLAY_EVENTS"), LOG_REPLAY_EVENTS, flowId);

                    IEnumerable<Event> events = EventStore.QueryEvents(flowId);

                    //
                    // Do not check the count here to enumerate the events only once
                    //

                    int eventCount = 0;

                    foreach (Event evt in EventStore.Features.HasFlag(EventStoreFeatures.OrderedQueries) ? events : events.OrderBy(static evt => evt.CreatedUtc))
                    {
                        if (!ReflectionModule.EventProcessors.TryGetValue(evt.EventId, out ProcessEventDelegate<TView> processor))
                        {
                            Logger?.LogError(new EventId(201, "PROCESSOR_NOT_FOUND"), LOG_INVALID_EVENT_ID, evt.EventId, flowId);
                            throw new InvalidOperationException(ERR_INVALID_EVENT_ID).WithArgs(("eventId", evt.EventId), (nameof(flowId), flowId));
                        }

                        try
                        {
                            processor(view, evt.Arguments, Serializer);
                        }
                        catch (Exception e)
                        {
                            Logger?.LogError(new EventId(202, "PROCESSOR_ERROR"), LOG_EVENT_PROCESSOR_ERROR, evt.EventId, flowId, e.Message);
                            throw;
                        }

                        eventCount++;
                    }

                    if (eventCount is 0)
                        throw new ArgumentException(ERR_INVALID_FLOW_ID).WithArgs((nameof(flowId), flowId));

                    Logger?.LogInformation(new EventId(506, "PROCESSED_EVENTS"), LOG_EVENTS_PROCESSED, eventCount, flowId);
                }

                viewConfig.EventingDisabled = false;
                return view;
            }
            catch
            {
                Lock.Release(flowId, RepoId);
                throw;
            }
        }

        /// <inheritdoc/>
        public TView Create(string? flowId)
        {
            flowId ??= CreateGuid();

            //
            // Lock the flow
            //

            Lock.Acquire(flowId, RepoId, LockTimeout);
            try
            {
                if (EventStore.QueryEvents(flowId).Any())
                    throw new ArgumentException(ERR_FLOW_ID_ALREADY_EXISTS, nameof(flowId)).WithArgs((nameof(flowId), flowId));

                Logger?.LogInformation(new EventId(505, "CREATE_RAW_VIEW"), LOG_CREATE_RAW_VIEW, flowId);

                return ReflectionModule.CreateRawView(flowId, this, out _);
            }
            catch
            {
                Lock.Release(flowId, RepoId);
                throw;
            }
        }

        void IViewRepository.Persist(ViewBase view, string eventId, object?[] args) => Persist((TView) view, eventId, args);

        ViewBase IViewRepository.Materialize(string flowId) => Materialize(flowId);

        ViewBase IViewRepository.Create(string? flowId) => Create(flowId);
    }
}
