/********************************************************************************
* ViewRepositoryTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

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
        public class View : ViewBase
        {
            public int Param { get; set; }

            [Event(Id = "some-event")]
            public virtual void Annotated(int param) => Param = param;

            public virtual void NotAnnotated(string param) { }

            public override IDictionary<string, object?> ToDict()
            {
                IDictionary<string, object?> dict = base.ToDict();
                dict.Add(nameof(Param), Param);
                return dict;
            }

            public override bool FromDict(IDictionary<string, object?> dict)
            {
                if (base.FromDict(dict) && dict.TryGetValue(nameof(Param), out object? param) && param is int intParam)
                {
                    Param = intParam;
                    return true;
                }
                return false;
            }
        }

        public static IEnumerable<Func<ViewRepository<View>, string, View>> MaterializeFns
        {
            get
            {
                yield return (repo, flowId) => (View) ((IViewRepository) repo).Materialize(flowId);
                yield return (repo, flowId) => ((IViewRepository<View>) repo).Materialize(flowId);
            }
        }

        public static IEnumerable<object?[]> Materialize_ShouldReplayEvents_Paramz
        {
            get
            {
                foreach (Func<ViewRepository<View>, string, View> materialize in MaterializeFns)
                {
                    yield return [materialize, EventStoreFeatures.OrderedQueries, 1991];
                    yield return [materialize, EventStoreFeatures.None, 1986];
                }
            }
        }

        [TestCaseSource(nameof(Materialize_ShouldReplayEvents_Paramz))]
        public void Materialize_ShouldReplayEvents(Func<ViewRepository<View>, string, View> materialize, EventStoreFeatures features, int expected)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            MockSequence seq = new();
            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockEventStore
                .InSequence(seq)
                .Setup(s => s.QueryEvents("flowId"))
                .Returns(
                [
                    new Event { FlowId = "flowId", CreatedUtc = DateTime.UtcNow, EventId = "some-event", Arguments = "[1986]" },
                    new Event { FlowId = "flowId", CreatedUtc = DateTime.UtcNow.AddDays(-1), EventId = "some-event", Arguments = "[1991]" }
                ]);
            mockEventStore
                .InSequence(seq)
                .SetupGet(s => s.Features)
                .Returns(features);
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            using View view = materialize(repo, "flowId");

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(expected));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldReturnViewsFromCache(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            MockSequence seq = new();
            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("flowId"))
                .Returns<string>(static _ =>
                {
                    using View view = new()
                    {
                        FlowId = "flowId",
                        OwnerRepository = new Mock<IViewRepository>(MockBehavior.Loose).Object,
                        Param = 1986
                    };
                    return JsonSerializer.Instance.Serialize(view.ToDict());
                });
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            using View view = materialize(repo, "flowId");

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents(It.IsAny<string>()), Times.Never);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldThrowOnLayoutMismatch(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            MockSequence seq = new();

            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("flowId"))
                .Returns(JsonSerializer.Instance.Serialize(new object()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => materialize(repo, "flowId"));
            Assert.That(ex.Message, Is.EqualTo(ERR_LAYOUT_MISMATCH));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents(It.IsAny<string>()), Times.Never);
            mockLock.Verify(l => l.Release("flowId", It.IsAny<string>()), Times.Once);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldThrowOnInvalidFlowId(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            mockEventStore
                .SetupGet(s => s.Features)
                .Returns(EventStoreFeatures.None);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            mockCache
                .Setup(c => c.Get("flowId"))
                .Returns((string) null!);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => materialize(repo, "flowId"))!;
            Assert.That(ex.Message, Does.StartWith(Format(ERR_INVALID_FLOW_ID, "flowId")));

            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldLock(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event { FlowId = "flowId", EventId = "some-event", Arguments = "[1986]" }]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            mockEventStore
                .SetupGet(s => s.Features)
                .Returns(EventStoreFeatures.None);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            using (View view = materialize(repo, "flowId"))
            {
                mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), repo.LockTimeout), Times.Once);
                mockLock.Verify(l => l.Release(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            mockLock.Verify(l => l.Release(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldRevertTheLock(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            mockEventStore
                .SetupGet(s => s.Features)
                .Returns(EventStoreFeatures.None);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentException>(() => materialize(repo, "flowId"));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), repo.LockTimeout), Times.Once);
            mockLock.Verify(l => l.Release(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldThrowOnInvalidEventId(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event { FlowId = "flowId", EventId = "invalid", Arguments = "[1986]" }]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            mockEventStore
                .SetupGet(s => s.Features)
                .Returns(EventStoreFeatures.None);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => materialize(repo, "flowId"))!;
            Assert.That(ex.Message, Is.EqualTo(Format(ERR_INVALID_EVENT_ID, "invalid")));
        }

        [TestCaseSource(nameof(MaterializeFns))]
        public void Materialize_ShouldThrowOnNullFlowId(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentNullException>(() => materialize(repo, null!));
        }

        public static IEnumerable<Action<ViewRepository<View>, View, string, object?[]>> PersistFns
        {
            get
            {
                yield return (repo, view, eventId, paramz) => ((IViewRepository) repo).Persist(view, eventId, paramz);
                yield return (repo, view, eventId, paramz) => ((IViewRepository<View>) repo).Persist(view, eventId, paramz);
            }
        }

        [TestCaseSource(nameof(PersistFns))]
        public void Persist_ShouldThrowIsTheLockIsNotOwned(Action<ViewRepository<View>, View, string, object?[]> persist)
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

            Assert.Throws<InvalidOperationException>(() => persist(repo, new View { FlowId = null!, OwnerRepository = null! }, "event", []));
        }

        [TestCaseSource(nameof(PersistFns))]
        public void Persist_ShouldBeNullSafe(Action<ViewRepository<View>, View, string, object?[]> persist)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            Assert.Throws<ArgumentNullException>(() => persist(repo, null!, "event", []));
            Assert.Throws<ArgumentNullException>(() => persist(repo, new View { FlowId = null!, OwnerRepository = null! }, null!, []));
            Assert.Throws<ArgumentNullException>(() => persist(repo, new View { FlowId = null!, OwnerRepository = null! }, "event", null!));
        }

        [TestCaseSource(nameof(PersistFns))]
        public void Persist_ShouldCacheTheActualStateAndStoreTheEvent(Action<ViewRepository<View>, View, string, object?[]> persist)
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

            using View view = new()
            {
                FlowId = "flowId",
                OwnerRepository = new Mock<IViewRepository>(MockBehavior.Loose).Object,
                Param = 1986
            };

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            
            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            mockCache
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view.ToDict()), repo.CacheEntryExpiration, DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);

            Assert.DoesNotThrow(() => persist(repo, view, "some-event", []));

            mockLock.Verify(l => l.IsHeld(It.IsAny<string>(), It.IsAny<string>()));
            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
        }

        [TestCaseSource(nameof(PersistFns))]
        public void Persist_ShouldRevertTheCacheOnDbError(Action<ViewRepository<View>, View, string, object?[]> persist)
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

            using View view = new()
            {
                FlowId = "flowId",
                OwnerRepository = new Mock<IViewRepository>(MockBehavior.Loose).Object,
                Param = 1986
            };

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object);

            mockCache
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view.ToDict()), repo.CacheEntryExpiration, DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);
            mockCache
                .Setup(c => c.Remove("flowId"))
                .Returns(true);

            Exception ex = Assert.Throws<InvalidOperationException>(() => persist(repo, view, "some-event", []))!;
            Assert.That(ex.Message, Is.EqualTo("cica"));

            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache.Verify(c => c.Remove("flowId"), Times.Once);
        }

        public static IEnumerable<Func<ViewRepository<View>, string?, View>> CreateFns
        {
            get
            {
                yield return (repo, flowId) => (View) ((IViewRepository) repo).Create(flowId);
                yield return (repo, flowId) => ((IViewRepository<View>) repo).Create(flowId);
            }
        }


        [Test]
        public void Create_ShouldCreateANewView([Values(null, "a35a0d2d-b316-4240-b573-0a1d39c2daef")] string? flowId, [ValueSource(nameof(CreateFns))] Func<ViewRepository<View>, string?, View> create)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release(It.IsAny<string>(), It.IsAny<string>()));

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents(It.IsAny<string>()))
                .Returns([]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            using View view = create(repo, flowId);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.OwnerRepository, Is.EqualTo(repo));
            Assert.That(Guid.TryParse(view.FlowId, out _));

            mockLock.Verify(l => l.Acquire(view.FlowId, It.IsAny<string>(), repo.LockTimeout));
            mockEventStore.Verify(s => s.QueryEvents(view.FlowId));
        }

        [TestCaseSource(nameof(CreateFns))]
        public void Create_ShouldThrowOnExistingFlowId(Func<ViewRepository<View>, string, View> create)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release(It.IsAny<string>(), It.IsAny<string>()));

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("existing"))
                .Returns([new Event { FlowId = "existing", EventId = "some-event", Arguments = "[]" }]);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);

            ArgumentException ex = Assert.Throws<ArgumentException>(() => create(repo, "existing"))!;
            Assert.That(ex.Message, Does.StartWith(Format(ERR_FLOW_ID_ALREADY_EXISTS, "existing")));
        }

        [Test]
        public void Close_ShouldReleaseTheLock()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Loose);

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object);
            repo.Close("flowId");
            mockLock.Verify(l => l.Release("flowId", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void Close_ShouldBeNullChecked()
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            ViewRepository<View> repo = new(mockEventStore.Object, new Mock<IDistributedLock>(MockBehavior.Loose).Object);
            Assert.Throws<ArgumentNullException>(() => repo.Close(null!));
        }

        [Test]
        public void Ctor_ShouldInitTheDatabase()
        {
            MockSequence seq = new();

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            bool schemaInitialized = false;

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .InSequence(seq)
                .SetupGet(s => s.SchemaInitialized)
                .Returns(() => schemaInitialized);
            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire(ViewRepository<View>.SCHEMA_INIT_LOCK_NAME, It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockEventStore
                .InSequence(seq)
                .SetupGet(s => s.SchemaInitialized)
                .Returns(() => schemaInitialized);
            mockEventStore
                .InSequence(seq)
                .Setup(s => s.InitSchema())
                .Callback(() => schemaInitialized = true);
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release(ViewRepository<View>.SCHEMA_INIT_LOCK_NAME, It.IsAny<string>()));

            _ = new ViewRepository<View>(mockEventStore.Object, mockLock.Object);

            Assert.That(schemaInitialized, Is.True);
            mockLock.Verify(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockLock.Verify(l => l.Release(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            mockEventStore.VerifyGet(s => s.SchemaInitialized, Times.Exactly(2));
        }

        [Test]
        public void Ctor_ShouldBeNullChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new ViewRepository<View>(null!, new Mock<IDistributedLock>().Object));
            Assert.Throws<ArgumentNullException>(() => new ViewRepository<View>(new Mock<IEventStore>().Object, null!));
        }
    }
}
