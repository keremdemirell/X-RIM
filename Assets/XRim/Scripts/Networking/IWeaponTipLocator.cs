using XRim.Core;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// Finds where a side's weapon tip rests on a frozen board, in its torso frame (D3: a stroke's lead-in starts
    /// there). Session 04 supplies the real one, reading the pose the last turn left the weapon in.
    /// </summary>
    public interface IWeaponTipLocator
    {
        IWeaponTipSource ForSide(BoardSnapshot board, Side side);
    }
}
