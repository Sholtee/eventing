/********************************************************************************
* ViewBase.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

namespace Solti.Utils.Eventing.Abstractions
{
    using Properties;
    using System.Threading.Tasks;

    /// <summary>
    /// The base of materialized views
    /// </summary>
    /// <remarks>A view also represents a session being carried out on a perticular flow. Therefore, disposing the view closes the underlying session too</remarks>
    public abstract class ViewBase(string flowId, IViewRepository ownerRepository) : IAsyncDisposable
    {
        /// <summary>
        /// The unique id if this view.
        /// </summary>
        public string FlowId { get; } = flowId ?? throw new ArgumentNullException(nameof(flowId));

        /// <summary>
        /// The repository that owns this view.
        /// </summary>
        public IViewRepository OwnerRepository { get; } = ownerRepository ?? throw new ArgumentNullException(nameof(ownerRepository));

        /// <summary>
        /// Immutable user data associated with this event in initialization phase.
        /// </summary>
        public object? Tag { get; private set; }

        /// <summary>
        /// Returns true if this instance has been disposed.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Setups the view actual state from the given <paramref name="dict"/>.
        /// </summary>
        public virtual bool FromDict(IDictionary<string, object?> dict)
        {
            if (dict is null)
                throw new ArgumentNullException(nameof(dict));

            //
            // Just verify that the given flow id is valid
            //

            if (!dict.TryGetValue(nameof(FlowId), out object? flowId) || flowId?.Equals(FlowId) is not true)
                return false;

            //
            // Grab the user data
            //

            if (!dict.TryGetValue(nameof(Tag), out object? tag))
                return false;

            Tag = tag;
            return true;
        }

        /// <summary>
        /// Converts this view to a dictionary.
        /// </summary>
        public virtual IDictionary<string, object?> ToDict() => new Dictionary<string, object?>
        {
            { nameof(FlowId), FlowId },
            { nameof(Tag), Tag },
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
        /// Method to initialize the view.
        /// </summary>
        [Event(Id = "@init-view")]
        internal virtual void Initialize(string classNameOfThis, object? tag)
        {
            if (classNameOfThis is null)
                throw new ArgumentNullException(nameof(classNameOfThis));

            //
            // Skip the proxy types
            //

            Type t;
            for (t = GetType(); t.GetCustomAttribute<GeneratedCodeAttribute>() is not null; t = t.BaseType) ;

            //
            // Validate whether the flow id matches the view
            //

            if (classNameOfThis != t.FullName)
                throw new InvalidOperationException(Resources.ERR_VIEW_TYPE_NOT_MATCH);

            Tag = tag;
        }

        /// <summary>
        /// Disposes this view asynchronously by releasing the lock on it
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {
            if (!Disposed)
            {
                await OwnerRepository.Close(FlowId);
                Disposed = true;
            }
        }
    }
}
