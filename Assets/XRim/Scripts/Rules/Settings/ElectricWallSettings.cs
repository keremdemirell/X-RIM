using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>Electric wall anti-stalemate (GDD §13). Trigger and sequence are Decided; numbers are Tunable.</summary>
    [Serializable]
    public sealed class ElectricWallSettings
    {
        [Placeholder("§13 wall damage is Tunable with no value")]
        public float Damage = 10f;

        [Placeholder("§13 bounce force is Tunable with no value")]
        public float BounceImpulse = 5f;

        [Placeholder("§13 advance distance is Tunable with no value")]
        public float AdvanceDistanceUnits = 50f;

        /// <summary>How far behind the dummy the warning wall appears on turn N.</summary>
        [Placeholder("§13 'appears behind the dummy': distance not given")]
        public float SpawnOffsetBehindUnits = 60f;

        [GddTbd("§13", "Does wall damage grow with each retreat?")]
        public float DamageGrowthPerConsecutiveRetreat = 0f;

        [GddTbd("§13", "Wall damage when knocked into it")]
        public bool DamageWhenKnockedInto = false;

        [GddTbd("§14", "Electric wall in sudden death")]
        public bool ActiveInSuddenDeath = true;
    }
}
