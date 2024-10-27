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
    public sealed record Event(string FlowId, string EventId, DateTime CreatedUtc, string Arguments)
    {
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
