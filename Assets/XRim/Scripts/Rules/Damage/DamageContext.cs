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

        /// <summary>The share of its damage the weapon still deals after shields partially blocked it this turn (GDD §7; A5).</summary>
        public float ShieldBlockDamageMultiplier { get; }

        /// <summary>
        /// The HP damage the attacker had taken in the match before this instant (GDD §15 build-up). Read before the hits of the instant
        /// were applied, so two hits at the same instant do not depend on the order they are taken in.
        /// </summary>
        public float AttackerHpDamageTaken { get; }

        public HitZone Zone => Hit.Part.ToHitZone();
        public float ZoneMultiplier => Settings.HitZones.MultiplierFor(Zone);

        public DamageContext(HitFacts hit, WeaponStats weapon, RulesSettings settings, BodyMoveStats attackerBodyMove = null,
            bool crushedThrough = false, float followThroughSpeedFraction = 1f, float shieldBlockDamageMultiplier = 1f,
            float attackerHpDamageTaken = 0f)
        {
            Hit = hit;
            Weapon = Guard.NotNull(weapon, nameof(weapon));
            Settings = Guard.NotNull(settings, nameof(settings));
            AttackerBodyMove = attackerBodyMove;
            CrushedThrough = crushedThrough;
            FollowThroughSpeedFraction = followThroughSpeedFraction;
            ShieldBlockDamageMultiplier = shieldBlockDamageMultiplier;
            AttackerHpDamageTaken = attackerHpDamageTaken;
        }
    }
}
