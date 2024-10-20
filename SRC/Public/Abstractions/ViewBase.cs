/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json.Serialization;

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
        public string FlowId { get; internal set; } = null!;

        /// <summary>
        /// The <see cref="IViewRepositoryWriter"/> that owns this view.
        /// </summary>
        [JsonIgnore]
        public IViewRepositoryWriter OwnerRepository { get; internal set; } = null!;
    }
}
