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
        public /*required*/ string FlowId { get; init; } = null!;

        /// <summary>
        /// The repository that owns this view.
        /// </summary>
        /// <remarks>This member should not be serialized.</remarks>
        [IgnoreDataMember] // do not use [JsonIgnore] here as we want a generic way to ignore properties
        public /*required*/ object OwnerRepository { get; init; } = null!;

        /// <summary>
        /// If set to true, eventized methods wont be intercepted.
        /// </summary>
        /// <remarks>Don't change this property unless you know what you are doing. This member should not be serialized.</remarks>
        [IgnoreDataMember]
        public bool DisableInterception { get; set; }

        /// <summary>
        /// Determines if the current view is valid
        /// </summary>
        /// <remarks>This member should not be serialized.</remarks>
        [IgnoreDataMember]
        public virtual bool IsValid => !string.IsNullOrWhiteSpace(FlowId);
    }
}
