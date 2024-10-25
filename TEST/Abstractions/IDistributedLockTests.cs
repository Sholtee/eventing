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

    public abstract class IDistributedLockTests
    {
        protected abstract IDistributedLock Createinstance();

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
                IDistributedLock @lock = Createinstance();

                using (IDisposable inst = @lock.Acquire("mylock", Guid.NewGuid().ToString(), TimeSpan.FromMinutes(1)))
                {
                    Assert.That(lockHeld, Is.False);
                    lockHeld = true;
                    Thread.Sleep(100); // wait for the other tasks
                    lockHeld = false;
                }
            }
        }

        [Test]
        public void Acquire_ShouldTimeout()
        {
            using ManualResetEventSlim evt = new();

            Task t = Task.Factory.StartNew(() =>
            {
                IDistributedLock lock1 = Createinstance();

                using (IDisposable inst = @lock1.Acquire("mylock", Guid.NewGuid().ToString(), TimeSpan.FromMinutes(1)))
                {
                    evt.Wait();
                }
            });
            t.Wait(100); // make sure the thread has grabed the lock


            IDistributedLock lock2 = Createinstance();

            Assert.Throws<TimeoutException>(() => lock2.Acquire("mylock", Guid.NewGuid().ToString(), TimeSpan.FromMilliseconds(0)));

            evt.Set();
            t.Wait();
        }

        [Test]
        public void IsHeld_ShouldDetermineIfTheLockIsOwnedByTheCaller([Values("owner_1", "owner_2")] string ownerId)
        {
            IDistributedLock @lock = Createinstance();

            using IDisposable inst = @lock.Acquire("mylock", ownerId, TimeSpan.FromMinutes(1));
            Assert.That(@lock.IsHeld("mylock", "owner_1"), Is.EqualTo(ownerId == "owner_1"));
        }
    }
}
