﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Solti.Utils.Eventing.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Solti.Utils.Eventing.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Array length does not match.
        /// </summary>
        internal static string ARRAY_LENGTH_NOT_MATCH {
            get {
                return ResourceManager.GetString("ARRAY_LENGTH_NOT_MATCH", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Duplicate event id: {0}.
        /// </summary>
        internal static string DUPLICATE_EVENT_ID {
            get {
                return ResourceManager.GetString("DUPLICATE_EVENT_ID", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid event id provided: {0}.
        /// </summary>
        internal static string INVALID_EVENT_ID {
            get {
                return ResourceManager.GetString("INVALID_EVENT_ID", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid flow id provided: {0}.
        /// </summary>
        internal static string INVALID_FLOW_ID {
            get {
                return ResourceManager.GetString("INVALID_FLOW_ID", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found cache entry for view: {0}.
        /// </summary>
        internal static string LOG_CACHE_ENTRY_FOUND {
            get {
                return ResourceManager.GetString("LOG_CACHE_ENTRY_FOUND", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Creating interceptor for view: {0}.
        /// </summary>
        internal static string LOG_CREATE_INTERCEPTOR {
            get {
                return ResourceManager.GetString("LOG_CREATE_INTERCEPTOR", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to View layout changed since the last update. Skip retrieving view from cache.
        /// </summary>
        internal static string LOG_LAYOUT_MISMATCH {
            get {
                return ResourceManager.GetString("LOG_LAYOUT_MISMATCH", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replaying events for view: {0}.
        /// </summary>
        internal static string LOG_REPLAY_EVENTS {
            get {
                return ResourceManager.GetString("LOG_REPLAY_EVENTS", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Malformed array.
        /// </summary>
        internal static string MALFORMED_ARRAY {
            get {
                return ResourceManager.GetString("MALFORMED_ARRAY", resourceCulture);
            }
        }
    }
}
