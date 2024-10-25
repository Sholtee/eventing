/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.Serialization;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// The base of materialized views
    /// </summary>
    /// <remarks>A view also represents a session being carried out on a perticular flow. Therefore, disposing the view closes the underlying session too</remarks>
    public abstract class ViewBase: IDisposable
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
        public /*required*/ IViewRepository OwnerRepository { get; init; } = null!;

        /// <summary>
        /// If set to true, eventized methods wont be intercepted.
        /// </summary>
        /// <remarks>Don't change the value of this property unless you know what you are doing. This member should not be serialized.</remarks>
        [IgnoreDataMember]
        public bool EventingDisabled { get; set; }

        /// <summary>
        /// Determines if the current view is valid
        /// </summary>
        /// <remarks>This member should not be serialized.</remarks>
        [IgnoreDataMember]
        public virtual bool IsValid => !string.IsNullOrWhiteSpace(FlowId);

        /// <summary>
        /// Returns true if this instance has been disposed.
        /// </summary>
        /// <remarks>This member should not be serialized.</remarks>
        [IgnoreDataMember]
        public bool Disposed { get; private set; }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the instance had already been disposed.
        /// </summary>
        public void CheckDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Disposes this view by releasing the lock on it
        /// </summary>
        public virtual void Dispose()
        {
            if (!Disposed)
            {
                OwnerRepository.Close(FlowId);
                Disposed = true;
            }
        }
    }
}
