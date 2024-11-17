/********************************************************************************
* JsonSerializer.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using SerializerCore = System.Text.Json.JsonSerializer;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Primitives.Patterns;

    using static Properties.Resources;

    /// <summary>
    /// The default implementation of <see cref="ISerializer"/> interface which uses the <see cref="SerializerCore"/> class under the hood.
    /// </summary>
    public sealed class JsonSerializer : Singleton<JsonSerializer>, ISerializer
    {
        #region Private
#if DEBUG
        internal
#else
        private
#endif
        sealed class MultiTypeArrayConverter(IReadOnlyList<Type> elementTypes) : JsonConverter<object?[]>
        {
            public override object?[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType is JsonTokenType.StartArray)
                {
                    //
                    // Only the top level array should be converted by this logic
                    //

                    options = new JsonSerializerOptions(options);
                    options.Converters.Remove(this);

                    object?[] result = new object?[elementTypes.Count];

                    for (int i = 0; reader.Read(); i++)
                    {
                        if (reader.TokenType is JsonTokenType.EndArray)
                        {
                            if (i < result.Length)
                                throw new JsonException(ERR_ARRAY_LENGTH_NOT_MATCH);

                            return result;
                        }

                        if (i == result.Length)
                            throw new JsonException(ERR_ARRAY_LENGTH_NOT_MATCH);

                        result[i] = SerializerCore.Deserialize(ref reader, elementTypes[i], options);
                    }
                }

                throw new JsonException(ERR_MALFORMED_ARRAY);
            }

            public override void Write(Utf8JsonWriter writer, object?[] value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
#if DEBUG
        internal
#else
        private
#endif
        sealed class ObjectConverter : JsonConverter<object>
        {
            private bool ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options, out object array)
            {
                for (List<object?> list = []; reader.Read();)
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        array = list;
                        return true;
                    }
                    list.Add(Read(ref reader, typeof(object), options));
                }

                array = null!;
                return false;
            }

            private bool ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options, out object obj)
            {
                for (Dictionary<string, object?> dict = []; reader.Read();)
                {
                    if (reader.TokenType is JsonTokenType.EndObject)
                    {
                        obj = dict;
                        return true;
                    }

                    if (reader.TokenType is JsonTokenType.PropertyName)
                    {
                        string key = reader.GetString()!;

                        reader.Read();
                        dict.Add(key, Read(ref reader, typeof(object), options));
                        continue;
                    }

                    break;
                }

                obj = null!;
                return false;
            }

            public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.False => false,
                JsonTokenType.True => true,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt32(out int i) ? i : (object) reader.GetDouble(),
                JsonTokenType.StartArray when ReadArray(ref reader, options, out object array) => array,
                JsonTokenType.StartObject when ReadObject(ref reader, options, out object obj) => obj,
                _ => throw new JsonException(ERR_MALFORMED_OBJECT)
            };

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) => throw new NotImplementedException();

            public static ObjectConverter Instance { get; } = new();
        }
        #endregion

        /// <inheritdoc/>
        public T? Deserialize<T>(string utf8String)
        {
            JsonSerializerOptions opts = new(Options);
            opts.Converters.Add(ObjectConverter.Instance);

            return SerializerCore.Deserialize<T>(utf8String ?? throw new ArgumentNullException(nameof(utf8String)), opts);
        }

        /// <inheritdoc/>
        public object?[] Deserialize(string utf8String, IReadOnlyList<Type> types)
        {
            JsonSerializerOptions opts = new(Options);
            opts.Converters.Add(new MultiTypeArrayConverter(types ?? throw new ArgumentNullException(nameof(types))));
            opts.Converters.Add(ObjectConverter.Instance);

            return SerializerCore.Deserialize<object?[]>(utf8String ?? throw new ArgumentNullException(nameof(utf8String)), opts)!;
        }

        /// <inheritdoc/>
        public string Serialize<T>(T? val) => SerializerCore.Serialize(val, Options);

        /// <summary>
        /// Options used during serialization
        /// </summary>
        public JsonSerializerOptions Options { get; set; } = JsonSerializerOptions.Default;
    }
}
