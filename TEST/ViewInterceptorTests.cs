/********************************************************************************
* ViewInterceptorTests.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Internals;
    using Proxy.Generators;

    [TestFixture]
    public class ViewInterceptorTests
    {
        public interface IView
        {
            void Annotated(int param);

            void NotAnnotated(string param);
        }

        public class View: ViewBase, IView
        {
            [Event(Name = "some-event")]
            public void Annotated(int param) {}

            public void NotAnnotated(string param){}
        }

        [Test]
        public void Interceptor_ShouldDispatchEvents()
        {
            Mock<IViewRepositoryWriter> mockEventRepo = new(MockBehavior.Strict);

            View impl = new()
            {
                FlowId = "id",
                OwnerRepository = mockEventRepo.Object
            };

            mockEventRepo.Setup(r => r.Persist(impl, "some-event", new object[] { 1986 }));

            IView view = ProxyGenerator<IView, ViewInterceptor<View, IView>>.Activate(Tuple.Create(impl));
            view.Annotated(1986);

            mockEventRepo.Verify(r => r.Persist(impl, "some-event", new object[] { 1986 }), Times.Once);
        }

        [Test]
        public void Interceptor_ShouldIgnoreNotAnnotatedMethods()
        {
            Mock<IViewRepositoryWriter> mockEventRepo = new(MockBehavior.Strict);

            View impl = new()
            {
                FlowId = "id",
                OwnerRepository = mockEventRepo.Object
            };

            IView view = ProxyGenerator<IView, ViewInterceptor<View, IView>>.Activate(Tuple.Create(impl));
            view.NotAnnotated("cica");

            mockEventRepo.Verify(r => r.Persist(It.IsAny<ViewBase>(), It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }
    }
}
