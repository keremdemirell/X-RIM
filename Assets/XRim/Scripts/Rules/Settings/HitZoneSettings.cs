using System;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Zone damage multipliers (GDD §11, all Tunable). Which zones can be severed is Decided and
    /// lives in code (<see cref="BodyParts.IsSeverable"/>).
    /// </summary>
    [Serializable]
    public sealed class HitZoneSettings
    {
        public float HeadMultiplier = 2.5f;
        public float TorsoMultiplier = 1.0f;
        public float ArmMultiplier = 0.8f;
        public float LegMultiplier = 0.7f;

        public float MultiplierFor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return HeadMultiplier;
                case HitZone.Torso: return TorsoMultiplier;
                case HitZone.Arm: return ArmMultiplier;
                default: return LegMultiplier;
            }
        }
    }
}
