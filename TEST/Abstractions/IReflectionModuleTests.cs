/********************************************************************************
* IReflectionModuleTests.cs                                                     *
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

    public abstract class IReflectionModuleTests
    {
        protected internal class View : ViewBase
        {
            [Event(Name = "some-event")]
            public virtual string Annotated(int param) => "cica";
            public virtual void NotAnnotated(string param) { }
        }

        protected abstract IReflectionModule<TView> CreateInstance<TView>() where TView: ViewBase, new();

        [Test]
        public void CreateRawView_ShouldCreateAFactoryFunction()
        {
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);

            View view = CreateInstance<View>().CreateRawView("id", mockRepo.Object);

            mockRepo.Setup(r => r.Persist((ViewBase) view, "some-event", new object[] { 1 }));

            Assert.That(view.Annotated(1), Is.EqualTo("cica"));

            mockRepo.Verify(r => r.Persist((ViewBase) view, "some-event", new object[] { 1 }), Times.Once);
        }

        [Test]
        public void Eventing_MayBeDisabled()
        {
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);

            View view = CreateInstance<View>().CreateRawView(null!, mockRepo.Object);

            view.EventingDisabled = true;
            view.Annotated(1);

            mockRepo.Verify(r => r.Persist(It.IsAny<View>(), It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Test]
        public void Views_ShouldThrowAfterDispose()
        {
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);
            mockRepo.Setup(r => r.Close("flowid"));

            View view = CreateInstance<View>().CreateRawView("flowid", mockRepo.Object);
            view.Dispose();

            Assert.Throws<ObjectDisposedException>(() => view.Annotated(123));
            Assert.Throws<ObjectDisposedException>(view.Dispose);
        }

        [Test]
        public void EventProcessors_ShouldCreateAProcessorFunctionForEachEvent()
        {
            Mock<View> mockView = new(MockBehavior.Strict);
            mockView.Setup(v => v.Annotated(1986)).Returns("cica");

            IReadOnlyDictionary<string, Action<View, string, ISerializer>> dict = CreateInstance<View>().EventProcessors;

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict, Does.ContainKey("some-event"));

            dict["some-event"](mockView.Object, "[1986]", JsonSerializer.Instance);

            mockView.Verify(v => v.Annotated(1986), Times.Once);
        }

        internal class ViewHavingDuplicateEvent : ViewBase
        {
            [Event(Name = "some-event")]
            public virtual void Annotated(int param) { }

            [Event(Name = "some-event")]
            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void Ctor_ShouldThrowOnDuplicateEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewHavingDuplicateEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(DUPLICATE_EVENT_ID, "some-event")));
        }

        internal class ViewHavingNonVirtualEvent : ViewBase
        {
            [Event(Name = "some-event")]
            public void Annotated(int param) => _ = this;
        }

        [Test]
        public void Ctor_ShouldThrowOnNonVirtualEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewHavingNonVirtualEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(NOT_VIRTUAL, nameof(ViewHavingNonVirtualEvent.Annotated))));
        }

        internal sealed class SealedView : ViewBase
        {
            [Event(Name = "some-event")]
            public void Annotated(int param)  => _ = this;
        }

        [Test]
        public void Ctor_ShouldThrowOnSealedView()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<SealedView>())!;
            Assert.That(ex.Message, Is.EqualTo(CANNOT_BE_INTERCEPTED));
        }
    }
}
