/********************************************************************************
* ModuleTestsBase.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;

using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Builders;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    public abstract class ModuleTestsBase
    {
        private ICompositeService FService;

        [SetUp]
        public virtual void Setup()
        {
            FService = new Builder()
                .UseContainer()
                .UseCompose()
                .FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-db.yml"))
                .RemoveOrphans()
                .WaitForHttp("dynamodb-local", "http://localhost:8000")
                .Build()
                .Start();
        }

        [TearDown]
        public virtual void Teardown() => FService?.Dispose();
    }
}
