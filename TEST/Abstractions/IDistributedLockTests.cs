/********************************************************************************
* IDistributedLockTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    public abstract class IDistributedLockTests
    {
        private sealed class Scope(IDistributedLock @lock, string key, string ownerId) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync() => await @lock.Release(key, ownerId);
        }

        protected async Task<IAsyncDisposable> ScopedLock(string key, TimeSpan timeout)
        {
            IDistributedLock @lock = CreateInstance();

            string ownerId = Guid.NewGuid().ToString("D");

            await @lock.Acquire(key, ownerId, timeout);
            return new Scope(@lock, key, ownerId);
        }

        protected abstract IDistributedLock CreateInstance();

        [Test]
        public void Acquire_ShouldBlock([Values(2,3,10)] int workers)
        {
            bool lockHeld = false;

            Task[] tasks = new Task[workers];
            for (int i = 0; i < workers; i++)
            {
                tasks[i] = Task.Factory.StartNew(Worker);
            }

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));

            async Task Worker()
            {
                await using IAsyncDisposable scope = await ScopedLock("mylock", TimeSpan.FromMinutes(1));

                Assert.That(lockHeld, Is.False);
                lockHeld = true;
                await Task.Delay(100); // wait for the other tasks
                lockHeld = false;
            }
        }

        [Test]
        public void Acquire_ShouldTimeout()
        {
            using ManualResetEventSlim evt = new();

            Task t = Task.Factory.StartNew(async () =>
            {
                await using IAsyncDisposable scope = await ScopedLock("mylock", TimeSpan.FromMinutes(1));
                evt.Wait();
            });
            t.Wait(100); // make sure the thread has grabed the lock

            Assert.ThrowsAsync<TimeoutException>(() => ScopedLock("mylock", TimeSpan.FromMilliseconds(10)));

            evt.Set();
            t.Wait();
        }

        [Test]
        public async Task Acquire_ShouldBlockOnNestedInvocation()
        {
            await using IAsyncDisposable scope = await ScopedLock("mylock", TimeSpan.FromMinutes(1));

            Assert.ThrowsAsync<TimeoutException>(() => ScopedLock("mylock", TimeSpan.FromMilliseconds(10)));
        }

        [Test]
        public async Task IsHeld_ShouldDetermineIfTheLockIsOwnedByTheCaller([Values("owner_1", "owner_2")] string ownerId)
        {
            IDistributedLock @lock = CreateInstance();

            await @lock.Acquire("mylock", ownerId, TimeSpan.FromMinutes(1));
            try
            {
                Assert.That(await @lock.IsHeld("mylock", "owner_1"), Is.EqualTo(ownerId == "owner_1"));
            }
            finally
            {
                await @lock.Release("mylock", ownerId);
            }
        }

        [Test]
        public async Task Release_ShouldThrowIfTheLockIsNotHeld()
        {
            IDistributedLock @lock = CreateInstance();

            await @lock.Acquire("mylock", "owner_1", TimeSpan.FromMinutes(1));
            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => @lock.Release("mylock", "unknown"));
            }
            finally
            {
                await @lock.Release("mylock", "owner_1");
            }
        }

        [Test]
        public void Acquire_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.Acquire(null!, "notnull", TimeSpan.Zero));
            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.Acquire("notnull", null!, TimeSpan.Zero));
        }

        [Test]
        public void IsHelpd_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.IsHeld(null!, "notnull"));
            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.IsHeld("notnull", null!));
        }

        [Test]
        public void Release_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.Release(null!, "notnull"));
            Assert.ThrowsAsync<ArgumentNullException>(() => @lock.Release("notnull", null!));
        }
    }
}
