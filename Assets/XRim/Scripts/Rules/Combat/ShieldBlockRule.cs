namespace XRim.Rules.Combat
{
    /// <summary>The GDD's options for what a shield does to a weapon that meets it (§7, TBD; D20).</summary>
    public enum ShieldBlockRule
    {
        /// <summary>A full block for square hits on the shield face, a partial reduction for rim or glancing hits (D20 default).</summary>
        SquareFaceFullOtherwisePartial = 0,

        /// <summary>Every hit on the shield is a full block.</summary>
        AlwaysFull = 1,

        /// <summary>Every hit on the shield only reduces the weapon's damage.</summary>
        AlwaysPartial = 2,
    }
}
