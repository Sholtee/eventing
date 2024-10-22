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

using static System.String;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    using static Properties.Resources;

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

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out View view);

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
                .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Instance.Serialize(new View { FlowId = "flowId", Param = 1986 })));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out View view);

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
                .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Instance.Serialize(new object())));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            repo.Materialize("flowId", out View view);

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

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => repo.Materialize("flowId", out View view))!;
            Assert.That(ex.Message, Does.StartWith(Format(INVALID_FLOW_ID, "flowId")));

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

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);
            
            repo.Materialize("flowId", out View view);

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>()), Times.Once);
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

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            Assert.Throws<ArgumentException>(() => repo.Materialize("flowId", out View view));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>()), Times.Once);
            mockDisposable.Verify(d => d.Dispose(), Times.Once);
        }

        [Test]
        public void Materialize_ShouldThrowOnInvalidEventId()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "invalid", DateTime.UtcNow, "[1986]")]);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((byte[]) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>()))
                .Returns(mockDisposable.Object);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => repo.Materialize("flowId", out View view))!;
            Assert.That(ex.Message, Is.EqualTo(Format(INVALID_EVENT_ID, "invalid")));
        }

        [Test]
        public void Materialize_ShouldThrowOnNullFlowId()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            IViewRepository<View> repo = new ViewRepository<View, IView>(mockEventStore.Object, mockCache.Object, mockLock.Object);

            Assert.Throws<ArgumentNullException>(() => repo.Materialize(null!, out View view));
        }
    }
}
