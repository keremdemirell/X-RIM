using XRim.Core;

namespace XRim.Profile
{
    /// <summary>
    /// The player's volume sliders (GDD §2; Settings, Session 18). Each is a share of the level the designer mixed in the Audio asset, from
    /// 0 (silent) to 1 (the mix as designed), so the sliders never fight the designer's balance between music, effects and voices.
    /// </summary>
    public sealed class AudioVolumes
    {
        /// <summary>The designer's mix, untouched.</summary>
        public const float AsMixed = 1f;

        private float _master = AsMixed;
        private float _music = AsMixed;
        private float _effects = AsMixed;
        private float _voices = AsMixed;

        public float Master
        {
            get => _master;
            set => _master = Sanitise(value);
        }

        public float Music
        {
            get => _music;
            set => _music = Sanitise(value);
        }

        public float Effects
        {
            get => _effects;
            set => _effects = Sanitise(value);
        }

        /// <summary>The comic grunts and screams.</summary>
        public float Voices
        {
            get => _voices;
            set => _voices = Sanitise(value);
        }

        /// <summary>Inside 0..1; a broken value (not a number) becomes the designer's mix.</summary>
        private static float Sanitise(float value) => float.IsNaN(value) ? AsMixed : XMath.Clamp01(value);
    }
}
