/********************************************************************************
* ViewRepositoryTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;
using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Properties;

    [TestFixture]
    public class ViewRepositoryTests
    {
        public interface IView
        {
            int Param { get; }

            string FlowId { get; }

            void Annotated(int param);

            void NotAnnotated(string param);
        }

        public class View : ViewBase, IView
        {
            public int Param { get; set; }

            [Event(Name = "some-event")]
            public void Annotated(int param) => Param = param;

            public void NotAnnotated(string param) { }
        }

        [Test]
        public void Materialize_ShouldReplayEvents()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "some-event", DateTime.UtcNow, "[1986]")]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((byte[]) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out IView view);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [Test]
        public void Materialize_ShouldReturnViewsFromCache()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new View { FlowId = "flowId", Param = 1986 })));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out IView view);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Materialize_ShouldReplayEventsOnLayoutMismatch()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "some-event", DateTime.UtcNow, "[1986]")]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object())));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out IView view);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [Test]
        public void Materialize_ShouldThrowOnInvalidFlowId()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((byte[]) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => repo.Materialize("flowId", out IView view))!;
            Assert.That(ex.Message, Does.StartWith(string.Format(Resources.INVALID_FLOW_ID, "flowId")));

            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [Test]
        public void Materialize_ShouldLock()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "some-event", DateTime.UtcNow, "[1986]")]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((byte[]) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);
            
            repo.Materialize("flowId", out IView view);

            mockLock.Verify(l => l.Acquire("flowId"), Times.Once);
            mockDisposable.Verify(d => d.Dispose(), Times.Never);
        }


        [Test]
        public void Materialize_ShouldRevertTheLock()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((byte[]) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<ILock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId"))
                .Returns(mockDisposable.Object);

            IViewRepository<IView> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            Assert.Throws<ArgumentException>(() => repo.Materialize("flowId", out IView view));

            mockLock.Verify(l => l.Acquire("flowId"), Times.Once);
            mockDisposable.Verify(d => d.Dispose(), Times.Once);
        }
    }
}
