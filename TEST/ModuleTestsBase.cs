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
        #pragma warning disable NUnit1032
        private ICompositeService FService;
        #pragma warning restore NUnit1032

        [OneTimeSetUp]
        public virtual void SetupFixture()
        {
            FService = new Builder()
                .UseContainer()
                .UseCompose()
                .FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infra", "test-db.yml"))
                .RemoveOrphans()
                .WaitForPort("redis-local", "6379/tcp")
                //.WaitForHttp("dynamodb-local", "http://localhost:8000")
                .Build()
                .Start();
        }

        [OneTimeTearDown]
        public virtual void TearDownFixture()
        {
            //
            // Reuse the stack in the cloud
            //

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
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
