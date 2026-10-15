using System;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// How long playback freezes for each kind of landing (GDD pillar 1; real seconds, presentation only). A hit freezes longer the more
    /// damage it deals, longer still on the head, and a kill, a sever or a crush gets its own bigger freeze. Plain data read from
    /// <see cref="FeelConfig"/> at the start of each play, so tuning edits show on the next turn.
    /// </summary>
    public sealed class HitStopSettings
    {
        public float BaseSeconds;
        public float SecondsPerDamage;
        public float HeadMultiplier = 1f;
        public float MaxSeconds;
        public float KillSeconds;
        public float SeverSeconds;
        public float CrushSeconds;
        public float ReboundSeconds;
        public float FullBlockSeconds;
        public float PartialBlockSeconds;
        public float WallZapSeconds;

        public static HitStopSettings From(FeelConfig config)
        {
            Guard.NotNull(config, nameof(config));
            return new HitStopSettings
            {
                BaseSeconds = config.HitStopBaseSeconds,
                SecondsPerDamage = config.HitStopSecondsPerDamage,
                HeadMultiplier = config.HeadHitStopMultiplier,
                MaxSeconds = config.MaxHitStopSeconds,
                KillSeconds = config.KillHitStopSeconds,
                SeverSeconds = config.SeverHitStopSeconds,
                CrushSeconds = config.CrushHitStopSeconds,
                ReboundSeconds = config.ReboundHitStopSeconds,
                FullBlockSeconds = config.FullBlockHitStopSeconds,
                PartialBlockSeconds = config.PartialBlockHitStopSeconds,
                WallZapSeconds = config.WallZapHitStopSeconds,
            };
        }

        /// <summary>
        /// The freeze an event calls for; 0 for events that do not land (a body move, a wall's warning, two weapons sliding past).
        /// A hit's freeze follows its damage before the no-instant-KO clamp (D27), so a clamped head hit still feels as hard as it was.
        /// </summary>
        public float SecondsFor(MatchEvent matchEvent)
        {
            switch (matchEvent)
            {
                case HitLandedEvent hit:
                    float seconds = BaseSeconds + SecondsPerDamage * Math.Max(0f, hit.Damage.Damage);
                    if (hit.Hit.Part == BodyPart.Head) seconds *= HeadMultiplier;
                    return Math.Max(0f, Math.Min(seconds, MaxSeconds));
                case FighterDiedEvent _:
                    return KillSeconds;
                case LimbSeveredEvent _:
                    return SeverSeconds;
                case WeaponClashEvent clash:
                    switch (clash.Result.Kind)
                    {
                        case ClashKind.CrushThrough:
                            return CrushSeconds;
                        case ClashKind.BothRebound:
                        case ClashKind.LighterDeflectsHeavier:
                            return ReboundSeconds;
                        default:
                            return 0f;
                    }
                case ShieldBlockEvent block:
                    return block.Result.AttackStopped ? FullBlockSeconds : PartialBlockSeconds;
                case ElectricWallEvent wall:
                    return wall.Phase == ElectricWallPhase.Active ? WallZapSeconds : 0f;
                default:
                    return 0f;
            }
        }
    }
}
