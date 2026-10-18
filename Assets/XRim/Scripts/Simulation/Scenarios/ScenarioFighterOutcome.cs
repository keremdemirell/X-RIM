using System;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Scenarios
{
    /// <summary>How one dummy ended a recorded turn: HP, limbs, the statuses it carries into the next turn, where it stands, what it holds.</summary>
    [Serializable]
    public sealed class ScenarioFighterOutcome
    {
        public float Hp;
        public bool IsDead;

        /// <summary>Each limb's damage, indexed by <see cref="BodyPart"/>.</summary>
        public float[] LimbDamage = new float[BodyParts.Count];

        /// <summary>Which limbs are severed, indexed by <see cref="BodyPart"/>.</summary>
        public bool[] Severed = new bool[BodyParts.Count];

        public bool IsStunned;
        public bool IsStaggered;

        /// <summary>The pelvis (the torso's pivot) on the final board, arena units.</summary>
        public Vec2 PelvisUnits;

        public string CurrentWeapon = string.Empty;
    }
}
