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

            Mock<ISerializer> mockSerializer = new Mock<ISerializer>(MockBehavior.Strict);
            mockSerializer
                .Setup(s => s.Deserialize("[1986]", new[] { typeof(int) }))
                .Returns([1986]);
            IReadOnlyDictionary<string, ProcessEventDelegate<View>> dict = CreateInstance<View>().EventProcessors;

            Assert.That(dict.Count, Is.EqualTo(2));
            Assert.That(dict, Does.ContainKey("@init-view"));
            Assert.That(dict, Does.ContainKey("some-event"));

            dict["some-event"](mockView.Object, "[1986]", mockSerializer.Object);

            mockView.Verify(v => v.Annotated(It.IsAny<int>()), Times.Once);
            mockSerializer.Verify(s => s.Deserialize(It.IsAny<string>(), It.IsAny<IReadOnlyList<Type>>()), Times.Once);
        }
    }
}
