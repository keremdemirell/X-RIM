using XRim.Core.Gdd;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>
    /// Whether the screen layout is mirrored. Decided (§4): left-handed mirrors the layout. Open: whether mirroring
    /// should also be a separate toggle, and whether handedness can be changed later in settings.
    /// </summary>
    [GddTbd("§4", "Separate mirrored-layout toggle")]
    [GddTbd("§4", "Can handedness be changed later?")]
    public interface IHandednessLayoutPolicy
    {
        bool IsScreenMirrored(Handedness handedness);
    }
}
