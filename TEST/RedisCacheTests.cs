/********************************************************************************
* RedisCacheTests.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    [TestFixture]
    public class RedisCacheTests: IDistributedCacheTests
    {
        private ModuleTestsBase FContainerHost;

        [OneTimeSetUp]
        public void SetupFixture()
        {
            FContainerHost = new ModuleTestsBase();
            FContainerHost.SetupFixture();
        }

        [OneTimeTearDown]
        public void TearDownFixture()
        {
            FContainerHost.TearDownFixture();
            FContainerHost = null!;
        }

        public override void SetupTest()
        {
            FContainerHost.SetupTest();
            base.SetupTest();
        }

        public override void TearDownTest()
        {
            base.TearDownTest();
            FContainerHost.TearDownTest();
        }

        protected override IDistributedCache CreateInstance() => new RedisCache("localhost", JsonSerializer.Instance);
    }
}
