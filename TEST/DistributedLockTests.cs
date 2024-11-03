/********************************************************************************
* DistributedLockTests.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    [TestFixture]
    public class DistributedLockTests: IDistributedLockTests
    {
        private ModuleTestsBase FContainerHost = null!;

        private RedisCache FRedisCache = null!;

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

        [SetUp]
        public void SetupTest()
        {
            FContainerHost.SetupTest();
            FRedisCache = new RedisCache("localhost", JsonSerializer.Instance);
        }

        [TearDown]
        public void TearDownTest()
        {
            FRedisCache.Dispose();
            FRedisCache = null!;

            FContainerHost.TearDownTest();
        }

        protected override IDistributedLock CreateInstance() => new DistributedLock(FRedisCache, JsonSerializer.Instance);

        [Test]
        public void Test_Flow()
        {
            string entry = JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } });

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance);

            MockSequence seq = new();

            mockCache
                .InSequence(seq)
                .Setup(c => c.Set("lock_key", entry, @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(true);
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("lock_key"))
                .Returns(entry);
            mockCache
                .InSequence(seq)
                .Setup(c => c.Remove("lock_key"))
                .Returns(true);

            @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Never);
            mockCache.Verify(c => c.Get(It.IsAny<string>()), Times.Never);

            @lock.Release("key", "owner");

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once); // Acquire
            mockCache.Verify(c => c.Get(It.IsAny<string>()), Times.Once);  // IsHeld()
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Once);  // Release()  
        }

        [Test]
        public void Acquire_ShouldBlock2([Values(20)] int pollingInterval, [Values(10000)] int lockTimeout)
        {
            Mock<Action<TimeSpan>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            TimeSpan
                delay = TimeSpan.FromMilliseconds(pollingInterval),
                timeout = TimeSpan.FromMilliseconds(lockTimeout); 

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockSleep.Object)
            {
                PollingInterval = delay,
                LockTimeout = timeout
            };

            int sleepCalled = 0;

            mockSleep
                .Setup(s => s.Invoke(delay))
                .Callback<TimeSpan>(_ => sleepCalled++);
            mockCache
                .Setup(c => c.Set("lock_key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), timeout, DistributedCacheInsertionFlags.None))
                .Returns<string, string, TimeSpan, DistributedCacheInsertionFlags>((_, _, _, _) => sleepCalled > 1);

            Assert.DoesNotThrow(() => @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Exactly(3));
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Exactly(2));
        }

        [Test]
        public void Acquire_ShouldTimeout2()
        {
            Mock<Action<TimeSpan>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockSleep.Object);

            mockSleep
                .Setup(s => s.Invoke(@lock.PollingInterval))
                .Callback<TimeSpan>(Thread.Sleep);
            mockCache
                .Setup(c => c.Set("lock_key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(false);

            Assert.Throws<TimeoutException>(() => @lock.Acquire("key", "owner", TimeSpan.FromMilliseconds(10 * @lock.PollingInterval.Milliseconds)));

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
        public void IsHeld_ShouldDetermineIfTheLockIsOwnedByTheCaller(string cacheRetVal, bool expected)
        {
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get(It.IsAny<string>()))
                .Returns(cacheRetVal);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance);

            Assert.That(@lock.IsHeld("key", "owner"), Is.EqualTo(expected));
        }
    }
}
