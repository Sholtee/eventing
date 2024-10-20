/********************************************************************************
* MultiTypeArrayConverter.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Eventing.Internals
{
    using static Properties.Resources;

    internal sealed class MultiTypeArrayConverter(IReadOnlyList<Type> ElementTypes) : JsonConverter<object?[]>
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

                    result[i] = JsonSerializer.Deserialize(ref reader, ElementTypes[i], options);
                }
            }

            throw new JsonException(MALFORMED_ARRAY);
        }

        public override void Write(Utf8JsonWriter writer, object?[] value, JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
