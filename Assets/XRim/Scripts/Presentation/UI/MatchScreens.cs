using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core;
using static XRim.Presentation.UI.HudElements;
using static XRim.Presentation.UI.UiTheme;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The full-screen moments around a match (UI Toolkit built in code, placeholder look): the hot-seat cover screen
    /// (opaque, so the next player cannot see the arena or the last plan; a tap anywhere hands over) and the match-over screen, which
    /// leaves the final pose visible behind it, and (Session 13) the instant replay's banner over a see-through screen that skips the replay
    /// when tapped, and (Session 14) the pre-match loadout picker and the reveal of both loadouts. It draws above every HUD.
    /// </summary>
    public sealed class MatchScreens : MonoBehaviour, IMatchScreensView
    {
        private const int SortingOrder = 200;
        private const float ActionWidth = 300f;
        private const float ActionHeight = 96f;
        private const float CardWidth = 540f;
        private const float CardHeight = 160f;
        private const float CardGap = 12f;
        private const float RewardPopSeconds = 0.35f;
        private const float RewardPopStartScale = 1.9f;
        private const float ConfirmWidth = 560f;
        private const float ConfirmHeight = 100f;
        private const float RevealColumnWidth = 620f;

        private static readonly Color ReplayBannerColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color ReplayTitleColor = new Color(1f, 0.25f, 0.2f, 1f);

        [SerializeField] private UIDocument _document;

        private PanelSettings _ownedPanelSettings;
        private VisualElement _root;
        private HudSafeArea _safeArea;
        private Button _cover;
        private Label _coverPlayer;
        private Label _coverInstruction;
        private Label _coverAction;
        private VisualElement _matchOver;
        private Label _overTitle;
        private Label _overReason;
        private Label _overSummary;
        private Label _overReward;
        private float _rewardShownAtSeconds = -1f;
        private Button _replay;
        private Label _replayTitle;
        private Label _replayCaption;
        private Label _replayHint;
        private VisualElement _loadout;
        private Label _loadoutTitle;
        private Label _loadoutNote;
        private VisualElement _loadoutCards;
        private readonly List<LoadoutCard> _cards = new List<LoadoutCard>();
        private Button _loadoutConfirm;
        private VisualElement _reveal;
        private Label _revealTitle;
        private Label _revealLeftName;
        private VisualElement _revealLeftWeapons;
        private Label _revealRightName;
        private VisualElement _revealRightWeapons;
        private Button _revealAction;

        public event Action CoverConfirmed;
        public event Action RematchPressed;
        public event Action MenuPressed;
        public event Action ReplaySkipPressed;
        public event Action<int> LoadoutChoiceTapped;
        public event Action LoadoutConfirmed;
        public event Action LoadoutRevealConfirmed;

        /// <summary>Makes the screens on a new GameObject of their own (see <see cref="PlanningHud.Create"/>); all hidden.</summary>
        public static MatchScreens Create(Vector2Int referenceResolution, PanelSettings panelSettingsTemplate = null)
        {
            UIDocument document = CreateDocument("MatchScreens", referenceResolution, SortingOrder, panelSettingsTemplate, out PanelSettings settings);
            var screens = document.gameObject.AddComponent<MatchScreens>();
            screens._document = document;
            screens._ownedPanelSettings = settings;
            screens._safeArea = new HudSafeArea(referenceResolution);
            document.gameObject.SetActive(true);
            return screens;
        }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError("[XRim] MatchScreens has no UI document to build in.", this);
                return;
            }

            Build(_document.rootVisualElement);
        }

        private void Update()
        {
            if (_root != null && _safeArea.Changed(out ScreenInsets insets)) PadScreens(insets);
            AnimateReward();
        }

        /// <summary>
        /// The reward line slams in: it starts big and settles to its size, so winning something is a small moment of its own. Real time, so
        /// slow motion and hitstop never touch it; presentation only.
        /// </summary>
        private void AnimateReward()
        {
            if (_overReward == null || _rewardShownAtSeconds < 0f) return;

            float progress = Mathf.Clamp01((Time.unscaledTime - _rewardShownAtSeconds) / RewardPopSeconds);
            float eased = 1f - (1f - progress) * (1f - progress);
            float scale = Mathf.Lerp(RewardPopStartScale, 1f, eased);
            _overReward.style.scale = new Scale(new Vector3(scale, scale, 1f));
            if (progress >= 1f) _rewardShownAtSeconds = -1f;
        }

        private void OnDestroy()
        {
            if (_ownedPanelSettings != null) Destroy(_ownedPanelSettings);
        }

        // ----- IMatchScreensView -----

        public void ShowCover(string player, string instruction, string action)
        {
            if (_cover == null) return;

            SetText(_coverPlayer, player);
            SetText(_coverInstruction, instruction);
            SetText(_coverAction, action);
            Show(_cover);
        }

        public void ShowMatchOver(string title, string reason, string summary, string reward)
        {
            if (_matchOver == null) return;

            SetText(_overTitle, title);
            SetText(_overReason, reason);
            SetText(_overSummary, summary);
            bool paid = !string.IsNullOrEmpty(reward);
            SetText(_overReward, reward);
            _overReward.style.display = paid ? DisplayStyle.Flex : DisplayStyle.None;
            _rewardShownAtSeconds = paid ? Time.unscaledTime : -1f;
            if (paid) AnimateReward();
            Show(_matchOver);
        }

        public void ShowReplay(string title, string caption, string hint)
        {
            if (_replay == null) return;

            SetText(_replayTitle, title);
            SetText(_replayCaption, caption);
            SetText(_replayHint, hint);
            Show(_replay);
        }

        public void ShowLoadoutPicker(string title, string note, IReadOnlyList<LoadoutChoice> choices, string confirmText, bool canConfirm)
        {
            if (_loadout == null) return;

            SetText(_loadoutTitle, title);
            SetText(_loadoutNote, note);
            if (_cards.Count != choices.Count) RebuildCards(choices.Count);
            for (int i = 0; i < choices.Count; i++)
            {
                LoadoutCard card = _cards[i];
                bool picked = choices[i].PickedOrder > 0;
                SetText(card.Title, picked ? choices[i].PickedOrder + ".  " + choices[i].Title : choices[i].Title);
                SetText(card.Detail, choices[i].Detail);
                card.Button.style.backgroundColor = picked ? ChosenColor : ButtonColor;
                SetBorderColor(card.Button, picked ? HighlightColor : EdgeColor);
            }

            _loadoutConfirm.text = confirmText;
            _loadoutConfirm.SetEnabled(canConfirm);
            _loadoutConfirm.style.backgroundColor = canConfirm ? ConfirmColor : ButtonColor;
            Show(_loadout);
        }

        public void ShowLoadoutReveal(string title, string leftName, IReadOnlyList<string> leftWeapons, string rightName,
            IReadOnlyList<string> rightWeapons, string action)
        {
            if (_reveal == null) return;

            SetText(_revealTitle, title);
            SetText(_revealLeftName, leftName);
            SetText(_revealRightName, rightName);
            FillWeaponList(_revealLeftWeapons, leftWeapons);
            FillWeaponList(_revealRightWeapons, rightWeapons);
            _revealAction.text = action;
            Show(_reveal);
        }

        public void Hide() => Show(null);

        // ----- building -----

        private void Build(VisualElement root)
        {
            PrepareRoot(root);
            _root = root;
            BuildCover();
            BuildMatchOver();
            BuildReplay();
            BuildLoadout();
            BuildReveal();
            Hide();
            if (_safeArea.Changed(out ScreenInsets insets)) PadScreens(insets);
        }

        /// <summary>Each screen's background still reaches every edge (the cover must hide everything); its content stays inside the safe area.</summary>
        private void PadScreens(ScreenInsets insets)
        {
            foreach (VisualElement screen in new VisualElement[] { _cover, _matchOver, _replay, _loadout, _reveal })
            {
                if (screen != null) PadBy(screen, insets);
            }
        }

        private void BuildCover()
        {
            _cover = new Button(() => CoverConfirmed?.Invoke()) { text = string.Empty };
            _root.Add(_cover);
            FullScreen(_cover, ScreenColor);
            _cover.style.borderTopWidth = 0;
            _cover.style.borderBottomWidth = 0;
            _cover.style.borderLeftWidth = 0;
            _cover.style.borderRightWidth = 0;
            _cover.style.marginLeft = 0;
            _cover.style.marginRight = 0;
            _cover.style.marginTop = 0;
            _cover.style.marginBottom = 0;
            _coverPlayer = Text(_cover, string.Empty, 80, HighlightColor);
            _coverPlayer.style.unityFontStyleAndWeight = FontStyle.Bold;
            _coverInstruction = Text(_cover, string.Empty, 30, TextColor);
            _coverInstruction.style.marginTop = Gap;
            _coverAction = Text(_cover, string.Empty, 32, GoodColor);
            _coverAction.style.marginTop = Gap * 3f;
            _coverAction.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void BuildMatchOver()
        {
            _matchOver = FullScreen(Plain(_root), DimmedScreenColor);
            _overTitle = Text(_matchOver, string.Empty, 84, HighlightColor);
            _overTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overReason = Text(_matchOver, string.Empty, 32, TextColor);
            _overReason.style.marginTop = Gap;
            _overSummary = Text(_matchOver, string.Empty, 24, DimTextColor);
            _overSummary.style.marginTop = Gap * 0.5f;
            _overReward = Text(_matchOver, string.Empty, 60, HighlightColor);
            _overReward.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overReward.style.marginTop = Gap * 1.5f;

            VisualElement actions = Plain(_matchOver);
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = Gap * 3f;
            Button rematch = AddAction(actions, "REMATCH", () => RematchPressed?.Invoke());
            rematch.style.backgroundColor = ConfirmColor;
            AddAction(actions, "MENU", () => MenuPressed?.Invoke());
        }

        /// <summary>A see-through screen (the replay shows through it) with a banner across the top; a tap anywhere skips.</summary>
        private void BuildReplay()
        {
            _replay = new Button(() => ReplaySkipPressed?.Invoke()) { text = string.Empty };
            _root.Add(_replay);
            FullScreen(_replay, Color.clear);
            _replay.style.justifyContent = Justify.FlexStart;
            _replay.style.borderTopWidth = 0;
            _replay.style.borderBottomWidth = 0;
            _replay.style.borderLeftWidth = 0;
            _replay.style.borderRightWidth = 0;
            _replay.style.marginLeft = 0;
            _replay.style.marginRight = 0;
            _replay.style.marginTop = 0;
            _replay.style.marginBottom = 0;

            VisualElement banner = Plain(_replay);
            banner.style.alignSelf = Align.Stretch;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = ReplayBannerColor;
            banner.style.paddingTop = Gap;
            banner.style.paddingBottom = Gap;
            banner.pickingMode = PickingMode.Ignore;
            _replayTitle = Text(banner, string.Empty, 44, ReplayTitleColor);
            _replayTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _replayCaption = Text(banner, string.Empty, 34, TextColor);
            _replayCaption.style.unityFontStyleAndWeight = FontStyle.Bold;
            _replayHint = Text(banner, string.Empty, 20, DimTextColor);
        }

        /// <summary>The loadout picker: a title, a note, the weapon cards in rows, and the lock-in button.</summary>
        private void BuildLoadout()
        {
            _loadout = FullScreen(Plain(_root), ScreenColor);
            _loadoutTitle = Text(_loadout, string.Empty, 64, HighlightColor);
            _loadoutTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _loadoutNote = Text(_loadout, string.Empty, 22, DimTextColor);
            _loadoutNote.style.marginTop = Gap * 0.5f;
            _loadoutNote.style.marginBottom = Gap;

            _loadoutCards = Plain(_loadout);
            _loadoutCards.style.flexDirection = FlexDirection.Row;
            _loadoutCards.style.flexWrap = Wrap.Wrap;
            _loadoutCards.style.justifyContent = Justify.Center;
            _loadoutCards.style.width = CardWidth * 3f + CardGap * 6f;

            _loadoutConfirm = new Button(() => LoadoutConfirmed?.Invoke()) { text = string.Empty };
            StyleButton(_loadoutConfirm, ConfirmWidth, ConfirmHeight, 36);
            _loadoutConfirm.style.marginTop = Gap * 2f;
            _loadout.Add(_loadoutConfirm);
        }

        /// <summary>The reveal: both loadouts side by side with a tap to start.</summary>
        private void BuildReveal()
        {
            _reveal = FullScreen(Plain(_root), ScreenColor);
            _revealTitle = Text(_reveal, string.Empty, 72, HighlightColor);
            _revealTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

            VisualElement row = Plain(_reveal);
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = Gap * 2f;
            row.style.alignItems = Align.FlexStart;
            BuildRevealColumn(row, out _revealLeftName, out _revealLeftWeapons);
            Label versus = Text(row, "VS", 56, DimTextColor);
            versus.style.marginLeft = Gap * 2f;
            versus.style.marginRight = Gap * 2f;
            versus.style.marginTop = Gap;
            BuildRevealColumn(row, out _revealRightName, out _revealRightWeapons);

            _revealAction = new Button(() => LoadoutRevealConfirmed?.Invoke()) { text = string.Empty };
            StyleButton(_revealAction, ConfirmWidth, ConfirmHeight, 36);
            _revealAction.style.backgroundColor = ConfirmColor;
            _revealAction.style.marginTop = Gap * 3f;
            _reveal.Add(_revealAction);
        }

        private static void BuildRevealColumn(VisualElement row, out Label name, out VisualElement weapons)
        {
            VisualElement column = Plain(row);
            column.style.width = RevealColumnWidth;
            column.style.alignItems = Align.Center;
            name = Text(column, string.Empty, 48, TextColor);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            weapons = Plain(column);
            weapons.style.alignItems = Align.Center;
            weapons.style.marginTop = Gap;
        }

        private static void FillWeaponList(VisualElement container, IReadOnlyList<string> weapons)
        {
            container.Clear();
            foreach (string weapon in weapons)
            {
                Label label = Text(container, weapon, 40, TextColor);
                label.style.marginTop = Gap * 0.5f;
            }
        }

        private void RebuildCards(int count)
        {
            _loadoutCards.Clear();
            _cards.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = new Button(() => LoadoutChoiceTapped?.Invoke(index)) { text = string.Empty };
                StyleButton(button, CardWidth, CardHeight, 30);
                button.style.marginLeft = CardGap;
                button.style.marginRight = CardGap;
                button.style.marginTop = CardGap;
                button.style.marginBottom = CardGap;
                button.style.flexDirection = FlexDirection.Column;
                button.style.justifyContent = Justify.Center;
                button.style.alignItems = Align.Center;
                Label title = Text(button, string.Empty, 36, TextColor);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                Label detail = Text(button, string.Empty, 20, DimTextColor);
                detail.style.unityTextAlign = TextAnchor.MiddleCenter;
                _loadoutCards.Add(button);
                _cards.Add(new LoadoutCard(button, title, detail));
            }
        }

        private static void SetBorderColor(Button button, Color color)
        {
            button.style.borderTopColor = color;
            button.style.borderBottomColor = color;
            button.style.borderLeftColor = color;
            button.style.borderRightColor = color;
        }

        private readonly struct LoadoutCard
        {
            public readonly Button Button;
            public readonly Label Title;
            public readonly Label Detail;

            public LoadoutCard(Button button, Label title, Label detail)
            {
                Button = button;
                Title = title;
                Detail = detail;
            }
        }

        private static Button AddAction(VisualElement parent, string text, Action pressed)
        {
            var button = new Button(pressed) { text = text };
            StyleButton(button, ActionWidth, ActionHeight, 30);
            button.style.marginLeft = Gap;
            button.style.marginRight = Gap;
            parent.Add(button);
            return button;
        }

        private void Show(VisualElement screen)
        {
            foreach (VisualElement candidate in new[] { _cover, _matchOver, _replay, _loadout, _reveal })
            {
                if (candidate != null) candidate.style.display = candidate == screen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
