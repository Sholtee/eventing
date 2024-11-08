/********************************************************************************
* RedisCache.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Solti.Utils.Eventing
{
    using Abstractions;

    using static Internals.EventIds;
    using static Properties.Resources;

    /// <summary>
    /// Implements the <see cref="IDistributedCache"/> interface over Redis
    /// </summary>
    public sealed class RedisCache(IConnectionMultiplexer connection, ISerializer serializer, ILogger<RedisCache>? logger = null) : IDistributedCache
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
        public RedisCache(string config, ISerializer serializer, ILogger<RedisCache>? logger = null) : this(ConnectionMultiplexer.Connect(config), serializer, logger) =>
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
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            logger?.LogInformation(Info.GET_CACHE_ITEM, LOG_GET_CACHE_ITEM, key);

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

                logger?.LogInformation(Info.SET_CACHE_ITEM_EXPIRATION, LOG_SET_CACHE_ITEM_EXPIRATION, entry.Expiration, key);

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
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            logger?.LogInformation(Info.REMOVE_CACHE_ITEM, LOG_REMOVE_CACHE_ITEM, key);

            IDatabase db = connection.GetDatabase();

            return db.KeyDelete(key);
        }

        /// <inheritdoc/>
        public bool Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (value is null)
                throw new ArgumentNullException(nameof(value));

            logger?.LogInformation(Info.SET_CACHE_ITEM, LOG_SET_CACHE_ITEM, key, slidingExpiration, flags);

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
