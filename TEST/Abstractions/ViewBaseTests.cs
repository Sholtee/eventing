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
    [TestFixture]
    public class ViewBaseTests
    {
        private class TestView : ViewBase
        {
        }

        [Test]
        public void FromDict_ShouldBeNullChecked()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new()
            {
                OwnerRepository = mockRepo.Object
            };

            Assert.Throws<ArgumentNullException>(() => view.FromDict(null!));
        }

        public static IEnumerable<object?[]> FromDict_ShouldVerifyTheFlowId_Paramz
        {
            get
            {
                yield return [new Dictionary<string, object?> { { "FlowId", "flowId" } }, true];
                yield return [new Dictionary<string, object?> { { "FlowId", "otherId" } }, false];
                yield return [new Dictionary<string, object?> { { "FlowId", 1986 } }, false];
                yield return [new Dictionary<string, object?> { { "FlowId", null } }, false];
                yield return [new Dictionary<string, object?> { }, false];
            }
        }

        [TestCaseSource(nameof(FromDict_ShouldVerifyTheFlowId_Paramz))]
        public void FromDict_ShouldVerifyTheFlowId(IDictionary<string, object?> dict, bool expected)
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new()
            {
                FlowId = "flowId",
                OwnerRepository = mockRepo.Object
            };

            Assert.That(view.FromDict(dict), Is.EqualTo(expected));
        }

        [Test]
        public void ToDict_ShouldAProperDict()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Loose);

            using TestView view = new()
            {
                FlowId = "flowId",
                OwnerRepository = mockRepo.Object
            };

            Assert.That(view.ToDict(), Is.EquivalentTo(new Dictionary<string, object?> { { "FlowId", "flowId" } }));
        }

        [Test]
        public void Dispose_ShouldCloseTheView()
        {
            Mock<IViewRepository> mockRepo = new(MockBehavior.Strict);
            mockRepo.Setup(r => r.Close("flowId"));

            using (TestView view = new() { FlowId = "flowId", OwnerRepository = mockRepo.Object })
            {
            }

            mockRepo.Verify(r => r.Close("flowId"), Times.Once);
        }
    }
}
