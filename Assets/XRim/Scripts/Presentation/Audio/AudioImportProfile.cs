using XRim.Core.Gdd;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// How Unity imports a sound dropped into <c>Assets/XRim/Audio/Incoming/</c> (Session 13; the Editor's import rules apply it every
    /// time a file there is imported, so nobody sets it by hand). An effect or a grunt is short and must start the instant it is
    /// triggered, so it is made mono and decompressed when loaded; the music loop is long, so it stays stereo and streams from storage.
    /// A file with an unknown name is treated as an effect. Pure.
    /// </summary>
    public readonly struct AudioImportProfile
    {
        /// <summary>Compression quality of effects and grunts, 0 (smallest file) to 1 (best sound).</summary>
        [Placeholder("Session 13: a usual phone setting for short sounds; raise it if an effect sounds grainy")]
        public const float EffectQuality = 0.7f;

        /// <summary>Compression quality of the music loop.</summary>
        [Placeholder("Session 13: a usual phone setting for a streamed loop; raise it if the music sounds thin")]
        public const float MusicQuality = 0.5f;

        private AudioImportProfile(bool forceToMono, bool streams, float quality)
        {
            ForceToMono = forceToMono;
            Streams = streams;
            Quality = quality;
        }

        /// <summary>One channel: the duel's effects and voices sit in the middle anyway, and mono halves their memory.</summary>
        public bool ForceToMono { get; }

        /// <summary>Played while it loads from storage (the music); otherwise decompressed when loaded, ready to start at once.</summary>
        public bool Streams { get; }

        /// <summary>The compression quality, 0 (smallest file) to 1 (best sound).</summary>
        public float Quality { get; }

        public static AudioImportProfile Effect => new AudioImportProfile(true, false, EffectQuality);

        public static AudioImportProfile Music => new AudioImportProfile(false, true, MusicQuality);

        /// <summary>Music by its name; everything else (an effect, a grunt, a file with an unknown name) as an effect.</summary>
        public static AudioImportProfile For(string fileName) => AudioFileNaming.IsMusic(fileName) ? Music : Effect;
    }
}
