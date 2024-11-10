/********************************************************************************
* IEventStore.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Describes an event on database level.
    /// </summary>
    public sealed class Event
    {
        /// <summary>
        /// Identifies the flow this event is assigned to
        /// </summary>
        public required string FlowId { get; init; }

        /// <summary>
        /// The event identifier.
        /// </summary>
        public required string EventId { get; init; }

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Serialized argument list associated with this event.
        /// </summary>
        public required string Arguments { get; init; }
    }

    /// <summary>
    /// Describes the <see cref="IEventStore"/> features.
    /// </summary>
    [Flags]
    public enum EventStoreFeatures
    {
        /// <summary>
        /// No features set
        /// </summary>
        None = 0,

        /// <summary>
        /// The <see cref="IEventStore.QueryEvents(string)"/> method returns an ordered result.
        /// </summary>
        OrderedQueries = 1 << 0,
    }

    /// <summary>
    /// Describes the contract of event stores
    /// </summary>
    public interface IEventStore: IDisposable
    {
        /// <summary>
        /// Gets the events associated with the given <paramref name="flowId"/>
        /// </summary>
        /// <remarks>This method returns an enumerable to support deferred queries.</remarks>
        IAsyncEnumerable<Event> QueryEvents(string flowId);

        /// <summary>
        /// Pushes a new event into the store.
        /// </summary>
        Task SetEvent(Event @event);

        /// <summary>
        /// Initializes the schema in the underlying data store.
        /// </summary>
        Task InitSchema();

        /// <summary>
        /// Determines if the underlying data schema had been initialized.
        /// </summary>
        Task<bool> SchemaInitialized { get; }

        /// <summary>
        /// Features of this instance.
        /// </summary>
        EventStoreFeatures Features { get; }
    }
}
