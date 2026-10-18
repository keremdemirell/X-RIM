using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Scenarios
{
    /// <summary>One dummy's rules state (<see cref="XRim.Rules.Match.FighterState"/>) in plain fields.</summary>
    [Serializable]
    public sealed class FighterStateData
    {
        public Handedness Handedness;
        public float Hp;
        public float HpBeforeSuddenDeath;
        public string CurrentWeapon = string.Empty;
        public float HpDamageTaken;
        public int LifestealHitCount;
        public float LifestealDamageDealt;
        public bool HitLandedThisTurn;
        public float SignatureCharge;
        public int SignatureUses;
        public int ConsecutiveIdleTurns;

        /// <summary>Each limb's damage, indexed by <see cref="BodyPart"/>.</summary>
        public float[] LimbDamage = new float[BodyParts.Count];

        /// <summary>Which limbs are severed, indexed by <see cref="BodyPart"/>.</summary>
        public bool[] Severed = new bool[BodyParts.Count];

        public List<StatusEffectData> Statuses = new List<StatusEffectData>();

        /// <summary>The dummy wields a severed limb as a club (GDD §12): whose and which.</summary>
        public bool HasHeldLimb;
        public Side HeldLimbOwner;
        public BodyPart HeldLimbPart;

        /// <summary>Loadout weapons it can no longer pick, in ordinal order so the file is stable.</summary>
        public List<string> LostWeapons = new List<string>();
    }
}
