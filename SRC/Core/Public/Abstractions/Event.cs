/********************************************************************************
* Event.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Describes an event on database level.
    /// </summary>
    public sealed record Event(string FlowId, string EventId, DateTime CreatedUtc, string Arguments)
    {
    }
}
