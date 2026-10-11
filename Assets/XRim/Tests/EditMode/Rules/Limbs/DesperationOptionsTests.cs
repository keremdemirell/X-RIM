using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>
    /// Which weapons a dummy that lost limbs can hold (GDD §12): the club beyond the 3 slots while a free limb lies on the floor
    /// (G1: anyone's), the body blow once both arms are gone, and no weapon lost with its arm (G2's option). Both dummies are
    /// right-handed with the loadout rapier, mace, shield.
    /// </summary>
    public sealed class DesperationOptionsTests
    {
        private static readonly WeaponId[] Loadout = PlanningTestData.Loadout;

        private RulesSettings _settings;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Rapier),
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Mace)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
        }

        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private PlanningConstraints Options(Side side)
        {
            PlanningConstraints constraints = PlanningConstraints.CreateDefault(_settings.Match);
            DesperationOptions.Apply(_state, side, Loadout, constraints, _settings);
            return constraints;
        }

        [Test]
        public void WithNoLimbOnTheFloor_OnlyTheLoadoutIsOffered()
        {
            PlanningConstraints constraints = Options(Side.Left);

            Assert.That(constraints.ExtraWeapons, Is.Empty);
            Assert.That(constraints.AvailableWeapons(Loadout), Is.EqualTo(Loadout));
            Assert.That(constraints.IsWeaponAvailable(WeaponIds.SeveredLimb, Loadout), Is.False);
        }

        [Test]
        public void ALimbOnTheFloor_IsOfferedAsAClub_BeyondTheThreeSlots()
        {
            Right.MarkSevered(BodyPart.LeftLeg);

            PlanningConstraints left = Options(Side.Left);
            PlanningConstraints right = Options(Side.Right);

            Assert.That(_settings.Loadout.SeveredLimbIsExtraOption, Is.True, "§12 default: a temporary extra option");
            Assert.That(left.AvailableWeapons(Loadout), Is.EqualTo(Loadout.Concat(new[] { WeaponIds.SeveredLimb })),
                "G1: the opponent's limb counts");
            Assert.That(right.ExtraWeapons, Is.EqualTo(new[] { WeaponIds.SeveredLimb }), "and so does its own");
        }

        [Test]
        public void G1_OwnLimbsOnly_HidesTheOpponentsLimb()
        {
            _settings.Damage.LimbLoss.AnySeveredLimbCanBeWielded = false;
            Right.MarkSevered(BodyPart.LeftLeg);

            Assert.That(Options(Side.Left).ExtraWeapons, Is.Empty);
            Assert.That(Options(Side.Right).ExtraWeapons, Is.EqualTo(new[] { WeaponIds.SeveredLimb }));
        }

        [Test]
        public void G1_ByDefault_AnyLimbCanBeWielded() => Assert.That(new LimbLossSettings().AnySeveredLimbCanBeWielded, Is.True);

        [Test]
        public void ALimbTheOpponentWields_IsNotFree_ButTheWielderKeepsItsClub()
        {
            Right.MarkSevered(BodyPart.LeftLeg);
            Right.HeldLimb = new SeveredLimbRef(Side.Right, BodyPart.LeftLeg);

            Assert.That(DesperationOptions.FreeLimbs(_state, Side.Left, _settings.Damage.LimbLoss), Is.Empty);
            Assert.That(Options(Side.Left).ExtraWeapons, Is.Empty);
            Assert.That(Options(Side.Right).ExtraWeapons, Is.EqualTo(new[] { WeaponIds.SeveredLimb }));
        }

        [Test]
        public void FreeLimbs_AreListedInAStableOrder()
        {
            Right.MarkSevered(BodyPart.RightLeg);
            Left.MarkSevered(BodyPart.RightArm);
            Left.MarkSevered(BodyPart.LeftLeg);

            Assert.That(DesperationOptions.FreeLimbs(_state, Side.Left, _settings.Damage.LimbLoss), Is.EqualTo(new[]
            {
                new SeveredLimbRef(Side.Left, BodyPart.RightArm), new SeveredLimbRef(Side.Left, BodyPart.LeftLeg),
                new SeveredLimbRef(Side.Right, BodyPart.RightLeg),
            }));
        }

        [Test]
        public void WithoutArms_TheBodyBlowIsTheOnlyOption_AndBecomesTheWeapon()
        {
            Left.MarkSevered(BodyPart.LeftArm);
            Left.MarkSevered(BodyPart.RightArm);

            PlanningConstraints constraints = Options(Side.Left);

            Assert.That(constraints.CanUseLoadoutWeapons, Is.False, "no arm to hold a weapon");
            Assert.That(constraints.AvailableWeapons(Loadout), Is.EqualTo(new[] { WeaponIds.BodyBlow }), "§12: body blows only");
            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.BodyBlow));
            Assert.That(constraints.IsWeaponAvailable(WeaponIds.SeveredLimb, Loadout), Is.False, "no arm to wield its own limbs either");
        }

        [Test]
        public void OneArmLeft_StillHoldsTheLoadout()
        {
            Left.MarkSevered(BodyPart.RightArm);

            PlanningConstraints constraints = Options(Side.Left);

            Assert.That(constraints.AvailableWeapons(Loadout), Is.EqualTo(Loadout.Concat(new[] { WeaponIds.SeveredLimb })),
                "§12: the other hand fights on, and its own arm lies on the floor");
            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.Rapier), "G2: the dropped weapon is not lost");
        }

        [Test]
        public void ALostWeapon_IsNotOffered_AndTheDummyTakesTheNextOne()
        {
            Left.LostWeapons.Add(WeaponIds.Rapier);

            PlanningConstraints constraints = Options(Side.Left);

            Assert.That(constraints.AvailableWeapons(Loadout), Is.EqualTo(new[] { WeaponIds.Mace, WeaponIds.Shield }));
            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void AClubWithNoLimbLeft_FallsBackToTheFirstLoadoutWeapon()
        {
            Left.CurrentWeapon = WeaponIds.SeveredLimb;

            Options(Side.Left);

            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void S4_WithTheExtraOptionOff_PickingTheClubGivesUpTheWeaponHeld()
        {
            _settings.Loadout.SeveredLimbIsExtraOption = false;

            DesperationOptions.OnWeaponLocked(Left, WeaponIds.SeveredLimb, Loadout, _settings);

            Assert.That(Left.LostWeapons, Is.EquivalentTo(new[] { WeaponIds.Rapier }), "the club takes its slot");
        }

        [Test]
        public void S4_ByDefault_PickingTheClubCostsNothing()
        {
            DesperationOptions.OnWeaponLocked(Left, WeaponIds.SeveredLimb, Loadout, _settings);
            Assert.That(Left.LostWeapons, Is.Empty);

            _settings.Loadout.SeveredLimbIsExtraOption = false;
            DesperationOptions.OnWeaponLocked(Left, WeaponIds.Mace, Loadout, _settings);
            Assert.That(Left.LostWeapons, Is.Empty, "only picking the club takes a slot");
        }
    }
}
