/********************************************************************************
* DistributedLockTests.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    [TestFixture]
    public class DistributedLockTests
    {
        [Test]
        public void Acquire_ShouldCreateALockInstance()
        {
            Mock<ISerializer> mockSerializer = new(MockBehavior.Strict);
            mockSerializer
                .Setup(s => s.Serialize(It.IsAny<It.IsAnyType>()))
                .Returns<object>(JsonSerializer.Instance.Serialize);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, mockSerializer.Object);

            mockCache
                .Setup(c => c.Remove("key"))
                .Returns(true);
            mockCache
                .Setup(c => c.Set("key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(true);

            IDisposable lifetime = @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Never);

            lifetime.Dispose();

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Once);
            mockSerializer.Verify(s => s.Serialize(It.IsAny<It.IsAnyType>()), Times.Once);
        }

        [Test]
        public void Acquire_ShouldBlock()
        {
            Mock<Action<TimeSpan>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockSleep.Object);

            int sleepCalled = 0;

            mockSleep
                .Setup(s => s.Invoke(@lock.PollingInterval))
                .Callback<TimeSpan>(_ => sleepCalled++);

            mockCache
                .Setup(c => c.Remove("key"))
                .Returns(true);
            mockCache
                .Setup(c => c.Set("key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns<string, string, TimeSpan, DistributedCacheInsertionFlags>((_, _, _, _) => sleepCalled > 1);

            Assert.DoesNotThrow(() => @lock.Acquire("key", "owner", TimeSpan.FromSeconds(1)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Exactly(3));
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Exactly(2));
        }

        [Test]
        public void Acquire_ShouldTimeout()
        {
            Mock<Action<TimeSpan>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockSleep.Object);

            mockSleep.Setup(s => s.Invoke(@lock.PollingInterval));

            mockCache
                .Setup(c => c.Remove("key"))
                .Returns(true);
            mockCache
                .Setup(c => c.Set("key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(false);

            Assert.Throws<TimeoutException>(() => @lock.Acquire("key", "owner", TimeSpan.FromMilliseconds(10 * @lock.PollingInterval.Milliseconds)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Exactly(10));
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Exactly(9));
        }

        [Test]
        public void Acquire_ShouldTimeout2()
        {
            Mock<Action<TimeSpan>> mockSleep = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            DistributedLock @lock = new(mockCache.Object, JsonSerializer.Instance, mockSleep.Object);

            mockSleep.Setup(s => s.Invoke(@lock.PollingInterval));

            mockCache
                .Setup(c => c.Remove("key"))
                .Returns(true);
            mockCache
                .Setup(c => c.Set("key", It.Is<string>(s => s == JsonSerializer.Instance.Serialize(new Dictionary<string, string> { { "OwnerId", "owner" } })), @lock.LockTimeout, DistributedCacheInsertionFlags.None))
                .Returns(false);

            Assert.Throws<TimeoutException>(() => @lock.Acquire("key", "owner", TimeSpan.FromMilliseconds(@lock.PollingInterval.Milliseconds / 10)));

            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockSleep.Verify(s => s.Invoke(It.IsAny<TimeSpan>()), Times.Never);
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
        public void IsHeld_ShouldDetermineIfTheHoldIsOwnedByTheCurrentApp(string cacheRetVal, bool expected)
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
