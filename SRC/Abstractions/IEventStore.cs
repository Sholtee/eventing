/********************************************************************************
* IEventStore.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

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
    /// Describes the contract of event stores
    /// </summary>
    public interface IEventStore: IDisposable
    {
        /// <summary>
        /// Gets the events associated with the given <paramref name="flowId"/>
        /// </summary>
        IList<Event> QueryEvents(string flowId);

        /// <summary>
        /// Pushes a new event into the store.
        /// </summary>
        void SetEvent(Event @event);

        /// <summary>
        /// Initializes the schema in the underlying data store.
        /// </summary>
        void InitSchema();

        /// <summary>
        /// Determines if the underlying data schema had been initialized.
        /// </summary>
        bool SchemaInitialized { get; }
    }
}
