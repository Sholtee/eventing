/********************************************************************************
* ILock.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Defines the contract of global locks
    /// </summary>
    public interface ILock
    {
        /// <summary>
        /// Creates a global lock over the given <paramref name="key"/>.
        /// </summary>
        IDisposable Lock(string key);
    }
}
