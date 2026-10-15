using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// The flow of the pre-match pick (GDD §6): hand-over, picking, locking in, the reveal. Hot-seat hands the device over before
    /// each player; against a bot only the human picks; two bots go straight to the reveal.
    /// </summary>
    public sealed class LoadoutPickSessionTests
    {
        private static readonly WeaponId[] Lancer = { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield };
        private static readonly WeaponId[] Duelist = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };

        private RulesSettings _settings;
        private IWeaponOwnership _ownership;
        private LoadoutSelection _selection;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _ownership = new AllWeaponsOwned();
            _selection = new LoadoutSelection(_settings, _ownership);
        }

        private LoadoutPickSession HotSeat(PerSide<IReadOnlyList<WeaponId>> previous = null) =>
            new LoadoutPickSession(_settings, _ownership, _selection, new[] { Side.Left, Side.Right }, true, previous);

        private LoadoutPickSession AgainstABot()
        {
            _selection.TryLockIn(Side.Right, Lancer, out _);
            return new LoadoutPickSession(_settings, _ownership, _selection, new[] { Side.Left }, false);
        }

        private static void Swap(LoadoutPickSession session, WeaponId out_, WeaponId in_)
        {
            Assert.That(session.Toggle(out_), Is.True);
            Assert.That(session.Toggle(in_), Is.True);
        }

        // --- Hot-seat: a hand-over before each player ---------------------------------------------------

        [Test]
        public void HotSeat_StartsWithAHandOverToPlayerOne_WhoseDefaultIsFilledIn()
        {
            LoadoutPickSession session = HotSeat();

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.HandOver));
            Assert.That(session.PickingSide, Is.EqualTo(Side.Left));
            Assert.That(session.Picks, Is.EqualTo(Duelist), "D8: rapier, mace, shield");
            Assert.That(session.CanConfirm, Is.False, "not while the device is changing hands");
            Assert.That(session.Toggle(WeaponIds.Sword), Is.False, "no picking behind the cover");
        }

        [Test]
        public void HotSeat_RunsHandOverPickHandOverPickReveal()
        {
            LoadoutPickSession session = HotSeat();

            Assert.That(session.ConfirmHandOver(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Picking));
            Assert.That(session.Confirm(), Is.True);

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.HandOver));
            Assert.That(session.PickingSide, Is.EqualTo(Side.Right));
            Assert.That(session.ConfirmHandOver(), Is.True);
            Assert.That(session.Confirm(), Is.True);

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Reveal));
            Assert.That(session.ConfirmReveal(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Done));
        }

        [Test]
        public void HotSeat_TheSecondPlayerNeverSeesTheFirstPlayersPicks()
        {
            LoadoutPickSession session = HotSeat();
            session.ConfirmHandOver();
            Swap(session, WeaponIds.Mace, WeaponIds.Spear);
            session.Confirm();

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.HandOver));
            Assert.That(session.Picks, Is.EqualTo(Duelist), "the second player starts from the default, not the first player's picks");
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Right), Is.Null);
            Assert.That(() => { _ = session.Result; }, Throws.InvalidOperationException);
        }

        [Test]
        public void TheRevealShowsBothLoadouts_InTheOrderPicked()
        {
            LoadoutPickSession session = HotSeat();
            session.ConfirmHandOver();
            Swap(session, WeaponIds.Mace, WeaponIds.Spear);
            session.Confirm();
            session.ConfirmHandOver();
            session.Confirm();

            LoadoutReveal result = session.Result;

            Assert.That(result.Left, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield, WeaponIds.Spear }));
            Assert.That(result.Right, Is.EqualTo(Duelist));
        }

        // --- Picking ------------------------------------------------------------------------------

        [Test]
        public void ATappedWeapon_GoesIntoTheNextSlot_AndBackWhenTappedAgain()
        {
            LoadoutPickSession session = AgainstABot();
            Assert.That(session.Toggle(WeaponIds.Mace), Is.True);
            Assert.That(session.Picks, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield }));

            Assert.That(session.Toggle(WeaponIds.Sword), Is.True);

            Assert.That(session.Picks, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield, WeaponIds.Sword }),
                "the order picked is kept: the first is the weapon the match starts with");
        }

        [Test]
        public void AFourthWeapon_IsRefusedUntilOneIsPutBack()
        {
            LoadoutPickSession session = AgainstABot();

            Assert.That(session.Toggle(WeaponIds.Sword), Is.False, "3 slots, all full");
            Assert.That(session.Picks, Has.Count.EqualTo(3));
        }

        [Test]
        public void ThePickCanOnlyConfirm_WithAllThreeSlotsFilled()
        {
            LoadoutPickSession session = AgainstABot();
            session.Toggle(WeaponIds.Mace);
            Assert.That(session.CanConfirm, Is.False);
            Assert.That(session.Confirm(), Is.False);
            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Picking));

            session.Toggle(WeaponIds.Mace);

            Assert.That(session.CanConfirm, Is.True);
        }

        [Test]
        public void ARequiredShield_BlocksALoadoutWithoutOne_AndSaysWhy()
        {
            _settings.Loadout.RequireShieldSlot = true;
            LoadoutPickSession session = AgainstABot();

            Assert.That(session.Picks, Is.EqualTo(Duelist), "the default has a shield");
            Swap(session, WeaponIds.Shield, WeaponIds.Sword);

            Assert.That(session.CanConfirm, Is.False);
            Assert.That(session.Problem, Does.Contain("shield"));
            Assert.That(session.Confirm(), Is.False);
            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Picking));

            Swap(session, WeaponIds.Sword, WeaponIds.Shield);

            Assert.That(session.CanConfirm, Is.True);
            Assert.That(session.Problem, Is.Null);
        }

        [Test]
        public void ADefaultLoadout_StartsAMatchWithOneTap()
        {
            LoadoutPickSession session = AgainstABot();

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Picking));
            Assert.That(session.CanConfirm, Is.True);
            Assert.That(session.Confirm(), Is.True);
            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Reveal));
            Assert.That(session.ConfirmReveal(), Is.True);
        }

        [Test]
        public void AgainstABot_OnlyTheHumanPicks_AndTheBotsLoadoutIsRevealedAtTheEnd()
        {
            LoadoutPickSession session = AgainstABot();
            Assert.That(session.PickingSide, Is.EqualTo(Side.Left));
            Assert.That(_selection.OpponentLoadoutSeenBy(Side.Left), Is.Null, "the bot's loadout is hidden while the human picks");
            session.Confirm();

            LoadoutReveal result = session.Result;

            Assert.That(result.Right, Is.EqualTo(Lancer));
            Assert.That(() => { _ = session.PickingSide; }, Throws.InvalidOperationException, "nobody is picking at the reveal");
        }

        // --- A rematch and two bots ----------------------------------------------------------------

        [Test]
        public void ARematch_StartsFromTheLastPicks_WhenTheyAreStillValid()
        {
            var previous = new PerSide<IReadOnlyList<WeaponId>>(Lancer, Duelist);

            LoadoutPickSession session = HotSeat(previous);

            Assert.That(session.Picks, Is.EqualTo(Lancer));
            session.ConfirmHandOver();
            session.Confirm();
            Assert.That(session.Picks, Is.EqualTo(Duelist));
        }

        [Test]
        public void LastPicksThatAreNoLongerOwned_FallBackToTheDefault()
        {
            _ownership = new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield);
            _selection = new LoadoutSelection(_settings, _ownership);
            var previous = new PerSide<IReadOnlyList<WeaponId>>(Lancer, Lancer);

            LoadoutPickSession session = HotSeat(previous);

            Assert.That(session.Picks, Is.EqualTo(Duelist));
        }

        [Test]
        public void TwoBots_GoStraightToTheReveal()
        {
            _selection.TryLockIn(Side.Left, Duelist, out _);
            _selection.TryLockIn(Side.Right, Lancer, out _);

            var session = new LoadoutPickSession(_settings, _ownership, _selection, new Side[0], false);

            Assert.That(session.Stage, Is.EqualTo(LoadoutPickStage.Reveal));
            Assert.That(session.Result.Left, Is.EqualTo(Duelist));
            Assert.That(session.ConfirmReveal(), Is.True);
        }

        [Test]
        public void ABotsOpenSide_CannotBeLeftWithoutAHuman()
        {
            Assert.That(() => new LoadoutPickSession(_settings, _ownership, _selection, new Side[0], false),
                Throws.ArgumentException);
        }

        [Test]
        public void AHumanWhoIsAlreadyLockedIn_IsRefused()
        {
            _selection.TryLockIn(Side.Left, Duelist, out _);

            Assert.That(() => new LoadoutPickSession(_settings, _ownership, _selection, new[] { Side.Left }, false),
                Throws.ArgumentException);
        }

        [Test]
        public void StepsOutOfOrder_DoNothing()
        {
            LoadoutPickSession session = AgainstABot();

            Assert.That(session.ConfirmHandOver(), Is.False);
            Assert.That(session.ConfirmReveal(), Is.False);
            Assert.That(() => { _ = session.Result; }, Throws.InvalidOperationException);
        }
    }
}
