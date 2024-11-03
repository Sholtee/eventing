/********************************************************************************
* ReflectionModuleTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Abstractions.Tests;

    [TestFixture]
    public class ReflectionModuleTests : IReflectionModuleTests
    {
        protected override IReflectionModule<TView> CreateInstance<TView>() => ReflectionModule<TView>.Instance;
    }
}
