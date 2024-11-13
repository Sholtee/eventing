/********************************************************************************
* DynamoDbEventStore.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Solti.Utils.Eventing
{
    using Abstractions;

    using static Properties.Resources;

    /// <summary>
    /// Implements the <see cref="IEventStore"/> interface over AWS's DynamoDb
    /// </summary>
    public class DynamoDbEventStore: IEventStore
    {
        private const string
            FLOW_ID     = "flowId",
            CREATED_UTC = "createdUTC",
            EVENT_ID    = "eventId",
            ARGS        = "args";

        private static readonly IReadOnlyList<KeySchemaElement> FSchema =
        [
            new KeySchemaElement { AttributeName = FLOW_ID, KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = CREATED_UTC, KeyType = KeyType.RANGE }
        ];

        private static readonly IReadOnlyList<AttributeDefinition> FAttributes =
        [
            new AttributeDefinition { AttributeName = FLOW_ID, AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = CREATED_UTC, AttributeType = ScalarAttributeType.N }
        ];

        private readonly bool FRequireDisose;

        private readonly string FTableName;

        private IAmazonDynamoDB FDb;

        /// <summary>
        /// Override this method if you want to customize the data being inserted.
        /// </summary>
        protected virtual Dictionary<string, AttributeValue> MapEvent(Event @event) => new()
        {
            { FLOW_ID,     new AttributeValue(@event.FlowId) },
            { CREATED_UTC, new AttributeValue(@event.CreatedUtc.Ticks.ToString()) },
            { EVENT_ID,    new AttributeValue(@event.EventId) },
            { ARGS,        new AttributeValue(@event.Arguments) }
        };

        /// <summary>
        /// Override this method if you want to retrieve customized data
        /// </summary>
        protected virtual Event MapEvent(Dictionary<string, AttributeValue> @event) => new()
        {
            FlowId     = @event[FLOW_ID].S,
            EventId    = @event[EVENT_ID].S,
            Arguments  = @event[ARGS].S,
            CreatedUtc = new DateTime
            (
                ticks: long.Parse(@event[CREATED_UTC].S)
            )
        };

        /// <summary>
        /// Creates a new <see cref="DynamoDbEventStore"/> instance using a pre-configured client.
        /// </summary>
        public DynamoDbEventStore(IAmazonDynamoDB db, string? appName)
        {
            FDb = db ?? throw new ArgumentNullException(nameof(db));

            FTableName = "event-data";
            if (!string.IsNullOrEmpty(appName))
                FTableName = $"{appName}-{FTableName}";
        }

        /// <summary>
        /// Creates a new <see cref="DynamoDbEventStore"/> instance.
        /// </summary>
        public DynamoDbEventStore(AmazonDynamoDBConfig config, string? appName): this(new AmazonDynamoDBClient(config ?? throw new ArgumentNullException(nameof(config))), appName) =>
            FRequireDisose = true;

        /// <inheritdoc/>
        public Task<bool> SchemaInitialized
        {
            get
            {
                return SchemaInitialized();

                async Task<bool> SchemaInitialized() // getters cannot be async... :(
                {
                    DescribeTableResponse response;

                    try
                    {

                        response = await FDb.DescribeTableAsync(new DescribeTableRequest(FTableName));
                    }
                    catch (ResourceNotFoundException)
                    {
                        return false;
                    }

                    if (!response.Table.KeySchema.All(FSchema.Contains))
                        throw new InvalidOperationException(ERR_SCHEMA_LAYOUT_MISMATCH);

                    return true;                  
                };
            }
        }

        /// <inheritdoc/>
        public Task InitSchema() => FDb.CreateTableAsync
        (
            FTableName,
            FSchema.ToList(),
            FAttributes.ToList(),
            Throughput
        );

        /// <inheritdoc/>
        public async IAsyncEnumerable<Event> QueryEvents(string flowId)
        {
            Dictionary<string, Condition> keyCondition = new()
            {
                {
                    FLOW_ID,
                    new Condition
                    {
                        ComparisonOperator = ComparisonOperator.EQ,
                        AttributeValueList = [new AttributeValue(flowId)]
                    }
                }
            };

            Dictionary<string, AttributeValue>? startKey = null;

            do
            {
                QueryResponse response = await FDb.QueryAsync(new QueryRequest
                {
                    TableName         = FTableName,
                    ExclusiveStartKey = startKey,
                    KeyConditions     = keyCondition,
                    Limit             = PageSize,
                    ScanIndexForward  = true
                });

                foreach (Dictionary<string, AttributeValue> item in response.Items)
                {
                    yield return MapEvent(item);
                }

                startKey = response.LastEvaluatedKey;
            } while (startKey?.Count > 0);
        }

        /// <inheritdoc/>
        public Task SetEvent(Event @event) => FDb.PutItemAsync
        (
            FTableName,
            MapEvent(@event ?? throw new ArgumentNullException(nameof(@event)))
        );

        /// <inheritdoc/>
        public EventStoreFeatures Features { get; } = EventStoreFeatures.OrderedQueries;

        /// <inheritdoc/>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Throughput to be assigned when initializing the schema
        /// </summary>
        public static ProvisionedThroughput Throughput { get; } = new() { ReadCapacityUnits = 1, WriteCapacityUnits = 1};

        /// <inheritdoc/>
        public void Dispose()
        {
            if (FRequireDisose && FDb is not null)
            {
                FDb.Dispose();
                FDb = null!;
            }
        }
    }
}
