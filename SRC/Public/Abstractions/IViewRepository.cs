/********************************************************************************
* IViewRepository.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    public interface IViewRepositoryWriter
    {
        /// <summary>
        /// Persits the given <see cref="Event"/>
        /// </summary>
        void Persist(ViewBase view, string eventId, object?[] args);
    }

    public interface IViewRepositoryWriterReader<IView> where IView: class
    {
        /// <summary>
        /// Materializes the given view.
        /// </summary>
        IDisposable Materialize(string flowId, out IView view);
    }

    public interface IViewRepository<IView> : IViewRepositoryWriter, IViewRepositoryWriterReader<IView> where IView: class
    {
    }
}
