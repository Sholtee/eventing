/********************************************************************************
* RedisCache.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

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
    public sealed class RedisCache: IDistributedCache
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
        public RedisCache(IConnectionMultiplexer connection, ISerializer serializer, ILogger<RedisCache>? logger = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Logger = logger;
        }

        /// <summary>
        /// Creates a new <see cref="RedisCache"/> instance.
        /// </summary>
        public RedisCache(string config, ISerializer serializer, ILogger<RedisCache>? logger = null)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Connection = ConnectionMultiplexer.Connect(config ?? throw new ArgumentNullException(nameof(config)));
            Logger = logger;

            FRequireDisposal = true;
        }

        /// <summary>
        /// The underlying connection.
        /// </summary>
        public IConnectionMultiplexer Connection
        {
            get;
#if DEBUG
            internal
#else
            private
#endif
                set;
        }

        /// <summary>
        /// The underlying serializer.
        /// </summary>
        public ISerializer Serializer { get; }

        /// <summary>
        /// The underyling logger
        /// </summary>
        public ILogger<RedisCache>? Logger { get; }

        /// <summary>
        /// Closes the underlying database connection
        /// </summary>
        public void Dispose()
        {
            if (FRequireDisposal && Connection is not null)
            {
                Connection.Dispose();
                Connection = null!;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> Get(string key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            Logger?.LogInformation(Info.GET_CACHE_ITEM, LOG_GET_CACHE_ITEM, key);

            IDatabase db = Connection.GetDatabase();

            //
            // db.StringGetWithExpiry(key) wont work here as it returns the time left instead of the original span.
            // 
            // db.Touch() doesn't reset the expiration either so we need to do it by hand.
            //

            string? value = await db.StringGetAsync(key);
            if (value is not null)
            {
                CacheEntry entry = Serializer.Deserialize<CacheEntry>(value)!;

                Logger?.LogInformation(Info.SET_CACHE_ITEM_EXPIRATION, LOG_SET_CACHE_ITEM_EXPIRATION, entry.Expiration, key);

                //
                // The key might get expired while we reached here
                //

                if (await db.KeyExpireAsync(key, TimeSpan.FromTicks(entry.Expiration)))
                    return entry.Value;
            }

            return null;
        }

        /// <inheritdoc/>
        public Task<bool> Remove(string key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            Logger?.LogInformation(Info.REMOVE_CACHE_ITEM, LOG_REMOVE_CACHE_ITEM, key);

            IDatabase db = Connection.GetDatabase();

            return db.KeyDeleteAsync(key);
        }

        /// <inheritdoc/>
        public Task<bool> Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (value is null)
                throw new ArgumentNullException(nameof(value));

            Logger?.LogInformation(Info.SET_CACHE_ITEM, LOG_SET_CACHE_ITEM, key, slidingExpiration, flags);

            IDatabase db = Connection.GetDatabase();

            return db.StringSetAsync
            (
                key,
                Serializer.Serialize
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
