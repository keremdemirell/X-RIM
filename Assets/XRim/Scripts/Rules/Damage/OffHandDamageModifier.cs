namespace XRim.Rules.Damage
{
    /// <summary>GDD §12 (Decided, 80% Tunable): a dummy fighting with its off hand deals less damage.</summary>
    public sealed class OffHandDamageModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) =>
            context.Hit.IsOffHand ? damage * context.Settings.Damage.OffHandDamageMultiplier : damage;
    }
}
