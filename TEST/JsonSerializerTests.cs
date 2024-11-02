/********************************************************************************
* JsonSerializerTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;

    using static Properties.Resources;

    [TestFixture]
    public class JsonSerializerTests: ISerializerTests
    {
        protected override ISerializer CreateInstance() => JsonSerializer.Instance;

        public static IEnumerable<object[]> Deserialize_ShouldThrowOnInvalidMultitypeArrays_Paramz
        {
            get
            {
                yield return new object[] { "[0, \"cica\"]", new Type[] { typeof(int) }, ERR_ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "[0]", new Type[] { typeof(double), typeof(string) }, ERR_ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "[1]", Array.Empty<Type>(), ERR_ARRAY_LENGTH_NOT_MATCH };
                yield return new object[] { "0", new Type[] { typeof(int) }, ERR_MALFORMED_ARRAY };
                yield return new object[] { "{},", new Type[] { typeof(int) }, ERR_MALFORMED_ARRAY };
            }
        }

        [TestCaseSource(nameof(Deserialize_ShouldThrowOnInvalidMultitypeArrays_Paramz))]
        public void Deserialize_ShouldThrowOnInvalidMultitypeArrays(string str, Type[] types, string err)
        {
            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Instance.Deserialize(str, types))!;
            Assert.That(ex.Message, Is.EqualTo(err));
        }
    }
}
