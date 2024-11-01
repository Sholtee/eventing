/********************************************************************************
* IDistributedLockTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Properties;

    public abstract class IDistributedLockTests
    {
        private sealed class Scope(IDistributedLock @lock, string key, string ownerId) : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                if (!Disposed)
                {
                    @lock.Release(key, ownerId);
                    Disposed = true;
                }
            }
        }

        protected IDisposable ScopedLock(string key, TimeSpan timeout)
        {
            IDistributedLock @lock = CreateInstance();

            string ownerId = Guid.NewGuid().ToString("D");

            @lock.Acquire(key, ownerId, timeout);
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
       
            void Worker()
            {
                using IDisposable scope = ScopedLock("mylock", TimeSpan.FromMinutes(1));

                Assert.That(lockHeld, Is.False);
                lockHeld = true;
                Thread.Sleep(100); // wait for the other tasks
                lockHeld = false;
            }
        }

        [Test]
        public void Acquire_ShouldTimeout()
        {
            using ManualResetEventSlim evt = new();

            Task t = Task.Factory.StartNew(() =>
            {
                using IDisposable scope = ScopedLock("mylock", TimeSpan.FromMinutes(1));
                evt.Wait();
            });
            t.Wait(100); // make sure the thread has grabed the lock

            Assert.Throws<TimeoutException>(() => ScopedLock("mylock", TimeSpan.FromMilliseconds(10)));

            evt.Set();
            t.Wait();
        }

        [Test]
        public void Acquire_ShouldBlockOnNestedInvocation()
        {
            using IDisposable scope = ScopedLock("mylock", TimeSpan.FromMinutes(1));

            Assert.Throws<TimeoutException>(() => ScopedLock("mylock", TimeSpan.FromMilliseconds(10)));
        }

        [Test]
        public void IsHeld_ShouldDetermineIfTheLockIsOwnedByTheCaller([Values("owner_1", "owner_2")] string ownerId)
        {
            IDistributedLock @lock = CreateInstance();

            @lock.Acquire("mylock", ownerId, TimeSpan.FromMinutes(1));
            try
            {
                Assert.That(@lock.IsHeld("mylock", "owner_1"), Is.EqualTo(ownerId == "owner_1"));
            }
            finally
            {
                @lock.Release("mylock", ownerId);
            }
        }

        [Test]
        public void Release_ShouldThrowIfTheLockIsNotHeld()
        {
            IDistributedLock @lock = CreateInstance();

            @lock.Acquire("mylock", "owner_1", TimeSpan.FromMinutes(1));
            try
            {
                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => @lock.Release("mylock", "unknown"))!;
                Assert.That(ex.Message, Is.EqualTo(Resources.ERR_NO_LOCK));
            }
            finally
            {
                @lock.Release("mylock", "owner_1");
            }
        }

        [Test]
        public void Acquire_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.Throws<ArgumentNullException>(() => @lock.Acquire(null!, "notnull", TimeSpan.Zero));
            Assert.Throws<ArgumentNullException>(() => @lock.Acquire("notnull", null!, TimeSpan.Zero));
        }

        [Test]
        public void IsHelpd_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.Throws<ArgumentNullException>(() => @lock.IsHeld(null!, "notnull"));
            Assert.Throws<ArgumentNullException>(() => @lock.IsHeld("notnull", null!));
        }

        [Test]
        public void Release_ShouldBeNullChecked()
        {
            IDistributedLock @lock = CreateInstance();

            Assert.Throws<ArgumentNullException>(() => @lock.Release(null!, "notnull"));
            Assert.Throws<ArgumentNullException>(() => @lock.Release("notnull", null!));
        }
    }
}
