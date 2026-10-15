using XRim.Config;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// What kind of sound a cue is: a voice (a dummy's grunt, from its grunt pack; each dummy speaks one at a time) or an effect, and how
    /// much a voice matters: a KO scream cuts off any grunt, an effort grunt never cuts off a scream.
    /// </summary>
    public static class SoundCues
    {
        public static bool IsVoice(SoundCue cue) =>
            cue == SoundCue.GruntAttack || cue == SoundCue.GruntEffort || cue == SoundCue.GruntHurt || cue == SoundCue.GruntBigHurt ||
            cue == SoundCue.GruntKnockOut;

        /// <summary>Higher matters more: effort, attack, hurt, big hurt, KO.</summary>
        public static int VoicePriority(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.GruntEffort: return 0;
                case SoundCue.GruntAttack: return 1;
                case SoundCue.GruntHurt: return 2;
                case SoundCue.GruntBigHurt: return 3;
                case SoundCue.GruntKnockOut: return 4;
                default: return -1;
            }
        }

        /// <summary>A dummy's new grunt plays when it is not speaking, or when the new one matters at least as much as the one it is saying.</summary>
        public static bool VoiceMayInterrupt(bool speaking, SoundCue current, SoundCue next) =>
            !speaking || VoicePriority(next) >= VoicePriority(current);
    }
}
