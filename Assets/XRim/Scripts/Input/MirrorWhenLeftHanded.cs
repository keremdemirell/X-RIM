using XRim.Rules;

namespace XRim.Input
{
    /// <summary>The Decided rule (GDD §4): a left-handed player gets the mirrored layout.</summary>
    public sealed class MirrorWhenLeftHanded : IHandednessLayoutPolicy
    {
        public bool IsScreenMirrored(Handedness handedness) => handedness == Handedness.Left;
    }
}
