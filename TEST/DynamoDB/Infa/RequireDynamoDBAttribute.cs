/********************************************************************************
* RequireDynamoDBAttribute.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Ductus.FluentDocker.Builders;

namespace Solti.Utils.Eventing.DynamoDB.Tests
{
    using Abstractions.Tests;

    internal interface IHasDynamoDbConnection
    {
        IAmazonDynamoDB Connection { get; set; }
    }

    public sealed class RequireDynamoDBAttribute() : RequireExternalServiceAttribute("amazon/dynamodb-local:2.5.3", 8000, "test_dynamodb")
    {
        private AmazonDynamoDBClient FConnection = null!;

        //
        // Under .NET Core only the async API is available =(
        //

        private static T GetResultSync<T>(Task<T> t) => t.GetAwaiter().GetResult();
        
        protected override ContainerBuilder TweakBuild(ContainerBuilder builder) => builder
            .Command("-jar DynamoDBLocal.jar")
            .UseWorkDir("/home/dynamodblocal");

        protected override bool TryConnect(object fixture)
        {
            try
            {
                FConnection = new AmazonDynamoDBClient(new BasicAWSCredentials("LOCAL", "LOCAL"), new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
                GetResultSync(FConnection.ListTablesAsync());

                if (fixture is IHasDynamoDbConnection hasDynamoDb)
                    hasDynamoDb.Connection = FConnection;

                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Cannot connect to DynamoDB instance: {e.Message}");
                return false;
            }
        }

        protected override void CloseConnection()
        {
            FConnection?.Dispose();
            FConnection = null!;
        }

        protected override void TearDownTest()
        {
            foreach (string table in GetResultSync(FConnection.ListTablesAsync()).TableNames)
            {
                GetResultSync(FConnection.DeleteTableAsync(table));
            }
        }
    }
}
