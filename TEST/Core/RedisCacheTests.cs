/********************************************************************************
* RedisCacheTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

using static System.String;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Abstractions.Tests;

    using static Internals.EventIds;
    using static Properties.Resources;

    [TestFixture, RequireRedis, NonParallelizable]
    public class RedisCacheTests: IDistributedCacheTests, IHasRedisConnection
    {
        public IConnectionMultiplexer RedisConnection { get; set; } = null!;

        protected override IDistributedCache CreateInstance() => new RedisCache(RedisConnection, JsonSerializer.Instance);

        [Test]
        public void Dispose_ShouldNotDisposeExternalConnections()
        {
            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            Mock<ISerializer> mockSerializer = new(MockBehavior.Strict);

            RedisCache cache = new(mockConnection.Object, mockSerializer.Object);
            cache.Dispose();

            Assert.That(cache.Connection, Is.Not.Null);
            mockConnection.Verify(c => c.Dispose(), Times.Never);
        }

        [Test]
        public void Dispose_ShouldDisposeInternalConnections([Values(1, 2)] int disposeInvocations)
        {
            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.Dispose());

            Mock<ISerializer> mockSerializer = new(MockBehavior.Strict);

            RedisCache cache = new("localhost", mockSerializer.Object);
            cache.Connection.Dispose();
            cache.Connection = mockConnection.Object;

            for (int i = 0; i < disposeInvocations; i++)
            {
                cache.Dispose();
            }

            Assert.That(cache.Connection, Is.Null);
            mockConnection.Verify(c => c.Dispose(), Times.Once);
        }

        [Test]
        public async Task Get_ShouldUpdateTheExpiration([Values(true, false)] bool hasLogger)
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);
            Mock<ILogger<RedisCache>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            MockSequence seq = new();

            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.GET_CACHE_ITEM, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_GET_CACHE_ITEM, "key")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGetAsync("key", CommandFlags.None))
                .Returns(Task.FromResult<RedisValue>(JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = 1986 })));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.SET_CACHE_ITEM_EXPIRATION, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_SET_CACHE_ITEM_EXPIRATION, 1986, "key")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockDb
                .InSequence(seq)
                .Setup(db => db.KeyExpireAsync("key", TimeSpan.FromTicks(1986), ExpireWhen.Always, CommandFlags.None))
                .Returns(Task.FromResult(true));

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance, mockLogger?.Object);
            Assert.That(await cache.Get("key"), Is.EqualTo("value"));

            mockDb.Verify(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            mockDb.Verify(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public async Task Get_ShouldReturnNullIfTheKeyNotFound2()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGetAsync("key", CommandFlags.None))
                .Returns(Task.FromResult<RedisValue>((string?) null));

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.That(await cache.Get("key"), Is.Null);

            mockDb.Verify(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            mockDb.Verify(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public async Task Get_ShouldReturnNullIfTheExpirationCouldNotBeUpdated()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGetAsync("key", CommandFlags.None))
                .Returns(Task.FromResult<RedisValue>(JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = 1986 })));
            mockDb
                .InSequence(seq)
                .Setup(db => db.KeyExpireAsync("key", TimeSpan.FromTicks(1986), ExpireWhen.Always, CommandFlags.None))
                .Returns(Task.FromResult(false));

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.That(await cache.Get("key"), Is.Null);

            mockDb.Verify(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void Get_ShouldBeNullChecked()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.Get(null!));
        }

        [Test]
        public async Task Remove_ShouldDeleteTheKey([Values(true, false)] bool expectedResult, [Values(true, false)] bool hasLogger)
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);
            Mock<ILogger<RedisCache>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            MockSequence seq = new();

            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.REMOVE_CACHE_ITEM, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_REMOVE_CACHE_ITEM, "key")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockDb
                .InSequence(seq)
                .Setup(db => db.KeyDeleteAsync("key", CommandFlags.None))
                .Returns(Task.FromResult(expectedResult));

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance, mockLogger?.Object);

            Assert.That(await cache.Remove("key"), Is.EqualTo(expectedResult));
            mockDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void Remove_ShouldBeNullChecked()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.Remove(null!));
        }

        [Test]
        public async Task Set_ShouldSaveTheGivenValue([Values(DistributedCacheInsertionFlags.AllowOverwrite, DistributedCacheInsertionFlags.None)] DistributedCacheInsertionFlags flags, [Values(true, false)] bool expectedResult, [Values(true, false)] bool hasLogger)
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);
            Mock<ILogger<RedisCache>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            Dictionary<DistributedCacheInsertionFlags, When> flagMappings = new()
            {
                { DistributedCacheInsertionFlags.AllowOverwrite, When.Always },
                { DistributedCacheInsertionFlags.None, When.NotExists }
            };

            MockSequence seq = new();

            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.SET_CACHE_ITEM, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_SET_CACHE_ITEM, "key", TimeSpan.FromMilliseconds(1986), flags)), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockDb
                .InSequence(seq)
                .Setup(db => db.StringSetAsync("key", JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = TimeSpan.FromMilliseconds(1986).Ticks }), TimeSpan.FromMilliseconds(1986), flagMappings[flags] ))
                .Returns(Task.FromResult(expectedResult));

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance, mockLogger?.Object);

            Assert.That(await cache.Set("key", "value", TimeSpan.FromMilliseconds(1986), flags), Is.EqualTo(expectedResult));
        }

        [Test]
        public void Set_ShouldBeNullChecked()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.Set(null!, "value", TimeSpan.Zero, DistributedCacheInsertionFlags.None));
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.Set("key", null!, TimeSpan.Zero, DistributedCacheInsertionFlags.None));
        }

        [Test]
        public void Ctor_ShouldBeNullChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new RedisCache(config: null!, new Mock<ISerializer>(MockBehavior.Strict).Object));
            Assert.Throws<ArgumentNullException>(() => new RedisCache("localhost", serializer: null!));
            Assert.Throws<ArgumentNullException>(() => new RedisCache(connection: null!, new Mock<ISerializer>(MockBehavior.Strict).Object));
            Assert.Throws<ArgumentNullException>(() => new RedisCache(new Mock<IConnectionMultiplexer>(MockBehavior.Strict).Object, serializer: null!));
        }
    }
}
