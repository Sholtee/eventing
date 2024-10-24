/********************************************************************************
* ReflectionModuleTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    [TestFixture]
    public class ReflectionModuleTests : IReflectionModuleTests
    {
        protected override IReflectionModule<TView> CreateInstance<TView>() => ReflectionModule<TView>.Instance;
    }
}
