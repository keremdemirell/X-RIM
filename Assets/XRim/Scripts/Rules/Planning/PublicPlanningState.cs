using System;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// What the opponent may see during planning. Body moves, paths and chosen signature moves stay
    /// hidden until execution (GDD §3, §8). Comparable, so the authority can tell when it changed.
    /// </summary>
    public readonly struct PublicPlanningState : IEquatable<PublicPlanningState>
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

        public bool Equals(PublicPlanningState other) =>
            Weapon == other.Weapon && IsReady == other.IsReady && SignatureChargedVisible == other.SignatureChargedVisible;

        public override bool Equals(object obj) => obj is PublicPlanningState other && Equals(other);

        public override int GetHashCode() => (Weapon.GetHashCode() * 31 + IsReady.GetHashCode()) * 31 + SignatureChargedVisible.GetHashCode();

        public static bool operator ==(PublicPlanningState a, PublicPlanningState b) => a.Equals(b);

        public static bool operator !=(PublicPlanningState a, PublicPlanningState b) => !a.Equals(b);
    }
}
