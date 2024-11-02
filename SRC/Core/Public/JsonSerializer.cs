/********************************************************************************
* JsonSerializer.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private sealed class MultiTypeArrayConverter(IReadOnlyList<Type> ElementTypes) : JsonConverter<object?[]>
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

                    object?[] result = new object?[ElementTypes.Count];

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

                        result[i] = SerializerCore.Deserialize(ref reader, ElementTypes[i], options);
                    }
                }

                throw new JsonException(ERR_MALFORMED_ARRAY);
            }

            [ExcludeFromCodeCoverage]
            public override void Write(Utf8JsonWriter writer, object?[] value, JsonSerializerOptions options) => throw new NotImplementedException();
        }

        private sealed class ObjectConverter : JsonConverter<object>
        {
            private ObjectConverter() { }

            public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    //
                    // For "null"s the system wont call the converter
                    //

                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.String:
                        return reader.GetString();
                    case JsonTokenType.Number:
                        if (reader.TryGetInt32(out int i))
                            return i;
                        if (reader.TryGetDouble(out double d))
                            return d;
                        break;
                    case JsonTokenType.StartArray:
                        for (List<object?> list = []; reader.Read();)
                        {
                            if (reader.TokenType == JsonTokenType.EndArray)
                                return list;
                            list.Add(Read(ref reader, typeof(object), options));
                        }
                        break;
                    case JsonTokenType.StartObject:
                        for (Dictionary<string, object?> dict = []; reader.Read();)
                        {
                            if (reader.TokenType is JsonTokenType.EndObject)
                                return dict;

                            if (reader.TokenType is JsonTokenType.PropertyName)
                            {
                                string key = reader.GetString()!;

                                reader.Read();
                                dict.Add(key, Read(ref reader, typeof(object), options));
                                continue;
                            }

                            break;
                        }
                        break;
                }

                //
                // It might be an assert as I couldn't manage to get here
                //

                throw new JsonException(ERR_MALFORMED_OBJECT);
            }

            [ExcludeFromCodeCoverage]
            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) => throw new NotImplementedException();

            public static ObjectConverter Instance { get; } = new();
        }
        #endregion

        /// <inheritdoc/>
        public T? Deserialize<T>(string utf8String)
        {
            JsonSerializerOptions opts = new(Options);
            opts.Converters.Add(ObjectConverter.Instance);

            return SerializerCore.Deserialize<T>(utf8String, opts);
        }

        /// <inheritdoc/>
        public object?[] Deserialize(string utf8String, IReadOnlyList<Type> types)
        {
            JsonSerializerOptions opts = new(Options);
            opts.Converters.Add(new MultiTypeArrayConverter(types));
            opts.Converters.Add(ObjectConverter.Instance);

            return SerializerCore.Deserialize<object?[]>(utf8String, opts)!;
        }

        /// <inheritdoc/>
        public string Serialize<T>(T? val) => SerializerCore.Serialize(val, Options);

        /// <summary>
        /// Options used during serialization
        /// </summary>
        public JsonSerializerOptions Options { get; set; } = JsonSerializerOptions.Default;
    }
}
