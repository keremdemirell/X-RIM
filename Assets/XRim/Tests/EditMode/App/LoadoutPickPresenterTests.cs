using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Bots;
using XRim.Core;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The pre-match pick on the match screens (Session 14, GDD §6): hot-seat hands the device over before each player, a bot match
    /// asks only the human, two bots go straight to the reveal. The rules live in <c>LoadoutPickSession</c>; this checks what the
    /// screens are told and how their taps are heard.
    /// </summary>
    public sealed class LoadoutPickPresenterTests
    {
        private RulesSettings _rules;
        private FakeScreens _screens;
        private LoadoutReveal _completed;
        private int _completedCount;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _screens = new FakeScreens();
            _completed = null;
            _completedCount = 0;
        }

        private LoadoutPickPresenter Start(MatchMode mode, PerSide<IReadOnlyList<WeaponId>> previous = null, uint seed = 7u)
        {
            LoadoutPickPresenter presenter = LoadoutPickPresenter.Create(_screens, _rules, new AllWeaponsOwned(), mode, Side.Left,
                new RandomLoadoutPicker(), new XorShiftRandom(seed), previous);
            presenter.Completed += reveal =>
            {
                _completed = reveal;
                _completedCount++;
            };
            presenter.Begin();
            return presenter;
        }

        [Test]
        public void Online_ThePickGoesToTheServer_AndTheRevealShowsWhatTheServerRevealed()
        {
            LoadoutPickPresenter presenter = Start(MatchMode.Online);
            IReadOnlyList<WeaponId> sent = null;
            presenter.LocalPicked += picks => sent = picks;

            _screens.TapConfirm();
            _screens.TapReveal();

            Assert.That(sent, Is.Not.Null.And.Count.EqualTo(RuleConstants.LoadoutSlotCount), "the pick goes out");
            Assert.That(_screens.Last, Is.Not.EqualTo("reveal"), "nothing is revealed before the server says so");
            Assert.That(_completedCount, Is.EqualTo(0));

            var fromServer = new LoadoutReveal(new PerSide<IReadOnlyList<WeaponId>>(sent, new[] { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield }));
            presenter.ShowRemoteReveal(fromServer);
            _screens.TapReveal();

            Assert.That(_screens.RevealRight, Is.EqualTo(new[] { "SPEAR", "SWORD", "SHIELD" }));
            Assert.That(_completed, Is.SameAs(fromServer));
            Assert.That(_completedCount, Is.EqualTo(1));
        }

        private static string[] Orders(IReadOnlyList<LoadoutChoice> choices)
        {
            var picked = new string[RuleConstants.LoadoutSlotCount];
            foreach (LoadoutChoice choice in choices)
            {
                if (choice.PickedOrder > 0) picked[choice.PickedOrder - 1] = choice.Title;
            }

            return picked;
        }

        // --- Hot-seat ---------------------------------------------------------------------------

        [Test]
        public void HotSeat_ShowsTheCoverForPlayerOneFirst_AndNothingOfTheirPicksBehindIt()
        {
            Start(MatchMode.HotSeat);

            Assert.That(_screens.Last, Is.EqualTo("cover"));
            Assert.That(_screens.CoverPlayer, Is.EqualTo("PLAYER 1 (LEFT)"));
            Assert.That(_screens.PickerShown, Is.False, "the picks are not drawn while the device changes hands");
        }

        [Test]
        public void HotSeat_RunsCoverPickerCoverPickerReveal_AndCompletesWithBothLoadouts()
        {
            Start(MatchMode.HotSeat);

            _screens.TapCover();
            Assert.That(_screens.Last, Is.EqualTo("picker"));
            Assert.That(_screens.PickerTitle, Is.EqualTo("PLAYER 1: PICK 3 WEAPONS"));
            Assert.That(_screens.Choices, Has.Count.EqualTo(5), "the five weapons; the shield counts as one");
            Assert.That(Orders(_screens.Choices), Is.EqualTo(new[] { "RAPIER", "MACE", "SHIELD" }), "D8 default, pre-filled");
            Assert.That(_screens.ConfirmText, Is.EqualTo("LOCK IN"));
            Assert.That(_screens.CanConfirm, Is.True);

            _screens.TapChoice(3); // the spear: all three slots are full
            Assert.That(_screens.PickerNote, Is.EqualTo(MatchLabels.LoadoutSlotsFull));
            _screens.TapChoice(2); // the mace goes back
            Assert.That(_screens.ConfirmText, Is.EqualTo("PICK 1 MORE"));
            Assert.That(_screens.CanConfirm, Is.False);
            _screens.TapChoice(3); // the spear takes the free slot
            Assert.That(Orders(_screens.Choices), Is.EqualTo(new[] { "RAPIER", "SHIELD", "SPEAR" }));
            _screens.TapConfirm();

            Assert.That(_screens.Last, Is.EqualTo("cover"));
            Assert.That(_screens.CoverPlayer, Is.EqualTo("PLAYER 2 (RIGHT)"));
            _screens.TapCover();
            Assert.That(_screens.PickerTitle, Is.EqualTo("PLAYER 2: PICK 3 WEAPONS"));
            Assert.That(Orders(_screens.Choices), Is.EqualTo(new[] { "RAPIER", "MACE", "SHIELD" }),
                "player 2 starts from the default, never from player 1's picks");
            _screens.TapConfirm();

            Assert.That(_screens.Last, Is.EqualTo("reveal"));
            Assert.That(_screens.RevealLeftName, Is.EqualTo("PLAYER 1"));
            Assert.That(_screens.RevealLeft, Is.EqualTo(new[] { "RAPIER", "SHIELD", "SPEAR" }));
            Assert.That(_screens.RevealRight, Is.EqualTo(new[] { "RAPIER", "MACE", "SHIELD" }));
            Assert.That(_completedCount, Is.EqualTo(0), "the match starts on the tap after the reveal");

            _screens.TapReveal();

            Assert.That(_completedCount, Is.EqualTo(1));
            Assert.That(_completed.Left, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield, WeaponIds.Spear }));
            Assert.That(_completed.Right, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
        }

        [Test]
        public void ARequiredShield_IsSaidOnTheLockInButton_AndRefusedUntilOneIsPicked()
        {
            _rules.Loadout.RequireShieldSlot = true;
            Start(MatchMode.HumanVsBot);

            _screens.TapChoice(4); // the shield goes back
            _screens.TapChoice(1); // the sword fills the slot
            Assert.That(_screens.CanConfirm, Is.False);
            Assert.That(_screens.ConfirmText, Is.EqualTo(MatchLabels.LoadoutShieldNeeded));
            Assert.That(_screens.PickerNote, Does.Contain("shield"));
            _screens.TapConfirm();
            Assert.That(_screens.Last, Is.EqualTo("picker"), "refused: still picking");
            Assert.That(_completedCount, Is.EqualTo(0));
        }

        // --- A new player's three weapons (Session 18) ------------------------------------------------

        [Test]
        public void ANewPlayer_WhoOwnsOnlyTheThreeStartingWeapons_IsToldWhereMoreComeFrom()
        {
            LoadoutPickPresenter presenter = LoadoutPickPresenter.Create(_screens, _rules, new StartingWeapons(), MatchMode.HumanVsBot, Side.Left,
                new RandomLoadoutPicker(), new XorShiftRandom(3u), null);
            presenter.Begin();

            Assert.That(_screens.Choices.Count, Is.EqualTo(3), "only what they own is offered");
            Assert.That(_screens.PickerNote, Is.EqualTo(MatchLabels.LoadoutFewWeapons));
        }

        [Test]
        public void APlayerWithManyWeapons_SeesTheUsualNote()
        {
            Start(MatchMode.HumanVsBot);

            Assert.That(_screens.PickerNote, Is.EqualTo(MatchLabels.LoadoutNote));
        }

        [Test]
        public void ABotMatch_GivesTheBotOnlyWhatThePlayerOwns()
        {
            LoadoutPickPresenter presenter = LoadoutPickPresenter.Create(_screens, _rules, new StartingWeapons(), MatchMode.HumanVsBot, Side.Left,
                new RandomLoadoutPicker(), new XorShiftRandom(3u), null);
            presenter.Completed += reveal => _completed = reveal;
            presenter.Begin();
            _screens.TapConfirm();
            _screens.TapReveal();

            Assert.That(_completed.Right, Is.EquivalentTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }),
                "the bot picks from the same collection (designer 2026-10-25, Q3)");
        }

        // --- Against a bot, and two bots -------------------------------------------------------------

        [Test]
        public void AgainstABot_OnlyTheHumanPicks_AndTheBotsLoadoutIsValidAndRevealed()
        {
            Start(MatchMode.HumanVsBot);

            Assert.That(_screens.Last, Is.EqualTo("picker"), "no cover: one human, one device");
            Assert.That(_screens.PickerTitle, Is.EqualTo("PICK YOUR 3 WEAPONS"));
            _screens.TapConfirm();

            Assert.That(_screens.Last, Is.EqualTo("reveal"));
            Assert.That(_screens.RevealLeftName, Is.EqualTo(MatchLabels.You));
            Assert.That(_screens.RevealRight, Has.Count.EqualTo(3));
            _screens.TapReveal();
            var issues = new List<string>();
            LoadoutValidator.Validate(_completed.Right, _rules, issues);
            Assert.That(issues, Is.Empty, "the bot's picks are a valid loadout");
        }

        [Test]
        public void TwoBots_GoStraightToTheReveal()
        {
            Start(MatchMode.BotVsBot);

            Assert.That(_screens.Last, Is.EqualTo("reveal"));
            Assert.That(_screens.RevealLeftName, Is.EqualTo("LEFT BOT"));
            _screens.TapReveal();
            Assert.That(_completedCount, Is.EqualTo(1));
            Assert.That(_completed.Left, Has.Count.EqualTo(3));
            Assert.That(_completed.Right, Has.Count.EqualTo(3));
        }

        [Test]
        public void ARematch_StartsFromTheLastPicks()
        {
            var previous = new PerSide<IReadOnlyList<WeaponId>>(
                new[] { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield }, new[] { WeaponIds.Mace, WeaponIds.Rapier, WeaponIds.Sword });

            Start(MatchMode.HumanVsBot, previous);

            Assert.That(Orders(_screens.Choices), Is.EqualTo(new[] { "SPEAR", "SWORD", "SHIELD" }));
        }

        [Test]
        public void TheSandbox_DoesNotPick()
        {
            Assert.That(() => LoadoutPickPresenter.Create(_screens, _rules, new AllWeaponsOwned(), MatchMode.Sandbox, Side.Left,
                new RandomLoadoutPicker(), new XorShiftRandom(1u), null), Throws.ArgumentException);
        }

        // --- Listening -----------------------------------------------------------------------------

        [Test]
        public void ADisposedPick_HearsNoMoreTaps()
        {
            LoadoutPickPresenter presenter = Start(MatchMode.HumanVsBot);
            int shownBefore = _screens.ShownCount;

            presenter.Dispose();
            _screens.TapChoice(1);
            _screens.TapConfirm();

            Assert.That(_screens.ShownCount, Is.EqualTo(shownBefore));
            Assert.That(_completedCount, Is.EqualTo(0));
        }

        [Test]
        public void TheRevealTap_CompletesOnlyOnce()
        {
            Start(MatchMode.BotVsBot);

            _screens.TapReveal();
            _screens.TapReveal();

            Assert.That(_completedCount, Is.EqualTo(1));
        }

        /// <summary>A new player's weapons: the rapier, the mace and the shield.</summary>
        private sealed class StartingWeapons : IWeaponOwnership
        {
            public bool Owns(WeaponId weapon) => weapon == WeaponIds.Rapier || weapon == WeaponIds.Mace || weapon == WeaponIds.Shield;
        }

        /// <summary>Records what the presenter shows and lets a test tap.</summary>
        internal sealed class FakeScreens : IMatchScreensView
        {
            public event Action CoverConfirmed;
            public event Action RematchPressed;
            public event Action MenuPressed;
            public event Action ReplaySkipPressed;
            public event Action<int> LoadoutChoiceTapped;
            public event Action LoadoutConfirmed;
            public event Action LoadoutRevealConfirmed;

            public string Last { get; private set; } = "none";
            public int ShownCount { get; private set; }
            public string CoverPlayer { get; private set; }
            public string PickerTitle { get; private set; }
            public string PickerNote { get; private set; }
            public IReadOnlyList<LoadoutChoice> Choices { get; private set; }
            public string ConfirmText { get; private set; }
            public bool CanConfirm { get; private set; }
            public string RevealLeftName { get; private set; }
            public IReadOnlyList<string> RevealLeft { get; private set; }
            public IReadOnlyList<string> RevealRight { get; private set; }
            public bool PickerShown => Last == "picker";

            public void TapCover() => CoverConfirmed?.Invoke();
            public void TapChoice(int index) => LoadoutChoiceTapped?.Invoke(index);
            public void TapConfirm() => LoadoutConfirmed?.Invoke();
            public void TapReveal() => LoadoutRevealConfirmed?.Invoke();
            public void TapRematch() => RematchPressed?.Invoke();
            public void TapMenu() => MenuPressed?.Invoke();
            public void TapReplay() => ReplaySkipPressed?.Invoke();

            public void ShowLoadoutPicker(string title, string note, IReadOnlyList<LoadoutChoice> choices, string confirmText, bool canConfirm)
            {
                PickerTitle = title;
                PickerNote = note;
                Choices = choices;
                ConfirmText = confirmText;
                CanConfirm = canConfirm;
                Note("picker");
            }

            public void ShowLoadoutReveal(string title, string leftName, IReadOnlyList<string> leftWeapons, string rightName,
                IReadOnlyList<string> rightWeapons, string action)
            {
                RevealLeftName = leftName;
                RevealLeft = leftWeapons;
                RevealRight = rightWeapons;
                Note("reveal");
            }

            public void ShowCover(string player, string instruction, string action)
            {
                CoverPlayer = player;
                Note("cover");
            }

            public void ShowMatchOver(string title, string reason, string summary, string reward) => Note("over");

            public void ShowReplay(string title, string caption, string hint) => Note("replay");

            public void Hide() => Note("hidden");

            private void Note(string screen)
            {
                Last = screen;
                ShownCount++;
            }
        }
    }
}
