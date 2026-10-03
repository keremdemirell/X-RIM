using XRim.Core;
using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>Inputs to one damage calculation (GDD §11): the hit, the weapon that landed it and what modifies it.</summary>
    public sealed class DamageContext
    {
        public HitFacts Hit { get; }
        public WeaponStats Weapon { get; }
        public RulesSettings Settings { get; }

        /// <summary>The attacker's planned body move this turn (D13 damage bonus); null without one or without data for it.</summary>
        public BodyMoveStats AttackerBodyMove { get; }

        /// <summary>The weapon crushed through a clash earlier this turn, so it deals less damage (GDD §10; set from Session 07).</summary>
        public bool CrushedThrough { get; }

        /// <summary>
        /// The share of its path speed the weapon still has when this hit lands (D26): 1 for its first hit, less after each
        /// hit it has already landed this turn.
        /// </summary>
        public float FollowThroughSpeedFraction { get; }

        public HitZone Zone => Hit.Part.ToHitZone();
        public float ZoneMultiplier => Settings.HitZones.MultiplierFor(Zone);

        public DamageContext(HitFacts hit, WeaponStats weapon, RulesSettings settings, BodyMoveStats attackerBodyMove = null,
            bool crushedThrough = false, float followThroughSpeedFraction = 1f)
        {
            Hit = hit;
            Weapon = Guard.NotNull(weapon, nameof(weapon));
            Settings = Guard.NotNull(settings, nameof(settings));
            AttackerBodyMove = attackerBodyMove;
            CrushedThrough = crushedThrough;
            FollowThroughSpeedFraction = followThroughSpeedFraction;
        }
    }
}
