using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Audio
{
    /// <summary>One sound an event asks for (presentation only): which cue, for whose set or voice, how loud.</summary>
    public readonly struct SoundRequest
    {
        public SoundCue Cue { get; }

        /// <summary>
        /// Whose sounds: an effect comes from this side's effects set (the attacker of a blow), a voice from this side's grunt pack (the
        /// dummy that grunts); null for the shared set.
        /// </summary>
        public Side? Side { get; }

        /// <summary>0..1 before the volume settings.</summary>
        public float Volume { get; }

        public SoundRequest(SoundCue cue, Side? side, float volume)
        {
            Cue = cue;
            Side = side;
            Volume = volume;
        }

        public bool IsVoice => SoundCues.IsVoice(Cue);
    }
}
