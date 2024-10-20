/********************************************************************************
* IViewRepository.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Eventing.Abstractions
{
    public interface IUntypedViewRepository
    {
        /// <summary>
        /// Materializes the given view.
        /// </summary>
        object Materialize(string flowId);

        /// <summary>
        /// Persits the given <see cref="Event"/>
        /// </summary>
        void Persist(ViewBase view, string eventId, object?[] args);
    }

    public interface IViewRepository<IView> : IUntypedViewRepository where IView: class
    {
        /// <summary>
        /// Materializes the given view.
        /// </summary>
        new IView Materialize(string flowId);
    }
}
