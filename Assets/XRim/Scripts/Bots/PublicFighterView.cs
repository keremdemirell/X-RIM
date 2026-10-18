using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;

namespace XRim.Bots
{
    /// <summary>
    /// What anyone watching the board can see of one dummy (Session 16): its HP bar, the weapon in its hand, its limbs (the damage they took
    /// and which are gone) and its electric wall. A copy taken when planning opens, so a bot never holds the rules state itself and can never
    /// read what stays hidden: the signature charge and uses, the statuses' details, the plan.
    /// </summary>
    public sealed class PublicFighterView
    {
        private readonly float[] _limbDamage = new float[BodyParts.Count];
        private readonly bool[] _severed = new bool[BodyParts.Count];

        public PublicFighterView(FighterState fighter, ElectricWallState wall)
        {
            Guard.NotNull(fighter, nameof(fighter));
            Handedness = fighter.Handedness;
            Hp = fighter.Hp;
            CurrentWeapon = fighter.CurrentWeapon;
            foreach (BodyPart part in BodyParts.All)
            {
                _limbDamage[(int)part] = fighter.GetLimbDamage(part);
                _severed[(int)part] = fighter.IsSevered(part);
            }

            WallIsUp = wall != null && wall.IsUp;
            WallPhase = wall != null ? wall.Phase : ElectricWallPhase.Inactive;
            WallPositionUnits = wall != null ? wall.PositionUnits : 0f;
        }

        public Handedness Handedness { get; }
        public float Hp { get; }
        public bool IsDead => Hp <= 0f;

        /// <summary>The weapon in its hand on the frozen board (GDD §6: a switch shows at once; see the opponent's public state for one made since).</summary>
        public WeaponId CurrentWeapon { get; }

        /// <summary>Its electric wall stands behind it (GDD §13); a warning means one more step back zaps it.</summary>
        public bool WallIsUp { get; }
        public ElectricWallPhase WallPhase { get; }
        public float WallPositionUnits { get; }

        public float GetLimbDamage(BodyPart part) => _limbDamage[(int)part];

        public bool IsSevered(BodyPart part) => _severed[(int)part];
    }
}
