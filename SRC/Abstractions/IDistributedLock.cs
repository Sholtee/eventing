/********************************************************************************
* IDistributedLock.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Defines the contract of global locks
    /// </summary>
    public interface IDistributedLock
    {
        /// <summary>
        /// Creates a global lock over the given <paramref name="key"/>.
        /// </summary>
        /// <remarks><paramref name="ownerId"/> is to help identifying the owner the lock</remarks>
        /// <exception cref="TimeoutException">If the lock could not be aquired in the given period of time.</exception>
        Task Acquire(string key, string ownerId, TimeSpan timeout);

        /// <summary>
        /// Releases a global lock that is assigned to the given <paramref name="key"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the <paramref name="key"/> is not assigned to the <paramref name="ownerId"/></exception>
        Task Release(string key, string ownerId);

        /// <summary>
        /// Returns whether the given lock is held by the provided owner.
        /// </summary>
        Task<bool> IsHeld(string key, string ownerId);
    }
}
