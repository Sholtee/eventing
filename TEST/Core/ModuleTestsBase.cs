/********************************************************************************
* ModuleTestsBase.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;

using NUnit.Framework;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Builders;
using StackExchange.Redis;

namespace Solti.Utils.Eventing.Tests
{
    public class ModuleTestsBase
    {
        private ICompositeService FService;

        [OneTimeSetUp]
        public virtual void SetupFixture()
        {
            CompositeBuilder bldr = new Builder()
                .UseContainer()
                .UseCompose()
                .FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infra", "test-db.yml"))
                .RemoveOrphans()
                //.WaitForPort("dynamodb-local", "8000/tcp")
                .WaitForPort("redis-local", "6379/tcp");

            //
            // Reuse the stack in the cloud
            //

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                bldr.KeepRunning();

            FService = bldr.Build().Start();
        }

        [OneTimeTearDown]
        public virtual void TearDownFixture()
        {
            FService.Dispose();
            FService = null!;
        }

        [SetUp]
        public virtual void SetupTest()
        {
        }

        [TearDown]
        public virtual void TearDownTest()
        {
            using ConnectionMultiplexer connection = ConnectionMultiplexer.Connect("localhost,allowAdmin=true");

            foreach (IServer server in connection.GetServers())
            {
                server.FlushAllDatabases();
            }
        }
    }
}
