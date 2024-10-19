/********************************************************************************
* ViewInterceptor.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Proxy;

    /// <summary>
    /// Dispatches view events towards the owner repository.
    /// </summary>
    public class ViewInterceptor<TView, IView>(TView view) : InterfaceInterceptor<IView, TView>(view ?? throw new ArgumentNullException(nameof(view))) where TView : ViewBase, IView, new() where IView : class
    {
        public override object? Invoke(InvocationContext context)
        {
            object? result = base.Invoke(context);

            EventAttribute? eventAttribute = context.TargetMethod.GetCustomAttribute<EventAttribute>();
            if (eventAttribute is not null)
                view.OwnerRepository.Persist(view, eventAttribute.Name, context.Args);

            return result;
        }
    }
}
