/********************************************************************************
* IViewRepository.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Specifies the write operations against a view repository.
    /// </summary>
    public interface IViewRepositoryWriter
    {
        /// <summary>
        /// Persits the given state
        /// </summary>
        void Persist(ViewBase view, string eventId, object?[] args);
    }

    /// <summary>
    /// Specifies the read operations against a view repository.
    /// </summary>
    public interface IViewRepositoryWriterReader<IView> where IView: class
    {
        /// <summary>
        /// Materializes the view belongs to the given <paramref name="flowId"/>.
        /// </summary>
        IDisposable Materialize(string flowId, out IView view);
    }

    /// <summary>
    /// Represents an abstract repository to store view instances.
    /// </summary>
    public interface IViewRepository<IView> : IViewRepositoryWriter, IViewRepositoryWriterReader<IView> where IView: class
    {
    }
}
