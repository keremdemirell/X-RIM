namespace XRim.Rules.Damage
{
    /// <summary>
    /// D13 (TBD §5): a body move may add damage to the turn's hits, <see cref="Settings.BodyMoveStats.DamageBonusFraction"/>
    /// (0 by default: the lunge adds reach only).
    /// </summary>
    public sealed class BodyMoveDamageBonusModifier : IDamageModifier
    {
        public float Modify(float damage, DamageContext context) =>
            context.AttackerBodyMove != null ? damage * (1f + context.AttackerBodyMove.DamageBonusFraction) : damage;
    }
}
