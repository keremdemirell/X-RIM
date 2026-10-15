using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// The pre-match pick (GDD §6, Decided): 3 slots from the owned weapons, the shield counting as one; neither player sees the
    /// other's picks until both are locked in, then both loadouts are revealed.
    /// </summary>
    public sealed class LoadoutSelectionTests
    {
        private static readonly WeaponId[] Duelist = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };
        private static readonly WeaponId[] Lancer = { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield };

        private RulesSettings _settings;
        private LoadoutSelection _selection;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _selection = new LoadoutSelection(_settings, new AllWeaponsOwned());
        }

        private bool LockIn(Side side, params WeaponId[] loadout) => _selection.TryLockIn(side, loadout, out _);

        // --- Slots and options --------------------------------------------------------------------

        [Test]
        public void TheChoices_AreTheFiveWeapons_TheShieldCountingAsOne()
        {
            Assert.That(_selection.Choices, Has.Count.EqualTo(5));
            Assert.That(_selection.Choices, Contains.Item(WeaponIds.Shield));
        }

        [Test]
        public void ALoadout_NeedsExactlyThreeSlots()
        {
            Assert.That(LockIn(Side.Left, WeaponIds.Rapier, WeaponIds.Mace), Is.False);
            Assert.That(LockIn(Side.Left, WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Sword, WeaponIds.Spear), Is.False);
            Assert.That(_selection.IsLockedIn(Side.Left), Is.False, "a refused pick stores nothing");

            Assert.That(LockIn(Side.Left, Duelist), Is.True);
            Assert.That(_selection.IsLockedIn(Side.Left), Is.True);
        }

        [Test]
        public void ARefusedPick_SaysWhy_AndTheSideCanTryAgain()
        {
            bool accepted = _selection.TryLockIn(Side.Left, new[] { WeaponIds.Rapier, WeaponIds.Rapier, WeaponIds.SeveredLimb },
                out IReadOnlyList<string> issues);

            Assert.That(accepted, Is.False);
            Assert.That(issues, Has.Some.Contains("twice"));
            Assert.That(issues, Has.Some.Contains("cannot be picked"));
            Assert.That(LockIn(Side.Left, Duelist), Is.True);
        }

        [Test]
        public void AnUnownedWeapon_IsRefused()
        {
            _selection = new LoadoutSelection(_settings, new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield));

            bool accepted = _selection.TryLockIn(Side.Left, Lancer, out IReadOnlyList<string> issues);

            Assert.That(accepted, Is.False);
            Assert.That(issues, Has.Some.Contains("does not own"));
        }

        [Test]
        public void TheShieldSlot_IsOnlyRequiredWhenTheFlagSaysSo()
        {
            WeaponId[] noShield = { WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Spear };

            Assert.That(LockIn(Side.Left, noShield), Is.True, "D8, designer 2026-10-15: no shield slot required");

            _settings.Loadout.RequireShieldSlot = true;
            Assert.That(LockIn(Side.Right, noShield), Is.False);
            Assert.That(LockIn(Side.Right, Duelist), Is.True);
        }

        [Test]
        public void ALockedInLoadout_CannotBeChanged()
        {
            LockIn(Side.Left, Duelist);

            Assert.That(LockIn(Side.Left, Lancer), Is.False);
            Assert.That(_selection.OwnLoadout(Side.Left), Is.EqualTo(Duelist));
        }

        [Test]
        public void TheOrderOfTheSlots_IsKept_TheFirstIsTheStartingWeapon()
        {
            LockIn(Side.Left, WeaponIds.Shield, WeaponIds.Rapier, WeaponIds.Mace);

            Assert.That(_selection.OwnLoadout(Side.Left), Is.EqualTo(new[] { WeaponIds.Shield, WeaponIds.Rapier, WeaponIds.Mace }));
        }

        [Test]
        public void TheLockedInLoadout_IsACopy_SoALaterEditCannotChangeIt()
        {
            var picks = new List<WeaponId>(Duelist);
            _selection.TryLockIn(Side.Left, picks, out _);

            picks[0] = WeaponIds.Spear;

            Assert.That(_selection.OwnLoadout(Side.Left), Is.EqualTo(Duelist));
        }

        // --- The reveal ---------------------------------------------------------------------------

        [Test]
        public void WhileOneSideIsStillPicking_NobodySeesTheOthersLoadout()
        {
            LockIn(Side.Left, Duelist);

            Assert.That(_selection.IsComplete, Is.False);
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Right), Is.Null, "the right player must not see the left's picks");
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Left), Is.Null);
            Assert.That(() => _selection.Reveal(), Throws.InvalidOperationException);
        }

        [Test]
        public void OnceBothAreLockedIn_BothLoadoutsAreRevealedToBoth()
        {
            LockIn(Side.Left, Duelist);
            LockIn(Side.Right, Lancer);

            Assert.That(_selection.IsComplete, Is.True);
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Left), Is.EqualTo(Lancer));
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Right), Is.EqualTo(Duelist));
            LoadoutReveal reveal = _selection.Reveal();
            Assert.That(reveal.Left, Is.EqualTo(Duelist));
            Assert.That(reveal.Right, Is.EqualTo(Lancer));
            Assert.That(reveal.Of(Side.Right), Is.EqualTo(Lancer));
        }

        [Test]
        public void TheFighters_CarryTheLockedInLoadouts_AndEachSidesHandedness()
        {
            LockIn(Side.Right, Duelist);
            LockIn(Side.Left, Lancer);

            PerSide<FighterSetup> fighters = _selection.CreateFighters(new PerSide<Handedness>(Handedness.Right, Handedness.Left));

            Assert.That(fighters.Left.Loadout, Is.EqualTo(Lancer));
            Assert.That(fighters.Left.Handedness, Is.EqualTo(Handedness.Right));
            Assert.That(fighters.Right.Loadout, Is.EqualTo(Duelist));
            Assert.That(fighters.Right.Handedness, Is.EqualTo(Handedness.Left));
        }

        [Test]
        public void TheMatchStartsWithTheFirstPickedWeapon()
        {
            LockIn(Side.Left, WeaponIds.Spear, WeaponIds.Rapier, WeaponIds.Shield);
            LockIn(Side.Right, Duelist);
            var setup = new MatchSetup(_selection.CreateFighters(PerSide<Handedness>.Create(_ => Handedness.Right)), 1u);

            MatchState state = MatchState.CreateInitial(setup, _settings);

            Assert.That(state.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Spear));
            Assert.That(state.Fighters[Side.Right].CurrentWeapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void TheCreateFighters_RefuseToRunBeforeBothAreLockedIn()
        {
            LockIn(Side.Left, Duelist);

            Assert.That(() => _selection.CreateFighters(PerSide<Handedness>.Create(_ => Handedness.Right)),
                Throws.InvalidOperationException);
        }
    }
}
