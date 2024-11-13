/********************************************************************************
* DynamoDbEventStoreTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using NUnit.Framework;

namespace Solti.Utils.Eventing.DynamoDB.Tests
{
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
    }
}
