/********************************************************************************
* IEventfulView.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Interface to be implemented by the system when converting views to proxies
    /// </summary>
    public interface IEventfulView
    {
        /// <summary>
        /// If set to true, eventized methods wont be intercepted.
        /// </summary>
        /// <remarks>Don't change the value of this property unless you know what you are doing.</remarks>
        bool EventingDisabled { get; set; }
    }
}
