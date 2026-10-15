using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core;
using static XRim.Presentation.UI.HudElements;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The full-screen moments around a match (UI Toolkit built in code, placeholder look): the mode picker, the hot-seat cover screen
    /// (opaque, so the next player cannot see the arena or the last plan; a tap anywhere hands over) and the match-over screen, which
    /// leaves the final pose visible behind it, and (Session 13) the instant replay's banner over a see-through screen that skips the replay
    /// when tapped. It draws above every HUD.
    /// </summary>
    public sealed class MatchScreens : MonoBehaviour, IMatchScreensView
    {
        private const int SortingOrder = 200;
        private const float OptionWidth = 760f;
        private const float OptionHeight = 112f;
        private const float ActionWidth = 300f;
        private const float ActionHeight = 96f;
        private const float Gap = 18f;

        private static readonly Color OpaqueColor = new Color(0.07f, 0.08f, 0.1f, 1f);
        private static readonly Color DimmedColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color RematchColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        private static readonly Color ReplayBannerColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color ReplayTitleColor = new Color(1f, 0.25f, 0.2f, 1f);

        [SerializeField] private UIDocument _document;

        private PanelSettings _ownedPanelSettings;
        private VisualElement _root;
        private HudSafeArea _safeArea;
        private VisualElement _picker;
        private Label _pickerTitle;
        private Label _pickerNote;
        private VisualElement _pickerOptions;
        private Button _cover;
        private Label _coverPlayer;
        private Label _coverInstruction;
        private Label _coverAction;
        private VisualElement _matchOver;
        private Label _overTitle;
        private Label _overReason;
        private Label _overSummary;
        private Button _replay;
        private Label _replayTitle;
        private Label _replayCaption;
        private Label _replayHint;

        public event Action<int> ModePicked;
        public event Action CoverConfirmed;
        public event Action RematchPressed;
        public event Action ChangeModePressed;
        public event Action ReplaySkipPressed;

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
        }

        private void OnDestroy()
        {
            if (_ownedPanelSettings != null) Destroy(_ownedPanelSettings);
        }

        // ----- IMatchScreensView -----

        public void ShowModePicker(string title, string note, IReadOnlyList<MenuOption> modes)
        {
            if (_picker == null) return;

            SetText(_pickerTitle, title);
            SetText(_pickerNote, note);
            _pickerOptions.Clear();
            for (int i = 0; i < modes.Count; i++)
            {
                int index = i;
                var button = new Button(() => ModePicked?.Invoke(index)) { text = string.Empty };
                StyleButton(button, OptionWidth, OptionHeight, 34);
                button.style.marginTop = Gap;
                button.style.flexDirection = FlexDirection.Column;
                button.style.justifyContent = Justify.Center;
                button.style.alignItems = Align.Center;
                Label optionTitle = Text(button, modes[i].Title, 36, TextColor);
                optionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                Text(button, modes[i].Detail, 22, DimTextColor);
                _pickerOptions.Add(button);
            }

            Show(_picker);
        }

        public void ShowCover(string player, string instruction, string action)
        {
            if (_cover == null) return;

            SetText(_coverPlayer, player);
            SetText(_coverInstruction, instruction);
            SetText(_coverAction, action);
            Show(_cover);
        }

        public void ShowMatchOver(string title, string reason, string summary)
        {
            if (_matchOver == null) return;

            SetText(_overTitle, title);
            SetText(_overReason, reason);
            SetText(_overSummary, summary);
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

        public void Hide() => Show(null);

        // ----- building -----

        private void Build(VisualElement root)
        {
            PrepareRoot(root);
            _root = root;
            BuildPicker();
            BuildCover();
            BuildMatchOver();
            BuildReplay();
            Hide();
            if (_safeArea.Changed(out ScreenInsets insets)) PadScreens(insets);
        }

        /// <summary>Each screen's background still reaches every edge (the cover must hide everything); its content stays inside the safe area.</summary>
        private void PadScreens(ScreenInsets insets)
        {
            foreach (VisualElement screen in new VisualElement[] { _picker, _cover, _matchOver, _replay })
            {
                if (screen != null) PadBy(screen, insets);
            }
        }

        private void BuildPicker()
        {
            _picker = FullScreen(Plain(_root), OpaqueColor);
            _pickerTitle = Text(_picker, string.Empty, 96, HighlightColor);
            _pickerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pickerNote = Text(_picker, string.Empty, 22, DimTextColor);
            _pickerNote.style.marginBottom = Gap;
            _pickerOptions = Plain(_picker);
            _pickerOptions.style.alignItems = Align.Center;
        }

        private void BuildCover()
        {
            _cover = new Button(() => CoverConfirmed?.Invoke()) { text = string.Empty };
            _root.Add(_cover);
            FullScreen(_cover, OpaqueColor);
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
            _matchOver = FullScreen(Plain(_root), DimmedColor);
            _overTitle = Text(_matchOver, string.Empty, 84, HighlightColor);
            _overTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overReason = Text(_matchOver, string.Empty, 32, TextColor);
            _overReason.style.marginTop = Gap;
            _overSummary = Text(_matchOver, string.Empty, 24, DimTextColor);
            _overSummary.style.marginTop = Gap * 0.5f;

            VisualElement actions = Plain(_matchOver);
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = Gap * 3f;
            Button rematch = AddAction(actions, "REMATCH", () => RematchPressed?.Invoke());
            rematch.style.backgroundColor = RematchColor;
            AddAction(actions, "CHANGE MODE", () => ChangeModePressed?.Invoke());
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

        private static Button AddAction(VisualElement parent, string text, Action pressed)
        {
            var button = new Button(pressed) { text = text };
            StyleButton(button, ActionWidth, ActionHeight, 30);
            button.style.marginLeft = Gap;
            button.style.marginRight = Gap;
            parent.Add(button);
            return button;
        }

        /// <summary>Covers the whole screen and centres its content in a column.</summary>
        private static VisualElement FullScreen(VisualElement element, Color background)
        {
            Absolute(element);
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            element.style.backgroundColor = background;
            element.style.flexDirection = FlexDirection.Column;
            element.style.justifyContent = Justify.Center;
            element.style.alignItems = Align.Center;
            element.pickingMode = PickingMode.Position; // a screen in front swallows touches meant for what is behind it
            return element;
        }

        private void Show(VisualElement screen)
        {
            foreach (VisualElement candidate in new[] { _picker, _cover, _matchOver, _replay })
            {
                if (candidate != null) candidate.style.display = candidate == screen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
