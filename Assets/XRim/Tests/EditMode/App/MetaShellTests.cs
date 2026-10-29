using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XRim.App;
using XRim.Bots;
using XRim.Config;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Profile;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The game outside a match (Session 18): the first-launch handedness question, the main menu, Play, Practice and Settings, driven
    /// through a fake view and a real profile file in a temporary folder.
    /// </summary>
    public sealed class MetaShellTests
    {
        private EconomySettings _economy;
        private string _folder;
        private FakeShellView _view;
        private MetaShell _shell;
        private readonly List<MatchRequest> _requests = new List<MatchRequest>();

        [SetUp]
        public void SetUp()
        {
            _economy = new EconomySettings();
            _folder = Path.Combine(Path.GetTempPath(), "XRimShellTests-" + Guid.NewGuid().ToString("N"));
            _requests.Clear();
            _view = new FakeShellView();
            _shell = NewShell(_view);
        }

        [TearDown]
        public void TearDown()
        {
            _shell.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private MetaShell NewShell(FakeShellView view, IPremiumPurchaseGateway gateway = null)
        {
            var store = new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy));
            var shell = new MetaShell(view, store, _economy, GddStartingValues.CreateRulesSettings, gateway);
            shell.PlayRequested += request => _requests.Add(request);
            return shell;
        }

        private void AnswerHandedness(Handedness handedness)
        {
            _shell.Show();
            _view.PickHandedness(handedness);
        }

        // --- the first launch -------------------------------------------------------------------

        [Test]
        public void ANewProfile_AsksWhichHandHoldsTheWeapon_BeforeAnyMenu()
        {
            _shell.Show();

            Assert.That(_view.Screen, Is.EqualTo("handedness"));
            Assert.That(_shell.Profile.HasChosenHandedness, Is.False);
            Assert.That(_shell.LoadResult.Status, Is.EqualTo(ProfileLoadStatus.Created));
        }

        [Test]
        public void TheAnswer_IsRemembered_AndOpensTheMainMenu()
        {
            AnswerHandedness(Handedness.Left);

            Assert.That(_shell.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(_view.Screen, Is.EqualTo("menu"));
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.GameTitle));
        }

        [Test]
        public void TheAnswer_IsSavedAtOnce_SoTheNextLaunchDoesNotAskAgain()
        {
            AnswerHandedness(Handedness.Left);
            _shell.Dispose();

            var nextView = new FakeShellView();
            MetaShell next = NewShell(nextView);
            next.Show();

            Assert.That(next.LoadResult.Status, Is.EqualTo(ProfileLoadStatus.Loaded));
            Assert.That(next.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(nextView.Screen, Is.EqualTo("menu"), "straight to the main menu");
            next.Dispose();
        }

        [Test]
        public void APickMadeOutsideTheQuestionOrSettings_IsIgnored()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickHandedness(Handedness.Left); // the main menu is showing

            Assert.That(_shell.Profile.ChosenHandedness, Is.EqualTo(Handedness.Right));
        }

        // --- the menus ---------------------------------------------------------------------------

        [Test]
        public void TheMainMenu_OffersPlayLoadoutShopAndSettings_WithNoBackButton()
        {
            AnswerHandedness(Handedness.Right);

            Assert.That(_view.Options.Count, Is.EqualTo(4));
            Assert.That(_view.Options[ShellLabels.MainPlay].Title, Is.EqualTo("PLAY"));
            Assert.That(_view.Options[ShellLabels.MainLoadout].Title, Is.EqualTo("LOADOUT"));
            Assert.That(_view.Options[ShellLabels.MainShop].Title, Is.EqualTo("SHOP"));
            Assert.That(_view.Options[ShellLabels.MainSettings].Title, Is.EqualTo("SETTINGS"));
            Assert.That(_view.BackText, Is.Empty);
        }

        [Test]
        public void EveryMainMenuEntry_IsLive()
        {
            AnswerHandedness(Handedness.Right);

            foreach (MenuOption option in _view.Options)
            {
                Assert.That(option.Enabled, Is.True, option.Title);
            }
        }

        [Test]
        public void TheMainMenu_ShowsTheWallet()
        {
            AnswerHandedness(Handedness.Right);
            Assert.That(_view.Note, Does.Contain("BOLTS 0   GEARS 0"));

            _shell.GrantForDebug(120, 5);

            Assert.That(_view.Note, Does.Contain("BOLTS 120   GEARS 5"), "the open menu shows the new balance at once");
        }

        // --- the Shop ----------------------------------------------------------------------------

        private void OpenShop()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainShop);
        }

        private ProfileLoadResult Reload() => new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy)).Load();

        [Test]
        public void TheShop_OpensOnTheWeaponsTab_WithTheWalletInTheHeader()
        {
            OpenShop();

            Assert.That(_view.Screen, Is.EqualTo("collection"));
            Assert.That(_view.Collection.Title, Is.EqualTo(ShellLabels.ShopTitle));
            Assert.That(_view.Collection.Header, Is.EqualTo("BOLTS 0   GEARS 0"));
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabWeapons));
        }

        [Test]
        public void AWeaponTheyCanAfford_IsBoughtWithEarnedCurrency_AndSaved()
        {
            OpenShop();
            _shell.GrantForDebug(500, 0);

            _view.PressRow(ShellLabels.TabWeapons, 1); // the sword

            Assert.That(_shell.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.True);
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(200L));
            Assert.That(_view.Collection.Status, Is.EqualTo("BOUGHT: SWORD"));
            Assert.That(_view.Collection.Header, Is.EqualTo("BOLTS 200   GEARS 0"));
            Assert.That(_view.Collection.Tabs[ShellLabels.TabWeapons].Rows[1].ButtonText, Is.EqualTo(ShellLabels.Owned));
            ProfileLoadResult saved = Reload();
            Assert.That(saved.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.True);
            Assert.That(saved.Profile.Wallet.Earned.Value, Is.EqualTo(200L));
        }

        [Test]
        public void AWeaponTheyCannotAffordYet_IsRefused_AndChargesNothing()
        {
            OpenShop();
            _shell.GrantForDebug(299, 0);

            _view.PressRow(ShellLabels.TabWeapons, 1);

            Assert.That(_shell.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.False);
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(299L));
            Assert.That(_view.Collection.Status, Is.EqualTo("Not enough Bolts"));
        }

        [Test]
        public void PremiumCurrency_CannotBuyAWeaponOrAMove_EvenWithAFortune()
        {
            OpenShop();
            _shell.GrantForDebug(0, 100000);

            _view.PressRow(ShellLabels.TabWeapons, 1, 0);
            _view.PressRow(ShellLabels.TabWeapons, 1, 1); // a weapon has no second button
            _view.SelectTab(ShellLabels.TabMoves);
            _view.PressRow(ShellLabels.TabMoves, 1);
            _view.PressRow(ShellLabels.TabMoves, 1, 1);

            Assert.That(_shell.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.False);
            Assert.That(_shell.Profile.Inventory.Owns(GameplayUnlockKind.SignatureMove, SignatureMoveIds.HeadImpactSimulation404.Value), Is.False);
            Assert.That(_shell.Profile.Wallet.Premium.Value, Is.EqualTo(100000L), "not a Gear was spent");
        }

        [Test]
        public void AMoveIsBoughtWithEarnedCurrency_AndBroughtToTheNextMatch()
        {
            OpenShop();
            _shell.GrantForDebug(400, 0);
            _view.SelectTab(ShellLabels.TabMoves);

            _view.PressRow(ShellLabels.TabMoves, 1);

            Assert.That(_view.Collection.Status, Is.EqualTo("BOUGHT: HEAD IMPACT SIMULATION #404"));
            Assert.That(ProfileSetups.OwnedSignatureMoves(_shell.Profile, _shell.Catalogue),
                Is.EqualTo(new[] { SignatureMoveIds.FiveFingerExplodingTouch, SignatureMoveIds.HeadImpactSimulation404 }));
        }

        [Test]
        public void ALook_CanBeBoughtWithEitherCurrency_ThroughItsTwoButtons()
        {
            OpenShop();
            _shell.GrantForDebug(250, 50);
            _view.SelectTab(ShellLabels.TabLooks);

            _view.PressRow(ShellLabels.TabLooks, 0, 0); // Neon Sparks with Bolts
            _view.PressRow(ShellLabels.TabLooks, 3, 1); // Drill Sergeant with Gears

            Assert.That(_shell.Profile.Inventory.OwnsCosmetic(CosmeticIds.NeonSparks), Is.True);
            Assert.That(_shell.Profile.Inventory.OwnsCosmetic(CosmeticIds.DrillSergeant), Is.True);
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(_shell.Profile.Wallet.Premium.Value, Is.EqualTo(0L));
        }

        [Test]
        public void ALookSoldOnlyForGears_CannotBeBoughtWithBolts()
        {
            OpenShop();
            _shell.GrantForDebug(100000, 0);
            _view.SelectTab(ShellLabels.TabLooks);
            Assert.That(_view.Collection.Tabs[ShellLabels.TabLooks].Rows[2].ButtonText, Is.Empty, "Night Shift has no Bolts button");

            _view.PressRow(ShellLabels.TabLooks, 2, 0);

            Assert.That(_shell.Profile.Inventory.OwnsCosmetic(CosmeticIds.NightShift), Is.False);
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(100000L));
            Assert.That(_view.Collection.Status, Does.Contain("not sold for Bolts"));
        }

        [Test]
        public void ABoughtLook_CanThenBePutOnInTheLoadoutScreen()
        {
            OpenShop();
            _shell.GrantForDebug(250, 0);
            _view.SelectTab(ShellLabels.TabLooks);
            _view.PressRow(ShellLabels.TabLooks, 0, 0);
            _view.PressBack();

            _view.PickOption(ShellLabels.MainLoadout);
            _view.SelectTab(ShellLabels.TabLooks);
            _view.PressRow(ShellLabels.TabLooks, 0);

            Assert.That(_shell.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo(CosmeticIds.NeonSparks));
        }

        [Test]
        public void TheGearsRow_IsAStub_ItSellsNothingAndSaysWhy()
        {
            OpenShop();
            _shell.GrantForDebug(77, 3);
            _view.SelectTab(ShellLabels.TabLooks);
            int gearsRow = _view.Collection.Tabs[ShellLabels.TabLooks].Rows.Count - 1;

            _view.PressRow(ShellLabels.TabLooks, gearsRow);

            Assert.That(_view.Collection.Status, Is.EqualTo(StorePurchasesNotAvailable.Reason));
            Assert.That(_shell.Profile.Wallet.Premium.Value, Is.EqualTo(3L));
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(77L));
        }

        [Test]
        public void ACompletedStorePurchase_AddsPremiumCurrencyOnly()
        {
            _shell.Dispose();
            _shell = NewShell(_view, new FakeGateway(new PremiumPurchaseResult(PremiumPurchaseOutcome.Completed, new PremiumAmount(500), "Thank you")));
            OpenShop();
            _shell.GrantForDebug(10, 0);
            _view.SelectTab(ShellLabels.TabLooks);
            int gearsRow = _view.Collection.Tabs[ShellLabels.TabLooks].Rows.Count - 1;

            _view.PressRow(ShellLabels.TabLooks, gearsRow);

            Assert.That(_shell.Profile.Wallet.Premium.Value, Is.EqualTo(500L));
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(10L), "real money never becomes earned currency");
            Assert.That(_view.Collection.Status, Is.EqualTo("Thank you"));
            Assert.That(Reload().Profile.Wallet.Premium.Value, Is.EqualTo(500L), "and it is saved");
        }

        [Test]
        public void ACancelledStorePurchase_ChangesNothing()
        {
            _shell.Dispose();
            _shell = NewShell(_view, new FakeGateway(new PremiumPurchaseResult(PremiumPurchaseOutcome.Cancelled, new PremiumAmount(500), "Cancelled")));
            OpenShop();
            _view.SelectTab(ShellLabels.TabLooks);
            int gearsRow = _view.Collection.Tabs[ShellLabels.TabLooks].Rows.Count - 1;

            _view.PressRow(ShellLabels.TabLooks, gearsRow);

            Assert.That(_shell.Profile.Wallet.Premium.Value, Is.EqualTo(0L), "only a completed purchase pays out");
        }

        [Test]
        public void TheShopTabs_AndBack_Work()
        {
            OpenShop();
            _view.SelectTab(ShellLabels.TabMoves);
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabMoves));
            Assert.That(_view.Collection.Title, Is.EqualTo(ShellLabels.ShopTitle));

            _view.PressBack();

            Assert.That(_view.Screen, Is.EqualTo("menu"));
        }

        [Test]
        public void ARowPress_OutsideTheShopOrTheCollection_IsIgnored()
        {
            AnswerHandedness(Handedness.Right);
            _shell.GrantForDebug(1000, 0);

            _view.PressRow(ShellLabels.TabWeapons, 1);

            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(1000L));
        }

        // --- after a match -------------------------------------------------------------------------

        private static MatchSummary Summary(Side winner, int leftHits = 0) => new MatchSummary(
            new MatchOutcome(winner, MatchEndReason.KnockOut, 9), new PerSide<int>(leftHits, 0), new PerSide<int>(0, 0));

        [Test]
        public void WinningAgainstTheBot_PaysTheReward_AndSavesIt()
        {
            AnswerHandedness(Handedness.Right);

            RewardReceipt receipt = _shell.PayForMatch(MatchKind.VersusBot, Summary(Side.Left), Side.Left);

            Assert.That(receipt.Decision, Is.EqualTo(RewardDecision.Paid));
            Assert.That(receipt.Paid.Value, Is.EqualTo(100L));
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(100L));
            Assert.That(Reload().Profile.Wallet.Earned.Value, Is.EqualTo(100L));
        }

        [Test]
        public void LosingToTheBot_PaysNothing()
        {
            AnswerHandedness(Handedness.Right);

            RewardReceipt receipt = _shell.PayForMatch(MatchKind.VersusBot, Summary(Side.Right), Side.Left);

            Assert.That(receipt.Decision, Is.EqualTo(RewardDecision.NothingEarned));
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(0L));
        }

        [Test]
        public void HotSeatAndPractice_PayNothing_EvenForAWin()
        {
            AnswerHandedness(Handedness.Right);

            Assert.That(_shell.PayForMatch(MatchKind.HotSeat, Summary(Side.Left), Side.Left).Decision, Is.EqualTo(RewardDecision.ModeDoesNotPay));
            Assert.That(_shell.PayForMatch(MatchKind.Practice, Summary(Side.Left), Side.Left).Decision, Is.EqualTo(RewardDecision.ModeDoesNotPay));
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(0L));
        }

        [Test]
        public void TheRewardsGoThroughTheEconomySettings()
        {
            _economy.RewardPerWin = 40;
            _economy.RewardPerHit = 5;
            _shell.Dispose();
            _shell = NewShell(_view);

            RewardReceipt receipt = _shell.PayForMatch(MatchKind.VersusBot, Summary(Side.Left, leftHits: 4), Side.Left);

            Assert.That(receipt.Paid.Value, Is.EqualTo(60L), "a 40 win and 4 hits at 5 each");
        }

        // --- debug ---------------------------------------------------------------------------------

        [Test]
        public void TheGrantCheat_AddsToTheWallet_SavesIt_AndRefreshesTheShop()
        {
            OpenShop();

            _shell.GrantForDebug(300, 20);

            Assert.That(_view.Collection.Header, Is.EqualTo("BOLTS 300   GEARS 20"));
            Assert.That(Reload().Profile.Wallet.Premium.Value, Is.EqualTo(20L));
        }

        [Test]
        public void TheResetCheat_StartsOverAsAFirstLaunch()
        {
            OpenShop();
            _shell.GrantForDebug(900, 90);
            _shell.Profile.Inventory.Grant(GameplayUnlockKind.Weapon, "spear");

            _shell.ResetForDebug();

            Assert.That(_view.Screen, Is.EqualTo("handedness"));
            Assert.That(_shell.Profile.HasChosenHandedness, Is.False);
            Assert.That(_shell.Profile.Wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(_shell.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "spear"), Is.False);
            Assert.That(Reload().Status, Is.EqualTo(ProfileLoadStatus.Created), "the saved file is gone");
        }

        private sealed class FakeGateway : IPremiumPurchaseGateway
        {
            private readonly PremiumPurchaseResult _result;

            public FakeGateway(PremiumPurchaseResult result)
            {
                _result = result;
            }

            public PremiumPurchaseResult RequestPurchase() => _result;
        }

        // --- the Loadout collection -------------------------------------------------------------

        [Test]
        public void Loadout_OpensTheCollection_OnTheWeaponsTab()
        {
            AnswerHandedness(Handedness.Right);

            _view.PickOption(ShellLabels.MainLoadout);

            Assert.That(_view.Screen, Is.EqualTo("collection"));
            Assert.That(_view.Collection.Title, Is.EqualTo(ShellLabels.CollectionTitle));
            Assert.That(_view.Collection.Tabs.Count, Is.EqualTo(ShellCollection.TabCount));
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabWeapons));
        }

        [Test]
        public void ThePlayerCanFlipBetweenTheTabs_AndBack()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainLoadout);

            _view.SelectTab(ShellLabels.TabLooks);
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabLooks));

            _view.SelectTab(7); // no such tab
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabLooks));

            _view.PressBack();
            Assert.That(_view.Screen, Is.EqualTo("menu"));
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.GameTitle));
        }

        [Test]
        public void ATabPress_OutsideTheCollection_IsIgnored()
        {
            AnswerHandedness(Handedness.Right);

            _view.SelectTab(ShellLabels.TabLooks);

            Assert.That(_view.Screen, Is.EqualTo("menu"));
        }

        [Test]
        public void ALookTheyOwn_CanBePutOnAndTakenOff_AndItIsSaved()
        {
            AnswerHandedness(Handedness.Right);
            _shell.Profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            _view.PickOption(ShellLabels.MainLoadout);
            _view.SelectTab(ShellLabels.TabLooks);
            Assert.That(_view.Collection.Tabs[ShellLabels.TabLooks].Rows[0].ButtonText, Is.EqualTo(ShellLabels.Equip));

            _view.PressRow(ShellLabels.TabLooks, 0);

            Assert.That(_shell.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo(CosmeticIds.NeonSparks));
            Assert.That(_view.Collection.Tabs[ShellLabels.TabLooks].Rows[0].ButtonText, Is.EqualTo(ShellLabels.Remove));
            Assert.That(_view.Collection.SelectedTab, Is.EqualTo(ShellLabels.TabLooks), "it stays on the same tab");
            var savedOn = new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy)).Load();
            Assert.That(savedOn.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo(CosmeticIds.NeonSparks), "and it is on disk");

            _view.PressRow(ShellLabels.TabLooks, 0);

            Assert.That(_shell.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.Null);
            Assert.That(_view.Collection.Tabs[ShellLabels.TabLooks].Rows[0].ButtonText, Is.EqualTo(ShellLabels.Equip));
            var savedOff = new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy)).Load();
            Assert.That(savedOff.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.Null);
            Assert.That(savedOff.Profile.Inventory.OwnsCosmetic(CosmeticIds.NeonSparks), Is.True, "taking it off does not give it away");
        }

        [Test]
        public void ALookTheyDoNotOwn_CannotBePutOn()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainLoadout);
            _view.SelectTab(ShellLabels.TabLooks);

            _view.PressRow(ShellLabels.TabLooks, 0);
            _view.PressRow(ShellLabels.TabLooks, 99);
            _view.PressRow(ShellLabels.TabWeapons, 0);

            Assert.That(_shell.Profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.Null);
        }

        [Test]
        public void Play_OffersHotSeatVsBotPracticeAndOnline()
        {
            AnswerHandedness(Handedness.Right);

            _view.PickOption(ShellLabels.MainPlay);

            Assert.That(_view.Title, Is.EqualTo(ShellLabels.PlayTitle));
            Assert.That(_view.Options.Count, Is.EqualTo(4));
            Assert.That(_view.BackText, Is.EqualTo(ShellLabels.Back));
        }

        [Test]
        public void Online_AsksForAnOnlineMatch_AndHidesTheMenus()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);

            _view.PickOption(ShellLabels.PlayOnline);

            Assert.That(_requests, Is.EqualTo(new[] { MatchRequest.Online() }));
            Assert.That(_shell.IsShowing, Is.False);
        }

        [Test]
        public void HotSeat_AsksForAHotSeatMatch_AndHidesTheMenus()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);

            _view.PickOption(ShellLabels.PlayHotSeat);

            Assert.That(_requests, Is.EqualTo(new[] { MatchRequest.HotSeat() }));
            Assert.That(_view.Screen, Is.EqualTo("hidden"));
            Assert.That(_shell.IsShowing, Is.False);
        }

        [Test]
        public void VsBot_AsksForTheMatchThatPays()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);

            _view.PickOption(ShellLabels.PlayVersusBot);

            Assert.That(_requests.Count, Is.EqualTo(1));
            Assert.That(_requests[0].Mode, Is.EqualTo(MatchMode.HumanVsBot));
            Assert.That(_requests[0].Kind, Is.EqualTo(MatchKind.VersusBot));
            Assert.That(_requests[0].Brain, Is.Null, "the Bots asset picks the brain");
        }

        [Test]
        public void Practice_LetsYouPickTheBot_OrWatchTwo_AndNeverPays()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);
            _view.PickOption(ShellLabels.PlayPractice);
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.PracticeTitle));

            _view.PickOption(ShellLabels.PracticeRandom);

            Assert.That(_requests[0].Mode, Is.EqualTo(MatchMode.HumanVsBot));
            Assert.That(_requests[0].Kind, Is.EqualTo(MatchKind.Practice));
            Assert.That(_requests[0].Brain, Is.EqualTo(BotBrainKind.Random));
            Assert.That(_economy.Pays(MatchKind.Practice), Is.False);
        }

        [Test]
        public void Practice_HeuristicAndWatch_AskForTheirMatches()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);
            _view.PickOption(ShellLabels.PlayPractice);
            _view.PickOption(ShellLabels.PracticeHeuristic);
            Assert.That(_requests[0].Brain, Is.EqualTo(BotBrainKind.Heuristic));
            Assert.That(_requests[0].Kind, Is.EqualTo(MatchKind.Practice));

            _shell.Show();
            _view.PickOption(ShellLabels.MainPlay);
            _view.PickOption(ShellLabels.PlayPractice);
            _view.PickOption(ShellLabels.PracticeWatch);

            Assert.That(_requests[1], Is.EqualTo(MatchRequest.WatchBots()));
        }

        [Test]
        public void Back_StepsOneScreenUp()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);
            _view.PickOption(ShellLabels.PlayPractice);

            _view.PressBack();
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.PlayTitle));
            _view.PressBack();
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.GameTitle));
            _view.PressBack();
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.GameTitle), "the main menu has nowhere further back");
        }

        [Test]
        public void ShowingTheShellAgain_AfterAMatch_OpensTheMainMenu()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainPlay);
            _view.PickOption(ShellLabels.PlayVersusBot);

            _shell.Show();

            Assert.That(_view.Screen, Is.EqualTo("menu"));
            Assert.That(_view.Title, Is.EqualTo(ShellLabels.GameTitle));
        }

        // --- Settings ----------------------------------------------------------------------------

        [Test]
        public void Settings_ShowTheCurrentHandAndFourVolumes()
        {
            AnswerHandedness(Handedness.Left);

            _view.PickOption(ShellLabels.MainSettings);

            Assert.That(_view.Screen, Is.EqualTo("settings"));
            Assert.That(_view.Settings.Handedness, Is.EqualTo(Handedness.Left));
            Assert.That(_view.Settings.Volumes.Count, Is.EqualTo(4));
            Assert.That(_view.Settings.Volumes[0].Label, Is.EqualTo("Master"));
            Assert.That(_view.Settings.Volumes[0].Fraction, Is.EqualTo(AudioVolumes.AsMixed));
        }

        [Test]
        public void ChangingTheHandInSettings_IsSaved_AndStaysInSettings()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainSettings);

            _view.PickHandedness(Handedness.Left);

            Assert.That(_shell.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(_view.Screen, Is.EqualTo("settings"));
            Assert.That(_view.Settings.Handedness, Is.EqualTo(Handedness.Left));
            Assert.That(ProfileSetups.ForPlayer(_shell.Profile, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield },
                ShopCatalogue.From(_economy)).Handedness, Is.EqualTo(Handedness.Left), "the next match uses it");

            var reloaded = new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy)).Load();
            Assert.That(reloaded.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left), "and it is on disk");
        }

        [Test]
        public void AVolumePress_MovesThatVolumeATenth_AndSavesIt()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainSettings);

            _view.StepVolume(1, -1);
            _view.StepVolume(1, -1);

            Assert.That(_shell.Profile.Volumes.Music, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(_shell.Profile.Volumes.Master, Is.EqualTo(1f), "the other volumes did not move");
            Assert.That(_view.Settings.Volumes[1].Fraction, Is.EqualTo(0.8f).Within(1e-5f));

            var reloaded = new ProfileStore(_folder, new ProfileJson(), ShopCatalogue.From(_economy)).Load();
            Assert.That(reloaded.Profile.Volumes.Music, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void Volumes_StopAtSilenceAndAtTheDesignersMix()
        {
            AnswerHandedness(Handedness.Right);
            _view.PickOption(ShellLabels.MainSettings);

            _view.StepVolume(2, 1);
            Assert.That(_shell.Profile.Volumes.Effects, Is.EqualTo(1f), "already as mixed: no louder");
            for (int i = 0; i < 14; i++)
            {
                _view.StepVolume(2, -1);
            }

            Assert.That(_shell.Profile.Volumes.Effects, Is.EqualTo(0f));
        }

        [Test]
        public void AVolumeStep_OutsideSettingsOrForNoSuchVolume_IsIgnored()
        {
            AnswerHandedness(Handedness.Right);
            _view.StepVolume(0, -1); // the main menu is showing
            Assert.That(_shell.Profile.Volumes.Master, Is.EqualTo(1f));

            _view.PickOption(ShellLabels.MainSettings);
            _view.StepVolume(9, -1);
            _view.StepVolume(0, 0);

            Assert.That(_shell.Profile.Volumes.Master, Is.EqualTo(1f));
        }

        [Test]
        public void Stepped_MovesOneTenthAndNeverDrifts()
        {
            float value = 1f;
            for (int i = 0; i < 10; i++)
            {
                value = MetaShell.Stepped(value, -1);
            }

            Assert.That(value, Is.EqualTo(0f).Within(1e-6f));
            for (int i = 0; i < 10; i++)
            {
                value = MetaShell.Stepped(value, 1);
            }

            Assert.That(value, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(MetaShell.Stepped(0.37f, 1), Is.EqualTo(0.5f).Within(1e-5f), "an odd value snaps to a tenth, then steps");
        }

        [Test]
        public void ADisposedShell_HearsNoMorePresses()
        {
            AnswerHandedness(Handedness.Right);
            _shell.Dispose();

            _view.PickOption(ShellLabels.MainSettings);

            Assert.That(_view.Screen, Is.EqualTo("menu"));
        }

        /// <summary>Records what the shell shows and lets a test press.</summary>
        private sealed class FakeShellView : IShellView
        {
            public event Action<int> OptionPicked;
            public event Action BackPressed;
            public event Action<Handedness> HandednessPicked;
            public event Action<int, int> VolumeStepped;
            public event Action<int> CollectionTabSelected;
            public event Action<int, int, int> CollectionRowPressed;

            public string Screen { get; private set; } = "hidden";
            public string Title { get; private set; }
            public string Note { get; private set; }
            public IReadOnlyList<MenuOption> Options { get; private set; }
            public string BackText { get; private set; }
            public ShellSettingsModel Settings { get; private set; }
            public ShellCollectionModel Collection { get; private set; }
            public int ShownCount { get; private set; }

            public void PickOption(int index) => OptionPicked?.Invoke(index);
            public void PressBack() => BackPressed?.Invoke();
            public void PickHandedness(Handedness handedness) => HandednessPicked?.Invoke(handedness);
            public void StepVolume(int channel, int direction) => VolumeStepped?.Invoke(channel, direction);
            public void SelectTab(int tab) => CollectionTabSelected?.Invoke(tab);
            public void PressRow(int tab, int row, int button = 0) => CollectionRowPressed?.Invoke(tab, row, button);

            public void ShowCollection(ShellCollectionModel model)
            {
                Screen = "collection";
                Collection = model;
                ShownCount++;
            }

            public void ShowHandednessPick(string title, string note, string rightText, string leftText)
            {
                Screen = "handedness";
                Title = title;
                ShownCount++;
            }

            public void ShowMenu(string title, string note, IReadOnlyList<MenuOption> options, string backText)
            {
                Screen = "menu";
                Title = title;
                Note = note;
                Options = options;
                BackText = backText;
                ShownCount++;
            }

            public void ShowSettings(ShellSettingsModel model)
            {
                Screen = "settings";
                Settings = model;
                ShownCount++;
            }

            public void Hide() => Screen = "hidden";
        }
    }
}
