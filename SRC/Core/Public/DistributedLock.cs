/********************************************************************************
* DistributedLock.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Properties;

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

        private static string GetLockKey(string key) => $"lock_{key ?? throw new ArgumentNullException(nameof(key))}";
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
        public void Acquire(string key, string ownerId, TimeSpan timeout)
        {
            key = GetLockKey(key);
            string entry = serializer.Serialize
            (
                new LockEntry
                {
                    OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId))
                }
            );

            for(Stopwatch sw = Stopwatch.StartNew(); !cache.Set(key, entry, LockTimeout, DistributedCacheInsertionFlags.None); )
            {
                sleep(PollingInterval);

                if (sw.Elapsed > timeout)
                    throw new TimeoutException();
            }
        }

        /// <inheritdoc/>
        public bool IsHeld(string key, string ownerId)
        {
            string? entryRaw = cache.Get(GetLockKey(key));
            if (entryRaw is not null)
            {
                LockEntry entry = serializer.Deserialize<LockEntry>(entryRaw)!;
                return entry.OwnerId == ownerId;
            }
            return false;
        }

        /// <inheritdoc/>
        public void Release(string key, string ownerId)
        {
            if (!IsHeld(key, ownerId))
                throw new InvalidOperationException(Resources.ERR_NO_LOCK);
            cache.Remove(GetLockKey(key));
        }
    }
}
