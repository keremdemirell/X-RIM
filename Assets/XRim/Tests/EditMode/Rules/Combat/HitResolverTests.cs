using System;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>
    /// Priority, interrupts and hits per weapon (GDD §9, §11; D15, D16, D26, E2). Left holds the rapier, right the mace; both
    /// are right-handed, so each one's weapon arm is its right arm. Both start at full HP with their weapons travelling.
    /// Rapier damage: head 20, torso 8, arm 6.4, leg 5.6. Mace: head 50 (stuns), torso 20, arm 16, leg 14.
    /// </summary>
    public sealed class HitResolverTests
    {
        private const float Tolerance = 1e-4f;
        private const float AlmostDeadHp = 5f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;
        private PerSide<bool> _travelling;
        private HitResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Rapier),
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Mace)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            _travelling = new PerSide<bool>(true, true);
            _resolver = null;
        }

        private WeaponStats Rapier => _settings.FindWeapon(WeaponIds.Rapier);
        private WeaponStats Mace => _settings.FindWeapon(WeaponIds.Mace);
        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        /// <summary>The resolver is built on first use, so a test can change the settings and state first.</summary>
        private HitResolver Resolver => _resolver ?? (_resolver = new HitResolver(_state, _settings, _policies,
            new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(Mace))));

        private static HitFacts Hit(Side attacker, BodyPart part, double milliseconds) =>
            new HitFacts(attacker, part, attacker == Side.Left ? WeaponIds.Rapier : WeaponIds.Mace, SimTime.FromMilliseconds(milliseconds), false);

        private HitResolution Resolve(params HitFacts[] sameInstant) => Resolver.Resolve(sameInstant, _travelling);

        private float Lost(FighterState fighter) => _settings.Damage.MaxHp - fighter.Hp;

        /// <summary>Whether a rapier hit on this part of the right dummy cancels its travelling mace swing.</summary>
        private bool LeftHitCancels(BodyPart part) => Resolve(Hit(Side.Left, part, 200)).Interrupted.Contains(Side.Right);

        // --- Priority and interrupts (§9, D15, D16) --------------------------------------------

        [Test]
        public void TheEarlierHit_LandsFirst_AndAHeadHitCancelsTheLaterAttack()
        {
            HitResolution first = Resolve(Hit(Side.Left, BodyPart.Head, 200));
            HitResolution later = Resolve(Hit(Side.Right, BodyPart.Torso, 500));

            Assert.That(first.Landed, Has.Count.EqualTo(1));
            Assert.That(first.Interrupted, Is.EqualTo(new[] { Side.Right }));
            Assert.That(first.Events.OfType<AttackInterruptedEvent>().Single().Interrupted, Is.EqualTo(Side.Right));
            Assert.That(Resolver.Attacks[Side.Right].Stop, Is.EqualTo(AttackStop.Interrupted));
            Assert.That(later.Landed, Is.Empty, "§9: an interrupted attack lands nothing");
            Assert.That(Left.Hp, Is.EqualTo(_settings.Damage.MaxHp));
        }

        [TestCase(BodyPart.Torso)]
        [TestCase(BodyPart.LeftArm)]
        [TestCase(BodyPart.LeftLeg)]
        [TestCase(BodyPart.RightLeg)]
        public void D15_AHitAwayFromTheWeaponArmAndHead_DoesNotCancel_SoBothAreDamaged(BodyPart part)
        {
            HitResolution first = Resolve(Hit(Side.Left, part, 200));
            HitResolution later = Resolve(Hit(Side.Right, BodyPart.Torso, 500));

            Assert.That(first.Interrupted, Is.Empty);
            Assert.That(later.Landed, Has.Count.EqualTo(1), "the mace still lands: a trade");
            Assert.That(Lost(Right), Is.GreaterThan(0f));
            Assert.That(Lost(Left), Is.EqualTo(Mace.BaseDamage).Within(Tolerance));
        }

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.RightArm)]
        public void D15_AHitOnTheWeaponArmOrTheHead_Cancels(BodyPart part) => Assert.That(LeftHitCancels(part), Is.True);

        [Test]
        public void D15_OnceTheDominantArmIsLost_TheOffArmIsTheWeaponArm()
        {
            Right.MarkSevered(BodyPart.RightArm);

            Assert.That(LeftHitCancels(BodyPart.LeftArm), Is.True, "§12: the off hand holds the weapon");
        }

        [Test]
        public void D15_AnyHitOption_CancelsOnALegHit()
        {
            _settings.Damage.InterruptRule = InterruptRule.AnyHit;

            Assert.That(LeftHitCancels(BodyPart.LeftLeg), Is.True);
        }

        [Test]
        public void D15_ThresholdOption_CancelsOnlyAboveTheThreshold()
        {
            _settings.Damage.InterruptRule = InterruptRule.AboveDamageThreshold;
            _settings.Damage.InterruptDamageThreshold = 10f;

            Assert.That(LeftHitCancels(BodyPart.Torso), Is.False, "rapier torso 8");
            SetUp();
            _settings.Damage.InterruptRule = InterruptRule.AboveDamageThreshold;
            _settings.Damage.InterruptDamageThreshold = 10f;
            Assert.That(LeftHitCancels(BodyPart.Head), Is.True, "rapier head 20");
            SetUp();
            _settings.Damage.InterruptRule = InterruptRule.AboveDamageThreshold;
            _settings.Damage.InterruptDamageThreshold = Rapier.BaseDamage * 2.5f;
            Assert.That(LeftHitCancels(BodyPart.Head), Is.False, "exactly at the threshold is not above it");
        }

        [Test]
        public void D16_SwingArmour_KeepsTheMaceSwinging_ThroughAHeadHit()
        {
            _settings.Damage.InterruptRule = InterruptRule.AnyHit;
            Mace.HasSwingArmour = true;

            HitResolution first = Resolve(Hit(Side.Left, BodyPart.Head, 200));
            HitResolution later = Resolve(Hit(Side.Right, BodyPart.Torso, 500));

            Assert.That(first.Interrupted, Is.Empty);
            Assert.That(later.Landed, Has.Count.EqualTo(1));
        }

        [Test]
        public void D16_IsOffForEveryWeapon_ByDefault()
        {
            Assert.That(_settings.Weapons.Any(weapon => weapon.HasSwingArmour), Is.False);
            Assert.That(_policies.Interrupt, Is.InstanceOf<SettingsInterruptPolicy>());
            Assert.That(_settings.Damage.InterruptRule, Is.EqualTo(InterruptRule.WeaponArmOrHead), "D15, designer 2026-10-03");
        }

        [Test]
        public void AKillingHit_StopsTheDeadDummysAttack_EvenThroughArmour()
        {
            Mace.HasSwingArmour = true;
            Right.Hp = AlmostDeadHp;

            HitResolution kill = Resolve(Hit(Side.Left, BodyPart.LeftLeg, 200));
            HitResolution later = Resolve(Hit(Side.Right, BodyPart.Torso, 500));

            Assert.That(kill.Landed.Single().KillsVictim, Is.True);
            Assert.That(kill.Died, Is.EqualTo(new[] { Side.Right }));
            Assert.That(kill.Interrupted, Is.EqualTo(new[] { Side.Right }));
            Assert.That(kill.Events.OfType<FighterDiedEvent>().Single().Side, Is.EqualTo(Side.Right));
            Assert.That(Resolver.Attacks[Side.Right].Stop, Is.EqualTo(AttackStop.WielderDied));
            Assert.That(later.Landed, Is.Empty);
        }

        [Test]
        public void ACancel_NeedsAnAttackTravellingItsPath()
        {
            _travelling.Right = false;

            HitResolution result = Resolve(Hit(Side.Left, BodyPart.Head, 200));

            Assert.That(result.Landed, Has.Count.EqualTo(1));
            Assert.That(result.Interrupted, Is.Empty, "a finished or idle attack has nothing to cancel");
            Assert.That(result.Events.OfType<AttackInterruptedEvent>(), Is.Empty);
        }

        [Test]
        public void ADeadDummy_TakesNoMoreHits_AndDealsNone()
        {
            Right.Hp = AlmostDeadHp;
            Resolve(Hit(Side.Left, BodyPart.Torso, 200));

            Assert.That(Resolve(Hit(Side.Left, BodyPart.Head, 250)).Landed, Is.Empty, "nothing hits a dead dummy");
            Assert.That(Resolve(Hit(Side.Right, BodyPart.Torso, 300)).Landed, Is.Empty, "a dead dummy deals no damage");
        }

        // --- The same instant ------------------------------------------------------------------

        [Test]
        public void HitsAtTheSameInstant_BothLand_ATrade()
        {
            HitResolution result = Resolve(Hit(Side.Left, BodyPart.Head, 300), Hit(Side.Right, BodyPart.Head, 300));

            Assert.That(result.Landed, Has.Count.EqualTo(2), "neither was hit before its own attack landed");
            Assert.That(Lost(Right), Is.EqualTo(Rapier.BaseDamage * 2.5f).Within(Tolerance));
            Assert.That(Lost(Left), Is.EqualTo(Mace.BaseDamage * 2.5f).Within(Tolerance));
            Assert.That(result.Interrupted, Is.EqualTo(new[] { Side.Left, Side.Right }), "both head hits cancel, after the instant");
        }

        [Test]
        public void ADoubleKo_AtTheSameInstant_KillsBoth()
        {
            Left.Hp = AlmostDeadHp;
            Right.Hp = AlmostDeadHp;

            HitResolution result = Resolve(Hit(Side.Left, BodyPart.Torso, 300), Hit(Side.Right, BodyPart.Torso, 300));

            Assert.That(result.Died, Is.EqualTo(new[] { Side.Left, Side.Right }));
            Assert.That(Left.IsDead && Right.IsDead, Is.True, "§3: a double KO goes to sudden death");
            Assert.That(result.Events.OfType<FighterDiedEvent>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void TheSameInstant_HasTheSameOutcome_InEitherStableOrder()
        {
            HitResolution leftFirst = Resolve(Hit(Side.Left, BodyPart.Head, 300), Hit(Side.Right, BodyPart.Torso, 300));
            float leftLost = Lost(Left), rightLost = Lost(Right);
            SetUp();
            HitResolution rightFirst = Resolve(Hit(Side.Right, BodyPart.Torso, 300), Hit(Side.Left, BodyPart.Head, 300));

            Assert.That(Lost(Left), Is.EqualTo(leftLost));
            Assert.That(Lost(Right), Is.EqualTo(rightLost));
            Assert.That(rightFirst.Interrupted, Is.EqualTo(leftFirst.Interrupted));
            Assert.That(leftFirst.Landed[0].Hit.Attacker, Is.EqualTo(Side.Left), "events follow the stable order given");
            Assert.That(rightFirst.Landed[0].Hit.Attacker, Is.EqualTo(Side.Right));
        }

        [Test]
        public void HitsOfOneInstant_MustShareItsTime()
        {
            Assert.Throws<ArgumentException>(() => Resolve(Hit(Side.Left, BodyPart.Head, 300), Hit(Side.Right, BodyPart.Head, 301)));
        }

        [Test]
        public void FirstHitTime_IsEachSidesFirstLandedHit()
        {
            Resolve(Hit(Side.Right, BodyPart.LeftLeg, 150));
            Resolve(Hit(Side.Right, BodyPart.Torso, 250));

            Assert.That(Resolver.FirstHitTime[Side.Right], Is.EqualTo(SimTime.FromMilliseconds(150)));
            Assert.That(Resolver.FirstHitTime[Side.Left], Is.Null);
        }

        // --- Several hits per weapon (D26) -----------------------------------------------------

        [Test]
        public void D26_AWeaponHitsSeveralParts_EachSlowerAndSofter_AndStopsAtItsLast()
        {
            float kept = Mace.SpeedKeptAfterHitFraction;

            LandedHit arm = Resolve(Hit(Side.Right, BodyPart.LeftArm, 300)).Landed.Single();
            LandedHit torso = Resolve(Hit(Side.Right, BodyPart.Torso, 400)).Landed.Single();
            LandedHit head = Resolve(Hit(Side.Right, BodyPart.Head, 500)).Landed.Single();

            Assert.That(arm.Damage.Damage, Is.EqualTo(Mace.BaseDamage * 0.8f).Within(Tolerance), "the first hit is full");
            Assert.That(arm.SpeedFractionAfter, Is.EqualTo(kept).Within(Tolerance));
            Assert.That(arm.StopsWeapon, Is.False);
            Assert.That(torso.SpeedFractionAtHit, Is.EqualTo(kept).Within(Tolerance));
            Assert.That(torso.Damage.Damage, Is.EqualTo(Mace.BaseDamage * kept).Within(Tolerance), "softer, in step with the speed left");
            Assert.That(head.Damage.Damage, Is.EqualTo(Mace.BaseDamage * 2.5f * kept * kept).Within(Tolerance));
            Assert.That(head.StopsWeapon, Is.True, "the third hit is its last");
            Assert.That(Resolver.Attacks[Side.Right].Stop, Is.EqualTo(AttackStop.LastHit));
        }

        [Test]
        public void D26_EachBodyPartOnlyOncePerSwing()
        {
            Resolve(Hit(Side.Right, BodyPart.Torso, 300));

            Assert.That(Resolve(Hit(Side.Right, BodyPart.Torso, 400)).Landed, Is.Empty);
            Assert.That(Lost(Left), Is.EqualTo(Mace.BaseDamage).Within(Tolerance));
        }

        [Test]
        public void D26_AfterItsLastHit_TheWeaponLandsNothing()
        {
            Resolve(Hit(Side.Right, BodyPart.LeftArm, 300));
            Resolve(Hit(Side.Right, BodyPart.LeftLeg, 350));
            Resolve(Hit(Side.Right, BodyPart.RightLeg, 400));

            Assert.That(Resolve(Hit(Side.Right, BodyPart.Torso, 450)).Landed, Is.Empty);
        }

        [Test]
        public void D26_OneHitPerTurn_StopsTheWeaponAtItsFirstHit()
        {
            _settings.Damage.MaxHitsPerWeaponPerTurn = 1;

            Assert.That(Resolve(Hit(Side.Right, BodyPart.LeftArm, 300)).Landed.Single().StopsWeapon, Is.True);
            Assert.That(Resolve(Hit(Side.Right, BodyPart.Torso, 400)).Landed, Is.Empty);
        }

        [Test]
        public void D26_NoSpeedKept_StopsAtTheFirstHit()
        {
            Mace.SpeedKeptAfterHitFraction = 0f;

            Assert.That(Resolve(Hit(Side.Right, BodyPart.LeftArm, 300)).Landed.Single().StopsWeapon, Is.True);
        }

        [Test]
        public void D26_TheRapierSticks_TheMacePloughsThrough()
        {
            Assert.That(Rapier.SpeedKeptAfterHitFraction, Is.LessThan(Mace.SpeedKeptAfterHitFraction));

            Resolve(Hit(Side.Left, BodyPart.LeftLeg, 200));
            LandedHit second = Resolve(Hit(Side.Left, BodyPart.Torso, 300)).Landed.Single();

            Assert.That(second.Damage.Damage, Is.EqualTo(Rapier.BaseDamage * Rapier.SpeedKeptAfterHitFraction).Within(Tolerance));
        }

        [Test]
        public void D26_ASlowedFollowThrough_CanStillBeCancelled()
        {
            Resolve(Hit(Side.Right, BodyPart.LeftArm, 300));
            HitResolution counter = Resolve(Hit(Side.Left, BodyPart.Head, 350));

            Assert.That(counter.Interrupted, Is.EqualTo(new[] { Side.Right }), "the slower follow-through gave the rapier time");
            Assert.That(Resolve(Hit(Side.Right, BodyPart.Torso, 400)).Landed, Is.Empty);
        }

        // --- What counts as a hit (E2) ---------------------------------------------------------

        [Test]
        public void E2_AWeaponNotTravellingItsPath_DealsNoDamage()
        {
            _travelling.Left = false;

            Assert.That(Resolve(Hit(Side.Left, BodyPart.Head, 200)).Landed, Is.Empty);
            Assert.That(Right.Hp, Is.EqualTo(_settings.Damage.MaxHp));
        }

        [Test]
        public void E2_TheFlag_LetsAWeaponAtRestHit()
        {
            _settings.Damage.RestingWeaponsDealDamage = true;
            _travelling.Left = false;

            Assert.That(Resolve(Hit(Side.Left, BodyPart.Head, 200)).Landed, Has.Count.EqualTo(1));
        }

        [Test]
        public void AHitOnASeveredLimb_IsIgnored()
        {
            Right.MarkSevered(BodyPart.LeftLeg);

            Assert.That(Resolve(Hit(Side.Left, BodyPart.LeftLeg, 200)).Landed, Is.Empty);
        }

        [Test]
        public void ADummyHoldingNothing_DealsNoDamage()
        {
            _resolver = new HitResolver(_state, _settings, _policies, new PerSide<TurnAttack>(new TurnAttack(null), new TurnAttack(Mace)));

            Assert.That(Resolve(Hit(Side.Left, BodyPart.Head, 200)).Landed, Is.Empty);
        }

        // --- Damage, stun and events -----------------------------------------------------------

        [Test]
        public void AStrongHeadHit_StunsTheVictimsNextTurn()
        {
            HitResolution result = Resolve(Hit(Side.Right, BodyPart.Head, 300));

            Assert.That(Left.Statuses.Single().Kind, Is.EqualTo(StatusKind.Stunned));
            StatusAppliedEvent stun = result.Events.OfType<StatusAppliedEvent>().Single();
            Assert.That(stun.Side, Is.EqualTo(Side.Left));
            Assert.That(stun.Time, Is.EqualTo(SimTime.FromMilliseconds(300)));
        }

        [Test]
        public void AKillingHeadHit_DoesNotStun()
        {
            Left.Hp = AlmostDeadHp;

            HitResolution result = Resolve(Hit(Side.Right, BodyPart.Head, 300));

            Assert.That(result.Events.OfType<StatusAppliedEvent>(), Is.Empty);
            Assert.That(Left.Statuses, Is.Empty);
        }

        [Test]
        public void D13_TheAttackersBodyMove_AddsItsDamageBonus()
        {
            BodyMoveStats lunge = _settings.FindBodyMove(BodyMove.Lunge);
            lunge.DamageBonusFraction = 0.5f;
            _resolver = new HitResolver(_state, _settings, _policies, new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(Mace, lunge)));

            Resolve(Hit(Side.Right, BodyPart.Torso, 300));

            Assert.That(Lost(Left), Is.EqualTo(Mace.BaseDamage * 1.5f).Within(Tolerance));
        }

        [Test]
        public void Events_AreHitsAndStunsFirst_ThenInterrupts_ThenDeaths_AllAtTheInstant()
        {
            Right.Hp = AlmostDeadHp;
            HitResolution result = Resolve(Hit(Side.Left, BodyPart.Head, 200), Hit(Side.Right, BodyPart.Head, 200));

            Assert.That(result.Events.Select(e => e.GetType()), Is.EqualTo(new[]
            {
                typeof(HitLandedEvent), typeof(HitLandedEvent), typeof(StatusAppliedEvent), typeof(AttackInterruptedEvent),
                typeof(AttackInterruptedEvent), typeof(FighterDiedEvent),
            }));
            Assert.That(result.Events.All(e => e.Time == SimTime.FromMilliseconds(200)), Is.True);
            var landed = (HitLandedEvent)result.Events[0];
            Assert.That(landed.Hit.Part, Is.EqualTo(BodyPart.Head));
            Assert.That(landed.Damage.HpDamage, Is.EqualTo(Rapier.BaseDamage * 2.5f).Within(Tolerance));
        }
    }
}
