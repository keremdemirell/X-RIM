using XRim.Core.Gdd;
using XRim.Rules.Combat;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// An in-match effect carried by a specific weapon, never a default dummy ability (GDD §15, Decided).
    /// Candidates: lifesteal after 3 hits, damage build-up from total damage taken. A low-HP rage
    /// multiplier is Rejected.
    /// </summary>
    [GddTbd("§15", "Lifesteal and build-up details, and which weapons carry them")]
    public interface IWeaponTrait : IDamageModifier
    {
        string Id { get; }

        void OnHitLanded(HitFacts hit, float hpDamage);

        void OnDamageTaken(float hpDamage);
    }
}
