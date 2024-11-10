/********************************************************************************
* IViewRepository.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Represents an abstract, untyped repository to store view instances.
    /// </summary>
    public interface IViewRepository
    {
        /// <summary>
        /// Closes the underlying session.
        /// </summary>
        /// <remarks>
        /// After calling this method you should discard the view that belongs to the given <paramref name="flowId"/>.
        /// You don't need to call this method directly. It is done by the system when calling the <see cref="IDisposable.Dispose"/> method on views
        /// </remarks>
        Task Close(string flowId);

        /// <summary>
        /// Persits the given state
        /// </summary>
        /// <remarks>You don't need to call this method directly. It is done by the system when calling eventized methods on the <paramref name="view"/></remarks>
        Task Persist(ViewBase view, string eventId, object?[] args);

        /// <summary>
        /// Materializes the view belongs to the given <paramref name="flowId"/>.
        /// </summary>
        Task<ViewBase> Materialize(string flowId);

        /// <summary>
        /// Creates a new raw view.
        /// </summary>
        /// <remarks>If the <paramref name="flowId"/> is null, the system will assign a unique value for it</remarks>
        Task<ViewBase> Create(string? flowId, object? tag = null);
    }

    /// <summary>
    /// Represents an abstract repository to store view instances.
    /// </summary>
    public interface IViewRepository<TView>: IViewRepository where TView : ViewBase
    {
        /// <summary>
        /// Persits the given state
        /// </summary>
        /// <remarks>You don't need to call this method directly. It is done by the system when calling eventized methods on the <paramref name="view"/></remarks>
        Task Persist(TView view, string eventId, object?[] args);

        /// <summary>
        /// Materializes the view belongs to the given <paramref name="flowId"/>.
        /// </summary>
        new Task<TView> Materialize(string flowId);

        /// <summary>
        /// Creates a new raw view.
        /// </summary>
        /// <remarks>If the <paramref name="flowId"/> is null, the system will assign a unique value for it</remarks>
        new Task<TView> Create(string? flowId, object? tag = null);
    }
}
