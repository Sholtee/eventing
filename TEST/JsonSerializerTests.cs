/********************************************************************************
* JsonSerializerTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using static Properties.Resources;

    [TestFixture]
    public class JsonSerializerTests
    {
        public static IEnumerable<object[]> TestConvertParamz
        {
            get
            {
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(int), typeof(string) }, new object[] { 0, "cica" } };
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(double), typeof(string) }, new object[] { 0.0, "cica" } };
            }
        }

        [TestCaseSource(nameof(TestConvertParamz))]
        public void Deserialize_ShouldDeserializeMultitypeArrays(string str, Type[] types, object[] expected) =>
            Assert.That(JsonSerializer.Instance.Deserialize(str, types), Is.EquivalentTo(expected));

        [Test]
        public void Deserialize_ShouldDeserializeMultitypeArrayContainingAnonArray()
        {
            object?[] ret = JsonSerializer.Instance.Deserialize("[[1]]", [typeof(object[])]);

            Assert.That(ret.Length, Is.EqualTo(1));
            
            ret = (ret[0] as object?[])!;
            Assert.NotNull(ret);
            Assert.That(ret.Length, Is.EqualTo(1));
            Assert.That(((JsonElement) ret[0]!).GetInt32(), Is.EqualTo(1));
        }

        public static IEnumerable<object[]> TestConvertterInvalidArrayParamz
        {
            get
            {
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(int) }, ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "[0]", new Type[] { typeof(double), typeof(string) }, ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "[1]", Array.Empty<Type>(), ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "0", new Type[] { typeof(int) }, MALFORMED_ARRAY };
                yield return new object[] { "{},", new Type[] { typeof(int) }, MALFORMED_ARRAY };
            }
        }

        [TestCaseSource(nameof(TestConvertterInvalidArrayParamz))]
        public void Deserialize_ShouldThrowOnInvalidMultitypeArrays(string str, Type[] types, string err)
        {
            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Instance.Deserialize(str, types))!;
            Assert.That(ex.Message, Is.EqualTo(err));
        }

        [Test]
        public void Deserialize_ShouldUseTheConstructorProvided()
        {
            Mock<Func<MyClass>> mockCtor = new Mock<Func<MyClass>>(MockBehavior.Strict);
            mockCtor
                .Setup(c => c.Invoke())
                .Returns(new MyClass());

            MyClass deserialized = JsonSerializer.Instance.Deserialize("{\"NonIgnored\": 5}", mockCtor.Object)!;

            Assert.That(deserialized.NonIgnored, Is.EqualTo(5));
            mockCtor.Verify(c => c.Invoke(), Times.Once);
        }

        public class MyClass
        {
            public int NonIgnored { get; init; }
        }

        public class MyClassHavingIgnoredMember: MyClass
        {
            [IgnoreDataMember]
            public string Ignored { get; init; } = null!;
        }

        [Test]
        public void Serialize_ShouldTakeIgnoreDataMemberAttributeIntoAccount() =>
            Assert.That(JsonSerializer.Instance.Serialize(new MyClassHavingIgnoredMember { Ignored = "cica", NonIgnored = 1986 }), Is.EqualTo(JsonSerializer.Instance.Serialize(new MyClass { NonIgnored = 1986 })));
    }
}
