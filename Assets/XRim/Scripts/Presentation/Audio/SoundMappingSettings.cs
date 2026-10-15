using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Audio
{
    /// <summary>The numbers the event-to-sound mapping reads: when a hit counts as heavy, and how loud hits are by damage. Plain data.</summary>
    public sealed class SoundMappingSettings
    {
        /// <summary>A hit at least this strong sounds heavy and makes its victim cry out (the same line the debris uses).</summary>
        public float HeavyHitMinDamage;

        /// <summary>A hit this strong plays at full volume.</summary>
        public float FullVolumeDamage = 1f;

        /// <summary>The quietest a landed hit plays.</summary>
        public float MinHitVolume;

        public static SoundMappingSettings From(AudioConfig audio, FeelConfig feel)
        {
            Guard.NotNull(audio, nameof(audio));
            Guard.NotNull(feel, nameof(feel));
            return new SoundMappingSettings
            {
                HeavyHitMinDamage = feel.HeavyHitMinDamage,
                FullVolumeDamage = audio.FullVolumeDamage,
                MinHitVolume = audio.MinHitVolume,
            };
        }
    }
}
