/********************************************************************************
* JsonSerializerTests.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

using NUnit.Framework;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions;
    using Abstractions.Tests;

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

        [Test]
        public void MultiTypeArrayConverter_ShouldThrowOnInvalidArray([Values("[", "[1,")] string input)
        {
            JsonSerializer.MultiTypeArrayConverter converter = new([typeof(int)]);

            JsonException err = Assert.Throws<JsonException>(() =>
            {
                Utf8JsonReader rdr = new  // must be here (CS8175)
                (
                    Encoding.UTF8.GetBytes(input),
                    isFinalBlock: false,
                    default
                );
                rdr.Read();
                converter.Read(ref rdr, null!, JsonSerializerOptions.Default);
            })!;

            Assert.That(err.Message, Is.EqualTo(ERR_MALFORMED_ARRAY));
        }

        [Test]
        public void MultiTypeArrayConverter_ShouldThrowOnWriteCall() => Assert.Throws<NotImplementedException>(() => new JsonSerializer.MultiTypeArrayConverter(null!).Write(null!, null!, null!));

        [Test]
        public void ObjectConverter_ShouldThrowOnInvalidObject([Values("{", "[", "{/*comment*/")] string input)
        {
            JsonSerializer.ObjectConverter converter = new();

            JsonException err = Assert.Throws<JsonException>(() =>
            {
                Utf8JsonReader rdr = new  // must be here (CS8175)
                (
                    Encoding.UTF8.GetBytes(input),
                    isFinalBlock: false,
                    new JsonReaderState
                    (
                        new JsonReaderOptions
                        {
                            CommentHandling = JsonCommentHandling.Allow
                        }
                    )
                );
                rdr.Read();
                converter.Read(ref rdr, null!, null!);
            })!;

            Assert.That(err.Message, Is.EqualTo(ERR_MALFORMED_OBJECT));
        }

        [Test]
        public void ObjectConverter_ShouldThrowOnWriteCall() => Assert.Throws<NotImplementedException>(() => new JsonSerializer.ObjectConverter().Write(null!, null!, null!));
    }
}
