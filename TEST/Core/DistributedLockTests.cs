/********************************************************************************
* DistributedLockTests.cs                                                       *
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
    public class DistributedLockTests: IDistributedLockTests, IHasRedisConnection
    {
        public RedisCache RedisCache { get; set; } = null!;

        public IConnectionMultiplexer RedisConnection { get; set; } = null!;

        [SetUp]
        public void SetupTest() => RedisCache = new RedisCache(RedisConnection, JsonSerializer.Instance);

        [TearDown]
        public void TearDownTest()
        {
            RedisCache.Dispose();
            RedisCache = null!;
        }

        protected override IDistributedLock CreateInstance() => new DistributedLock(RedisCache, JsonSerializer.Instance);

        [Test]
        public async Task Test_Flow([Values(10000)] int lockTimeout, [Values(true, false)] bool hasLogger)
        {
            string entry = JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } });

            TimeSpan timeout = TimeSpan.FromMilliseconds(lockTimeout);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<ILogger<DistributedLock>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null; 

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockLogger?.Object)
            {
                LockTimeout = timeout
            };

            MockSequence seq = new();

            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.ACQUIRE_LOCK, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_ACQUIRE_LOCK, "key", "owner")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Set("lock_key", entry, timeout, DistributedCacheInsertionFlags.None))
                .Returns(Task.FromResult(true));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("lock_key"))
                .Returns(Task.FromResult((string?) entry));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.RELEASE_LOCK, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_RELEASE_LOCK, "key", "owner")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Remove("lock_key"))
                .Returns(Task.FromResult(true));

            await @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Never);
            mockCache.Verify(c => c.Get(It.IsAny<string>()), Times.Never);

            await @lock.Release("key", "owner");

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once); // Acquire
            mockCache.Verify(c => c.Get(It.IsAny<string>()), Times.Once);  // IsHeld()
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Once);  // Release()  
        }

        [Test]
        public void Acquire_ShouldBlock2([Values(20)] int pollingInterval)
        {
            Mock<Func<TimeSpan, Task>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            TimeSpan delay = TimeSpan.FromMilliseconds(pollingInterval);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, null, mockSleep.Object)
            {
                PollingInterval = delay
            };

            int sleepCalled = 0;

            mockSleep
                .Setup(s => s.Invoke(delay))
                .Returns<TimeSpan>(_ => { sleepCalled++; return Task.CompletedTask; });
            mockCache
                .Setup(c => c.Set("lock_key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns<string, string, TimeSpan, DistributedCacheInsertionFlags>((_, _, _, _) => Task.FromResult(sleepCalled > 1));

            Assert.DoesNotThrowAsync(() => @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Exactly(3));
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Exactly(2));
        }

        [Test]
        public void Acquire_ShouldTimeout2([Values(true, false)] bool hasLogger)
        {
            Mock<Func<TimeSpan, Task>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            Mock<ILogger<DistributedLock>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Information, Info.ACQUIRE_LOCK, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_ACQUIRE_LOCK, "key", "owner")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.ACQUIRE_LOCK_TIMEOUT, It.Is<It.IsAnyType>((object v, Type _) => v.ToString()!.StartsWith(Format(LOG_ACQUIRE_LOCK_TIMEOUT.Replace(": {2}ms", ""), "key", "owner"))), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockLogger?.Object, mockSleep.Object);

            mockSleep
                .Setup(s => s.Invoke(@lock.PollingInterval))
                .Returns<TimeSpan>(Task.Delay);
            mockCache
                .Setup(c => c.Set("lock_key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(Task.FromResult(false));

            Assert.ThrowsAsync<TimeoutException>(() => @lock.Acquire("key", "owner", TimeSpan.FromMilliseconds(10 * @lock.PollingInterval.Milliseconds)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Between(8, 10, Moq.Range.Inclusive));
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Between(8, 10, Moq.Range.Inclusive));
        }

        public static IEnumerable<object?[]> IsHeld_ShouldDetermineIfTheHoldIsOwnedByTheCurrentApp_Params
        {
            get
            {
                yield return new object?[] { null, false };
                yield return new object?[] { JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "different" } }), false };
                yield return new object?[] { JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } }), true };
            }
        }

        [TestCaseSource(nameof(IsHeld_ShouldDetermineIfTheHoldIsOwnedByTheCurrentApp_Params))]
        public async Task IsHeld_ShouldDetermineIfTheLockIsOwnedByTheCaller(string cacheRetVal, bool expected)
        {
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get(It.IsAny<string>()))
                .Returns(Task.FromResult<string?>(cacheRetVal));

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance);

            Assert.That(await @lock.IsHeld("key", "owner"), Is.EqualTo(expected));
        }

        [Test]
        public void Release_ShouldRemoveTheUnderlyingEntry([Values(true, false)] bool hasLogger)
        {
            string entry = JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } });

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<ILogger<DistributedLock>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockLogger?.Object);

            MockSequence seq = new();

            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("lock_key"))  // IsHeld()
                .Returns(Task.FromResult<string?>(entry));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.RELEASE_LOCK, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_RELEASE_LOCK, "key", "owner")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Remove("lock_key"))
                .Returns(Task.FromResult(true));

            Assert.DoesNotThrowAsync(() => @lock.Release("key", "owner"));

            mockCache.Verify(c => c.Remove("lock_key"), Times.Once);
        }

        [Test]
        public void Release_ShouldThrowIfTheLockIsNotHeld([Values(true, false)] bool hasLogger)
        {
            string entry = JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } });

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<ILogger<DistributedLock>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockLogger?.Object);

            MockSequence seq = new();

            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("lock_key"))  // IsHeld()
                .Returns(Task.FromResult<string?>(entry));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Warning, Warning.FOREIGN_LOCK_RELEASE, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_FOREIGN_LOCK_RELEASE, "key", "unknown")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(() => @lock.Release("key", "unknown"))!;
            Assert.That(ex.Message, Is.EqualTo(ERR_FOREIGN_LOCK_RELEASE));

            mockCache.Verify(c => c.Get("lock_key"), Times.Once);
        }
    }
}
