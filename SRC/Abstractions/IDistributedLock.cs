/********************************************************************************
* IDistributedLock.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

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
        IDisposable Acquire(string key, string ownerId, TimeSpan timeout);

        /// <summary>
        /// Returns whether the given lock is held be the provided owner.
        /// </summary>
        bool IsHeld(string key, string ownerId);
    }
}
