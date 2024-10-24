/********************************************************************************
* ISerializerTests.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    public abstract class ISerializerTests
    {
        protected abstract ISerializer CreateInstance();

        protected ISerializer Serializer { get; private set; } = null!;

        [SetUp]
        public virtual void Setup() => Serializer = CreateInstance();

        [TearDown]
        public virtual void TearDown() => Serializer = null!;

        public static IEnumerable<object[]> TestConvertParamz
        {
            get
            {
                yield return new object[] { new Type[] { typeof(int), typeof(string) }, new object[] { 0, "cica" } };
                yield return new object[] { new Type[] { typeof(double), typeof(string) }, new object[] { 0.0, "cica" } };
            }
        }

        [TestCaseSource(nameof(TestConvertParamz))]
        public void Deserialize_ShouldDeserializeMultitypeArrays(Type[] types, object[] expected)
        {
            string s = Serializer.Serialize(expected);
            Assert.That(Serializer.Deserialize(s, types), Is.EquivalentTo(expected));
        }

        [Test]
        public void Deserialize_ShouldDeserializeMultitypeArrayContainingAnonArray()
        {
            object[] ar = [new object[] { 1 }]; 

            object?[] ret = Serializer.Deserialize(Serializer.Serialize(ar), [typeof(object[])]);

            Assert.That(ret.Length, Is.EqualTo(1));
            
            ret = (ret[0] as object?[])!;
            Assert.That(ret, Is.Not.Null);
            Assert.That(ret, Has.Length.EqualTo(1));
        }

        [Test]
        public void Deserialize_ShouldUseTheConstructorProvided()
        {
            Mock<Func<MyClass>> mockCtor = new(MockBehavior.Strict);
            mockCtor
                .Setup(c => c.Invoke())
                .Returns(new MyClass());

            MyClass
                original = new MyClass { NonIgnored = 5 },
                deserialized = Serializer.Deserialize(Serializer.Serialize(original), mockCtor.Object)!;

            Assert.That(original, Is.EqualTo(deserialized));
            mockCtor.Verify(c => c.Invoke(), Times.Once);
        }

        internal record MyClass
        {
            public int NonIgnored { get; init; }
        }

        internal record MyClassHavingIgnoredMember: MyClass
        {
            [IgnoreDataMember]
            public string Ignored { get; init; } = null!;
        }

        [Test]
        public void Serialize_ShouldTakeIgnoreDataMemberAttributeIntoAccount() =>
            Assert.That(Serializer.Serialize(new MyClassHavingIgnoredMember { Ignored = "cica", NonIgnored = 1986 }), Is.EqualTo(Serializer.Serialize(new MyClass { NonIgnored = 1986 })));
    }
}
