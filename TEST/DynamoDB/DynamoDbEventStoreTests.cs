/********************************************************************************
* DynamoDbEventStoreTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NUnit.Framework;

namespace Solti.Utils.Eventing.DynamoDB.Tests
{
    using static Properties.Resources;

    [TestFixture, RequireDynamoDB]
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

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => store.SchemaInitialized);
            Assert.That(ex.Message, Is.EqualTo(ERR_SCHEMA_LAYOUT_MISMATCH));
        }
    }
}
