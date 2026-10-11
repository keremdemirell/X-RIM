namespace XRim.Rules.Limbs
{
    /// <summary>When picking the severed-limb club retrieves a limb from the floor (GDD §12 open point).</summary>
    public enum LimbRetrievalRule
    {
        /// <summary>Designer 2026-10-10 (recommended default): selecting the club retrieves a limb automatically, even one out of reach.</summary>
        Automatic = 0,

        /// <summary>Only a limb within the club's reach (the arm plus the club's length from the shoulder) can be retrieved.</summary>
        WithinReach = 1,
    }
}
