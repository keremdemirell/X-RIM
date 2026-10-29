using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Bots;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Profile;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>
    /// The game outside a match (Session 18): owns the player's profile and walks the screens. A new profile first asks which hand holds
    /// the weapon (GDD §4, Decided: asked once, applies to the whole account); then the main menu offers Play (hot-seat, against a bot,
    /// practice), Loadout (what they own), Shop (buy weapons, moves and looks with what they earned) and Settings, where the hand and the
    /// volumes can be changed later (D24). Every change is saved at once. A finished match is paid here (<see cref="PayForMatch"/>).
    /// The shell never starts a match itself: it raises <see cref="PlayRequested"/> and <see cref="MatchBootstrap"/> does. No rule lives
    /// here and nothing here can change an outcome.
    /// </summary>
    [GddTbd("§4", "Can handedness be changed later?", Proposal = "Designer 2026-10-03 (D24): changeable in Settings; no separate mirror toggle")]
    public sealed class MetaShell : IDisposable
    {
        /// <summary>One press of a volume's minus or plus moves it this far (a tenth of the designer's mix).</summary>
        public const float VolumeStepFraction = 0.1f;

        private const int VolumeMaster = 0;
        private const int VolumeMusic = 1;
        private const int VolumeEffects = 2;
        private const int VolumeVoices = 3;

        private readonly IShellView _view;
        private readonly ProfileStore _store;
        private readonly EconomySettings _economy;
        private readonly Func<RulesSettings> _rules;
        private readonly IPremiumPurchaseGateway _gateway;
        private readonly RewardSettlement _rewards;
        private ShellScreen _screen = ShellScreen.None;
        private int _collectionTab;
        private string _status = string.Empty;
        private List<Action> _menuActions = new List<Action>();

        /// <summary>The player asked to play this match; the shell has hidden its screens.</summary>
        public event Action<MatchRequest> PlayRequested;

        /// <param name="rules">The current rules snapshot, for the weapons' and moves' names and numbers on the Loadout screen.</param>
        /// <param name="gateway">The way real money becomes premium currency; null: none yet (the store arrives with Session 22).</param>
        public MetaShell(IShellView view, ProfileStore store, EconomySettings economy, Func<RulesSettings> rules,
            IPremiumPurchaseGateway gateway = null)
        {
            _view = Guard.NotNull(view, nameof(view));
            _store = Guard.NotNull(store, nameof(store));
            _economy = Guard.NotNull(economy, nameof(economy));
            _rules = Guard.NotNull(rules, nameof(rules));
            _gateway = gateway ?? new StorePurchasesNotAvailable();
            _rewards = RewardSettlement.CreateDefault(economy);
            Catalogue = ShopCatalogue.From(economy);

            LoadResult = _store.Load();
            Profile = LoadResult.Profile;

            _view.OptionPicked += OnOptionPicked;
            _view.BackPressed += OnBackPressed;
            _view.HandednessPicked += OnHandednessPicked;
            _view.VolumeStepped += OnVolumeStepped;
            _view.CollectionTabSelected += OnCollectionTabSelected;
            _view.CollectionRowPressed += OnCollectionRowPressed;
        }

        public PlayerProfile Profile { get; private set; }

        public EconomySettings Economy => _economy;

        /// <summary>What the shop sells and what a new player starts with, from the economy settings.</summary>
        public ShopCatalogue Catalogue { get; }

        /// <summary>How the profile was found: a first launch, loaded, upgraded, recovered or not saveable (see its message).</summary>
        public ProfileLoadResult LoadResult { get; }

        public bool IsShowing => _screen != ShellScreen.None;

        public void Dispose()
        {
            _view.OptionPicked -= OnOptionPicked;
            _view.BackPressed -= OnBackPressed;
            _view.HandednessPicked -= OnHandednessPicked;
            _view.VolumeStepped -= OnVolumeStepped;
            _view.CollectionTabSelected -= OnCollectionTabSelected;
            _view.CollectionRowPressed -= OnCollectionRowPressed;
        }

        /// <summary>The way into the game: the handedness question on a profile that has not answered it, otherwise the main menu.</summary>
        public void Show()
        {
            if (Profile.HasChosenHandedness) ShowMain();
            else ShowHandednessQuestion();
        }

        public void Hide()
        {
            _screen = ShellScreen.None;
            _menuActions = new List<Action>();
            _view.Hide();
        }

        /// <summary>Saves the profile (after a purchase, a reward, a setting). A failed save is logged and the game carries on.</summary>
        public void Save()
        {
            if (!_store.Save(Profile)) Debug.LogWarning("[XRim] " + _store.LastSaveProblem);
        }

        // ----- screens -----

        private void ShowHandednessQuestion()
        {
            _screen = ShellScreen.HandednessQuestion;
            _view.ShowHandednessPick(ShellLabels.HandednessQuestion, ShellLabels.HandednessAnswerNote, ShellLabels.RightHanded, ShellLabels.LeftHanded);
        }

        private void ShowMain()
        {
            _screen = ShellScreen.Main;
            _menuActions = new List<Action>
            {
                () => ShowPlay(),
                () => ShowCollection(ShellLabels.TabWeapons),
                () => ShowShop(ShellLabels.TabWeapons, string.Empty),
                () => ShowSettings(),
            };
            _view.ShowMenu(ShellLabels.GameTitle, ShellLabels.MainNote + " · " + ShellLabels.WalletLine(_economy, Profile.Wallet), ShellLabels.MainMenu(),
                string.Empty);
        }

        private void ShowPlay()
        {
            _screen = ShellScreen.Play;
            _menuActions = new List<Action>
            {
                () => Request(MatchRequest.HotSeat()),
                () => Request(MatchRequest.VersusBot()),
                () => ShowPractice(),
                () => Request(MatchRequest.Online()),
            };
            _view.ShowMenu(ShellLabels.PlayTitle, ShellLabels.PlayNote, ShellLabels.PlayMenu(_economy), ShellLabels.Back);
        }

        private void ShowPractice()
        {
            _screen = ShellScreen.Practice;
            _menuActions = new List<Action>
            {
                () => Request(MatchRequest.Practice(BotBrainKind.Heuristic)),
                () => Request(MatchRequest.Practice(BotBrainKind.Random)),
                () => Request(MatchRequest.WatchBots()),
            };
            _view.ShowMenu(ShellLabels.PracticeTitle, ShellLabels.PracticeNote, ShellLabels.PracticeMenu(), ShellLabels.Back);
        }

        private void ShowCollection(int tab)
        {
            _screen = ShellScreen.Collection;
            _collectionTab = tab;
            _view.ShowCollection(ShellCollection.Build(_rules(), Catalogue, Profile, _economy, tab));
        }

        private void ShowShop(int tab, string status)
        {
            _screen = ShellScreen.Shop;
            _collectionTab = tab;
            _status = status;
            _view.ShowCollection(ShellShop.Build(_rules(), Catalogue, Profile, _economy, tab, status));
        }

        private void ShowSettings()
        {
            _screen = ShellScreen.Settings;
            AudioVolumes volumes = Profile.Volumes;
            var sliders = new[]
            {
                new ShellVolume(ShellLabels.VolumeNames[VolumeMaster], volumes.Master),
                new ShellVolume(ShellLabels.VolumeNames[VolumeMusic], volumes.Music),
                new ShellVolume(ShellLabels.VolumeNames[VolumeEffects], volumes.Effects),
                new ShellVolume(ShellLabels.VolumeNames[VolumeVoices], volumes.Voices),
            };
            _view.ShowSettings(new ShellSettingsModel(ShellLabels.SettingsTitle, ShellLabels.HandednessTitle, ShellLabels.HandednessNote,
                ShellLabels.RightHanded, ShellLabels.LeftHanded, ProfileSetups.HandednessOf(Profile), ShellLabels.VolumesTitle, sliders,
                ShellLabels.Back));
        }

        private void Request(MatchRequest request)
        {
            Hide();
            PlayRequested?.Invoke(request);
        }

        // ----- presses -----

        private void OnOptionPicked(int index)
        {
            if (index < 0 || index >= _menuActions.Count) return;
            _menuActions[index]();
        }

        private void OnBackPressed()
        {
            switch (_screen)
            {
                case ShellScreen.Play:
                case ShellScreen.Settings:
                case ShellScreen.Collection:
                case ShellScreen.Shop:
                    ShowMain();
                    break;
                case ShellScreen.Practice:
                    ShowPlay();
                    break;
            }
        }

        private void OnHandednessPicked(Handedness handedness)
        {
            if (_screen != ShellScreen.HandednessQuestion && _screen != ShellScreen.Settings) return;

            Profile.ChooseHandedness(handedness);
            Save();
            if (_screen == ShellScreen.Settings) ShowSettings();
            else ShowMain();
        }

        private void OnVolumeStepped(int channel, int direction)
        {
            if (_screen != ShellScreen.Settings || direction == 0) return;

            AudioVolumes volumes = Profile.Volumes;
            switch (channel)
            {
                case VolumeMaster:
                    volumes.Master = Stepped(volumes.Master, direction);
                    break;
                case VolumeMusic:
                    volumes.Music = Stepped(volumes.Music, direction);
                    break;
                case VolumeEffects:
                    volumes.Effects = Stepped(volumes.Effects, direction);
                    break;
                case VolumeVoices:
                    volumes.Voices = Stepped(volumes.Voices, direction);
                    break;
                default:
                    return;
            }

            Save();
            ShowSettings();
        }

        private void OnCollectionTabSelected(int tab)
        {
            if (tab < 0 || tab >= ShellCollection.TabCount) return;
            if (_screen == ShellScreen.Collection) ShowCollection(tab);
            else if (_screen == ShellScreen.Shop) ShowShop(tab, string.Empty);
        }

        private void OnCollectionRowPressed(int tab, int row, int button)
        {
            if (_screen == ShellScreen.Shop) Buy(tab, row, button);
            else if (_screen == ShellScreen.Collection && button == 0) ToggleLook(tab, row);
        }

        /// <summary>A press on a look's button in the Loadout screen equips it, or takes it off when it is already on (only an owned look has one).</summary>
        private void ToggleLook(int tab, int row)
        {
            if (tab != ShellLabels.TabLooks) return;

            CosmeticItem look = ShellCollection.LookAt(Catalogue, row);
            if (look == null || !Profile.Inventory.OwnsCosmetic(look.Id)) return;

            if (Profile.Inventory.EquippedCosmetic(look.Kind) == look.Id) Profile.Inventory.Unequip(look.Kind);
            else Profile.Inventory.Equip(look.Kind, look.Id);
            Save();
            ShowCollection(_collectionTab);
        }

        /// <summary>
        /// A press on a buy button in the Shop (GDD §16). A weapon, shield or move has one button and costs earned currency; a look's first
        /// button pays with earned currency, its second with premium; the last row of the Looks tab is the real-money stub. A purchase is saved
        /// at once.
        /// </summary>
        private void Buy(int tab, int row, int button)
        {
            var shop = new Shop(Profile.Wallet, Profile.Inventory);
            PurchaseResult result;
            string name;
            CurrencyKind paidWith = CurrencyKind.Earned;
            if (tab == ShellLabels.TabLooks)
            {
                if (row == ShellShop.GearsRow(Catalogue))
                {
                    GetPremiumCurrency(tab);
                    return;
                }

                CosmeticItem look = ShellCollection.LookAt(Catalogue, row);
                if (look == null) return;
                name = look.DisplayName;
                paidWith = button == 0 ? CurrencyKind.Earned : CurrencyKind.Premium;
                result = shop.Buy(look, paidWith);
            }
            else
            {
                GameplayUnlock unlock = ShellShop.UnlockAt(_rules(), Catalogue, tab, row, out name);
                if (unlock == null) return;
                result = shop.Buy(unlock);
            }

            if (result == PurchaseResult.Bought) Save();
            ShowShop(tab, ShellLabels.PurchaseStatus(result, name, paidWith, _economy));
        }

        /// <summary>
        /// The real-money way to premium currency. Only the gateway can add it, and only as premium currency: a completed purchase never
        /// touches the earned balance (GDD §16). Until the store arrives the gateway sells nothing and says so.
        /// </summary>
        private void GetPremiumCurrency(int tab)
        {
            PremiumPurchaseResult purchase = _gateway.RequestPurchase();
            if (purchase.Outcome == PremiumPurchaseOutcome.Completed)
            {
                Profile.Wallet.Add(purchase.Credited);
                Save();
            }

            ShowShop(tab, purchase.Message);
        }

        // ----- after a match, and for the debug tools -----

        /// <summary>
        /// Pays a finished match into the wallet and saves it (GDD §16): whether this kind of match pays, what the reward policy asks, what
        /// the farming guard lets through. Only a match summary goes in: the rules are never read.
        /// </summary>
        public RewardReceipt PayForMatch(MatchKind kind, MatchSummary summary, Side side)
        {
            RewardReceipt receipt = _rewards.Settle(Profile.Wallet, summary, side, kind, false);
            if (receipt.Decision == RewardDecision.Paid) Save();
            return receipt;
        }

        /// <summary>Debug only (the Cheats tab): adds currency to the wallet and shows the new balance. Never called by the game itself.</summary>
        public void GrantForDebug(long earned, long premium)
        {
            if (earned > 0) Profile.Wallet.Add(new EarnedAmount(earned));
            if (premium > 0) Profile.Wallet.Add(new PremiumAmount(premium));
            Save();
            Refresh();
        }

        /// <summary>Debug only (the Cheats tab): erases the saved profile and starts over as a first launch, on this device too.</summary>
        public void ResetForDebug()
        {
            _store.Delete();
            Profile = PlayerProfile.CreateNew(Catalogue);
            Show();
        }

        /// <summary>Shows the current screen again with what is true now (a balance that changed).</summary>
        private void Refresh()
        {
            switch (_screen)
            {
                case ShellScreen.Main:
                    ShowMain();
                    break;
                case ShellScreen.Shop:
                    ShowShop(_collectionTab, _status);
                    break;
                case ShellScreen.Collection:
                    ShowCollection(_collectionTab);
                    break;
            }
        }

        /// <summary>One step up or down, kept on a whole tenth so repeated presses never drift.</summary>
        public static float Stepped(float fraction, int direction)
        {
            float tenths = (float)Math.Round(fraction / VolumeStepFraction) + Math.Sign(direction);
            return XMath.Clamp01(tenths * VolumeStepFraction);
        }

        private enum ShellScreen
        {
            None,
            HandednessQuestion,
            Main,
            Play,
            Practice,
            Settings,
            Collection,
            Shop,
        }
    }
}
