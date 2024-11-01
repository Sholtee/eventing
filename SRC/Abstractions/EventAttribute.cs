/********************************************************************************
* EventAttribute.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Attribute to annotate interface methods with event name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class EventAttribute: Attribute
    {
        /// <summary>
        /// The id of the event
        /// </summary>
        public required string Id { get; init; }
    }
}
