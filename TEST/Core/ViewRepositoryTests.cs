/********************************************************************************
* ViewRepositoryTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

using static System.String;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    using static Internals.EventIds;
    using static Properties.Resources;

    [TestFixture]
    public class ViewRepositoryTests
    {
        public class View(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
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

        public static IEnumerable<(EventStoreFeatures, int)> Materialize_ShouldReplayEvents_Order
        {
            get
            {
                yield return (EventStoreFeatures.OrderedQueries, 1991);
                yield return (EventStoreFeatures.None, 1986);
            }
        }

        [Test]
        public void Materialize_ShouldReplayEvents([ValueSource(nameof(MaterializeFns))] Func<ViewRepository<View>, string, View> materialize, [ValueSource(nameof(Materialize_ShouldReplayEvents_Order))] (EventStoreFeatures Features, int Expected) order, [Values(true, false)] bool hasLogger)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            MockSequence seq = new();
            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.REPLAY_EVENTS, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_REPLAY_EVENTS, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
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
                .Returns(order.Features);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.PROCESSED_EVENTS, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_EVENTS_PROCESSED, 2, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, logger: mockLogger?.Object);

            using View view = materialize(repo, "flowId");

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(order.Expected));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents("flowId"), Times.Once);
        }

        [Test]
        public void Materialize_ShouldReturnViewsFromCache([ValueSource(nameof(MaterializeFns))] Func<ViewRepository<View>, string, View> materialize, [Values(true, false)] bool hasLogger)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            MockSequence seq = new();
            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("flowId"))
                .Returns<string>(static _ =>
                {
                    using View view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object)
                    {
                        Param = 1986
                    };
                    return JsonSerializer.Instance.Serialize(view.ToDict());
                });
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.CACHE_ENTRY_FOUND, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_CACHE_ENTRY_FOUND, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object, logger: mockLogger?.Object);

            using View view = materialize(repo, "flowId");

            Assert.That(view, Is.Not.Null);
            Assert.That(view.FlowId, Is.EqualTo("flowId"));
            Assert.That(view.Param, Is.EqualTo(1986));

            mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
            mockCache.Verify(c => c.Get("flowId"), Times.Once);
            mockEventStore.Verify(s => s.QueryEvents(It.IsAny<string>()), Times.Never);
        }

        public static IEnumerable<object?> Materialize_ShouldThrowOnLayoutMismatch_InvalidCacheValues
        {
            get
            {
                yield return null;
                yield return 1986;
                yield return new object();
            }
        }

        [Test]
        public void Materialize_ShouldThrowOnLayoutMismatch([ValueSource(nameof(MaterializeFns))] Func<ViewRepository<View>, string, View> materialize, [ValueSource(nameof(Materialize_ShouldThrowOnLayoutMismatch_InvalidCacheValues))] object? invalidCacheValue, [Values(true, false)] bool hasLogger)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);
            Mock<IDistributedCache> mockCache = new(MockBehavior.Strict);
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;

            MockSequence seq = new();

            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire("flowId", It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockCache
                .InSequence(seq)
                .Setup(c => c.Get("flowId"))
                .Returns(JsonSerializer.Instance.Serialize(invalidCacheValue));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.CACHE_ENTRY_FOUND, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_CACHE_ENTRY_FOUND, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Error, Error.CANNOT_MATERIALIZE, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_CANNOT_MATERIALIZE, "flowId", ERR_LAYOUT_MISMATCH)), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache.Object, logger: mockLogger?.Object);

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
        public void Materialize_ShouldThrowOnTypeMismatch(Func<ViewRepository<View>, string, View> materialize)
        {
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .Setup(s => s.QueryEvents("flowId"))
                .Returns([new Event { FlowId = "flowId", CreatedUtc = DateTime.UtcNow, EventId = "@init-view", Arguments = "[\"another-view-type-name\", null]" }]);
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

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => materialize(repo, "flowId"));
            Assert.That(ex.Message, Is.EqualTo(Abstractions.Properties.Resources.ERR_VIEW_TYPE_NOT_MATCH));
        }

        [Test]
        public void Materialize_ShouldLock([ValueSource(nameof(MaterializeFns))] Func<ViewRepository<View>, string, View> materialize, [Values(10000)] int lockTimeOut)
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

            TimeSpan timeout = TimeSpan.FromMilliseconds(lockTimeOut);

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire("flowId", It.IsAny<string>(), timeout));
            mockLock.Setup(l => l.Release("flowId", It.IsAny<string>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object)
            {
                LockTimeout = timeout
            };

            using (View view = materialize(repo, "flowId"))
            {
                mockLock.Verify(l => l.Acquire("flowId", It.IsAny<string>(), timeout), Times.Once);
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
        public void Persist_ShouldThrowIfTheLockIsNotOwned(Action<ViewRepository<View>, View, string, object?[]> persist)
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

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => persist(repo, new View("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object), "event", []));
            Assert.That(ex.Message, Is.EqualTo(ERR_NO_LOCK));
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
            Assert.Throws<ArgumentNullException>(() => persist(repo, new View("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object), null!, []));
            Assert.Throws<ArgumentNullException>(() => persist(repo, new View("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object), "event", null!));
        }

        [Test]
        public void Persist_ShouldStoreTheEvent([ValueSource(nameof(PersistFns))] Action<ViewRepository<View>, View, string, object?[]> persist, [Values(null, 10000)] int? cacheExpiration, [Values(true, false)] bool hasLogger)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock
                .Setup(l => l.IsHeld("flowId", It.IsAny<string>()))
                .Returns(true);
            
            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            using View view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object)
            {
                Param = 1986
            };

            Mock<IDistributedCache>? mockCache = cacheExpiration.HasValue ? new(MockBehavior.Strict) : null;
            
            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache?.Object, logger: mockLogger?.Object);
            if (cacheExpiration.HasValue)
                repo.CacheEntryExpiration = TimeSpan.FromMilliseconds(cacheExpiration.Value);

            MockSequence seq = new();

            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.UPDATE_CACHE, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_UPDATE_CACHE, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockCache?
                .InSequence(seq)
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view.ToDict()), TimeSpan.FromMilliseconds(cacheExpiration!.Value), DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.INSERT_EVENT, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_INSERT_EVENT, "some-event", "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockEventStore
                .InSequence(seq)
                .Setup(s => s.SetEvent(It.Is<Event>(evt => evt.EventId == "some-event" && evt.FlowId == "flowId" && evt.Arguments == "[]")));

            Assert.DoesNotThrow(() => persist(repo, view, "some-event", []));

            mockLock.Verify(l => l.IsHeld(It.IsAny<string>(), It.IsAny<string>()));
            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache?.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
        }

        [Test]
        public void Persist_ShouldRevertTheCacheOnDbError([ValueSource(nameof(PersistFns))] Action<ViewRepository<View>, View, string, object?[]> persist, [Values(true, false)] bool hasCache, [Values(true, false)] bool hasLogger)
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

            using View view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object)
            {
                Param = 1986
            };

            Mock<IDistributedCache>? mockCache = hasCache ? new(MockBehavior.Strict) : null;

            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLogger?
                .Setup(l => l.Log(LogLevel.Information, Info.UPDATE_CACHE, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_UPDATE_CACHE, "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLogger?
                .Setup(l => l.Log(LogLevel.Information, Info.INSERT_EVENT, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_INSERT_EVENT, "some-event", "flowId")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, cache: mockCache?.Object, logger: mockLogger?.Object);

            MockSequence seq = new();

            mockCache?
                .InSequence(seq)
                .Setup
                (
                    c => c.Set("flowId", JsonSerializer.Instance.Serialize(view.ToDict()), repo.CacheEntryExpiration, DistributedCacheInsertionFlags.AllowOverwrite)
                )
                .Returns(true);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Error, Error.EVENT_NOT_SAVED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_EVENT_NOT_SAVED, "some-event", "flowId", "cica")), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockCache?
                .InSequence(seq)
                .Setup(c => c.Remove("flowId"))
                .Returns(true);

            Exception ex = Assert.Throws<InvalidOperationException>(() => persist(repo, view, "some-event", []))!;
            Assert.That(ex.Message, Is.EqualTo("cica"));

            mockEventStore.Verify(s => s.SetEvent(It.IsAny<Event>()), Times.Once);
            mockCache?.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<DistributedCacheInsertionFlags>()), Times.Once);
            mockCache?.Verify(c => c.Remove("flowId"), Times.Once);
        }

        public static IEnumerable<Func<ViewRepository<View>, string?, object?, View>> CreateFns
        {
            get
            {
                yield return (repo, flowId, tag) => (View) ((IViewRepository) repo).Create(flowId, tag);
                yield return (repo, flowId, tag) => ((IViewRepository<View>) repo).Create(flowId, tag);
            }
        }

        [Test]
        public void Create_ShouldCreateANewView([Values(null, "a35a0d2d-b316-4240-b573-0a1d39c2daef")] string? flowId, [ValueSource(nameof(CreateFns))] Func<ViewRepository<View>, string?, object?, View> create, [Values(true, false)] bool hasLogger, [Values("tag", null!)] object tag)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, logger: mockLogger?.Object);

            MockSequence seq = new();

            mockLock
                .InSequence(seq)
                .Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockEventStore
                .InSequence(seq)
                .Setup(s => s.QueryEvents(It.IsAny<string>()))
                .Returns([]);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.CREATE_RAW_VIEW, It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release(It.IsAny<string>(), It.IsAny<string>()));

            using View view = create(repo, flowId, tag);

            Assert.That(view, Is.Not.Null);
            Assert.That(view.OwnerRepository, Is.EqualTo(repo));
            Assert.That(Guid.TryParse(view.FlowId, out _));
            Assert.That(view.Tag, Is.EqualTo(tag));

            mockLock.Verify(l => l.Acquire(view.FlowId, It.IsAny<string>(), repo.LockTimeout));
            mockEventStore.Verify(s => s.QueryEvents(view.FlowId));
        }

        [Test]
        public void Create_ShouldThrowOnExistingFlowId([ValueSource(nameof(CreateFns))] Func<ViewRepository<View>, string, object?, View> create, [Values(true, false)] bool hasLogger)
        {
            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);
            mockLock.Setup(l => l.Acquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()));
            mockLock.Setup(l => l.Release(It.IsAny<string>(), It.IsAny<string>()));

            Mock<IEventStore> mockEventStore = new(MockBehavior.Strict);
            mockEventStore
                .SetupGet(s => s.SchemaInitialized)
                .Returns(true);

            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            ViewRepository<View> repo = new(mockEventStore.Object, mockLock.Object, logger: mockLogger?.Object);

            MockSequence seq = new();

            mockEventStore
                .InSequence(seq)
                .Setup(s => s.QueryEvents("existing"))
                .Returns([new Event { FlowId = "existing", EventId = "some-event", Arguments = "[]" }]);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Error, Error.CANNOT_CREATE_RAW_VIEW, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == Format(LOG_CANNOT_CREATE_RAW_VIEW, "existing", new ArgumentException(ERR_FLOW_ID_ALREADY_EXISTS, "flowId").Message)), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            ArgumentException ex = Assert.Throws<ArgumentException>(() => create(repo, "existing", null))!;
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
        public void Ctor_ShouldInitTheDatabase([Values(true, false)] bool hasLogger)
        {
            MockSequence seq = new();

            Mock<IDistributedLock> mockLock = new(MockBehavior.Strict);

            Mock<ILogger<ViewRepository<View>>>? mockLogger = hasLogger ? new(MockBehavior.Strict) : null;
            mockLogger?
                .Setup(l => l.Log(LogLevel.Warning, Warning.CACHING_DISABLED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_CACHING_DISABLED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

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
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.INIT_SCHEMA, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_INIT_SCHEMA), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockEventStore
                .InSequence(seq)
                .Setup(s => s.InitSchema())
                .Callback(() => schemaInitialized = true);
            mockLogger?
                .InSequence(seq)
                .Setup(l => l.Log(LogLevel.Information, Info.SCHEMA_INITIALIZED, It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == LOG_SCHEMA_INITIALIZED), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            mockLock
                .InSequence(seq)
                .Setup(l => l.Release(ViewRepository<View>.SCHEMA_INIT_LOCK_NAME, It.IsAny<string>()));

            _ = new ViewRepository<View>(mockEventStore.Object, mockLock.Object, logger: mockLogger?.Object);

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