/********************************************************************************
* DynamoDbEventStoreTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.DynamoDB.Tests
{
    using Abstractions;
    using Abstractions.Tests;

    using static Properties.Resources;

    [TestFixture, RequireDynamoDB, NonParallelizable]
    public class DynamoDbEventStoreTests: IEventStoreTests, IHasDynamoDbConnection
    {
        public IAmazonDynamoDB DynamoDbConnection { get; set; } = null!;

        protected override IEventStore CreateInstance(string? appName) => new DynamoDbEventStore(DynamoDbConnection, appName);

        [Test]
        public async Task SchemaInitialized_ShouldThrowOnInvalidSchema([ValueSource(nameof(AppNames))] string? appName)
        {
            using DynamoDbEventStore store = new(DynamoDbConnection, appName);

            await DynamoDbConnection.CreateTableAsync
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

        [Test]
        public void Ctor_ShouldThrowOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new DynamoDbEventStore(config: null!, appName: null));
            Assert.Throws<ArgumentNullException>(() => new DynamoDbEventStore(db: null!, appName: null));
        }

        [Test]
        public void CheckStoreFeatures()
        {
            using DynamoDbEventStore store = new(DynamoDbConnection);

            Assert.That(store.Features, Is.EqualTo(EventStoreFeatures.OrderedQueries));
        }
    }
}
