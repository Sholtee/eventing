/********************************************************************************
* IDistributedCache.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Specifies the cache behavior when inserting new items.
    /// </summary>
    [Flags]
    public enum DistributedCacheInsertionFlags
    {
        /// <summary>
        /// No flags specified.
        /// </summary>
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
        Task<bool> Set(string key, string value, TimeSpan slidingExpiration, DistributedCacheInsertionFlags flags);

        /// <summary>
        /// Gets the value from the cache associated with the given <paramref name="key"/>
        /// </summary>
        Task<string?> Get(string key);

        /// <summary>
        /// Removesthe given key.
        /// </summary>
        Task<bool> Remove(string key);
    }
}
