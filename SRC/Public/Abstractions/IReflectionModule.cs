/********************************************************************************
* IReflectionModule.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Contract of reflection related dependencies.
    /// </summary>
    public interface IReflectionModule<TView> where TView : ViewBase, new()
    {
        /// <summary>
        /// Event processors belonging to the given <typeparamref name="TView"/>.
        /// </summary>
        IReadOnlyDictionary<string, Action<TView, string, ISerializer>> EventProcessors { get; }

        /// <summary>
        /// Function to crate <typeparamref name="TView"/> instaces.
        /// </summary>
        Func<string, IViewRepository<TView>, TView> CreateRawView { get; }
    }
}
