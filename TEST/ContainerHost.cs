/********************************************************************************
* ContainerHost.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;

using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Builders;

namespace Solti.Utils.Eventing.Tests
{
    public sealed class ContainerHost: IDisposable
    {
        private ICompositeService FService;

        public ContainerHost()
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

        public void Dispose()
        {
            FService?.Dispose();
            FService = null!;
        }
    }
}
