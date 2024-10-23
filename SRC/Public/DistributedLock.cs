/********************************************************************************
* DistributedLock.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

namespace Solti.Utils.Eventing
{
    using Abstractions;

    /// <summary>
    /// Implements global locking mechanism over the <see cref="IDistributedCache"/> interface.
    /// </summary>
    public sealed class DistributedLock(IDistributedCache cache, ISerializer serializer, Action<TimeSpan> sleep /*for testing*/): IDistributedLock
    {
        #region Private
        private  sealed class LockEntry
        {
            public string OwnerId { get; set; } = null!;
        }

        private sealed class LockLifetime(IDistributedCache cache, string key) : IDisposable
        {
            private bool FDisposed = false;

            public void Dispose()
            {
                if (!FDisposed)
                {
                    cache.Remove(key);
                    FDisposed = true;
                }
            }
        }
        #endregion

        /// <summary>
        /// Creates a new <see cref="DistributedLock"/> instance.
        /// </summary>
        public DistributedLock(IDistributedCache cache, ISerializer serializer) : this(cache, serializer, Thread.Sleep) { }

        /// <summary>
        /// Gets or sets the polling interval to be used
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Maximum lifespan of created locks.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromHours(1);

        /// <inheritdoc/>
        public IDisposable Acquire(string key, string ownerId, TimeSpan timeout)
        {
            string entry = serializer.Serialize(new LockEntry { OwnerId = ownerId });

            for(; ; )
            {
                if (cache.Set(key, entry, LockTimeout, DistributedCacheInsertionFlags.None))
                    return new LockLifetime(cache, key);

                timeout -= PollingInterval;
                if (timeout <= TimeSpan.Zero)
                    throw new TimeoutException();

                sleep(PollingInterval);
            }
        }

        /// <inheritdoc/>
        public bool IsHeld(string key, string ownerId)
        {
            string? entryRaw = cache.Get(key);
            if (entryRaw is not null)
            {
                LockEntry entry = serializer.Deserialize(entryRaw, static () => new LockEntry())!;
                return entry.OwnerId == ownerId;
            }
            return false;
        }
    }
}
