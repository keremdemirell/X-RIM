namespace XRim.Rules.Combat
{
    /// <summary>The four outcomes of the two-stage clash model (GDD §10, Decided).</summary>
    public enum ClashKind
    {
        /// <summary>Hard clash, power ratio ≥ crush ratio: the stronger continues with reduced damage, the weaker is knocked off and staggered.</summary>
        CrushThrough = 0,

        /// <summary>Hard clash, ratio below crush ratio: both rebound in sparks, neither continues.</summary>
        BothRebound = 1,

        /// <summary>Glancing, masses differ: the lighter weapon redirects the heavier and continues.</summary>
        LighterDeflectsHeavier = 2,

        /// <summary>Glancing, masses within the similar-mass band: both slide past and continue.</summary>
        BothSlidePast = 3,
    }
}
