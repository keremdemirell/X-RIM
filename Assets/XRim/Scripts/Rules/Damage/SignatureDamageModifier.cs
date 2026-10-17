namespace XRim.Rules.Damage
{
    /// <summary>
    /// GDD §8 and §11: a signature move's special property can scale the damage of the hits its own swing lands (the damage boost
    /// effect); 1 for every other hit.
    /// </summary>
    public sealed class SignatureDamageModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) => damage * context.Signature.DamageMultiplier;
    }
}
