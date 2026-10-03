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
    /// What a clash does to the turn (GDD §10; D17 stagger, D19 repeats, A1 speeds). Both dummies are right-handed at full HP;
    /// the weapons are chosen per test and travel at their own speeds unless a test says otherwise. Powers with the starting
    /// values: a travelling rapier 6.5, a travelling mace 11.25; held still, rapier 2 and mace 10.
    /// </summary>
    public sealed class WeaponContactResolverTests
    {
        private const float Tolerance = 1e-4f;
        private const float Square = 90f;
        private const float Glancing = 10f;
        private static readonly SimTime ClashTime = SimTime.FromMilliseconds(120);
        private static readonly Vec2 ContactPoint = new Vec2(30f, 110f);

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;
        private PerSide<bool> _travelling;
        private PerSide<float> _pathSpeeds;
        private PerSide<TurnAttack> _attacks;
        private WeaponContactResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _travelling = new PerSide<bool>(true, true);
            Arm(WeaponIds.Rapier, WeaponIds.Mace);
        }

        private WeaponStats Stats(WeaponId id) => _settings.FindWeapon(id);

        /// <summary>Rebuilds the turn with these weapons, each travelling at its own speed.</summary>
        private void Arm(WeaponId left, WeaponId right)
        {
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, left),
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, right)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            _attacks = new PerSide<TurnAttack>(new TurnAttack(Stats(left)), new TurnAttack(Stats(right)));
            _pathSpeeds = PerSide<float>.Create(side => _attacks[side].Weapon.SpeedUnitsPerSecond);
            _resolver = new WeaponContactResolver(_state, _settings, _policies, _attacks);
        }

        private bool Clash(float angle, out ClashResolution resolution) =>
            _resolver.TryResolveClash(ClashTime, angle, ContactPoint, _travelling, _pathSpeeds, out resolution);

        private ClashResolution Clash(float angle)
        {
            Assert.That(Clash(angle, out ClashResolution resolution), Is.True, "the clash resolves");
            return resolution;
        }

        private HitResolver Hits() => new HitResolver(_state, _settings, _policies, _attacks);

        private static HitFacts Hit(Side attacker, BodyPart part, WeaponId weapon, double milliseconds) =>
            new HitFacts(attacker, part, weapon, SimTime.FromMilliseconds(milliseconds), false);

        private FighterState Fighter(Side side) => _state.Fighters[side];

        // --- The four outcomes applied to the turn ---------------------------------------------

        [Test]
        public void ACrushThrough_KnocksTheLoserOff_StaggersItsDummy_AndTheWinnerCarriesOn()
        {
            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(resolution.Result.Winner, Is.EqualTo(Side.Right), "the mace");
            Assert.That(_attacks.Left.Stop, Is.EqualTo(AttackStop.KnockedOff));
            Assert.That(_attacks.Right.IsStopped, Is.False);
            Assert.That(_attacks.Right.CrushedThrough, Is.True);
            Assert.That(_attacks.Left.CrushedThrough, Is.False);

            IStatusEffect stagger = Fighter(Side.Left).Statuses.Single();
            Assert.That(stagger.Kind, Is.EqualTo(StatusKind.Staggered));
            Assert.That(stagger, Is.InstanceOf<NoBodyMoveStatus>(), "D17: the stagger shares the stun's effect");
            Assert.That(Fighter(Side.Right).Statuses, Is.Empty);

            Assert.That(resolution.Events[0], Is.InstanceOf<WeaponClashEvent>());
            var staggered = (StatusAppliedEvent)resolution.Events[1];
            Assert.That(staggered.Side, Is.EqualTo(Side.Left));
            Assert.That(staggered.Kind, Is.EqualTo(StatusKind.Staggered));
            Assert.That(staggered.Time, Is.EqualTo(ClashTime));
        }

        [Test]
        public void TheCrushThroughWinner_DealsLessOnItsLaterHits_AndTheLoserLandsNothing()
        {
            Clash(Square);
            HitResolver hits = Hits();

            hits.Resolve(new[] { Hit(Side.Left, BodyPart.Torso, WeaponIds.Rapier, 150) }, _travelling);
            hits.Resolve(new[] { Hit(Side.Right, BodyPart.Torso, WeaponIds.Mace, 200) }, _travelling);

            Assert.That(Fighter(Side.Right).Hp, Is.EqualTo(_settings.Damage.MaxHp), "the knocked-off rapier misses");
            float maceTorso = Stats(WeaponIds.Mace).BaseDamage * _settings.Clash.CrushThroughDamageMultiplier;
            Assert.That(_settings.Damage.MaxHp - Fighter(Side.Left).Hp, Is.EqualTo(maceTorso).Within(Tolerance), "20 × 0.7");
        }

        [Test]
        public void ARebound_StopsBothAttacks_AndStaggersNobody()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);

            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.BothRebound));
            Assert.That(_attacks.Left.Stop, Is.EqualTo(AttackStop.Rebounded));
            Assert.That(_attacks.Right.Stop, Is.EqualTo(AttackStop.Rebounded));
            Assert.That(Fighter(Side.Left).Statuses, Is.Empty);
            Assert.That(Fighter(Side.Right).Statuses, Is.Empty);
            Assert.That(resolution.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void ADeflection_KnocksTheHeavierWeaponOff_WithoutAStagger()
        {
            ClashResolution resolution = Clash(Glancing);

            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier));
            Assert.That(_attacks.Left.IsStopped, Is.False, "the rapier carries on");
            Assert.That(_attacks.Left.CrushedThrough, Is.False, "a deflection costs no damage");
            Assert.That(_attacks.Right.Stop, Is.EqualTo(AttackStop.KnockedOff), "the mace misses");
            Assert.That(Fighter(Side.Right).Statuses, Is.Empty);
        }

        [Test]
        public void SlidingPast_LeavesBothAttacksGoing()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);

            ClashResolution resolution = Clash(Glancing);

            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.BothSlidePast));
            Assert.That(_attacks.Left.IsStopped || _attacks.Right.IsStopped, Is.False);
            Assert.That(_attacks.Left.CrushedThrough || _attacks.Right.CrushedThrough, Is.False);
        }

        [Test]
        public void TheEvent_CarriesTimeAngleBothPowersAndThePoint()
        {
            var clash = (WeaponClashEvent)Clash(Square).Events[0];

            Assert.That(clash.Time, Is.EqualTo(ClashTime));
            Assert.That(clash.ContactAngleDegrees, Is.EqualTo(Square));
            Assert.That(clash.ContactPointUnits, Is.EqualTo(ContactPoint));
            Assert.That(clash.Result.LeftPower, Is.EqualTo(6.5f).Within(Tolerance));
            Assert.That(clash.Result.RightPower, Is.EqualTo(11.25f).Within(Tolerance));
        }

        // --- D19: repeats ---------------------------------------------------------------

        [Test]
        public void D19_OnlyTheFirstContactBetweenThePairResolves()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
            Clash(Glancing);

            bool second = Clash(Square, out ClashResolution resolution);

            Assert.That(second, Is.False, "a later contact passes through");
            Assert.That(resolution, Is.Null);
            Assert.That(_attacks.Left.IsStopped || _attacks.Right.IsStopped, Is.False, "it would have rebounded both");
        }

        [Test]
        public void D19_WithNoLimit_EveryContactResolves()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
            _settings.Clash.MaxResolvedContactsPerWeaponPair = 0;
            Clash(Glancing);

            Assert.That(Clash(Square).Result.Kind, Is.EqualTo(ClashKind.BothRebound));
        }

        [Test]
        public void D19_AContactTheRulesIgnore_DoesNotUseUpTheFirst()
        {
            _travelling = new PerSide<bool>(false, false);
            Assert.That(Clash(Square, out _), Is.False, "nothing is attacking");

            _travelling = new PerSide<bool>(true, true);
            Assert.That(Clash(Square, out _), Is.True);
        }

        // --- A1: the speed in a clash -------------------------------------------------------

        [Test]
        public void A1_AWeaponHeldStill_ClashesWithItsMassAlone()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
            _travelling = new PerSide<bool>(false, true);

            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.LeftPower, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.CrushThrough), "6.5 against 2");
            Assert.That(resolution.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(Fighter(Side.Left).Statuses.Single().Kind, Is.EqualTo(StatusKind.Staggered));
        }

        [Test]
        public void A1_AMaceHeldStill_StopsARapierThrust()
        {
            _travelling = new PerSide<bool>(true, false);

            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.RightPower, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(resolution.Result.Winner, Is.EqualTo(Side.Right), "10 against 6.5: you ran into my mace");
            Assert.That(_attacks.Left.Stop, Is.EqualTo(AttackStop.KnockedOff));
            Assert.That(Fighter(Side.Left).Statuses.Single().Kind, Is.EqualTo(StatusKind.Staggered));
        }

        [Test]
        public void A1_AWeaponSlowedByItsHit_ClashesAtTheSpeedItHasLeft()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
            Hits().Resolve(new[] { Hit(Side.Left, BodyPart.LeftLeg, WeaponIds.Rapier, 80) }, _travelling);

            ClashResolution resolution = Clash(Square);

            float left = 2f + 0.005f * 900f * Stats(WeaponIds.Rapier).SpeedKeptAfterHitFraction;
            Assert.That(resolution.Result.LeftPower, Is.EqualTo(left).Within(Tolerance), "2 + 0.005 × 270");
            Assert.That(resolution.Result.Winner, Is.EqualTo(Side.Right), "the fresh thrust crushes the slowed one");
        }

        [Test]
        public void A1_AnAttackTheRulesStopped_ClashesAsNotTravelling()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
            Hits().Resolve(new[] { Hit(Side.Right, BodyPart.Head, WeaponIds.Rapier, 80) }, _travelling);
            Assert.That(_attacks.Left.Stop, Is.EqualTo(AttackStop.Interrupted));

            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.LeftPower, Is.EqualTo(2f).Within(Tolerance), "its driver may still be moving this step");
        }

        [Test]
        public void A1_ABodyMovesSpeedBonus_CountsThroughThePathSpeed()
        {
            _pathSpeeds.Left = 1800f;

            ClashResolution resolution = Clash(Square);

            Assert.That(resolution.Result.LeftPower, Is.EqualTo(11f).Within(Tolerance));
            Assert.That(resolution.Result.Kind, Is.EqualTo(ClashKind.BothRebound), "11 against 11.25 is close");
        }

        // --- Shield blocks (§7; D20, D21, A5) ------------------------------------------------------

        private const float StraightIn = 90f;
        private const float MiddleOfTheFace = 0f;
        private const float OnTheRim = 0.95f;

        private bool Block(Side blocker, float angle, float facePosition, out BlockResolution resolution) =>
            _resolver.TryResolveBlock(ClashTime, blocker, angle, facePosition, ContactPoint, _travelling, _pathSpeeds, out resolution);

        private BlockResolution Block(Side blocker, float angle, float facePosition)
        {
            Assert.That(Block(blocker, angle, facePosition, out BlockResolution resolution), Is.True, "the block resolves");
            return resolution;
        }

        [Test]
        public void AFullBlock_StopsTheAttack_AndItLandsNothingAfterwards()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);

            BlockResolution resolution = Block(Side.Right, StraightIn, MiddleOfTheFace);
            Hits().Resolve(new[] { Hit(Side.Left, BodyPart.Torso, WeaponIds.Rapier, 150) }, _travelling);

            Assert.That(resolution.Result.AttackStopped, Is.True);
            Assert.That(resolution.Blocker, Is.EqualTo(Side.Right));
            Assert.That(resolution.Attacker, Is.EqualTo(Side.Left));
            Assert.That(_attacks.Left.Stop, Is.EqualTo(AttackStop.Blocked));
            Assert.That(_attacks.Right.IsStopped, Is.False, "the shield carries on along its path");
            Assert.That(Fighter(Side.Right).Hp, Is.EqualTo(_settings.Damage.MaxHp));
            Assert.That(Fighter(Side.Right).Statuses, Is.Empty, "D21 default: no stagger");
        }

        [Test]
        public void APartialBlock_LetsTheWeaponCarryOn_AndItsLaterHitsDealHalf()
        {
            Arm(WeaponIds.Mace, WeaponIds.Shield);

            BlockResolution resolution = Block(Side.Right, StraightIn, OnTheRim);
            HitResolver hits = Hits();
            hits.Resolve(new[] { Hit(Side.Left, BodyPart.Torso, WeaponIds.Mace, 150) }, _travelling);
            hits.Resolve(new[] { Hit(Side.Left, BodyPart.Head, WeaponIds.Mace, 160) }, _travelling);

            Assert.That(resolution.Result.AttackStopped, Is.False);
            Assert.That(_attacks.Left.ShieldBlockDamageMultiplier, Is.EqualTo(0.5f).Within(Tolerance));
            WeaponStats mace = Stats(WeaponIds.Mace);
            float torso = mace.BaseDamage * 0.5f;
            float head = mace.BaseDamage * 2.5f * 0.5f * mace.SpeedKeptAfterHitFraction;
            Assert.That(_settings.Damage.MaxHp - Fighter(Side.Right).Hp, Is.EqualTo(torso + head).Within(Tolerance),
                "every later hit this turn: 10, then 50 × 0.5 × 0.7");
        }

        [Test]
        public void TwoPartialBlocks_Multiply_WhenEveryContactResolves()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            _settings.Clash.MaxResolvedContactsPerWeaponPair = 0;

            Block(Side.Right, StraightIn, OnTheRim);
            Block(Side.Right, StraightIn, OnTheRim);

            Assert.That(_attacks.Left.ShieldBlockDamageMultiplier, Is.EqualTo(0.25f).Within(Tolerance));
        }

        [Test]
        public void D19_ABlockAfterAClash_PassesThrough_AndViceVersa()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            Block(Side.Right, StraightIn, OnTheRim);

            Assert.That(Block(Side.Right, StraightIn, MiddleOfTheFace, out _), Is.False, "the pair already met this turn");
            Assert.That(_attacks.Left.IsStopped, Is.False);
        }

        [Test]
        public void TheShieldBlocks_WhereverItIs_EvenHeldStill()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            _travelling = new PerSide<bool>(true, false);

            Assert.That(Block(Side.Right, StraightIn, MiddleOfTheFace).Result.AttackStopped, Is.True);
        }

        [Test]
        public void AWeaponNotTravelling_HasNoAttackToBlock_AndDoesNotUseUpTheFirstContact()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            _travelling = new PerSide<bool>(false, true);
            Assert.That(Block(Side.Right, StraightIn, MiddleOfTheFace, out _), Is.False);

            _travelling = new PerSide<bool>(true, true);
            Assert.That(Block(Side.Right, StraightIn, MiddleOfTheFace, out _), Is.True);
        }

        [Test]
        public void D21StaggersHolder_AHeldBlockAgainstAMace_StaggersTheShieldHolder()
        {
            Arm(WeaponIds.Mace, WeaponIds.Shield);
            _settings.Clash.ShieldBlock.HeavyWeapon = HeavyWeaponBlockRule.StaggersHolder;

            BlockResolution resolution = Block(Side.Right, StraightIn, MiddleOfTheFace);

            Assert.That(Fighter(Side.Right).Statuses.Single().Kind, Is.EqualTo(StatusKind.Staggered));
            var staggered = (StatusAppliedEvent)resolution.Events[1];
            Assert.That(staggered.Side, Is.EqualTo(Side.Right));
        }

        [Test]
        public void TheBlockEvent_CarriesTheBlockerTheAngleThePointAndWhereOnTheFace()
        {
            Arm(WeaponIds.Shield, WeaponIds.Rapier);

            var block = (ShieldBlockEvent)Block(Side.Left, 42f, 0.3f).Events.Single();

            Assert.That(block.Time, Is.EqualTo(ClashTime));
            Assert.That(block.Blocker, Is.EqualTo(Side.Left));
            Assert.That(block.Attacker, Is.EqualTo(Side.Right));
            Assert.That(block.ContactAngleDegrees, Is.EqualTo(42f));
            Assert.That(block.FacePositionFraction, Is.EqualTo(0.3f));
            Assert.That(block.ContactPointUnits, Is.EqualTo(ContactPoint));
        }

        [Test]
        public void A7_TwoShieldsMeeting_HaveNoRule()
        {
            Arm(WeaponIds.Shield, WeaponIds.Shield);

            Assert.That(Block(Side.Right, StraightIn, MiddleOfTheFace, out _), Is.False);
        }

        [Test]
        public void OnlyASideHoldingAShield_Blocks()
        {
            Assert.Throws<InvalidOperationException>(() => Block(Side.Right, StraightIn, MiddleOfTheFace, out _));
        }

        // --- Contacts no rule applies to ------------------------------------------------------

        [Test]
        public void ADeadWielder_IsNoClash()
        {
            Fighter(Side.Right).Hp = 0f;

            Assert.That(Clash(Square, out _), Is.False);
            Assert.That(_attacks.Left.IsStopped, Is.False);
        }

        [Test]
        public void AShield_NeverUsesTheClashModel()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);

            Assert.Throws<InvalidOperationException>(() => Clash(Square, out _));
        }

        [Test]
        public void ADummyHoldingNothing_IsNoClash()
        {
            _attacks = new PerSide<TurnAttack>(new TurnAttack(Stats(WeaponIds.Rapier)), new TurnAttack(null));
            _resolver = new WeaponContactResolver(_state, _settings, _policies, _attacks);

            Assert.That(Clash(Square, out _), Is.False);
        }
    }
}
