/********************************************************************************
* IReflectionModule.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Function to crate <typeparamref name="TView"/> instaces.
    /// </summary>
    public delegate TView CreateRawViewDelegate<TView>(string flowId, IViewRepository<TView> repo, out IEventfulViewConfig config) where TView : ViewBase;

    /// <summary>
    /// Event processor belonging to a particular event.
    /// </summary>
    public delegate void ProcessEventDelegate<TView>(TView view, string serializedArguments, ISerializer serializer) where TView : ViewBase;

    /// <summary>
    /// Contract of reflection related dependencies.
    /// </summary>
    public interface IReflectionModule<TView> where TView : ViewBase
    {
        /// <summary>
        /// Event processors belonging to the given <typeparamref name="TView"/>.
        /// </summary>
        IReadOnlyDictionary<string, ProcessEventDelegate<TView>> EventProcessors { get; }

        /// <summary>
        /// Function to crate <typeparamref name="TView"/> instaces.
        /// </summary>
        CreateRawViewDelegate<TView> CreateRawView { get; }
    }
}
