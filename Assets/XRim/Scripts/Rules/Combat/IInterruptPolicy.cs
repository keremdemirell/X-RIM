using XRim.Core.Gdd;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Whether a landed hit cancels the victim's attack. The priority rule (first weapon to a valid hitbox lands; a dummy hit
    /// first has its attack interrupted) is Decided in GDD §9; which hits qualify was open (D15, decided by the designer
    /// 2026-10-03: weapon arm or head), and whether heavy weapons resist interruption is still open (D16).
    /// </summary>
    [GddTbd("§9", "Do heavy weapons resist interruption?", Proposal = "D16 default: no swing armour, a toggle per weapon")]
    public interface IInterruptPolicy
    {
        bool Interrupts(InterruptCheck check);
    }
}
