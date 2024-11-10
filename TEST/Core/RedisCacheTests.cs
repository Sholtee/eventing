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

    [TestFixture, RequireRedis]
    public class RedisCacheTests: IDistributedCacheTests
    {
        protected override IDistributedCache CreateInstance() => new RedisCache("localhost", JsonSerializer.Instance);

        [Test]
        public void Dispose_ShouldNotDisposeExternalConnections()
        {
            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            Mock<ISerializer> mockSerializer = new(MockBehavior.Strict);

            new RedisCache(mockConnection.Object, mockSerializer.Object).Dispose();

            mockConnection.Verify(c => c.Dispose(), Times.Never);
        }

        [Test]
        public void Dispose_MightBeCalledMultipleTimes()
        {
            IDistributedCache cache = CreateInstance();
            cache.Dispose();
            Assert.DoesNotThrow(cache.Dispose);
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
    }
}
