/********************************************************************************
* RedisCache.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;

using StackExchange.Redis;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    
    public sealed class RedisCache(string config) : IDistributedCache, IDisposable
    {
        const string DATA_FIELD = "data";

        private ConnectionMultiplexer FConnection = ConnectionMultiplexer.Connect(config);

        public void Dispose()
        {
            FConnection?.Dispose();
            FConnection = null!;
        }

        /// <inheritdoc/>
        public string? Get(string key)
        {
            IDatabase db = FConnection.GetDatabase();

            RedisValue val = db.StringGet(key);
            if (val == RedisValue.Null)
                return null;

            return val;
        }

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags)
        {
            IDatabase db = FConnection.GetDatabase();

            bool
                allowOverwrite = flags.HasFlag(DistributedCacheInsertionFlags.AllowOverwrite),
                newFieldSet = db.StringSet(key, value, slidingExpiration, allowOverwrite ? When.Always : When.NotExists);

            //
            // StringSet() returns true if a new value was created, in the case the value was updated or remained untouched it returns false.
            //

            return newFieldSet || allowOverwrite;
        }
    }
}
