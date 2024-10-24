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
        private ContainerHost FContainerHost;

        public override void Setup()
        {
            FContainerHost = new ContainerHost();
            base.Setup();      
        }

        public override void Teardown()
        {
            base.Teardown();

            FContainerHost?.Dispose();
            FContainerHost = null!;
        }

        protected override IDistributedCache CreateInstance() => new RedisCache("localhost", JsonSerializer.Instance);
    }
}
