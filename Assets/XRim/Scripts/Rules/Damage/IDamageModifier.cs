namespace XRim.Rules.Damage
{
    /// <summary>
    /// One term of "Modifiers" in Damage = BaseDamage × ZoneMultiplier × Modifiers (GDD §11):
    /// off-hand penalty (§12), crush-through reduction (§10), shield reduction (§7), weapon traits (§15).
    /// </summary>
    public interface IDamageModifier
    {
        float Modify(float damage, DamageContext context);
    }
}
