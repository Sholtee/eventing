/********************************************************************************
* ISerializer.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Defines an abstract serializer.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes the given value.
        /// </summary>
        string Serialize<T>(T? val);

        /// <summary>
        /// Deserializes a value
        /// </summary>
        T? Deserialize<T>(string utf8String);

        /// <summary>
        /// Deserializes a multi-type array
        /// </summary>
        object?[] Deserialize(string utf8String, IReadOnlyList<Type> types);
    }
}
