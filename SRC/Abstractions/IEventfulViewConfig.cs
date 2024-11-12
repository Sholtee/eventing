/********************************************************************************
* IEventfulViewConfig.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Config interface over the view proxy
    /// </summary>
    public interface IEventfulViewConfig
    {
        /// <summary>
        /// If set to true, eventized methods wont be intercepted.
        /// </summary>
        /// <remarks>Don't change the value of this property unless you know what you are doing.</remarks>
        bool EventingDisabled { get; set; }
    }
}
