/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// The base of materialized views
    /// </summary>
    /// <remarks>A view also represents a session being carried out on a perticular flow. Therefore, disposing the view closes the underlying session too</remarks>
    public abstract class ViewBase : IDisposable
    {
        /// <summary>
        /// The unique id if this view.
        /// </summary>
        public /*required*/ string FlowId { get; init; } = null!;

        /// <summary>
        /// The repository that owns this view.
        /// </summary>
        public /*required*/ IViewRepository OwnerRepository { get; init; } = null!;

        /// <summary>
        /// Returns true if this instance has been disposed.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Setups the view actual state from the given <paramref name="dict"/>. The provided dictionary should support case insensitive queries.
        /// </summary>
        public virtual bool FromDict(IDictionary<string, object?> dict)
        {
            if (dict is null)
                throw new ArgumentNullException(nameof(dict));

            //
            // Just verify that the given flow id is valid
            //

            return dict.TryGetValue("FlowId", out object? flowId) && flowId?.Equals(FlowId) is true;
        }

        /// <summary>
        /// Converts this view to a dictionary.
        /// </summary>
        /// <remarks>The returned dictionary is case insensitive.</remarks>
        public virtual IDictionary<string, object?> ToDict() => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(FlowId), FlowId }
        };

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
