/********************************************************************************
* RedisCacheTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using System.Collections.Generic;

    [TestFixture]
    public class RedisCacheTests: IDistributedCacheTests
    {
        private ModuleTestsBase FContainerHost;

        [OneTimeSetUp]
        public void SetupFixture()
        {
            FContainerHost = new ModuleTestsBase();
            FContainerHost.SetupFixture();
        }

        [OneTimeTearDown]
        public void TearDownFixture()
        {
            FContainerHost.TearDownFixture();
            FContainerHost = null!;
        }

        public override void SetupTest()
        {
            FContainerHost.SetupTest();
            base.SetupTest();
        }

        public override void TearDownTest()
        {
            base.TearDownTest();
            FContainerHost.TearDownTest();
        }

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
        public void Get_ShouldUpdateTheExpiration()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGet("key", CommandFlags.None))
                .Returns(JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = 1986 }));
            mockDb
                .InSequence(seq)
                .Setup(db => db.KeyExpire("key", TimeSpan.FromTicks(1986), ExpireWhen.Always, CommandFlags.None))
                .Returns(true);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.That(cache.Get("key"), Is.EqualTo("value"));

            mockDb.Verify(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            mockDb.Verify(db => db.KeyExpire(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void Get_ShouldReturnNullIfTheKeyNotFound2()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGet("key", CommandFlags.None))
                .Returns((string?) null);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.That(cache.Get("key"), Is.Null);

            mockDb.Verify(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            mockDb.Verify(db => db.KeyExpire(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public void Get_ShouldReturnNullIfTheExpirationCouldNotBeUpdated()
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockDb
                .InSequence(seq)
                .Setup(db => db.StringGet("key", CommandFlags.None))
                .Returns(JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = 1986 }));
            mockDb
                .InSequence(seq)
                .Setup(db => db.KeyExpire("key", TimeSpan.FromTicks(1986), ExpireWhen.Always, CommandFlags.None))
                .Returns(false);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);
            Assert.That(cache.Get("key"), Is.Null);

            mockDb.Verify(db => db.KeyExpire(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void Remove_ShouldDeleteTheKey([Values(true, false)] bool expectedResult)
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            mockDb
                .Setup(db => db.KeyDelete("key", CommandFlags.None))
                .Returns(expectedResult);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);

            Assert.That(cache.Remove("key"), Is.EqualTo(expectedResult));
            mockDb.Verify(db => db.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void Set_ShouldSaveTheGivenValue([Values(DistributedCacheInsertionFlags.AllowOverwrite, DistributedCacheInsertionFlags.None)] DistributedCacheInsertionFlags flags, [Values(true, false)] bool expectedResult)
        {
            Mock<IDatabase> mockDb = new(MockBehavior.Strict);

            Dictionary<DistributedCacheInsertionFlags, When> flagMappings = new()
            {
                { DistributedCacheInsertionFlags.AllowOverwrite, When.Always },
                { DistributedCacheInsertionFlags.None, When.NotExists }
            }; 

            mockDb
                .Setup(db => db.StringSet("key", JsonSerializer.Instance.Serialize(new { Value = "value", Expiration = TimeSpan.FromMilliseconds(1986).Ticks }), TimeSpan.FromMilliseconds(1986), flagMappings[flags] ))
                .Returns(expectedResult);

            Mock<IConnectionMultiplexer> mockConnection = new(MockBehavior.Strict);
            mockConnection
                .Setup(c => c.GetDatabase(-1, null))
                .Returns(mockDb.Object);

            using RedisCache cache = new(mockConnection.Object, JsonSerializer.Instance);

            Assert.That(cache.Set("key", "value", TimeSpan.FromMilliseconds(1986), flags), Is.EqualTo(expectedResult));
        }
    }
}
