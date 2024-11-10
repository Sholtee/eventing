/********************************************************************************
* EventIds.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Eventing.Internals
{
    internal static class EventIds
    {
        public static class Error
        {
            public static EventId EVENT_NOT_SAVED { get; } = new(200, nameof(EVENT_NOT_SAVED));
            public static EventId CANNOT_MATERIALIZE { get; } = new(201, nameof(CANNOT_MATERIALIZE));
            public static EventId CANNOT_CREATE_RAW_VIEW { get; } = new(202, nameof(CANNOT_CREATE_RAW_VIEW));
        }

        public static class Warning
        {
            public static EventId CACHING_DISABLED { get; } = new(300, nameof(CACHING_DISABLED));
            public static EventId ACQUIRE_LOCK_TIMEOUT { get; } = new(301, nameof(ACQUIRE_LOCK_TIMEOUT));
            public static EventId FOREIGN_LOCK_RELEASE { get; } = new(302, nameof(FOREIGN_LOCK_RELEASE));
        }

        public static class Info
        {
            public static EventId INIT_SCHEMA { get; } = new(500, nameof(INIT_SCHEMA));
            public static EventId SCHEMA_INITIALIZED { get; } = new(501, nameof(SCHEMA_INITIALIZED));
            public static EventId UPDATE_CACHE { get; } = new(502, nameof(UPDATE_CACHE));
            public static EventId INSERT_EVENT { get; } = new(503, nameof(INSERT_EVENT));
            public static EventId CACHE_ENTRY_FOUND { get; } = new(504, nameof(CACHE_ENTRY_FOUND));
            public static EventId REPLAY_EVENTS { get; } = new(505, nameof(REPLAY_EVENTS));
            public static EventId PROCESSED_EVENTS { get; } = new(506, nameof(PROCESSED_EVENTS));
            public static EventId CREATE_RAW_VIEW { get; } = new(507, nameof(CREATE_RAW_VIEW));
            public static EventId ACQUIRE_LOCK { get; } = new(508, nameof(ACQUIRE_LOCK));
            public static EventId RELEASE_LOCK { get; } = new(509, nameof(RELEASE_LOCK));
            public static EventId GET_CACHE_ITEM { get; } = new(510, nameof(GET_CACHE_ITEM));
            public static EventId SET_CACHE_ITEM_EXPIRATION { get; } = new(511, nameof(SET_CACHE_ITEM_EXPIRATION));
            public static EventId REMOVE_CACHE_ITEM { get; } = new(512, nameof(REMOVE_CACHE_ITEM));
            public static EventId SET_CACHE_ITEM { get; } = new(513, nameof(SET_CACHE_ITEM));
        }
    }
}
