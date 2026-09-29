namespace XRim.Rules.Planning
{
    /// <summary>
    /// What the opponent may see during planning. Body moves, paths and chosen signature moves stay
    /// hidden until execution (GDD §3, §8).
    /// </summary>
    public readonly struct PublicPlanningState
    {
        /// <summary>GDD §6 (Decided): a weapon switch is shown to the opponent immediately.</summary>
        public WeaponId Weapon { get; }

        /// <summary>Decided by the designer on 2026-09-29 (not yet written into the GDD): the opponent sees Ready.</summary>
        public bool IsReady { get; }

        /// <summary>Only true when the visibility policy reveals a charged signature move (GDD §8, TBD).</summary>
        public bool SignatureChargedVisible { get; }

        public PublicPlanningState(WeaponId weapon, bool isReady, bool signatureChargedVisible)
        {
            Weapon = weapon;
            IsReady = isReady;
            SignatureChargedVisible = signatureChargedVisible;
        }
    }
}
