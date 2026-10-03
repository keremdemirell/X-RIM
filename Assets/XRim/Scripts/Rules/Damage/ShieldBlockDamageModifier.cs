namespace XRim.Rules.Damage
{
    /// <summary>
    /// GDD §7, §11 (the "shield reduction" modifier): a weapon a shield partially blocked earlier this turn deals the reduced
    /// share of its damage with every later hit (D20, A5), like a weapon that crushed through a clash (§10).
    /// </summary>
    public sealed class ShieldBlockDamageModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) => damage * context.ShieldBlockDamageMultiplier;
    }
}
