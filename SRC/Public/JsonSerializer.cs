/********************************************************************************
* JsonSerializer.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using SerializerCore = System.Text.Json.JsonSerializer;

namespace Solti.Utils.Eventing
{
    using Abstractions;
    using Primitives.Patterns;

    using static Properties.Resources;

    public sealed class JsonSerializer : Singleton<JsonSerializer>, ISerializer
    {
        public T? Deserialize<T>(string utf8String) => SerializerCore.Deserialize<T>(utf8String, Options);

        public object?[] Deserialize(string utf8String, IReadOnlyList<Type> types)
        {
            JsonSerializerOptions opts = new(Options);
            opts.Converters.Add(new MultiTypeArrayConverter(types));

            return SerializerCore.Deserialize<object?[]>(utf8String, opts)!;
        }

        public string Serialize<T>(T? val) => SerializerCore.Serialize(val, Options);

        public JsonSerializerOptions Options { get; set; } = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { DetectIgnoreDataMemberAttribute }
            }
        };

        #region Private
        private static void DetectIgnoreDataMemberAttribute(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind is JsonTypeInfoKind.Object)
            {
                foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties)
                {
                    if (propertyInfo.AttributeProvider is ICustomAttributeProvider provider &&
                        provider.IsDefined(typeof(IgnoreDataMemberAttribute), inherit: true))
                    {
                        //
                        // Disable both serialization and deserialization
                        //

                        propertyInfo.Get = null;
                        propertyInfo.Set = null;
                    }
                }
            }
        }

        private sealed class MultiTypeArrayConverter(IReadOnlyList<Type> ElementTypes) : JsonConverter<object?[]>
        {
            public override object?[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType is JsonTokenType.StartArray)
                {
                    options = new JsonSerializerOptions(options);
                    options.Converters.Remove(this);

                    object?[] result = new object?[ElementTypes.Count];

                    for (int i = 0; reader.Read(); i++)
                    {
                        if (reader.TokenType is JsonTokenType.EndArray)
                        {
                            if (i < result.Length)
                                throw new JsonException(ARRAY_LENGTH_NOT_MATCH);

                            return result;
                        }

                        if (i == result.Length)
                            throw new JsonException(ARRAY_LENGTH_NOT_MATCH);

                        result[i] = SerializerCore.Deserialize(ref reader, ElementTypes[i], options);
                    }
                }

                throw new JsonException(MALFORMED_ARRAY);
            }

            public override void Write(Utf8JsonWriter writer, object?[] value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
        #endregion
    }
}
