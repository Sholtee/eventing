/********************************************************************************
* IEventStoreTests.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    public abstract class IEventStoreTests
    {
        protected abstract IEventStore CreateInstance(string? appName);

        public static IEnumerable<string?> AppNames
        {
            get
            {
                yield return null;
                yield return "testapp";
            }
        }

        [Test]
        public async Task SchemaInitialized_ShouldReturnIfTheSchemaWasSetUp([ValueSource(nameof(AppNames))] string? appName)
        {
            using IEventStore store = CreateInstance(appName);

            Assert.That(await store.SchemaInitialized, Is.False);

            Assert.DoesNotThrowAsync(store.InitSchema);

            Assert.That(await store.SchemaInitialized, Is.True);
        }

        [Test]
        public async Task Set_ShouldInsertANewEvent([ValueSource(nameof(AppNames))] string? appName)
        {
            using IEventStore store = CreateInstance(appName);

            await store.InitSchema();

            Event evt = new() { FlowId = "flowId", EventId = "event", Arguments = "args" };

            await store.SetEvent(evt);

            Assert.That(await store.QueryEvents("flowId").SingleAsync(), Is.EqualTo(evt));
        }

        [Test]
        public async Task QueryEvents_ShouldReturnOrderedResult([ValueSource(nameof(AppNames))] string? appName, [Values(1, 2, 3, 10)] int pageSize)
        {
            using IEventStore store = CreateInstance(appName);

            if (!store.Features.HasFlag(EventStoreFeatures.OrderedQueries))
                Assert.Ignore("Ordered queries are not supported");

            await store.InitSchema();

            Event[] events =
            [
                new Event { FlowId = "flowId", EventId = "event_3", Arguments = "args_3", CreatedUtc = DateTime.UtcNow.AddSeconds(3) },
                new Event { FlowId = "flowId", EventId = "event_2", Arguments = "args_2", CreatedUtc = DateTime.UtcNow.AddSeconds(2) },
                new Event { FlowId = "flowId", EventId = "event_1", Arguments = "args_1", CreatedUtc = DateTime.UtcNow.AddSeconds(1) }
            ];

            foreach (Event e in events)
            {
                await store.SetEvent(e);
            }

            store.PageSize = pageSize;

            Assert.That(events.Reverse().SequenceEqual(await store.QueryEvents("flowId").ToListAsync()));
        }

        [Test]
        public void SetEvent_ShouldThrowOnNull()
        {
            using IEventStore store = CreateInstance(null);

            Assert.ThrowsAsync<ArgumentNullException>(() => store.SetEvent(null!));
        }

        [Test]
        public void QueryEvents_ShouldThrowOnNull()
        {
            using IEventStore store = CreateInstance(null);

            Assert.ThrowsAsync<ArgumentNullException>(async () => await store.QueryEvents(null!).SingleAsync());
        }
    }
}
