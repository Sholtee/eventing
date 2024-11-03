/********************************************************************************
* RedisCache.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using StackExchange.Redis;

namespace Solti.Utils.Eventing
{
    using Abstractions;

    /// <summary>
    /// Implements the <see cref="IDistributedCache"/> interface over Redis
    /// </summary>
    /// <param name="config"></param>
    /// <param name="serializer"></param>
    public sealed class RedisCache(IConnectionMultiplexer connection, ISerializer serializer) : IDistributedCache
    {
        #region Private
        private readonly bool FRequireDisposal;

        private sealed class CacheEntry
        {
            public required string Value { get; init; }
            public required long Expiration { get; init; }
        }
        #endregion

        /// <summary>
        /// Creates a new <see cref="RedisCache"/> instance.
        /// </summary>
        public RedisCache(string config, ISerializer serializer) : this(ConnectionMultiplexer.Connect(config), serializer) =>
            FRequireDisposal = true;

        /// <summary>
        /// Closes the underlying database connection
        /// </summary>
        public void Dispose()
        {
            if (FRequireDisposal)
            {
                connection?.Dispose();
                connection = null!;
            }
        }

        /// <inheritdoc/>
        public string? Get(string key)
        {
            IDatabase db = connection.GetDatabase();

            //
            // db.StringGetWithExpiry(key) wont work here as it returns the time left instead of the original span.
            // 
            // db.Touch() doesn't reset the expiration either so we need to do it by hand.
            //

            string? value = db.StringGet(key);
            if (value is not null)
            {
                CacheEntry entry = serializer.Deserialize<CacheEntry>(value)!;

                //
                // The key might get expired while we reached here
                //

                if (db.KeyExpire(key, TimeSpan.FromTicks(entry.Expiration)))
                    return entry.Value;
            }

            return null;
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            IDatabase db = connection.GetDatabase();

            return db.KeyDelete(key);
        }

        /// <inheritdoc/>
        public bool Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags)
        {
            IDatabase db = connection.GetDatabase();

            return db.StringSet
            (
                key,
                serializer.Serialize
                (
                    new CacheEntry
                    {
                        Value = value,
                        Expiration = slidingExpiration.Ticks
                    }
                ),
                slidingExpiration,
                flags.HasFlag(DistributedCacheInsertionFlags.AllowOverwrite) ? When.Always : When.NotExists
            );
        }
    }
}
