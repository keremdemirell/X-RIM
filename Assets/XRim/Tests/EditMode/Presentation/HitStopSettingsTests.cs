using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Feel;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The freeze each landing gets (Session 13): a hit longer the more damage it deals and longer still on the head, up to a cap; a kill,
    /// a sever, a crush, a rebound, a block and a wall zap their own; nothing for what does not land.
    /// </summary>
    public sealed class HitStopSettingsTests
    {
        private const float Tolerance = 1e-5f;

        private static readonly HitStopSettings Settings = new HitStopSettings
        {
            BaseSeconds = 0.05f, SecondsPerDamage = 0.005f, HeadMultiplier = 1.5f, MaxSeconds = 0.16f, KillSeconds = 0.22f, SeverSeconds = 0.12f,
            CrushSeconds = 0.1f, ReboundSeconds = 0.07f, FullBlockSeconds = 0.06f, PartialBlockSeconds = 0.04f, WallZapSeconds = 0.1f,
        };

        private static readonly SimTime Time = SimTime.FromSeconds(0.3);

        private static HitLandedEvent Hit(BodyPart part, float damage, float hpDamage) => new HitLandedEvent(Time,
            new HitFacts(Side.Left, part, WeaponIds.Mace, Time, false), new DamageResult(damage, hpDamage, 0f, false, false));

        [Test]
        public void AHit_FreezesLongerTheMoreDamageItDeals()
        {
            Assert.That(Settings.SecondsFor(Hit(BodyPart.Torso, 8f, 8f)), Is.EqualTo(0.09f).Within(Tolerance));
            Assert.That(Settings.SecondsFor(Hit(BodyPart.Torso, 20f, 20f)), Is.EqualTo(0.15f).Within(Tolerance));
        }

        [Test]
        public void AHeadHit_FreezesLonger_UpToTheCap()
        {
            Assert.That(Settings.SecondsFor(Hit(BodyPart.Head, 4f, 4f)), Is.EqualTo(0.105f).Within(Tolerance));
            Assert.That(Settings.SecondsFor(Hit(BodyPart.Head, 50f, 50f)), Is.EqualTo(0.16f).Within(Tolerance));
        }

        [Test]
        public void AHitClampedByTheNoInstantKoRule_StillFreezesForItsFullDamage()
        {
            Assert.That(Settings.SecondsFor(Hit(BodyPart.Torso, 20f, 1f)), Is.EqualTo(0.15f).Within(Tolerance));
        }

        [Test]
        public void BigMoments_GetTheirOwnFreezes()
        {
            Assert.That(Settings.SecondsFor(new FighterDiedEvent(Time, Side.Right)), Is.EqualTo(0.22f));
            Assert.That(Settings.SecondsFor(new LimbSeveredEvent(Time, Side.Right, BodyPart.LeftArm)), Is.EqualTo(0.12f));
            Assert.That(Settings.SecondsFor(Clash(ClashKind.CrushThrough, Side.Left)), Is.EqualTo(0.1f));
            Assert.That(Settings.SecondsFor(Clash(ClashKind.BothRebound, null)), Is.EqualTo(0.07f));
            Assert.That(Settings.SecondsFor(Clash(ClashKind.LighterDeflectsHeavier, Side.Right)), Is.EqualTo(0.07f));
            Assert.That(Settings.SecondsFor(Block(true)), Is.EqualTo(0.06f));
            Assert.That(Settings.SecondsFor(Block(false)), Is.EqualTo(0.04f));
            Assert.That(Settings.SecondsFor(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Active, 10f, -500f)), Is.EqualTo(0.1f));
        }

        [Test]
        public void WhatDoesNotLand_DoesNotFreeze()
        {
            Assert.That(Settings.SecondsFor(Clash(ClashKind.BothSlidePast, null)), Is.EqualTo(0f));
            Assert.That(Settings.SecondsFor(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Warning, 0f, -500f)), Is.EqualTo(0f));
            Assert.That(Settings.SecondsFor(new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Lunge)), Is.EqualTo(0f));
            Assert.That(Settings.SecondsFor(new AttackInterruptedEvent(Time, Side.Right)), Is.EqualTo(0f), "its hit already froze");
        }

        private static WeaponClashEvent Clash(ClashKind kind, Side? winner) =>
            new WeaponClashEvent(Time, new ClashResult(kind, winner, 60f, 3f, 12f), new Vec2(300f, 120f));

        private static ShieldBlockEvent Block(bool full) =>
            new ShieldBlockEvent(Time, Side.Right, new BlockResult(full, full ? 0f : 0.5f, false), new Vec2(300f, 120f), 80f, 0.1f);
    }
}
