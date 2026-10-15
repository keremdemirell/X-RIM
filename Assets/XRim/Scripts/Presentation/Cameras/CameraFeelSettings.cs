using System;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// How the camera frames the duel and reacts to it (GDD §2: camera behaviour is TBD; the fixed side view stays the default; presentation
    /// only). Plain data read from <see cref="FeelConfig"/> at each turn, so tuning edits show on the next one. It also says which events
    /// shake the camera and how hard, and which ones punch in.
    /// </summary>
    public sealed class CameraFeelSettings
    {
        /// <summary>A partial shield block shakes this share of a full block's shake.</summary>
        private const float PartialBlockShakeShare = 0.5f;

        public CameraFramingMode Framing;
        public float DuelViewHeightUnits;
        public float FramingMarginBehindUnits;
        public float FramingFloorMarginUnits;
        public float FramingEdgeMarginUnits;
        public float FramingGlideSeconds;

        /// <summary>The duel framing never shows more than this height (the whole-arena view); 0 = no limit.</summary>
        public float MaxViewHeightUnits;

        public bool ShakeOnImpacts;
        public float ShakeBaseViewFraction;
        public float ShakePerDamageViewFraction;
        public float MaxShakeViewFraction;
        public float CrushShakeViewFraction;
        public float ClashShakeViewFraction;
        public float WallZapShakeViewFraction;
        public float ShakeSeconds;
        public float ShakeFrequencyHz;

        public bool PunchInOnBigMoments;
        public float PunchInViewFraction = 1f;
        public float BigPunchInViewFraction = 1f;
        public float PunchInSeconds;
        public float PunchHoldSeconds;
        public float PunchOutSeconds;

        public static CameraFeelSettings From(FeelConfig config, float maxViewHeightUnits)
        {
            Guard.NotNull(config, nameof(config));
            return new CameraFeelSettings
            {
                Framing = config.CameraFraming,
                DuelViewHeightUnits = config.DuelViewHeightUnits,
                FramingMarginBehindUnits = config.FramingMarginBehindUnits,
                FramingFloorMarginUnits = config.FramingFloorMarginUnits,
                FramingEdgeMarginUnits = config.FramingEdgeMarginUnits,
                FramingGlideSeconds = config.FramingGlideSeconds,
                MaxViewHeightUnits = maxViewHeightUnits,
                ShakeOnImpacts = config.ShakeOnImpacts,
                ShakeBaseViewFraction = config.ShakeBaseViewFraction,
                ShakePerDamageViewFraction = config.ShakePerDamageViewFraction,
                MaxShakeViewFraction = config.MaxShakeViewFraction,
                CrushShakeViewFraction = config.CrushShakeViewFraction,
                ClashShakeViewFraction = config.ClashShakeViewFraction,
                WallZapShakeViewFraction = config.WallZapShakeViewFraction,
                ShakeSeconds = config.ShakeSeconds,
                ShakeFrequencyHz = config.ShakeFrequencyHz,
                PunchInOnBigMoments = config.PunchInOnBigMoments,
                PunchInViewFraction = config.PunchInViewFraction,
                BigPunchInViewFraction = config.BigPunchInViewFraction,
                PunchInSeconds = config.PunchInSeconds,
                PunchHoldSeconds = config.PunchHoldSeconds,
                PunchOutSeconds = config.PunchOutSeconds,
            };
        }

        /// <summary>
        /// How hard an event shakes the camera, as a share of the screen height (0 = not at all): a hit by its damage, a death and a
        /// sever the most, a crush, a rebound or a block, and a wall zap by their own values. Off when shaking is switched off.
        /// </summary>
        public float ShakeFor(MatchEvent matchEvent)
        {
            if (!ShakeOnImpacts) return 0f;
            float shake;
            switch (matchEvent)
            {
                case HitLandedEvent hit:
                    shake = ShakeBaseViewFraction + ShakePerDamageViewFraction * Math.Max(0f, hit.Damage.Damage);
                    break;
                case FighterDiedEvent _:
                case LimbSeveredEvent _:
                    shake = MaxShakeViewFraction;
                    break;
                case WeaponClashEvent clash:
                    shake = clash.Result.Kind == ClashKind.CrushThrough ? CrushShakeViewFraction
                        : clash.Result.Kind == ClashKind.BothSlidePast ? 0f
                        : ClashShakeViewFraction;
                    break;
                case ShieldBlockEvent block:
                    shake = block.Result.AttackStopped ? ClashShakeViewFraction : ClashShakeViewFraction * PartialBlockShakeShare;
                    break;
                case ElectricWallEvent wall:
                    shake = wall.Phase == ElectricWallPhase.Active ? WallZapShakeViewFraction : 0f;
                    break;
                default:
                    shake = 0f;
                    break;
            }

            return XMath.Clamp(shake, 0f, Math.Max(0f, MaxShakeViewFraction));
        }

        /// <summary>
        /// The share of its size the view punches in to for an event (1 = no punch): the biggest moments (a sever, a death) the most, a
        /// crush and an interrupt counter-hit a little. Off when punch-ins are switched off.
        /// </summary>
        public float PunchFor(MatchEvent matchEvent)
        {
            if (!PunchInOnBigMoments) return 1f;
            switch (matchEvent)
            {
                case FighterDiedEvent _:
                case LimbSeveredEvent _:
                    return Fraction(BigPunchInViewFraction);
                case WeaponClashEvent clash when clash.Result.Kind == ClashKind.CrushThrough:
                case AttackInterruptedEvent _:
                    return Fraction(PunchInViewFraction);
                default:
                    return 1f;
            }
        }

        private static float Fraction(float value) => value > 0f && value < 1f ? value : 1f;
    }
}
