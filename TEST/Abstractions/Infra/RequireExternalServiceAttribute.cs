/********************************************************************************
* RequireExternalServiceAttribute.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    [AttributeUsage(AttributeTargets.Class)]
    public abstract class RequireExternalServiceAttribute(string image, int exposePort, params KeyValuePair<string, string>[] envVars) : Attribute, ITestAction
    {
        private IContainerService? FService;

        public virtual void SetupTest() { }

        public virtual void TearDownTest() { }

        public abstract bool TryConnect();

        public abstract void CloseConnection();

        public int RetryCount { get; init; } = 10;

        public ActionTargets Targets { get; } = ActionTargets.Test | ActionTargets.Suite;

        public virtual void BeforeTest(ITest test)
        {
            if (test.IsSuite)
            {
                FService = new Builder()
                    .UseContainer()
                    .UseImage(image)
                    .WithName($"test_{image.Substring(0, image.IndexOf(':'))}")
                    .ExposePort(exposePort, exposePort)
                    .WithEnvironment(envVars.Select(static envVar => $"{envVar.Key}={envVar.Value}").ToArray())
                    .Build()
                    .Start();

                for (int i = 0; i < RetryCount; i++)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(i * 5));

                    if (TryConnect())
                        return;
                }

                throw new TimeoutException($"Failed to start: {image}");
            }
            else SetupTest();
        }

        public virtual void AfterTest(ITest test)
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
