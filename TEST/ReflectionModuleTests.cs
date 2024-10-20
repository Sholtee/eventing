/********************************************************************************
* ReflectionModuleTests.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;

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

        public interface IView
        {
            void Annotated(int param);

            void NotAnnotated(string param);
        }

        public class View : ViewBase, IView
        {
            [Event(Name = "some-event")]
            public virtual void Annotated(int param) { }

            public virtual void NotAnnotated(string param) { }
        }

        [Test]
        public void CreateInterceptorFactory_ShouldCreateAFactoryFunction()
        {
            Mock<IUntypedViewRepository> mockEventRepo = new(MockBehavior.Strict);

            View impl = new()
            {
                FlowId = "id",
                OwnerRepository = mockEventRepo.Object
            };

            mockEventRepo.Setup(r => r.Persist(impl, "some-event", new object[] { 1 }));

            Func<View, IView> fact = ReflectionModule.CreateInterceptorFactory<View, IView>();
            IView view = fact(impl);

            view.Annotated(1);

            mockEventRepo.Verify(r => r.Persist(impl, "some-event", new object[] { 1 }), Times.Once);
        }

        [Test]
        public void CreateEventProcessorsDict_ShouldCreateAProcessorFunctionForEachEvent()
        {
            Mock<View> mockView = new(MockBehavior.Strict);
            mockView.Setup(v => v.Annotated(1986));

            IReadOnlyDictionary<string, Action<View, string, JsonSerializerOptions>> dict = ReflectionModule.CreateEventProcessorsDict<View>();

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict, Does.ContainKey("some-event"));

            dict["some-event"](mockView.Object, "[1986]", JsonSerializerOptions.Default);

            mockView.Verify(v => v.Annotated(1986), Times.Once);
        }

        public class ViewHavingDuplicateEvent : ViewBase, IView
        {
            [Event(Name = "some-event")]
            public void Annotated(int param) { }

            [Event(Name = "some-event")]
            public void NotAnnotated(string param) { }
        }

        [Test]
        public void CreateEventProcessorsDict_ShouldThrowOnDuplicateEvent()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ReflectionModule.CreateEventProcessorsDict<ViewHavingDuplicateEvent>())!;
            Assert.That(ex.Message, Is.EqualTo(string.Format(Resources.DUPLICATE_EVENT_ID, "some-event")));
        }
    }
}
