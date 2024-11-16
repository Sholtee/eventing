/********************************************************************************
* ViewBaseTests.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    using Properties;

    [TestFixture]
    public class ViewBaseTests
    {
        public class TestView(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
        }

        [GeneratedCode("SomeTool", "SomeVersion")]
        public class TestViewProxy(string flowId, IViewRepository ownerRepository) : TestView(flowId, ownerRepository)
        {
        }

        [Test]
        public async Task FromDict_ShouldBeNullChecked()
        {
            await using TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);

            Assert.Throws<ArgumentNullException>(() => view.FromDict(null!));
        }

        public static IEnumerable<object?[]> FromDict_ShouldVerifyTheFlowId_Paramz
        {
            get
            {
                yield return [new Dictionary<string, object?> { { "FlowId", "flowId" }, { "Tag", null } }, true];
                yield return [new Dictionary<string, object?> { { "FlowId", "otherId" }, { "Tag", null } }, false];
                yield return [new Dictionary<string, object?> { { "FlowId", 1986 }, { "Tag", null } }, false];
                yield return [new Dictionary<string, object?> { { "FlowId", null }, { "Tag", null } }, false];
                yield return [new Dictionary<string, object?> { { "Tag", null } }, false];
            }
        }

        [TestCaseSource(nameof(FromDict_ShouldVerifyTheFlowId_Paramz))]
        public async Task FromDict_ShouldVerifyTheFlowId(IDictionary<string, object?> dict, bool expected)
        {
            await using TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);

            Assert.That(view.FromDict(dict), Is.EqualTo(expected));
        }

        [Test]
        public async Task FromDict_ShouldSetTheTag([Values(true, false)] bool available)
        {
            await using TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);

            Dictionary<string, object?> d = new() { { "FlowId", "flowId" } };
            if (available)
                d["Tag"] = "tag";

            Assert.That(view.FromDict(d), Is.EqualTo(available));
            Assert.That(view.Tag, Is.EqualTo(available ? "tag" : null));
        }

        [Test]
        public async Task ToDict_ShouldConvertTheView()
        {
            await using TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);

            Assert.That(view.ToDict(), Is.EquivalentTo(new Dictionary<string, object?> { { "FlowId", "flowId" }, { "Tag", null } }));
        }

        public static IEnumerable<Func<string, IViewRepository<TestView>, TestView>> TestViewFactories
        {
            get
            {
                yield return (flowId, repo) => new TestView(flowId,repo);
                yield return (flowId, repo) => new TestViewProxy(flowId, repo);
            }
        }

        [Test]
        public async Task Initialize_ShouldSetTheTag([ValueSource(nameof(TestViewFactories))] Func<string, IViewRepository<TestView>, TestView> factory)
        {
            await using TestView view = factory("flowId", new Mock<IViewRepository<TestView>>(MockBehavior.Loose).Object);

            Assert.DoesNotThrow(() => view.Initialize(typeof(TestView).FullName!, "tag"));
            Assert.That(view.Tag, Is.EqualTo("tag"));
        }

        [Test]
        public async Task Initialize_ShouldThrowOnTypeNameMismatch([ValueSource(nameof(TestViewFactories))] Func<string, IViewRepository<TestView>, TestView> factory)
        {
            await using TestView view = factory("flowId", new Mock<IViewRepository<TestView>>(MockBehavior.Loose).Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => view.Initialize("invalid", "tag"))!;
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_VIEW_TYPE_NOT_MATCH));
        }

        [Test]
        public async Task Initialize_ShouldBeNullChecked()
        {
            await using TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);
            Assert.Throws<ArgumentNullException>(() => view.Initialize(null!, "tag"));
        }

        [Test]
        public void Ctor_ShouldBeNullChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new TestView(null!, new Mock<IViewRepository>(MockBehavior.Loose).Object));
            Assert.Throws<ArgumentNullException>(() => new TestView("flowId", null!));
        }

        [Test]
        public async Task DisposeAsync_ShouldCloseTheView([Values(1, 5)] int callCount)
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Strict);
            mockRepo
                .Setup(r => r.Close("flowId"))
                .Returns(Task.CompletedTask);

            TestView view = new("flowId", mockRepo.Object);
            for (int i = 0; i < callCount; i++)
                await view.DisposeAsync();

            Assert.That(view.Disposed, Is.True);
            mockRepo.Verify(r => r.Close("flowId"), Times.Once);
        }

        [Test]
        public async Task CheckDisposed_ShouldThrowIfTheViewHadBeenDisposed()
        {
            TestView view = new("flowId", new Mock<IViewRepository>(MockBehavior.Loose).Object);

            Assert.DoesNotThrow(view.CheckDisposed);
            await view.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(view.CheckDisposed);
        }
    }
}
