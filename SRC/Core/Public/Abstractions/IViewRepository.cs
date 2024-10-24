/********************************************************************************
* IViewRepository.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Represents an abstract repository to store view instances.
    /// </summary>
    public interface IViewRepository<TView> where TView : ViewBase, new()
    {
        /// <summary>
        /// Persits the given state
        /// </summary>
        /// <remarks>You don't need to call this method directly. It is done by the system when calling eventized methods on the <paramref name="view"/></remarks>
        void Persist(TView view, string eventId, object?[] args);

        /// <summary>
        /// Materializes the view belongs to the given <paramref name="flowId"/>.
        /// </summary>
        /// <returns>A lock instance to ensure operation exclusivity.</returns>
        IDisposable Materialize(string flowId, out TView view);

        /// <summary>
        /// Creates a new raw view.
        /// </summary>
        /// <returns>A lock instance to ensure operation exclusivity.</returns>
        /// <remarks>If the <paramref name="flowId"/> is null, the system will assign a unique value for it</remarks>
        IDisposable Create(string? flowId, out TView view);
    }
}
