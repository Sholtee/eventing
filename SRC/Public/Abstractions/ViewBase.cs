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
        public string FlowId { get; set; } = null!;

        /// <summary>
        /// The repository that owns this view.
        /// </summary>
        [IgnoreDataMember] // do not use [JsonIgnore] here as we want a generic way to ignore properties
        public object OwnerRepository { get; internal set; } = null!;

        /// <summary>
        /// If set to true, eventized methods wont be intercepted.
        /// </summary>
        [IgnoreDataMember]
        public bool DisableInterception { get; set; }

        /// <summary>
        /// Determines if the current view is valid
        /// </summary>
        [IgnoreDataMember]
        public virtual bool IsValid => !string.IsNullOrWhiteSpace(FlowId);
    }
}
