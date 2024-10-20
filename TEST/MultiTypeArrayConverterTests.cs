/********************************************************************************
* MultiTypeArrayConverterTests.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Internals;

    using static Properties.Resources;

    [TestFixture]
    public class MultiTypeArrayConverterTests
    {
        private static object?[] Deserialize(string str, params Type[] types)
        {
            JsonSerializerOptions options = new();
            options.Converters.Add(new MultiTypeArrayConverter(types));

            return JsonSerializer.Deserialize<object?[]>(str, options)!;
        }

        public static IEnumerable<object[]> TestConvertParamz
        {
            get
            {
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(int), typeof(string) }, new object[] { 0, "cica" } };
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(double), typeof(string) }, new object[] { 0.0, "cica" } };
            }
        }

        [TestCaseSource(nameof(TestConvertParamz))]
        public void TestConverter(string str, Type[] types, object[] expected) =>
            Assert.That(Deserialize(str, types), Is.EquivalentTo(expected));

        [Test]
        public void TestConverterNestedAnonArray()
        {
            object?[] ret = Deserialize("[[1]]", typeof(object[]));

            Assert.That(ret.Length, Is.EqualTo(1));
            
            ret = (ret[0] as object?[])!;
            Assert.NotNull(ret);
            Assert.That(ret.Length, Is.EqualTo(1));
            Assert.That(ret[0]!.ToString(), Is.EqualTo(JsonSerializer.Deserialize<object>("1")!.ToString()));
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
        public void TestConverterInvalidArray(string str, Type[] types, string err)
        {
            JsonException ex = Assert.Throws<JsonException>(() => Deserialize(str, types))!;
            Assert.That(ex.Message, Is.EqualTo(err));
        }
    }
}
