/********************************************************************************
* ViewTests.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Abstractions.Tests;

    [TestFixture]
    public class ViewTests : ViewBaseTests
    {
        protected override TView CreateProxyView<TView>(string flowId, IViewRepository<TView> ownerRepository) =>
            ReflectionModule<TView>.Instance.CreateRawView(flowId, ownerRepository, out _);
    }
}
