namespace XRim.Rules.Damage
{
    /// <summary>Which hits count toward lifesteal's "after 3 successful hits" (GDD §15, TBD).</summary>
    public enum LifestealHitCounting
    {
        /// <summary>Every hit landed with a weapon that carries the trait counts, whenever it landed (the default).</summary>
        Total = 0,

        /// <summary>
        /// Only hits in a row: a turn in which the dummy attacked with a weapon that carries the trait and landed nothing starts the
        /// count again (a turn spent with another weapon, or with no attack, does not).
        /// </summary>
        Consecutive = 1,
    }
}
