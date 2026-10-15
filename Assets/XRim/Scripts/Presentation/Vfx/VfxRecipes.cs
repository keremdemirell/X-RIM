using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// What each played event sprays (GDD §2, Decided: bolts, nuts, springs, sparks and hydraulic fluid; no blood; presentation only):
    /// <list type="bullet">
    /// <item>A <b>hit</b>: sparks off the blow, more the harder it lands, flying on through the struck part; a flash; a heavy hit also
    /// knocks bolts and nuts loose.</item>
    /// <item>A <b>clash</b> (§10): two weapons rebounding throw a shower of sparks; a crush-through a shock ring of dust, sparks and debris;
    /// a deflection or a slide skids a few sparks.</item>
    /// <item>A <b>block</b> (§7): a bright ring on the shield for a full block, a smaller orange one for a partial block.</item>
    /// <item>A <b>sever</b> (§12): bolts, nuts, springs and a gear burst from the joint, and a jet of hydraulic fluid.</item>
    /// <item>A <b>death</b> (§11): the torso splits in a bigger burst with puffs of steam.</item>
    /// <item>An <b>electric wall</b> (§13): arcs from the wall into the dummy on a zap; a crackle up the wall when the warning springs up.</item>
    /// <item>A <b>body move</b> (§5): a little dust from the floor under the dummy.</item>
    /// </list>
    /// Pure: where (from <see cref="MatchEventLocator"/>), which way, how many; <see cref="MatchVfx"/> draws it.
    /// </summary>
    public static class VfxRecipes
    {
        // The look of each effect (technical: shapes and cones, not gameplay). Degrees counter-clockwise from +X.
        private const float UpDegrees = 90f;
        private const float HalfTurnDegrees = 180f;
        private const float SparkLiftDegrees = 20f;
        private const float HitSparkSpreadDegrees = 70f;
        private const float ShowerSpreadDegrees = 100f;
        private const float SkidSpreadDegrees = 35f;
        private const float BlockSparkSpreadDegrees = 60f;
        private const float DebrisSpreadDegrees = 70f;
        private const float BurstSparkSpreadDegrees = 90f;
        private const float JetBackDegrees = 25f;
        private const float JetSpreadDegrees = 22f;
        private const float JetSpeedMultiplier = 1.4f;
        private const float KnockedLooseSpeedShare = 0.6f;
        private const float CrushSparkShare = 0.75f;
        private const float SkidSparkShare = 1f / 3f;
        private const float ShowerSpeedMultiplier = 1.1f;
        internal const float NutSizeMultiplier = 1.3f;
        internal const float SpringSizeMultiplier = 1.4f;
        internal const float GearSizeMultiplier = 1.8f;
        private const float GearSpeedShare = 0.8f;
        private const int SeverSparks = 12;
        private const int DeathSparks = 20;
        private const int FullBlockSparks = 10;
        private const int PartialBlockSparks = 5;
        internal const float FlashSizeUnits = 60f;
        internal const float BigFlashSizeUnits = 140f;
        private const float FlashSeconds = 0.09f;
        private const float BigFlashSeconds = 0.16f;
        internal const float FullBlockRingUnits = 120f;
        internal const float PartialBlockRingUnits = 70f;
        private const float BlockRingSeconds = 0.2f;
        internal const float CrushRingUnits = 160f;
        private const float CrushRingSeconds = 0.25f;
        internal const float SteamPuffUnits = 70f;
        private const float SteamSeconds = 0.9f;
        private const float SteamSpeedUnitsPerSecond = 90f;
        private const float SteamSpreadDegrees = 40f;
        private const int DustPuffs = 3;
        internal const float DustPuffUnits = 40f;
        private const float DustSeconds = 0.45f;
        private const float DustSpeedUnitsPerSecond = 120f;
        private const float DustSpreadDegrees = 75f;
        private const int ZapArcs = 3;
        private const float ZapArcSeconds = 0.25f;
        private const float ZapJitterUnits = 18f;
        private const int ZapSparks = 14;
        private const float ZapSparkSpreadDegrees = 120f;
        internal const float ZapFlashSizeUnits = 90f;
        private const int CrackleArcs = 2;
        private const float CrackleArcSeconds = 0.18f;
        private const float CrackleJitterUnits = 10f;
        private const float CrackleHeightUnits = 320f;
        private const int CrackleSparks = 6;
        private const float CrackleSparkSpeedShare = 0.5f;

        /// <summary>Adds the event's effects; nothing for an event that sprays nothing (a stun, a dropped weapon, a raw contact).</summary>
        public static void Add(MatchEvent matchEvent, TurnResult turn, VfxSettings settings, List<VfxBurst> bursts, List<VfxArc> arcs)
        {
            Guard.NotNull(matchEvent, nameof(matchEvent));
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(bursts, nameof(bursts));
            Guard.NotNull(arcs, nameof(arcs));
            switch (matchEvent)
            {
                case HitLandedEvent hit:
                    AddHit(hit, turn, settings, bursts);
                    break;
                case WeaponClashEvent clash:
                    AddClash(clash, settings, bursts);
                    break;
                case ShieldBlockEvent block:
                    AddBlock(block, settings, bursts);
                    break;
                case LimbSeveredEvent severed when MatchEventLocator.TryLocate(turn, severed, out Vec2 joint):
                    AddMechanicalBurst(joint, severed.Side, 1f, severed.Side.Opponent(), settings, bursts);
                    bursts.Add(new VfxBurst(VfxParticleKind.Spark, SeverSparks, joint, UpDegrees, BurstSparkSpreadDegrees, settings.SparkSpeedUnitsPerSecond,
                        settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark, severed.Side.Opponent()));
                    bursts.Add(Flash(joint, BigFlashSizeUnits, BigFlashSeconds, VfxColorRole.Flash, severed.Side.Opponent()));
                    break;
                case FighterDiedEvent died when MatchEventLocator.TryLocate(turn, died, out Vec2 torso):
                    AddMechanicalBurst(torso, died.Side, settings.DeathBurstMultiplier, null, settings, bursts);
                    bursts.Add(new VfxBurst(VfxParticleKind.Spark, DeathSparks, torso, UpDegrees, BurstSparkSpreadDegrees, settings.SparkSpeedUnitsPerSecond,
                        settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark));
                    bursts.Add(new VfxBurst(VfxParticleKind.Puff, Math.Max(0, settings.DeathSteamPuffs), torso, UpDegrees, SteamSpreadDegrees,
                        SteamSpeedUnitsPerSecond, SteamPuffUnits, SteamSeconds, VfxColorRole.Steam));
                    bursts.Add(Flash(torso, BigFlashSizeUnits, BigFlashSeconds, VfxColorRole.Flash, null));
                    break;
                case ElectricWallEvent wall:
                    AddWall(wall, turn, settings, bursts, arcs);
                    break;
                case BodyMoveStartedEvent move when move.Move != BodyMove.None && MatchEventLocator.TryLocate(turn, move, out Vec2 pelvis):
                    bursts.Add(new VfxBurst(VfxParticleKind.Puff, DustPuffs, new Vec2(pelvis.X, 0f), UpDegrees, DustSpreadDegrees, DustSpeedUnitsPerSecond,
                        DustPuffUnits, DustSeconds, VfxColorRole.Dust));
                    break;
            }
        }

        /// <summary>How many sparks a hit throws: a base, plus a share per point of damage (before the D27 clamp), up to the most.</summary>
        public static int HitSparks(float damage, VfxSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            int count = settings.HitSparksBase + (int)Math.Round(settings.HitSparksPerDamage * Math.Max(0f, damage));
            return Math.Max(0, Math.Min(count, settings.MaxHitSparks));
        }

        private static void AddHit(HitLandedEvent hit, TurnResult turn, VfxSettings settings, List<VfxBurst> bursts)
        {
            if (!MatchEventLocator.TryLocate(turn, hit, out Vec2 point)) return;
            Side attacker = hit.Hit.Attacker;
            float onward = Forward(attacker);
            float damage = hit.Damage.Damage;
            bursts.Add(new VfxBurst(VfxParticleKind.Spark, HitSparks(damage, settings), point, onward, HitSparkSpreadDegrees,
                settings.SparkSpeedUnitsPerSecond, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark, attacker));
            bursts.Add(Flash(point, FlashSizeUnits, FlashSeconds, VfxColorRole.Flash, attacker));
            if (damage >= settings.HeavyHitMinDamage) AddBoltsAndNuts(point, settings.HeavyHitDebrisPieces, KnockedLooseSpeedShare, settings, bursts);
        }

        private static void AddClash(WeaponClashEvent clash, VfxSettings settings, List<VfxBurst> bursts)
        {
            Vec2 point = clash.ContactPointUnits;
            ClashResult result = clash.Result;
            switch (result.Kind)
            {
                case ClashKind.BothRebound:
                    bursts.Add(new VfxBurst(VfxParticleKind.Spark, settings.ClashSparks, point, UpDegrees, ShowerSpreadDegrees,
                        settings.SparkSpeedUnitsPerSecond * ShowerSpeedMultiplier, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark));
                    bursts.Add(Flash(point, FlashSizeUnits, FlashSeconds, VfxColorRole.Flash, null));
                    break;
                case ClashKind.CrushThrough:
                    Side winner = result.Winner ?? Side.Left;
                    bursts.Add(new VfxBurst(VfxParticleKind.Spark, (int)Math.Round(settings.ClashSparks * CrushSparkShare), point, Forward(winner),
                        HitSparkSpreadDegrees, settings.SparkSpeedUnitsPerSecond, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark, winner));
                    bursts.Add(new VfxBurst(VfxParticleKind.Ring, 1, point, UpDegrees, 0f, 0f, CrushRingUnits, CrushRingSeconds, VfxColorRole.Dust, winner));
                    bursts.Add(Flash(point, BigFlashSizeUnits, FlashSeconds, VfxColorRole.Flash, winner));
                    AddBoltsAndNuts(point, settings.CrushDebrisPieces, KnockedLooseSpeedShare, settings, bursts);
                    break;
                default:
                    bursts.Add(new VfxBurst(VfxParticleKind.Spark, (int)Math.Round(settings.ClashSparks * SkidSparkShare), point, UpDegrees,
                        SkidSpreadDegrees, settings.SparkSpeedUnitsPerSecond, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark));
                    break;
            }
        }

        private static void AddBlock(ShieldBlockEvent block, VfxSettings settings, List<VfxBurst> bursts)
        {
            Vec2 point = block.ContactPointUnits;
            bool full = block.Result.AttackStopped;
            Side blocker = block.Blocker;
            bursts.Add(new VfxBurst(VfxParticleKind.Ring, 1, point, UpDegrees, 0f, 0f, full ? FullBlockRingUnits : PartialBlockRingUnits,
                BlockRingSeconds, full ? VfxColorRole.FullBlock : VfxColorRole.PartialBlock, blocker));
            bursts.Add(new VfxBurst(VfxParticleKind.Spark, full ? FullBlockSparks : PartialBlockSparks, point, Forward(blocker), BlockSparkSpreadDegrees,
                settings.SparkSpeedUnitsPerSecond, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Spark, blocker));
            if (full) bursts.Add(Flash(point, FlashSizeUnits, FlashSeconds, VfxColorRole.FullBlock, blocker));
        }

        private static void AddWall(ElectricWallEvent wall, TurnResult turn, VfxSettings settings, List<VfxBurst> bursts, List<VfxArc> arcs)
        {
            if (wall.Phase == ElectricWallPhase.Active)
            {
                if (!MatchEventLocator.TryLocate(turn, wall, out Vec2 torso)) return;
                var face = new Vec2(wall.PositionUnits, torso.Y);
                for (int i = 0; i < ZapArcs; i++)
                {
                    arcs.Add(new VfxArc(face, torso, ZapArcSeconds, ZapJitterUnits));
                }

                bursts.Add(new VfxBurst(VfxParticleKind.Spark, ZapSparks, torso, UpDegrees, ZapSparkSpreadDegrees, settings.SparkSpeedUnitsPerSecond,
                    settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Zap));
                bursts.Add(Flash(torso, ZapFlashSizeUnits, BigFlashSeconds, VfxColorRole.Zap, null));
                return;
            }

            var foot = new Vec2(wall.PositionUnits, 0f);
            var top = new Vec2(wall.PositionUnits, CrackleHeightUnits);
            for (int i = 0; i < CrackleArcs; i++)
            {
                arcs.Add(new VfxArc(foot, top, CrackleArcSeconds, CrackleJitterUnits));
            }

            bursts.Add(new VfxBurst(VfxParticleKind.Spark, CrackleSparks, new Vec2(wall.PositionUnits, CrackleHeightUnits * 0.5f), UpDegrees,
                ZapSparkSpreadDegrees, settings.SparkSpeedUnitsPerSecond * CrackleSparkSpeedShare, settings.SparkSizeUnits, settings.SparkSeconds, VfxColorRole.Zap));
        }

        /// <summary>Bolts, nuts, springs and gears bursting upward, and a jet of hydraulic fluid leaving backward (away from the opponent).</summary>
        private static void AddMechanicalBurst(Vec2 origin, Side owner, float multiplier, Side? styleSide, VfxSettings settings, List<VfxBurst> bursts)
        {
            float scale = Math.Max(0f, multiplier);
            AddBoltsAndNuts(origin, Scaled(settings.SeverDebrisPieces, scale), 1f, settings, bursts);
            bursts.Add(new VfxBurst(VfxParticleKind.Spring, Scaled(settings.SeverSprings, scale), origin, UpDegrees, DebrisSpreadDegrees,
                settings.DebrisSpeedUnitsPerSecond, settings.DebrisSizeUnits * SpringSizeMultiplier, 0f, VfxColorRole.Spring, styleSide));
            bursts.Add(new VfxBurst(VfxParticleKind.Gear, Scaled(settings.SeverGears, scale), origin, UpDegrees, DebrisSpreadDegrees,
                settings.DebrisSpeedUnitsPerSecond * GearSpeedShare, settings.DebrisSizeUnits * GearSizeMultiplier, 0f, VfxColorRole.Gear, styleSide));
            bursts.Add(new VfxBurst(VfxParticleKind.Drop, Scaled(settings.SeverFluidDrops, scale), origin, UpDegrees + owner.FacingSign() * JetBackDegrees,
                JetSpreadDegrees, settings.DebrisSpeedUnitsPerSecond * JetSpeedMultiplier, settings.FluidDropSizeUnits, 0f, VfxColorRole.Fluid, styleSide));
        }

        private static void AddBoltsAndNuts(Vec2 origin, int pieces, float speedShare, VfxSettings settings, List<VfxBurst> bursts)
        {
            if (pieces <= 0) return;
            int bolts = (pieces + 1) / 2;
            float speed = settings.DebrisSpeedUnitsPerSecond * speedShare;
            bursts.Add(new VfxBurst(VfxParticleKind.Bolt, bolts, origin, UpDegrees, DebrisSpreadDegrees, speed, settings.DebrisSizeUnits, 0f,
                VfxColorRole.Bolt));
            bursts.Add(new VfxBurst(VfxParticleKind.Nut, pieces - bolts, origin, UpDegrees, DebrisSpreadDegrees, speed,
                settings.DebrisSizeUnits * NutSizeMultiplier, 0f, VfxColorRole.Nut));
        }

        private static VfxBurst Flash(Vec2 point, float sizeUnits, float seconds, VfxColorRole color, Side? styleSide) =>
            new VfxBurst(VfxParticleKind.Flash, 1, point, UpDegrees, 0f, 0f, sizeUnits, seconds, color, styleSide);

        /// <summary>Onward from a side (toward its opponent) and a little up: where sparks off its blow fly.</summary>
        private static float Forward(Side side) => side.FacingSign() > 0 ? SparkLiftDegrees : HalfTurnDegrees - SparkLiftDegrees;

        private static int Scaled(int count, float multiplier) => Math.Max(0, (int)Math.Round(count * multiplier));
    }
}
