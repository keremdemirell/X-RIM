using System;
using System.Collections.Generic;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// Which sounds each played event makes (GDD §2, Decided; presentation only):
    /// <list type="bullet">
    /// <item>A swing (<see cref="AttackStartedEvent"/>): a whoosh, and the attacker's effort grunt, like a tennis player's (not for a
    /// shield raised).</item>
    /// <item>A body move: its sound, and an effort grunt for a lunge or a jump.</item>
    /// <item>A hit: a light or heavy impact (a head hit its own clang), louder with more damage; the victim grunts, or cries out at a
    /// heavy hit.</item>
    /// <item>A clash (§10): a rebound's ring, a crush's crunch, a slide's scrape. A block (§7): a full or a partial block.</item>
    /// <item>A sever (§12): the joint snaps, bolts scatter, the victim cries out. A death (§11): the torso splits, bolts scatter, a KO
    /// scream. A dropped weapon clatters.</item>
    /// <item>The electric wall (§13): a warning's crackle; a zap, and its owner cries out.</item>
    /// </list>
    /// Effects of a blow use the attacker's effects set (a cosmetic, §16); grunts the grunting dummy's pack.
    /// </summary>
    public static class SoundCueMapper
    {
        private const float FullVolume = 1f;

        public static void Add(MatchEvent matchEvent, SoundMappingSettings settings, List<SoundRequest> into)
        {
            Guard.NotNull(matchEvent, nameof(matchEvent));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(into, nameof(into));
            switch (matchEvent)
            {
                case AttackStartedEvent attack:
                    into.Add(new SoundRequest(SoundCue.SwingWhoosh, attack.Side, FullVolume));
                    if (attack.Kind != WeaponKind.Shield) into.Add(new SoundRequest(SoundCue.GruntAttack, attack.Side, FullVolume));
                    break;
                case BodyMoveStartedEvent move when move.Move != BodyMove.None:
                    into.Add(new SoundRequest(SoundCue.BodyMove, move.Side, FullVolume));
                    if (move.Move == BodyMove.Lunge || move.Move == BodyMove.Jump) into.Add(new SoundRequest(SoundCue.GruntEffort, move.Side, FullVolume));
                    break;
                case HitLandedEvent hit:
                    AddHit(hit, settings, into);
                    break;
                case WeaponClashEvent clash:
                    into.Add(new SoundRequest(ClashCue(clash.Result.Kind), clash.Result.Winner, FullVolume));
                    break;
                case ShieldBlockEvent block:
                    into.Add(new SoundRequest(block.Result.AttackStopped ? SoundCue.BlockFull : SoundCue.BlockPartial, block.Blocker, FullVolume));
                    break;
                case LimbSeveredEvent severed:
                    into.Add(new SoundRequest(SoundCue.JointSnap, severed.Side.Opponent(), FullVolume));
                    into.Add(new SoundRequest(SoundCue.BoltScatter, severed.Side.Opponent(), FullVolume));
                    into.Add(new SoundRequest(SoundCue.GruntBigHurt, severed.Side, FullVolume));
                    break;
                case FighterDiedEvent died:
                    into.Add(new SoundRequest(SoundCue.TorsoSplit, null, FullVolume));
                    into.Add(new SoundRequest(SoundCue.BoltScatter, null, FullVolume));
                    into.Add(new SoundRequest(SoundCue.GruntKnockOut, died.Side, FullVolume));
                    break;
                case WeaponDroppedEvent dropped:
                    into.Add(new SoundRequest(SoundCue.WeaponDrop, dropped.Side, FullVolume));
                    break;
                case ElectricWallEvent wall when wall.Phase == ElectricWallPhase.Warning:
                    into.Add(new SoundRequest(SoundCue.WallWarning, null, FullVolume));
                    break;
                case ElectricWallEvent wall:
                    into.Add(new SoundRequest(SoundCue.WallZap, null, FullVolume));
                    into.Add(new SoundRequest(SoundCue.GruntBigHurt, wall.Side, FullVolume));
                    break;
            }
        }

        /// <summary>How loud a landed hit plays: from the quietest up to full at the full-volume damage.</summary>
        public static float HitVolume(float damage, SoundMappingSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            float share = settings.FullVolumeDamage > 0f ? XMath.Clamp01(Math.Max(0f, damage) / settings.FullVolumeDamage) : FullVolume;
            return settings.MinHitVolume + (FullVolume - settings.MinHitVolume) * share;
        }

        private static void AddHit(HitLandedEvent hit, SoundMappingSettings settings, List<SoundRequest> into)
        {
            float damage = hit.Damage.Damage;
            bool heavy = damage >= settings.HeavyHitMinDamage;
            SoundCue impact = hit.Hit.Part == BodyPart.Head ? SoundCue.HitHead : heavy ? SoundCue.HitHeavy : SoundCue.HitLight;
            into.Add(new SoundRequest(impact, hit.Hit.Attacker, HitVolume(damage, settings)));
            into.Add(new SoundRequest(heavy ? SoundCue.GruntBigHurt : SoundCue.GruntHurt, hit.Hit.Victim, FullVolume));
        }

        private static SoundCue ClashCue(ClashKind kind)
        {
            switch (kind)
            {
                case ClashKind.CrushThrough: return SoundCue.ClashCrush;
                case ClashKind.BothSlidePast: return SoundCue.ClashSlide;
                default: return SoundCue.ClashRebound;
            }
        }
    }
}
