using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// One side's attack during one turn, as the hit rules track it: the weapon, the body move that carries it (D13), the body
    /// parts it has hit, how much of its path speed it still has (D26) and whether it has stopped. Built fresh every turn.
    /// </summary>
    public sealed class TurnAttack
    {
        private readonly HashSet<BodyPart> _partsHit = new HashSet<BodyPart>();

        /// <param name="weapon">The held weapon, shield or club; null when the dummy holds nothing.</param>
        /// <param name="bodyMove">The planned body move's data; null without one.</param>
        public TurnAttack(WeaponStats weapon, BodyMoveStats bodyMove = null)
        {
            Weapon = weapon;
            BodyMove = bodyMove;
        }

        public WeaponStats Weapon { get; }
        public BodyMoveStats BodyMove { get; }
        public int HitsLanded { get; private set; }

        /// <summary>
        /// The share of its path speed the weapon still has: 1 until its first hit, then multiplied by the weapon's
        /// <see cref="WeaponStats.SpeedKeptAfterHitFraction"/> at every hit (D26).
        /// </summary>
        public float SpeedFraction { get; private set; } = 1f;

        public AttackStop Stop { get; private set; }
        public bool IsStopped => Stop != AttackStop.None;

        /// <summary>The weapon crushed through a clash earlier this turn, so its hits deal less (GDD §10). Session 07 sets it.</summary>
        public bool CrushedThrough { get; set; }

        /// <summary>D26: a weapon hits each body part at most once per turn.</summary>
        public bool HasHit(BodyPart part) => _partsHit.Contains(part);

        internal void RegisterHit(BodyPart part, int maxHits)
        {
            _partsHit.Add(part);
            HitsLanded++;
            SpeedFraction *= Weapon != null ? XMath.Clamp01(Weapon.SpeedKeptAfterHitFraction) : 0f;
            if (HitsLanded >= maxHits || SpeedFraction <= 0f) Stop = AttackStop.LastHit;
        }

        internal void StopWith(AttackStop reason)
        {
            if (!IsStopped) Stop = reason;
        }
    }
}
