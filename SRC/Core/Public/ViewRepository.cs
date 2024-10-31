/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using static System.String;

namespace Solti.Utils.Eventing
{
    using Abstractions;

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
                Serializer.Serialize(view),
                CacheEntryExpiration,
                DistributedCacheInsertionFlags.AllowOverwrite
            );

            Logger?.LogInformation(new EventId(503, "INSERT_EVENT"), LOG_INSERT_EVENT, eventId, view.FlowId);

            try
            {
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
            TView view;

            if (flowId is null)
                throw new ArgumentNullException(nameof(flowId));

            //
            // Lock the flow
            //

            Lock.Acquire(flowId, RepoId, LockTimeout);
            try
            {
                //
                // Check if we can grab the view from the cache
                //

                string? cached = Cache?.Get(flowId);
                if (cached is not null)
                {
                    Logger?.LogInformation(new EventId(504, "CACHE_ENTRY_FOUND"), LOG_CACHE_ENTRY_FOUND, flowId);

                    view = Serializer.Deserialize(cached, CreateRawView)!;
                    if (view.IsValid)
                        goto ret;

                    Logger?.LogWarning(new EventId(301, "LAYOUT_MISMATCH"), LOG_LAYOUT_MISMATCH);
                }

                //
                // Materialize the view by replaying the events
                //

                Logger?.LogInformation(new EventId(505, "REPLAY_EVENTS"), LOG_REPLAY_EVENTS, flowId);

                IList<Event> events = EventStore.QueryEvents(flowId);
                if (events.Count is 0)
                    throw new ArgumentException(Format(ERR_INVALID_FLOW_ID, flowId), nameof(flowId));

                view = CreateRawView();

                foreach (Event evt in events.OrderBy(static evt => evt.CreatedUtc))
                {
                    if (!ReflectionModule.EventProcessors.TryGetValue(evt.EventId, out Action<TView, string, ISerializer> processor))
                        throw new InvalidOperationException(Format(ERR_INVALID_EVENT_ID, evt.EventId));

                    processor(view, evt.Arguments, Serializer);
                }

                ret:
                    view.EventingDisabled = false;
                    return view;
            }
            catch
            {
                Lock.Release(flowId, RepoId);
                throw;
            }

            TView CreateRawView()
            {
                TView view = ReflectionModule.CreateRawView(flowId, this);

                //
                // Disable interceptors while deserializing or replaying the events
                //

                view.EventingDisabled = true;

                return view;
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
                if (EventStore.QueryEvents(flowId).Count > 0)
                    throw new ArgumentException(Format(ERR_FLOW_ID_ALREADY_EXISTS, flowId), nameof(flowId));

                Logger?.LogInformation(new EventId(505, "CREATE_RAW_VIEW"), LOG_CREATE_RAW_VIEW, flowId);

                return ReflectionModule.CreateRawView(flowId, this);
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
