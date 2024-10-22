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

    using static Properties.Resources;

    [TestFixture]
    public class ReflectionModuleTests
    {
        public class View : ViewBase
        {
            [Event(Name = "some-event")]
            public virtual string Annotated(int param) => "cica";

            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void ReflectionModule_ShouldCreateAFactoryFunction()
        {
            Mock<IViewRepository<View>> mockEventRepo = new(MockBehavior.Strict);

            View view = ReflectionModule<View>.Instance.CreateRawView("id", mockEventRepo.Object);

            mockEventRepo.Setup(r => r.Persist(view, "some-event", new object[] { 1 }));

            Assert.That(view.Annotated(1), Is.EqualTo("cica"));

            mockEventRepo.Verify(r => r.Persist(view, "some-event", new object[] { 1 }), Times.Once);
        }

        [Test]
        public void Interceptor_MayBeDisabled()
        {
            Mock<IViewRepository<View>> mockEventRepo = new(MockBehavior.Strict);

            View view = ReflectionModule<View>.Instance.CreateRawView(null!, mockEventRepo.Object);

            view.DisableInterception = true;
            view.Annotated(1);

            mockEventRepo.Verify(r => r.Persist(It.IsAny<View>(), It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Test]
        public void ReflectionModule_ShouldCreateAProcessorFunctionForEachEvent()
        {
            Mock<View> mockView = new(MockBehavior.Strict);
            mockView.Setup(v => v.Annotated(1986)).Returns("cica");

            IReadOnlyDictionary<string, Action<View, string, ISerializer>> dict = ReflectionModule<View>.Instance.EventProcessors;

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
        public void ReflectionModule_ShouldThrowOnDuplicateEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _ = ReflectionModule<ViewHavingDuplicateEvent>.Instance)!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(DUPLICATE_EVENT_ID, "some-event")));
        }

        public class ViewHavingNonVirtualEvent : ViewBase
        {
            [Event(Name = "some-event")]
            public void Annotated(int param) { }
        }

        [Test]
        public void ReflectionModule_ShouldThrowOnNonVirtualEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _ = ReflectionModule<ViewHavingNonVirtualEvent>.Instance)!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(NOT_VIRTUAL, nameof(ViewHavingNonVirtualEvent.Annotated))));
        }

        public sealed class SealedView : ViewBase
        {
            [Event(Name = "some-event")]
            public void Annotated(int param) { }
        }

        [Test]
        public void ReflectionModule_ShouldThrowOnSealedView()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _ = ReflectionModule<SealedView>.Instance)!;
            Assert.That(ex.Message, Is.EqualTo(CANNOT_BE_INTERCEPTED));
        }
    }
}
