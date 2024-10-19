/********************************************************************************
* ISerializer.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json;

namespace Solti.Utils.Eventing.Abstractions
{
    /// <summary>
    /// Defines the contract of generic serializers.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes the given object to string.
        /// </summary>
        string Serialize<T>(T? obj);

        /// <summary>
        /// Deserializes the given value.
        /// </summary>
        T? Deserialize<T>(string str);

        /// <summary>
        /// The default implementation which uses the <see cref="JsonSerializer"/> class.
        /// </summary>
        public sealed class Default : ISerializer
        {
            /// <inheritdoc/>
            public T? Deserialize<T>(string str) => JsonSerializer.Deserialize<T>(str);

            /// <inheritdoc/>
            public string Serialize<T>(T? obj) => JsonSerializer.Serialize(obj);

            /// <summary>
            /// The global instance.
            /// </summary>
            public static ISerializer Instance { get; } = new Default();
        }
    }
}
