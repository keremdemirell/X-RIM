using System;
using UnityEngine;

namespace XRim.Config
{
    /// <summary>
    /// A ScriptableObject wrapping one engine-free settings class from Rules, Simulation or Economy.
    /// The pure class holds the GDD starting values as field initializers (the single source of truth);
    /// this asset only makes them editable and saveable. Matches read <see cref="CreateSnapshot"/>, never the live object.
    /// </summary>
    public abstract class SettingsConfig<T> : SettingsConfigBase where T : class, new()
    {
        // Must stay named as SettingsConfigBase.SettingsFieldName.
        [SerializeField] private T _settings = new T();

        /// <summary>The live values that the inspector and tuning panel edit.</summary>
        public T Settings => _settings;

        public override object SettingsObject => _settings;
        public override Type SettingsType => typeof(T);

        public T CreateSnapshot() => SettingsCopy.Clone(_settings);

        /// <summary>Replaces every value, e.g. when the Editor creates an asset from GddStartingValues.</summary>
        public void Overwrite(T values) => _settings = SettingsCopy.Clone(values ?? throw new ArgumentNullException(nameof(values)));
    }
}
