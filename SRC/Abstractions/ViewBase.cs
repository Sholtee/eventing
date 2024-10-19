/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// The base of materialized views
    /// </summary>
    public abstract class ViewBase
    {
        /// <summary>
        /// The unique id if this view.
        /// </summary>
        public required string FlowId { get; init; }

        /// <summary>
        /// The <see cref="IUntypedViewRepository"/> that owns this view.
        /// </summary>
        public required IUntypedViewRepository OwnerRepository { get; init; }

        /// <summary>
        /// When implemented, converts this instance to a dictionary.
        /// </summary>
        public abstract IDictionary<string, object?> AsDict();

        /// <summary>
        /// When implemented sets the state of this view according to the given <paramref name="dict"/>.
        /// </summary>
        /// <returns>Returns true if the conversation was successful, false otherwise</returns>
        public abstract bool FromDict(IDictionary<string, object?> dict);
    }
}
