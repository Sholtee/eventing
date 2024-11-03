/********************************************************************************
* ISerializerTests.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Abstractions.Tests
{
    public abstract class ISerializerTests
    {
        protected abstract ISerializer CreateInstance();

        protected ISerializer Serializer { get; private set; } = null!;

        [SetUp]
        public virtual void Setup() => Serializer = CreateInstance();

        [TearDown]
        public virtual void TearDown() => Serializer = null!;

        public static IEnumerable<object[]> Deserialize_ShouldDeserializeMultitypeArrays_Paramz
        {
            get
            {
                yield return new object[] { new Type[] { typeof(int), typeof(string) }, new object[] { 0, "cica" } };
                yield return new object[] { new Type[] { typeof(double), typeof(string) }, new object[] { 0.0, "cica" } };
            }
        }

        [TestCaseSource(nameof(Deserialize_ShouldDeserializeMultitypeArrays_Paramz))]
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
            Assert.That(ret[0], Is.EqualTo(1));
        }

        public static IEnumerable<object?> Deserialize_ShouldSupportAnonObjects_Paramz
        {
            get
            {
                yield return null;
                yield return true;
                yield return false;
                yield return 1986;
                yield return 1986.1026;
                yield return new object[] { 1, 2.0, "3" };
                yield return new Dictionary<string, object?> { { "Key1", 1988 }, { "Key2", "string" } };
            }
        }

        [Test]
        public void Deserialize_ShouldSupportUntypedObjects([ValueSource(nameof(Deserialize_ShouldSupportAnonObjects_Paramz))] object expected)
        {
            string serialized = Serializer.Serialize(expected);

            object? ret = Serializer.Deserialize<object>(serialized);
            Assert.That(ret, expected is IEnumerable enumerable ? Is.EquivalentTo(enumerable) : Is.EqualTo(expected));
        }
    }
}
