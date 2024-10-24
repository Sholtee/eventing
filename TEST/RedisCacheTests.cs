/********************************************************************************
* RedisCacheTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    [TestFixture]
    public class RedisCacheTests: ModuleTestsBase
    {
        private RedisCache FCache = null!;

        public override void Setup()
        {
            base.Setup();
            FCache = new RedisCache("localhost:6379", JsonSerializer.Instance);
        }

        public override void Teardown()
        {
            FCache?.Dispose();
            base.Teardown();
        }

        [Test]
        public void Get_ShouldReturnTheProperValue([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            FCache.Set("key", "value", TimeSpan.FromSeconds(1), flags);
            Assert.That(FCache.Get("key"), Is.EqualTo("value"));
        }

        [Test]
        public void Get_ShouldReturnNullIfTheKeyNotFound() => Assert.That(FCache.Get("invalid"), Is.Null);

        [Test]
        public void Get_ShouldReturnNullIfTheKeyExpired([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            FCache.Set("key", "value", TimeSpan.FromMilliseconds(1), flags);
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            Assert.That(FCache.Get("key"), Is.Null);
        }

        [Test]
        public void Get_ShouldUpdateTheSlidingExpiration()
        {
            FCache.Set("key", "value", TimeSpan.FromMilliseconds(200), DistributedCacheInsertionFlags.AllowOverwrite);
            Thread.Sleep(150);
            FCache.Get("key");
            Thread.Sleep(150);
            Assert.That(FCache.Get("key"), Is.EqualTo("value"));
        }

        [Test]
        public void Set_ShouldOverwrite()
        {
            Assert.That(FCache.Set("key", "value1", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.AllowOverwrite));
            Assert.That(FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.AllowOverwrite));
            Assert.That(FCache.Get("key"), Is.EqualTo("value2"));
        }

        [Test]
        public void Set_ShouldOverwriteExpiredKey()
        {
            Assert.That(FCache.Set("key", "value1", TimeSpan.FromMilliseconds(10), DistributedCacheInsertionFlags.None));
            Thread.Sleep(20);
            Assert.That(FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None));
            Assert.That(FCache.Get("key"), Is.EqualTo("value2"));
        }

        [Test]
        public void Set_ShouldSkipUpdate()
        {
            Assert.That(FCache.Set("key", "value1", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None));
            Assert.That(FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None), Is.False);
            Assert.That(FCache.Get("key"), Is.EqualTo("value1"));
        }
    }
}
