/********************************************************************************
* IReflectionModuleTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    using static Eventing.Properties.Resources;

    public abstract class IReflectionModuleTests
    {
        protected internal class View(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            public Action? AnnotatedCallback { get; set; }

            [Event(Id = "some-event")]
            public virtual void Annotated(int param) => AnnotatedCallback?.Invoke();

            public virtual void NotAnnotated(string param) { }
        }

        protected abstract IReflectionModule<TView> CreateInstance<TView>() where TView: ViewBase;

        [Test]
        public void Eventing_ShouldPersistTheState()
        {
            Mock<Action> mockCallback = new(MockBehavior.Strict);
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);

            View view = CreateInstance<View>().CreateRawView("id", mockRepo.Object, out _);
            view.AnnotatedCallback = mockCallback.Object;

            MockSequence seq = new();
            mockCallback.InSequence(seq).Setup(cb => cb.Invoke());
            mockRepo
                .InSequence(seq)
                .Setup(r => r.Persist((ViewBase) view, "some-event", new object[] { 1 }))
                .Returns(Task.CompletedTask);
           
            Assert.DoesNotThrow(() => view.Annotated(1));

            mockCallback.Verify(cb => cb.Invoke(), Times.Once);
            mockRepo.Verify(r => r.Persist((ViewBase) view, "some-event", new object[] { 1 }), Times.Once);           
        }

        [Test]
        public void Eventing_MayBeDisabled()
        {
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);

            View view = CreateInstance<View>().CreateRawView("flowId", mockRepo.Object, out IEventfulViewConfig viewConfig);

            viewConfig.EventingDisabled = true;
            view.Annotated(1);

            mockRepo.Verify(r => r.Persist(It.IsAny<View>(), It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Test]
        public async Task Views_ShouldThrowAfterDispose()
        {
            Mock<IViewRepository<View>> mockRepo = new(MockBehavior.Strict);
            mockRepo
                .Setup(r => r.Close("flowid"))
                .Returns(Task.CompletedTask);

            View view = CreateInstance<View>().CreateRawView("flowid", mockRepo.Object, out _);
            await view.DisposeAsync();

            Assert.Throws<ObjectDisposedException>(() => view.Annotated(123));
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await view.DisposeAsync());
        }

        [Test]
        public async Task Views_ShouldBeAnnotatedByGeneratedCodeAttribute()
        {
            await using View view = CreateInstance<View>().CreateRawView("flowid", new Mock<IViewRepository<View>>().Object, out _);

            Assert.That(view.GetType().GetCustomAttribute<GeneratedCodeAttribute>(), Is.Not.Null);
        }

        [Test]
        public void EventProcessors_ShouldCreateAProcessorFunctionForEachEvent()
        {
            Mock<View> mockView = new(MockBehavior.Strict, "flowId", new Mock<IViewRepository>(MockBehavior.Strict).Object);
            mockView.Setup(v => v.Annotated(1986));

            IReadOnlyDictionary<string, ProcessEventDelegate<View>> dict = CreateInstance<View>().EventProcessors;

            Assert.That(dict.Count, Is.EqualTo(2));
            Assert.That(dict, Does.ContainKey("@init-view"));
            Assert.That(dict, Does.ContainKey("some-event"));

            dict["some-event"](mockView.Object, "[1986]", JsonSerializer.Instance);

            mockView.Verify(v => v.Annotated(1986), Times.Once);
        }

        internal class ViewHavingDuplicateEvent(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            [Event(Id = "some-event")]
            public virtual void Annotated(int param) { }

            [Event(Id = "some-event")]
            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void Ctor_ShouldThrowOnDuplicateEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewHavingDuplicateEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(ERR_DUPLICATE_EVENT_ID, "some-event")));
        }

        internal class ViewHavingNonVirtualEvent(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            [Event(Id = "some-event")]
            public void Annotated(int param) => _ = this;
        }

        [Test]
        public void Ctor_ShouldThrowOnNonVirtualEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewHavingNonVirtualEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(ERR_NOT_VIRTUAL, nameof(ViewHavingNonVirtualEvent.Annotated))));
        }

        internal sealed class SealedView(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            [Event(Id = "some-event")]
            public void Annotated(int param)  => _ = this;
        }

        [Test]
        public void Ctor_ShouldThrowOnSealedView()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<SealedView>())!;
            Assert.That(ex.Message, Is.EqualTo(ERR_CANNOT_BE_INTERCEPTED));
        }

        internal class ViewReturningAValue1(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            [Event(Id = "some-event")]
            public virtual string Annotated(int param) => "cica";

            [Event(Id = "other-event")]
            public virtual void Annotated(out string param) => param = "cica";
        }

        internal class ViewReturningAValue2(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
            [Event(Id = "some-event")]
            public virtual void Annotated(out string param) => param = "cica";
        }

        [Test]
        public void Ctor_ShouldThrowOnBadMethodLayout()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewReturningAValue1>().CreateRawView("flowid", new Mock<IViewRepository<ViewReturningAValue1>>(MockBehavior.Strict).Object, out _));
            Assert.That(ex.Message, Is.EqualTo(string.Format(ERR_HAS_RETVAL, nameof(ViewReturningAValue1.Annotated))));

            ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewReturningAValue2>().CreateRawView("flowid", new Mock<IViewRepository<ViewReturningAValue2>>(MockBehavior.Strict).Object, out _));
            Assert.That(ex.Message, Is.EqualTo(string.Format(ERR_HAS_RETVAL, nameof(ViewReturningAValue2.Annotated))));
        }

        internal class ViewHavingNastyCotr(string flowId, IViewRepository ownerRepository, object extra) : ViewBase(flowId, ownerRepository)
        {
            public object Extra { get; } = extra;
        }

        [Test]
        public void Ctor_ShouldThrowOnIncompatibleCtorLayout()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => CreateInstance<ViewHavingNastyCotr>().CreateRawView("flowid", new Mock<IViewRepository<ViewHavingNastyCotr>>(MockBehavior.Strict).Object, out _));
            Assert.That(ex.Message, Is.EqualTo(ERR_NO_COMPATIBLE_CTOR));
        }
    }
}
