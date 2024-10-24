/********************************************************************************
* ViewRepositoryTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

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
        internal class View : ViewBase
        {
            public int Param { get; set; }

            [Event(Name = "some-event")]
            public virtual void Annotated(int param) => Param = param;

            public virtual void NotAnnotated(string param) { }

            public override bool IsValid => base.IsValid && Param > 0;
        }

        [Test]
        public void Materialize_ShouldReplayEvents()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "some-event", DateTime.UtcNow, "[1986]")]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            repo.Materialize("flowId", out View view);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [Test]
        public void Materialize_ShouldReturnViewsFromCache()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns(JsonSerializer.Instance.Serialize(new View { FlowId = "flowId", OwnerRepository = null!, Param = 1986 }));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

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
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns(JsonSerializer.Instance.Serialize(new object()));

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

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
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((string) null!);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

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
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new ViewRepository<View>(mockEventStore.Object, mockLock.Object);
            
            repo.Materialize("flowId", out View view);

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), repo.LockTimeout), Times.Once);
            mockDisposable.Verify(d => d.Dispose(), Times.Never);
        }

        [Test]
        public void Materialize_ShouldRevertTheLock()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new ViewRepository<View>(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentException>(() => repo.Materialize("flowId", out View view));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), repo.LockTimeout), Times.Once);
            mockDisposable.Verify(d => d.Dispose(), Times.Once);
        }

        [Test]
        public void Materialize_ShouldThrowOnInvalidEventId()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event("flowId", "invalid", DateTime.UtcNow, "[1986]")]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => repo.Materialize("flowId", out View view))!;
            Assert.That(ex.Message, Is.EqualTo(Format(INVALID_EVENT_ID, "invalid")));
        }

        [Test]
        public void Materialize_ShouldThrowOnNullFlowId()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentNullException>(() => repo.Materialize(null!, out View view));
        }

        [Test]
        public void Persist_ShouldThrowIsTheLockIsNotOwned()
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.IsHeld(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<InvalidOperationException>(() => repo.Persist(new View { FlowId = null!, OwnerRepository = null! }, "event", []));
        }

        [Test]
        public void Persist_ShouldBeNullSafe()
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentNullException>(() => repo.Persist(null!, "event", []));
            Assert.Throws<ArgumentNullException>(() => repo.Persist(new View { FlowId = null!, OwnerRepository = null! }, null!, []));
            Assert.Throws<ArgumentNullException>(() => repo.Persist(new View { FlowId = null!, OwnerRepository = null! }, "event", null!));
        }

        [Test]
        public void Persist_ShouldCacheTheActualStateAndStoreTheEvent()
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.IsHeld("flowId", It.IsAny<string>()))
                .Returns(true);
            
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.SetEvent(It.Is<Event>(evt => evt.EventId == "some-event" && evt.FlowId == "flowId" && evt.Arguments == "[]")));
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            View view = new() { FlowId = "flowId", OwnerRepository = null!, Param = 1986 };

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            
            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            mockCache
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view), repo.CacheEntryExpiration, DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);

            Assert.DoesNotThrow(() => repo.Persist(view, "some-event", []));

            mockLock.Verify(l => l.IsHeld(It.IsAny<string>(), It.IsAny<string>()));
            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
        }

        [Test]
        public void Persist_ShouldRevertTheCacheOnDbError()
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.IsHeld("flowId", It.IsAny<string>()))
                .Returns(true);

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.SetEvent(It.IsAny<Event>()))
                .Throws(new InvalidOperationException("cica"));
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            View view = new() { FlowId = "flowId", OwnerRepository = null!, Param = 1986 };

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            mockCache
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view), repo.CacheEntryExpiration, DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);
            mockCache
                .Setup(c => c.Remove("flowId"))
                .Returns(true);

            Exception ex = Assert.Throws<InvalidOperationException>(() => repo.Persist(view, "some-event", []))!;
            Assert.That(ex.Message, Is.EqualTo("cica"));

            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove("flowId"), Times.Once);
        }

        [Test]
        public void Create_ShouldCreateANewView([Values(null, "a35a0d2d-b316-4240-b573-0a1d39c2daef")] string? flowId)
        {
            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents(It.IsAny<string>()))
                .Returns([]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            repo.Create(flowId, out View view);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.OwnerRepository, Is.EqualTo(repo));
            Assert.That(Guid.TryParse(view.FlowId, out _));

            mockLock.Verify(l => l.Acquire(view.FlowId, It.IsAny<string>(), repo.LockTimeout));
            mockEventStore.Verify(s => s.QueryEvents(view.FlowId));
        }

        [Test]
        public void Create_ShouldThrowOnExistingFlowId()
        {
            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("existing"))
                .Returns([new Event("existing", "some-event", DateTime.UtcNow, "[]")]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => repo.Create("existing", out View view))!;
            Assert.That(ex.Message, Does.StartWith(Format(FLOW_ID_ALREADY_EXISTS, "existing")));
        }

        [Test]
        public void Ctor_ShouldInitTheDatabase()
        {
            Mock<IDisposable> mockDisposable = new(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.Acquire(ViewRepository<View>.SCHEMA_INIT_LOCK_NAME, It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(mockDisposable.Object);

            bool schemaInitialized = false;

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(() => schemaInitialized);
            mockEventStore
                .Setup(s => s.InitSchema())
                .Callback(() => schemaInitialized = true);

             _ = new ViewRepository<View>(mockEventStore.Object, mockLock.Object);

            Assert.That(schemaInitialized, Is.True);
            mockLock.Verify(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
        }
    }
}
