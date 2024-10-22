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
        void Persist(TView view, string eventId, object?[] args);

        /// <summary>
        /// Materializes the view belongs to the given <paramref name="flowId"/>.
        /// </summary>
        IDisposable Materialize(string flowId, out TView view);
    }
}
