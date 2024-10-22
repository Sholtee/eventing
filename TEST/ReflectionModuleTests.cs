/********************************************************************************
* ReflectionModuleTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Internals;
    using Properties;

    [TestFixture]
    public class ReflectionModuleTests
    {
        private static ReflectionModule ReflectionModule { get; } = new ReflectionModule();

        public class View : ViewBase
        {
            [Event(Name = "some-event")]
            public virtual string Annotated(int param) => "cica";

            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void CreateInterceptorFactory_ShouldCreateAFactoryFunction()
        {
            Mock<IViewRepositoryWriter> mockEventRepo = new(MockBehavior.Strict);
            
            Func<View> fact = ReflectionModule.CreateInterceptorFactory<View>();
            View view = fact();

            mockEventRepo.Setup(r => r.Persist(view, "some-event", new object[] { 1 }));

            view.OwnerRepository = mockEventRepo.Object;
            view.FlowId = "id";

            Assert.That(view.Annotated(1), Is.EqualTo("cica"));

            mockEventRepo.Verify(r => r.Persist(view, "some-event", new object[] { 1 }), Times.Once);
        }

        [Test]
        public void Interceptor_MayBeDisabled()
        {
            Mock<IViewRepositoryWriter> mockEventRepo = new(MockBehavior.Strict);

            Func<View> fact = ReflectionModule.CreateInterceptorFactory<View>();
            View view = fact();
            view.OwnerRepository = mockEventRepo.Object;

            view.DisableInterception = true;
            view.Annotated(1);

            mockEventRepo.Verify(r => r.Persist(It.IsAny<ViewBase>(), It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Test]
        public void CreateEventProcessorsDict_ShouldCreateAProcessorFunctionForEachEvent()
        {
            Mock<View> mockView = new(MockBehavior.Strict);
            mockView.Setup(v => v.Annotated(1986));

            IReadOnlyDictionary<string, Action<View, string, ISerializer>> dict = ReflectionModule.CreateEventProcessorsDict<View>();

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict, Does.ContainKey("some-event"));

            dict["some-event"](mockView.Object, "[1986]", JsonSerializer.Instance);

            mockView.Verify(v => v.Annotated(1986), Times.Once);
        }

        public class ViewHavingDuplicateEvent : ViewBase
        {
            [Event(Name = "some-event")]
            public virtual void Annotated(int param) { }

            [Event(Name = "some-event")]
            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void CreateEventProcessorsDict_ShouldThrowOnDuplicateEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ReflectionModule.CreateEventProcessorsDict<ViewHavingDuplicateEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.DUPLICATE_EVENT_ID, "some-event")));
        }
    }
}
