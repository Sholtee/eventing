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
    using static Internals.EventIds;

    /// <summary>
    /// View repository
    /// </summary>
    public class ViewRepository<TView>: IViewRepository<TView> where TView: ViewBase
    {
        internal const string SCHEMA_INIT_LOCK_NAME = "SCHEMA_INIT_LOCK";

        private static string CreateGuid() => Guid.NewGuid().ToString("D");

        /// <summary>
        /// Creates a new <see cref="ViewRepository{TView}"/> instance
        /// </summary>
        public ViewRepository(IEventStore eventStore, IDistributedLock @lock, ISerializer? serializer = null, IReflectionModule<TView>? reflectionModule = null, IDistributedCache? cache = null, ILogger<ViewRepository<TView>>? logger = null)
        {
            EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            Lock = @lock ?? throw new ArgumentNullException(nameof(@lock));

            Serializer = serializer ?? JsonSerializer.Instance;
            ReflectionModule = reflectionModule ?? ReflectionModule<TView>.Instance;

            Logger = logger;
            Cache = cache;
            if (Cache is null)
                Logger?.LogWarning(Warning.CACHING_DISABLED, LOG_CACHING_DISABLED);
   
            RepositoryId = CreateGuid();

            if (!eventStore.SchemaInitialized)
            {
                Lock.Acquire(SCHEMA_INIT_LOCK_NAME, RepositoryId, LockTimeout);
                try
                {
                    if (!eventStore.SchemaInitialized)
                    {
                        Logger?.LogInformation(Info.INIT_SCHEMA, LOG_INIT_SCHEMA);
                        eventStore.InitSchema();
                        Logger?.LogInformation(Info.SCHEMA_INITIALIZED, LOG_SCHEMA_INITIALIZED);
                    }
                }
                finally
                {
                    Lock.Release(SCHEMA_INIT_LOCK_NAME, RepositoryId);
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
        public string RepositoryId { get; }

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

            if (!Lock.IsHeld(view.FlowId, RepositoryId))
                throw new InvalidOperationException(ERR_NO_LOCK);

            Logger?.LogInformation(Info.UPDATE_CACHE, LOG_UPDATE_CACHE, view.FlowId);

            Cache?.Set
            (
                view.FlowId,
                Serializer.Serialize(view.ToDict()),
                CacheEntryExpiration,
                DistributedCacheInsertionFlags.AllowOverwrite
            );

            Logger?.LogInformation(Info.INSERT_EVENT, LOG_INSERT_EVENT, eventId, view.FlowId);

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
            catch(Exception e)
            {
                Logger?.LogError(Error.EVENT_NOT_SAVED, LOG_EVENT_NOT_SAVED, eventId, view.FlowId, e.Message);

                //
                // Drop the cache to prevent inconsistent state
                //

                Cache?.Remove(view.FlowId);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Close(string flowId) => Lock.Release(flowId ?? throw new ArgumentNullException(nameof(flowId)), RepositoryId);

        /// <inheritdoc/>
        public TView Materialize(string flowId)
        {
            //
            // Lock the flow
            //

            Lock.Acquire(flowId ?? throw new ArgumentNullException(nameof(flowId)), RepositoryId, LockTimeout);
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
                    Logger?.LogInformation(Info.CACHE_ENTRY_FOUND, LOG_CACHE_ENTRY_FOUND, flowId);

                    if (Serializer.Deserialize<object>(cached) is not IDictionary<string, object?> cacheItem || !view.FromDict(cacheItem))
                        throw new InvalidOperationException(ERR_LAYOUT_MISMATCH).WithData((nameof(flowId), flowId));
                }

                //
                // Materialize the view by replaying the events
                //

                else
                {
                    Logger?.LogInformation(Info.REPLAY_EVENTS, LOG_REPLAY_EVENTS, flowId);

                    IEnumerable<Event> events = EventStore.QueryEvents(flowId);

                    //
                    // Do not check the count here to enumerate the events only once
                    //

                    int eventCount = 0;

                    foreach (Event evt in EventStore.Features.HasFlag(EventStoreFeatures.OrderedQueries) ? events : events.OrderBy(static evt => evt.CreatedUtc))
                    {
                        if (!ReflectionModule.EventProcessors.TryGetValue(evt.EventId, out ProcessEventDelegate<TView> processor))
                            throw new InvalidOperationException(ERR_INVALID_EVENT_ID).WithData(("eventId", evt.EventId), (nameof(flowId), flowId));

                        processor(view, evt.Arguments, Serializer);
                        eventCount++;
                    }

                    if (eventCount is 0)
                        throw new ArgumentException(ERR_INVALID_FLOW_ID).WithData((nameof(flowId), flowId));

                    Logger?.LogInformation(Info.PROCESSED_EVENTS, LOG_EVENTS_PROCESSED, eventCount, flowId);
                }

                viewConfig.EventingDisabled = false;
                return view;
            }
            catch(Exception e)
            {
                Logger?.LogError(Error.CANNOT_MATERIALIZE, LOG_CANNOT_MATERIALIZE, flowId, e.Message);
 
                Lock.Release(flowId, RepositoryId);
  
                throw;
            }
        }

        /// <inheritdoc/>
        public TView Create(string? flowId, object? tag)
        {
            flowId ??= CreateGuid();

            //
            // Lock the flow
            //

            Lock.Acquire(flowId, RepositoryId, LockTimeout);
            try
            {
                if (EventStore.QueryEvents(flowId).Any())
                    throw new ArgumentException(ERR_FLOW_ID_ALREADY_EXISTS, nameof(flowId)).WithData((nameof(flowId), flowId));

                Logger?.LogInformation(Info.CREATE_RAW_VIEW, LOG_CREATE_RAW_VIEW, flowId);

                TView view = ReflectionModule.CreateRawView(flowId, this, out _);
                view.Initialize(typeof(TView).FullName, tag);

                return view;
            }
            catch(Exception e)
            {
                Logger?.LogError(Error.CANNOT_CREATE_RAW_VIEW, LOG_CANNOT_CREATE_RAW_VIEW, flowId, e.Message);

                Lock.Release(flowId, RepositoryId);

                throw;
            }
        }

        void IViewRepository.Persist(ViewBase view, string eventId, object?[] args) => Persist((TView) view, eventId, args);

        ViewBase IViewRepository.Materialize(string flowId) => Materialize(flowId);

        ViewBase IViewRepository.Create(string? flowId, object? tag) => Create(flowId, tag);
    }
}
