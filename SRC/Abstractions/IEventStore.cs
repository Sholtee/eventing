/********************************************************************************
* IEventStore.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Dapper;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Describes the contract of event stores
    /// </summary>
    public interface IEventStore
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
        /// The default implementation that uses Dapper to initiate SQL queries.
        /// </summary>
        public sealed class Sql(IDbConnection DbConnection): IEventStore
        {
            private static readonly string
                FColumns = string.Join(", ", typeof(Event).GetProperties().Select(p => p.Name)),
                FParamz = string.Join(", ", typeof(Event).GetProperties().Select(p => $"@{p.Name}"));

            public IList<Event> QueryEvents(string flowId) => DbConnection.Query<Event>("SELECT * FROM Event WHERE FlowId = @flowId ORDER BY CreatedUtc ASC", new { flowId }).ToList();

            public void SetEvent(Event @event) => DbConnection.Execute($"INSERT INTO Event({FColumns}) values ({FParamz})", @event);
        }
    }
}
