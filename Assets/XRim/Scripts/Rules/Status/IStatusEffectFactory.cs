using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Status
{
    /// <summary>Creates the effect a stun or a stagger has, when the rules apply one (GDD §10, §11).</summary>
    [GddTbd("§11", "Effect of a stun", Proposal = "D17 default (designer, 2026-10-03): no body move next turn")]
    [GddTbd("§10", "Effect of a stagger", Proposal = "Same as head stun")]
    public interface IStatusEffectFactory
    {
        IStatusEffect Create(StatusKind kind, DamageSettings settings);
    }
}
