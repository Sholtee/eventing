/********************************************************************************
* DistributedLock.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;

    using static Internals.EventIds;
    using static Properties.Resources;

    /// <summary>
    /// Implements global locking mechanism over the <see cref="IDistributedCache"/> interface.
    /// </summary>
    public sealed class DistributedLock(IDistributedCache cache, ISerializer serializer, ILogger<DistributedLock>? logger, Action<TimeSpan> sleep /*for testing*/): IDistributedLock
    {
        #region Private
        private sealed class LockEntry
        {
            public required string OwnerId { get; init; }
        }

        private static string GetLockKey(string key) => $"lock_{key ?? throw new ArgumentNullException(nameof(key))}";
        #endregion

        /// <summary>
        /// Creates a new <see cref="DistributedLock"/> instance.
        /// </summary>
        public DistributedLock(IDistributedCache cache, ISerializer serializer, ILogger<DistributedLock>? logger = null) : this(cache, serializer, logger, Thread.Sleep) { }

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
            //
            // "key" is validated by GetLockKey() method
            //

            string prefixedKey = GetLockKey(key);

            string entry = serializer.Serialize
            (
                new LockEntry
                {
                    OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId))
                }
            );

            logger?.LogInformation(Info.ACQUIRE_LOCK, LOG_ACQUIRE_LOCK, key, ownerId);

            for (Stopwatch sw = Stopwatch.StartNew(); !cache.Set(prefixedKey, entry, LockTimeout, DistributedCacheInsertionFlags.None); )
            {
                sleep(PollingInterval);

                if (sw.Elapsed > timeout)
                {
                    logger?.LogWarning(Warning.ACQUIRE_LOCK_TIMEOUT, LOG_ACQUIRE_LOCK_TIMEOUT, key, ownerId, sw.ElapsedMilliseconds);
                    throw new TimeoutException().WithData((nameof(timeout), timeout), ("elapsed", sw.Elapsed));
                }
            }
        }

        /// <inheritdoc/>
        public bool IsHeld(string key, string ownerId)
        {
            //
            // "key" is validated by GetLockKey() method
            //

            if (ownerId is null)
                throw new ArgumentNullException(nameof(ownerId));

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
            {
                logger?.LogWarning(Warning.FOREIGN_LOCK_RELEASE, LOG_FOREIGN_LOCK_RELEASE, key, ownerId);
                throw new InvalidOperationException(ERR_FOREIGN_LOCK_RELEASE).WithData((nameof(key), key), (nameof(ownerId), ownerId));
            }

            logger?.LogInformation(Info.RELEASE_LOCK, LOG_RELEASE_LOCK, key, ownerId);

            //
            // Invoking IsHeld() should refresh the expiration so it's sure we still own the lock here
            // 

            cache.Remove(GetLockKey(key));
        }
    }
}
