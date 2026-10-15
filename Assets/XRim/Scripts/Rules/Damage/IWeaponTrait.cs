using XRim.Core.Gdd;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// An in-match effect carried by a specific weapon, never a default dummy ability (GDD §15, Decided). Candidates: lifesteal after
    /// 3 hits, damage build-up from total damage taken. A low-HP rage multiplier is Rejected.
    /// <para>
    /// A trait holds no state of its own: a match is a pure function of its state and its settings, and the state is cloned. What a
    /// trait counts lives on the <see cref="XRim.Rules.Match.FighterState"/> it is given, and a trait does nothing unless its flag in
    /// <see cref="XRim.Rules.Settings.TraitSettings"/> is on and the weapon lists its <see cref="Id"/> in
    /// <see cref="XRim.Rules.Settings.WeaponStats.TraitIds"/>. As a damage modifier it sees every hit; as a trait it also sees each
    /// hit that has landed.
    /// </para>
    /// </summary>
    [GddTbd("§15", "Lifesteal and build-up details, and which weapons carry them")]
    public interface IWeaponTrait : IDamageModifier
    {
        string Id { get; }

        /// <summary>
        /// A hit landed with a weapon that carries this trait: update the attacker's counters and return the HP it recovers (0 for
        /// none). The caller applies the recovery after every hit of that instant, never above the maximum HP.
        /// </summary>
        float OnHitLanded(TraitHitContext hit);
    }
}
