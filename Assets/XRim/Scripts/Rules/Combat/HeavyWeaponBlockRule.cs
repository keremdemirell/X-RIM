namespace XRim.Rules.Combat
{
    /// <summary>The GDD's options for a heavy weapon (a mace) against a shield (§7, TBD; D21).</summary>
    public enum HeavyWeaponBlockRule
    {
        /// <summary>No special rule: the block holds, and the push back on the holder does the rest (D21 default).</summary>
        None = 0,

        /// <summary>A full block against a heavy weapon also staggers the shield holder (the stun's effect, D17).</summary>
        StaggersHolder = 1,

        /// <summary>A heavy weapon breaks through: even a square face hit is only a partial block.</summary>
        BreaksThrough = 2,
    }
}
