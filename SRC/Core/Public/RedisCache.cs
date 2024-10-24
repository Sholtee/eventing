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

    public sealed class RedisCache(string config, ISerializer serializer) : IDistributedCache, IDisposable
    {
        private ConnectionMultiplexer FConnection = ConnectionMultiplexer.Connect(config);

        private sealed class CacheEntry
        {
            public required string Value { get; init; }
            public required long Expiration { get; init; }
        }

        public void Dispose()
        {
            FConnection?.Dispose();
            FConnection = null!;
        }

        /// <inheritdoc/>
        public string? Get(string key)
        {
            IDatabase db = FConnection.GetDatabase();

            //
            // db.StringGetWithExpiry(key) wont work here as it returns the time left instead of the original span.
            // 
            // Also db.Touch() wont reset the expiration so we need to reset the expiration by hand.
            //

            string? value = db.StringGet(key);
            if (value is not null)
            {
                CacheEntry entry = serializer.Deserialize<CacheEntry>(value)!;
                if (db.KeyExpire(key, TimeSpan.FromTicks(entry.Expiration)))
                    return entry.Value;
            }

            return null;
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            IDatabase db = FConnection.GetDatabase();

            return db.KeyDelete(key);
        }

        /// <inheritdoc/>
        public bool Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags)
        {
            IDatabase db = FConnection.GetDatabase();

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
