using System;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>
    /// What a sever does to the rules state (GDD §11, §12): the limb is marked, the arm holding the item drops it, a club goes back to
    /// the floor, and G2 decides whether a dropped weapon is lost. Left is right-handed with the rapier, right is left-handed with the mace.
    /// </summary>
    public sealed class SeverRulesTests
    {
        private static readonly SimTime At = SimTime.FromMilliseconds(400);

        private readonly SeverRules _rules = new SeverRules();
        private LimbLossSettings _settings;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = new LimbLossSettings();
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, 100f, WeaponIds.Rapier),
                    new FighterState(Handedness.Left, 100f, WeaponIds.Mace)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
        }

        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private SeverOutcome Sever(Side side, BodyPart part, bool holdsItem = true) => _rules.Sever(_state, side, part, At, holdsItem, _settings);

        [Test]
        public void Severing_MarksTheLimb_AndRecordsItAtTheInstant()
        {
            SeverOutcome outcome = Sever(Side.Left, BodyPart.LeftLeg);

            Assert.That(Left.IsSevered(BodyPart.LeftLeg), Is.True);
            LimbSeveredEvent severed = outcome.Events.Cast<LimbSeveredEvent>().Single();
            Assert.That(severed.Side, Is.EqualTo(Side.Left));
            Assert.That(severed.Part, Is.EqualTo(BodyPart.LeftLeg));
            Assert.That(severed.Time, Is.EqualTo(At));
            Assert.That(outcome.DropsHeldItem, Is.False, "a leg holds nothing");
            Assert.That(outcome.Limb, Is.EqualTo(new SeveredLimbRef(Side.Left, BodyPart.LeftLeg)));
        }

        [Test]
        public void TheDominantArm_DropsTheWeapon_ButTheDummyKeepsIt()
        {
            SeverOutcome outcome = Sever(Side.Left, BodyPart.RightArm);

            Assert.That(outcome.DropsHeldItem, Is.True, "§12 (Decided): the weapon drops");
            Assert.That(outcome.DroppedItem, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(outcome.Events.Select(e => e.GetType()), Is.EqualTo(new[] { typeof(LimbSeveredEvent), typeof(WeaponDroppedEvent) }));
            Assert.That(((WeaponDroppedEvent)outcome.Events[1]).Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.Rapier), "G2: the other hand holds it from the next turn");
            Assert.That(Left.LostWeapons, Is.Empty);
            Assert.That(Left.HeldItemArm, Is.EqualTo(BodyPart.LeftArm), "§12: the dummy fights on with its other hand");
        }

        [Test]
        public void G2_ByDefault_ADroppedWeaponIsNotLost() => Assert.That(new LimbLossSettings().DroppedWeaponIsLost, Is.False);

        [Test]
        public void G2_TheFlag_LosesTheDroppedWeaponForTheMatch()
        {
            _settings.DroppedWeaponIsLost = true;

            Sever(Side.Left, BodyPart.RightArm);

            Assert.That(Left.LostWeapons, Is.EquivalentTo(new[] { WeaponIds.Rapier }));
        }

        [Test]
        public void TheOffArm_WhileTheDominantArmHolds_DropsNothing()
        {
            SeverOutcome outcome = Sever(Side.Left, BodyPart.LeftArm);

            Assert.That(outcome.DropsHeldItem, Is.False);
            Assert.That(outcome.Events.OfType<WeaponDroppedEvent>(), Is.Empty);
            Assert.That(Left.HeldItemArm, Is.EqualTo(BodyPart.RightArm));
        }

        [Test]
        public void ALeftHandedDummy_DropsItsWeaponWithTheLeftArm()
        {
            Assert.That(Sever(Side.Right, BodyPart.RightArm).DropsHeldItem, Is.False, "the right arm is its off arm");
            SetUp();
            Assert.That(Sever(Side.Right, BodyPart.LeftArm).DroppedItem, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void OnceTheDominantArmIsGone_TheOffArmHoldsTheWeapon_AndDropsIt()
        {
            Left.MarkSevered(BodyPart.RightArm);

            SeverOutcome outcome = Sever(Side.Left, BodyPart.LeftArm);

            Assert.That(outcome.DropsHeldItem, Is.True);
            Assert.That(Left.HeldItemArm, Is.Null);
            Assert.That(Left.RemainingArms, Is.EqualTo(0));
        }

        [Test]
        public void AnArmHoldingNothingAtThisInstant_DropsNothing()
        {
            SeverOutcome outcome = Sever(Side.Left, BodyPart.RightArm, holdsItem: false);

            Assert.That(outcome.DropsHeldItem, Is.False, "the weapon already dropped earlier this turn");
            Assert.That(outcome.Events.OfType<WeaponDroppedEvent>(), Is.Empty);
        }

        [Test]
        public void AWieldedClub_DropsBackToTheFloor_AndIsNeverLost()
        {
            _settings.DroppedWeaponIsLost = true;
            var club = new SeveredLimbRef(Side.Right, BodyPart.LeftLeg);
            Left.CurrentWeapon = WeaponIds.SeveredLimb;
            Left.HeldLimb = club;

            SeverOutcome outcome = Sever(Side.Left, BodyPart.RightArm);

            Assert.That(outcome.DroppedItem, Is.EqualTo(WeaponIds.SeveredLimb));
            Assert.That(outcome.ReleasedClub, Is.EqualTo(club));
            Assert.That(Left.HeldLimb, Is.Null);
            Assert.That(Left.LostWeapons, Is.Empty, "the limb lies on the floor again");
        }

        [Test]
        public void AnAlreadySeveredLimb_IsNotSeveredAgain()
        {
            Sever(Side.Left, BodyPart.LeftLeg);

            Assert.That(Sever(Side.Left, BodyPart.LeftLeg), Is.Null);
        }

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.Torso)]
        public void HeadAndTorso_CannotBeSevered(BodyPart part) => Assert.Throws<ArgumentException>(() => Sever(Side.Left, part));

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.Torso)]
        public void ASeveredLimbRef_IsOnlyAnArmOrALeg(BodyPart part) =>
            Assert.Throws<ArgumentException>(() => new SeveredLimbRef(Side.Left, part));
    }
}
