/********************************************************************************
* IDistributedCache.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    [Flags]
    public enum DistributedCacheInsertionFlags
    {
        None = 0,
        /// <summary>
        /// Allow entry to be overwritten if exists already. It also resets the sliding expiration
        /// </summary>
        AllowOverwrite = 1 << 0,
    }

    /// <summary>
    /// DEfines the contract of distributed chaches
    /// </summary>
    public interface IDistributedCache: IDisposable
    {
        /// <summary>
        /// Sets or updates an entry in the cache.
        /// </summary>
        /// <returns>If the operation was successful</returns>
        bool Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags);

        /// <summary>
        /// Gets the value from the cache associated with the given <paramref name="key"/>
        /// </summary>
        string? Get(string key);

        /// <summary>
        /// Removesthe given key.
        /// </summary>
        bool Remove(string key);
    }
}
