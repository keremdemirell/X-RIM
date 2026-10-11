using System.Collections.Generic;
using XRim.Rules.Limbs;
using XRim.Rules.Status;

namespace XRim.Rules.Match
{
    /// <summary>Rules-side state of one dummy. Pose and position live in the simulation's PoseSnapshot.</summary>
    public sealed class FighterState
    {
        private readonly float[] _limbDamage = new float[BodyParts.Count];
        private readonly bool[] _severed = new bool[BodyParts.Count];

        public Handedness Handedness { get; }

        /// <summary>One global HP pool (GDD §11). At 0 the dummy dies.</summary>
        public float Hp { get; set; }

        public WeaponId CurrentWeapon { get; set; }
        public int ConsecutiveIdleTurns { get; set; }
        public List<IStatusEffect> Statuses { get; } = new List<IStatusEffect>();

        /// <summary>The severed limb this dummy wields as a club (GDD §12); null when it holds none.</summary>
        public SeveredLimbRef? HeldLimb { get; set; }

        /// <summary>
        /// Loadout weapons this dummy can no longer pick: each dropped with its arm while
        /// <see cref="Settings.LimbLossSettings.DroppedWeaponIsLost"/> is on (off by default, G2).
        /// </summary>
        public HashSet<WeaponId> LostWeapons { get; } = new HashSet<WeaponId>();

        public FighterState(Handedness handedness, float hp, WeaponId currentWeapon)
        {
            Handedness = handedness;
            Hp = hp;
            CurrentWeapon = currentWeapon;
        }

        public bool IsDead => Hp <= 0f;

        /// <summary>While the dominant arm is intact the weapon is held in the dominant hand (GDD §12).</summary>
        public bool HasDominantArm => !IsSevered(BodyParts.DominantArm(Handedness));

        /// <summary>
        /// The arm that holds the weapon, shield or club: the dominant arm while it is intact, then the other one (GDD §12); null
        /// once both are gone.
        /// </summary>
        public BodyPart? HeldItemArm
        {
            get
            {
                if (HasDominantArm) return BodyParts.DominantArm(Handedness);
                BodyPart off = BodyParts.OffArm(Handedness);
                return IsSevered(off) ? (BodyPart?)null : off;
            }
        }

        public int RemainingArms => CountIntact(BodyPart.LeftArm, BodyPart.RightArm);

        public int RemainingLegs => CountIntact(BodyPart.LeftLeg, BodyPart.RightLeg);

        /// <summary>Limb damage never heals during a match (GDD §11).</summary>
        public float GetLimbDamage(BodyPart part) => _limbDamage[(int)part];

        public void SetLimbDamage(BodyPart part, float damage) => _limbDamage[(int)part] = damage;

        public bool IsSevered(BodyPart part) => _severed[(int)part];

        public void MarkSevered(BodyPart part) => _severed[(int)part] = true;

        public FighterState Clone()
        {
            var copy = new FighterState(Handedness, Hp, CurrentWeapon) { ConsecutiveIdleTurns = ConsecutiveIdleTurns, HeldLimb = HeldLimb };
            _limbDamage.CopyTo(copy._limbDamage, 0);
            _severed.CopyTo(copy._severed, 0);
            copy.Statuses.AddRange(Statuses);
            copy.LostWeapons.UnionWith(LostWeapons);
            return copy;
        }

        private int CountIntact(BodyPart first, BodyPart second) => (IsSevered(first) ? 0 : 1) + (IsSevered(second) ? 0 : 1);
    }
}
