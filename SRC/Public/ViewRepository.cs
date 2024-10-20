/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;

    /// <summary>
    /// Repository to store view instances
    /// </summary>
    public class ViewRepository<TView, IView>(IEventStore EventStore, IDistributedCache Cache, ILock Lock, ILogger? Logger = null) : ViewRepositoryBase<TView, IView, ReflectionModule>(EventStore, Cache, Lock, Logger) where TView : ViewBase, IView, new() where IView : class
    {
    }
}
