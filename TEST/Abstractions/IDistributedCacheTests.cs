/********************************************************************************
* IDistributedCacheTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    public abstract class IDistributedCacheTests
    {
        private IDistributedCache FCache = null!;

        protected abstract IDistributedCache CreateInstance();

        [SetUp]
        public virtual void SetupTest() => FCache = CreateInstance();


        [TearDown]
        public virtual void TearDownTest() => FCache?.Dispose();


        [Test]
        public async Task Get_ShouldReturnTheProperValue([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            await FCache.Set("key", "value", TimeSpan.FromSeconds(1), flags);
            Assert.That(await FCache.Get("key"), Is.EqualTo("value"));
        }

        [Test]
        public async Task Get_ShouldReturnNullIfTheKeyNotFound() => Assert.That(await FCache.Get("invalid"), Is.Null);

        [Test]
        public async Task Get_ShouldReturnNullIfTheKeyExpired([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            await FCache.Set("key", "value", TimeSpan.FromMilliseconds(1), flags);
            await Task.Delay(TimeSpan.FromMilliseconds(20));
            Assert.That(await FCache.Get("key"), Is.Null);
        }

        [Test]
        public async Task Get_ShouldUpdateTheSlidingExpiration()
        {
            await FCache.Set("key", "value", TimeSpan.FromMilliseconds(200), DistributedCacheInsertionFlags.AllowOverwrite);
            await Task.Delay(150);
            await FCache.Get("key");
            await Task.Delay(150);
            Assert.That(await FCache.Get("key"), Is.EqualTo("value"));
        }

        [Test]
        public async Task Set_ShouldOverwrite()
        {
            Assert.That(await FCache.Set("key", "value1", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.AllowOverwrite));
            Assert.That(await FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.AllowOverwrite));
            Assert.That(await FCache.Get("key"), Is.EqualTo("value2"));
        }

        [Test]
        public async Task Set_ShouldOverwriteExpiredKey()
        {
            Assert.That(await FCache.Set("key", "value1", TimeSpan.FromMilliseconds(10), DistributedCacheInsertionFlags.None));
            await Task.Delay(20);
            Assert.That(await FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None));
            Assert.That(await FCache.Get("key"), Is.EqualTo("value2"));
        }

        [Test]
        public async Task Set_ShouldSkipUpdate()
        {
            Assert.That(await FCache.Set("key", "value1", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None));
            Assert.That(await FCache.Set("key", "value2", TimeSpan.FromSeconds(1), DistributedCacheInsertionFlags.None), Is.False);
            Assert.That(await FCache.Get("key"), Is.EqualTo("value1"));
        }

        [Test]
        public async Task Remove_ShouldRemoveTheEntry([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            await FCache.Set("key", "value", TimeSpan.FromSeconds(1), flags);
            Assert.That(await FCache.Remove("key"));
            Assert.That(await FCache.Get("key"), Is.Null);
            Assert.That(await FCache.Remove("key"), Is.False);
        }

        [Test]
        public async Task Remove_ShouldReturnFalseOnExpiredEntry([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            await FCache.Set("key", "value", TimeSpan.FromMilliseconds(1), flags);
            await Task.Delay(10);
            Assert.That(await FCache.Remove("key"), Is.False);
        }

        [Test]
        public async Task RemovedEntry_CanBeReset([Values(DistributedCacheInsertionFlags.None, DistributedCacheInsertionFlags.AllowOverwrite)] DistributedCacheInsertionFlags flags)
        {
            await FCache.Set("key", "value1", TimeSpan.FromSeconds(1), flags);
            await FCache.Remove("key");
            Assert.That(await FCache.Set("key", "value2", TimeSpan.FromSeconds(1), flags));
            Assert.That(await FCache.Get("key"), Is.EqualTo("value2"));
        }
    }
}
