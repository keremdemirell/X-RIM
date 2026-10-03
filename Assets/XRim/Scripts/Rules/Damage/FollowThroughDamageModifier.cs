namespace XRim.Rules.Damage
{
    /// <summary>
    /// D26 (designer, 2026-10-03): a weapon that already hit this turn hits softer, in step with the speed it has left
    /// (<see cref="DamageContext.FollowThroughSpeedFraction"/>), as a weapon that crushes through a clash does (§10).
    /// </summary>
    public sealed class FollowThroughDamageModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) => damage * context.FollowThroughSpeedFraction;
    }
}
