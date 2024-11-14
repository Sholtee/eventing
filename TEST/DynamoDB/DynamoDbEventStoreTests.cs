/********************************************************************************
* DynamoDbEventStoreTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.DynamoDB.Tests
{
    using Abstractions;

    using static Properties.Resources;

    [TestFixture, RequireDynamoDB, NonParallelizable]
    public class DynamoDbEventStoreTests: IHasDynamoDbConnection
    {
        public IAmazonDynamoDB Connection { get; set; } = null!;

        [Test]
        public async Task SchemaInitialized_ShouldReturnIfTheSchemaWasSetUp([Values(null, "testapp")] string? appName)
        {
            using DynamoDbEventStore store = new(Connection, appName);

            Assert.That(await store.SchemaInitialized, Is.False);

            Assert.DoesNotThrowAsync(store.InitSchema);

            Assert.That(await store.SchemaInitialized, Is.True);
        }

        [Test]
        public async Task SchemaInitialized_ShouldThrowOnInvalidSchema([Values(null, "testapp")] string? appName)
        {
            using DynamoDbEventStore store = new(Connection, appName);

            await Connection.CreateTableAsync
            (
                store.TableName,
                [new KeySchemaElement { AttributeName = "cica", KeyType = KeyType.HASH }],
                [new AttributeDefinition { AttributeName = "cica", AttributeType = ScalarAttributeType.S }],
                new ProvisionedThroughput(1, 1)
            );

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => store.SchemaInitialized)!;
            Assert.That(ex.Message, Is.EqualTo(ERR_SCHEMA_LAYOUT_MISMATCH));
        }

        [Test]
        public async Task QueryEvents_ShouldReturnOrderedResult([Values(1, 2, 3, 10)] int pageSize)
        {
            using DynamoDbEventStore store = new(Connection);

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
        public void Dispose_ShouldNotDisposeExternalClient()
        {
            Mock<IAmazonDynamoDB> mockDb = new(MockBehavior.Strict);

            new DynamoDbEventStore(mockDb.Object).Dispose();

            mockDb.Verify(d => d.Dispose(), Times.Never);
        }

        [Test]
        public void Dispose_ShouldDisposeInternalClient()
        {

            DynamoDbEventStore store = new(config: new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" }, appName: null);
            store.Dispose();

            Assert.That(store.DB, Is.Null);
        }
    }
}
