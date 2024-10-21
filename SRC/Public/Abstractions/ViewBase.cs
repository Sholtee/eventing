/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Runtime.Serialization;

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
        public string FlowId { get; init; } = null!;

        /// <summary>
        /// The <see cref="IViewRepositoryWriter"/> that owns this view.
        /// </summary>
        [IgnoreDataMember] // do not use [JsonIgnore] here as we want a generic way to ignore properties
        public IViewRepositoryWriter OwnerRepository { get; internal set; } = null!;

        /// <summary>
        /// Determines if the current view is valid
        /// </summary>
        public virtual bool IsValid() => !string.IsNullOrWhiteSpace(FlowId);
    }
}
