namespace XRim.Rules.Damage
{
    /// <summary>GDD §10 (Decided, −30% Tunable): a weapon that crushed through a clash continues with less damage.</summary>
    public sealed class CrushThroughDamageModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) =>
            context.CrushedThrough ? damage * context.Settings.Clash.CrushThroughDamageMultiplier : damage;
    }
}
