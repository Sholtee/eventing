/********************************************************************************
* ReflectionModuleTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Abstractions.Tests;

    using static Properties.Resources;

    [TestFixture]
    public class ReflectionModuleTests : IReflectionModuleTests
    {
        protected override IReflectionModule<TView> CreateInstance<TView>() => ReflectionModule<TView>.Instance;

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
            public void Annotated(int param) => _ = this;
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
