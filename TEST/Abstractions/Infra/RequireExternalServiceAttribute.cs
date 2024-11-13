/********************************************************************************
* RequireExternalServiceAttribute.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;

using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    [AttributeUsage(AttributeTargets.Class)]
    public abstract class RequireExternalServiceAttribute(string image, int exposePort, string name) : Attribute, ITestAction
    {
        private IContainerService? FService;

        protected virtual ContainerBuilder TweakBuild(ContainerBuilder builder) => builder;

        protected virtual void SetupTest() { }

        protected virtual void TearDownTest() { }

        protected abstract bool TryConnect(object fixture);

        protected abstract void CloseConnection();

        public int RetryCount { get; init; } = 10;

        ActionTargets ITestAction.Targets { get; } = ActionTargets.Test | ActionTargets.Suite;

        void ITestAction.BeforeTest(ITest test)
        {
            if (test.IsSuite)
            {
                FService = TweakBuild
                (
                    new Builder()
                        .UseContainer()
                        .UseImage(image)
                        .WithName(name)
                        .ExposePort(exposePort, exposePort)
                ).Build().Start();

                for (int i = 0; i < RetryCount; i++)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(i * 5));

                    if (TryConnect(test.Fixture!))
                        return;
                }

                throw new TimeoutException($"Failed to start: {image}");
            }
            else SetupTest();
        }

        void ITestAction.AfterTest(ITest test)
        {
            if (test.IsSuite)
            {
                CloseConnection();

                FService?.Dispose();
                FService = null!;
            }
            else TearDownTest();
        }
    }
}
