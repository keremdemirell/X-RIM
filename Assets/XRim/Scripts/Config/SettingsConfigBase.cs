using System;
using UnityEngine;

namespace XRim.Config
{
    /// <summary>Non-generic base so the inspector and the debug tuning panel can treat every settings asset alike.</summary>
    public abstract class SettingsConfigBase : ScriptableObject
    {
        /// <summary>Name of the serialized field that holds the wrapped settings object.</summary>
        public const string SettingsFieldName = "_settings";

        public abstract object SettingsObject { get; }
        public abstract Type SettingsType { get; }
    }
}
