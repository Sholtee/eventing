/********************************************************************************
* ViewRepository.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Internals;

    /// <summary>
    /// Repository to store view instances
    /// </summary>
    public class ViewRepository<TView, IView>(IEventStore EventStore, IDistributedCache Cache, IDistributedLock Lock, ILogger? Logger = null) : 
        ViewRepositoryBase<TView, IView, ReflectionModule>
        (
            EventStore ?? throw new ArgumentNullException(nameof(EventStore)),
            Cache ?? throw new ArgumentNullException(nameof(Cache)),
            Lock ?? throw new ArgumentNullException(nameof(Lock)),
            JsonSerializer.Instance,
            Logger
        )
        where TView : ViewBase, IView, new() where IView : class {}
}
