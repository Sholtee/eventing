/********************************************************************************
* ViewBaseTests.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    using Properties;

    public abstract class ViewBaseTests
    {
        private static TestView CreateView(string flowId, IViewRepository<TestView> ownerRepository) => new(flowId, ownerRepository);

        internal class TestView(string flowId, IViewRepository ownerRepository) : ViewBase(flowId, ownerRepository)
        {
        }

        protected abstract TView CreateProxyView<TView>(string flowId, IViewRepository<TView> ownerRepository) where TView : ViewBase;

        [Test]
        public void FromDict_ShouldBeNullChecked()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new("flowId", mockRepo.Object);

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
        public void FromDict_ShouldVerifyTheFlowId(IDictionary<string, object?> dict, bool expected)
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new("flowId", mockRepo.Object);

            Assert.That(view.FromDict(dict), Is.EqualTo(expected));
        }

        [Test]
        public void FromDict_ShouldSetTheTag([Values(true, false)] bool available)
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new("flowId", mockRepo.Object);

            Dictionary<string, object?> d = new() { { "FlowId", "flowId" } };
            if (available)
                d["Tag"] = "tag";

            Assert.That(view.FromDict(d), Is.EqualTo(available));
            Assert.That(view.Tag, Is.EqualTo(available ? "tag" : null));
        }

        [Test]
        public void ToDict_ShouldConvertTheView()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new("flowId", mockRepo.Object);

            Assert.That(view.ToDict(), Is.EquivalentTo(new Dictionary<string, object?> { { "FlowId", "flowId" }, { "Tag", null } }));
        }

        [Test]
        public void Initialize_ShouldSetTheTag([Values(true, false)] bool requireProxy)
        {
            Func<string, IViewRepository<TestView>, TestView> factory = requireProxy ? CreateProxyView<TestView> : CreateView;

            using TestView view = factory("flowId", new Mock<IViewRepository<TestView>>(MockBehavior.Loose).Object);

            Assert.DoesNotThrow(() => view.Initialize(typeof(TestView).FullName!, "tag"));
            Assert.That(view.Tag, Is.EqualTo("tag"));
        }

        [Test]
        public void Initialize_ShouldThrowOnTypeNameMismatch([Values(true, false)] bool requireProxy)
        {
            Func<string, IViewRepository<TestView>, TestView> factory = requireProxy ? CreateProxyView<TestView> : CreateView;

            using TestView view = factory("flowId", new Mock<IViewRepository<TestView>>(MockBehavior.Loose).Object);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => view.Initialize("invalid", "tag"));
            Assert.That(ex.Message, Is.EqualTo(Resources.ERR_VIEW_TYPE_NOT_MATCH));
        }

        [Test]
        public void Dispose_ShouldCloseTheView()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Strict);
            mockRepo.Setup(r => r.Close("flowId"));

            TestView view;
            using (view = new TestView("flowId", mockRepo.Object)) { }

            Assert.That(view.Disposed, Is.True);
            mockRepo.Verify(r => r.Close("flowId"), Times.Once);
        }
    }
}
