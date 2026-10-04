using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core.Gdd;
using XRim.Rules;
using XRim.Rules.Planning;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// Planning-phase HUD (UI Toolkit, built in code so no UXML, stylesheet or panel asset is needed): the weapon selector, the
    /// planning timer, the opponent's public state (weapon and Ready), the remaining ink, the body move set so far, the Ready button,
    /// a short note for refusals, and an empty slot for the signature buttons (Session 15). It only shows what a presenter tells it
    /// and reports presses; the rules decide what a press does.
    /// Placement (D23, flagged): the weapon selector runs along the top of the weapon zone from the body zone's edge, the timer is at
    /// the top centre, the opponent's state in the far top corner, Ready in the far bottom corner. The own edge of the screen is the
    /// left one unless the layout is mirrored (GDD §4); everything follows it. Sizes are in a 1920×1080 reference screen that scales
    /// with the screen height, like the arena view.
    /// </summary>
    [GddTbd("§4", "Placement of weapon selector, signature buttons and Ready",
        Proposal = "Selector along the top of the weapon zone, Ready in the far bottom corner, timer top centre (D23)")]
    [GddTbd("§6", "Remaining-ink UI", Proposal = "Show ink while drawing (D7)")]
    public sealed class PlanningHud : MonoBehaviour, IPlanningHudView
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const float Margin = 16f;
        private const float TopMargin = 12f;
        private const float ButtonWidth = 170f;
        private const float ButtonHeight = 70f;
        private const float InkTrackWidth = 360f;
        private const float InkTrackHeight = 18f;
        private const float RowGap = 10f;
        private const long MessageSeconds = 2500;
        private const float DisabledOpacity = 0.4f;
        private const float WholeSecondsAbove = 10f;
        private const int SortingOrder = 100;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TextColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color DimTextColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color SelectedTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        private static readonly Color EdgeColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color ReadyColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        private static readonly Color CancelColor = new Color(0.9f, 0.55f, 0.1f, 1f);
        private static readonly Color LockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color LockoutColor = new Color(1f, 0.45f, 0.1f, 1f);
        private static readonly Color InkFullColor = new Color(0.25f, 0.8f, 0.3f);
        private static readonly Color InkEmptyColor = new Color(0.85f, 0.2f, 0.15f);
        private static readonly Color BodyStripColor = new Color(1f, 1f, 1f, 0.06f);

        [SerializeField] private UIDocument _document;

        private readonly List<WeaponButton> _weaponButtons = new List<WeaponButton>();
        private PanelSettings _ownedPanelSettings;
        private VisualElement _root;
        private VisualElement _bodyStrip;
        private VisualElement _selectorColumn;
        private VisualElement _selectorRow;
        private VisualElement _opponentBox;
        private VisualElement _bottomCorner;
        private VisualElement _inkFill;
        private Label _moveLabel;
        private Label _timerLabel;
        private Label _inkLabel;
        private Label _statusLabel;
        private Label _opponentLabel;
        private Label _messageLabel;
        private Button _readyButton;
        private IVisualElementScheduledItem _hideMessage;
        private WeaponId _selected;
        private bool _switchLocked;
        private bool _mirrored;
        private float _bodyZoneWidthFraction = 0.15f;
        private bool _visible = true;

        public event Action<WeaponId> WeaponChosen;
        public event Action ReadyPressed;

        /// <summary>Where the signature move buttons will go (Session 15): an empty row beside the Ready button.</summary>
        public VisualElement SignatureSlot { get; private set; }

        /// <summary>
        /// Makes a HUD on a new GameObject of its own, with the panel settings it needs created in code (scales with the screen
        /// height from <paramref name="referenceResolution"/>). The object starts inactive so the document is ready when the HUD builds.
        /// </summary>
        public static PlanningHud Create(Vector2Int referenceResolution)
        {
            var gameObject = new GameObject("PlanningHud");
            gameObject.SetActive(false);
            var document = gameObject.AddComponent<UIDocument>();
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = referenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f; // by height, so a wider screen shows more but nothing grows (like the arena view)
            settings.sortingOrder = SortingOrder;
            document.panelSettings = settings;

            var hud = gameObject.AddComponent<PlanningHud>();
            hud._document = document;
            hud._ownedPanelSettings = settings;
            gameObject.SetActive(true);
            return hud;
        }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError("[XRim] PlanningHud has no UI document to build in.", this);
                return;
            }

            Build(_document.rootVisualElement);
        }

        private void OnDestroy()
        {
            if (_ownedPanelSettings != null) Destroy(_ownedPanelSettings);
        }

        // ----- IPlanningHudView -----

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowLayout(bool mirrored, float bodyZoneWidthFraction)
        {
            _mirrored = mirrored;
            _bodyZoneWidthFraction = bodyZoneWidthFraction;
            if (_root != null) ApplyLayout();
        }

        public void SetLoadout(IReadOnlyList<WeaponId> loadout)
        {
            _weaponButtons.Clear();
            if (_selectorRow == null) return;

            _selectorRow.Clear();
            foreach (WeaponId weapon in loadout)
            {
                WeaponId chosen = weapon;
                var button = new Button(() => WeaponChosen?.Invoke(chosen)) { text = weapon.Value.ToUpperInvariant() };
                StyleButton(button, ButtonWidth, ButtonHeight, 26);
                button.style.marginRight = RowGap;
                button.style.marginLeft = RowGap;
                _selectorRow.Add(button);
                _weaponButtons.Add(new WeaponButton(chosen, button));
            }

            RefreshWeaponButtons();
        }

        public void ShowSelectedWeapon(WeaponId weapon)
        {
            if (_selected == weapon) return;
            _selected = weapon;
            RefreshWeaponButtons();
        }

        public void ShowSwitchLocked(bool locked, string reason)
        {
            if (_statusLabel != null)
            {
                SetText(_statusLabel, locked ? reason : string.Empty);
                _statusLabel.style.display = locked && !string.IsNullOrEmpty(reason) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_switchLocked == locked) return;
            _switchLocked = locked;
            RefreshWeaponButtons();
        }

        public void ShowCountdown(double secondsLeft, bool inLockout)
        {
            if (_timerLabel == null) return;

            double left = Math.Max(secondsLeft, 0.0);
            string text = left >= WholeSecondsAbove
                ? Math.Ceiling(left).ToString("0", CultureInfo.InvariantCulture)
                : left.ToString("0.0", CultureInfo.InvariantCulture);
            SetText(_timerLabel, text);
            _timerLabel.style.color = inLockout ? LockoutColor : TextColor;
        }

        public void ShowInkRemaining(float fraction)
        {
            if (_inkFill == null) return;

            float share = Mathf.Clamp01(fraction);
            _inkFill.style.width = new Length(share * 100f, LengthUnit.Percent);
            _inkFill.style.backgroundColor = Color.Lerp(InkEmptyColor, InkFullColor, share);
            SetText(_inkLabel, "INK " + Mathf.RoundToInt(share * 100f).ToString(CultureInfo.InvariantCulture) + "%");
        }

        public void ShowBodyMove(BodyMove move) =>
            SetText(_moveLabel, move == BodyMove.None ? "none" : move.ToString().ToUpperInvariant());

        public void ShowOpponent(PublicPlanningState state)
        {
            if (_opponentLabel == null) return;

            SetText(_opponentLabel, state.Weapon.Value.ToUpperInvariant() + (state.IsReady ? "   READY" : "   planning…"));
            _opponentLabel.style.color = state.IsReady ? InkFullColor : TextColor;
        }

        public void ShowReady(ReadyButtonState state)
        {
            if (_readyButton == null) return;

            switch (state)
            {
                case ReadyButtonState.Ready:
                    _readyButton.text = "READY";
                    _readyButton.style.backgroundColor = ReadyColor;
                    break;
                case ReadyButtonState.Cancel:
                    _readyButton.text = "CANCEL READY";
                    _readyButton.style.backgroundColor = CancelColor;
                    break;
                default:
                    _readyButton.text = "READY";
                    _readyButton.style.backgroundColor = LockedColor;
                    break;
            }

            bool pressable = state != ReadyButtonState.LockedIn;
            _readyButton.SetEnabled(pressable);
            _readyButton.style.opacity = pressable ? 1f : DisabledOpacity + 0.2f;
        }

        public void ShowMessage(string text)
        {
            if (_messageLabel == null) return;

            _hideMessage?.Pause();
            if (string.IsNullOrEmpty(text))
            {
                _messageLabel.style.display = DisplayStyle.None;
                return;
            }

            _messageLabel.text = text;
            _messageLabel.style.display = DisplayStyle.Flex;
            _hideMessage = _messageLabel.schedule.Execute(() => _messageLabel.style.display = DisplayStyle.None).StartingIn(MessageSeconds);
        }

        /// <summary>
        /// True when a screen point (pixels, origin bottom-left) is on one of the HUD's controls, so a touch there is a button press
        /// and must not draw or swipe. The plain layout containers never count.
        /// </summary>
        public bool IsOverControl(Vector2 screenPixels)
        {
            if (!_visible || _root == null || _root.panel == null) return false;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPixels.x, Screen.height - screenPixels.y));
            return _root.panel.Pick(panelPoint) != null;
        }

        // ----- building -----

        private void Build(VisualElement root)
        {
            root.Clear();
            _root = root;
            _root.pickingMode = PickingMode.Ignore;
            Absolute(_root);
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;

            // Without a theme the panel has no default font: set one, inherited by every label and button below.
            Font font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
            if (font != null) _root.style.unityFontDefinition = FontDefinition.FromFont(font);

            BuildBodyStrip();
            BuildSelectorColumn();
            BuildTimer();
            BuildOpponent();
            BuildMessage();
            BuildBottomCorner();
            ApplyLayout();
        }

        private void BuildBodyStrip()
        {
            _bodyStrip = Plain(_root);
            Absolute(_bodyStrip);
            _bodyStrip.style.top = 0;
            _bodyStrip.style.bottom = 0;
            _bodyStrip.style.backgroundColor = BodyStripColor;
            _bodyStrip.style.flexDirection = FlexDirection.Column;
            _bodyStrip.style.justifyContent = Justify.FlexEnd;
            _bodyStrip.style.alignItems = Align.Center;
            _bodyStrip.style.paddingBottom = 28;

            Label caption = Text(_bodyStrip, "BODY MOVE: SWIPE", 18, DimTextColor);
            caption.style.marginBottom = 4;
            _moveLabel = Text(_bodyStrip, "none", 30, TextColor);
            _moveLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void BuildSelectorColumn()
        {
            _selectorColumn = Plain(_root);
            Absolute(_selectorColumn);
            _selectorColumn.style.top = TopMargin;
            _selectorColumn.style.flexDirection = FlexDirection.Column;

            _selectorRow = Plain(_selectorColumn);
            _selectorRow.style.flexDirection = FlexDirection.Row;

            VisualElement inkRow = Plain(_selectorColumn);
            inkRow.style.flexDirection = FlexDirection.Row;
            inkRow.style.alignItems = Align.Center;
            inkRow.style.marginTop = RowGap;
            inkRow.style.marginLeft = RowGap;
            inkRow.style.marginRight = RowGap;

            VisualElement track = Plain(inkRow);
            track.style.width = InkTrackWidth;
            track.style.height = InkTrackHeight;
            track.style.backgroundColor = PanelColor;
            track.style.flexDirection = FlexDirection.Row;
            _inkFill = Plain(track);
            _inkFill.style.height = new Length(100f, LengthUnit.Percent);
            _inkLabel = Text(inkRow, "INK 100%", 20, TextColor);
            _inkLabel.style.marginLeft = RowGap;
            ShowInkRemaining(1f);

            _statusLabel = Text(_selectorColumn, string.Empty, 20, LockoutColor);
            _statusLabel.style.marginTop = RowGap;
            _statusLabel.style.marginLeft = RowGap;
            _statusLabel.style.marginRight = RowGap;
            _statusLabel.style.display = DisplayStyle.None;
        }

        private void BuildTimer()
        {
            VisualElement row = Plain(_root);
            Absolute(row);
            row.style.left = 0;
            row.style.right = 0;
            row.style.top = 6;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            _timerLabel = Text(row, "12", 56, TextColor);
            _timerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void BuildOpponent()
        {
            _opponentBox = Plain(_root);
            Absolute(_opponentBox);
            _opponentBox.style.top = TopMargin;
            _opponentBox.style.backgroundColor = PanelColor;
            _opponentBox.style.paddingLeft = 14;
            _opponentBox.style.paddingRight = 14;
            _opponentBox.style.paddingTop = 8;
            _opponentBox.style.paddingBottom = 8;
            Text(_opponentBox, "OPPONENT", 18, DimTextColor);
            _opponentLabel = Text(_opponentBox, string.Empty, 28, TextColor);
            _opponentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void BuildMessage()
        {
            VisualElement row = Plain(_root);
            Absolute(row);
            row.style.left = 0;
            row.style.right = 0;
            row.style.bottom = 150;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            _messageLabel = Text(row, string.Empty, 26, TextColor);
            _messageLabel.style.backgroundColor = PanelColor;
            _messageLabel.style.paddingLeft = 24;
            _messageLabel.style.paddingRight = 24;
            _messageLabel.style.paddingTop = 10;
            _messageLabel.style.paddingBottom = 10;
            _messageLabel.style.display = DisplayStyle.None;
        }

        private void BuildBottomCorner()
        {
            _bottomCorner = Plain(_root);
            Absolute(_bottomCorner);
            _bottomCorner.style.bottom = 24;
            _bottomCorner.style.flexDirection = FlexDirection.Row;
            _bottomCorner.style.alignItems = Align.FlexEnd;

            SignatureSlot = Plain(_bottomCorner);
            SignatureSlot.style.flexDirection = FlexDirection.Row;
            SignatureSlot.style.marginRight = Margin;

            _readyButton = new Button(() => ReadyPressed?.Invoke());
            StyleButton(_readyButton, ButtonWidth * 1.6f, ButtonHeight * 1.4f, 32);
            _bottomCorner.Add(_readyButton);
            ShowReady(ReadyButtonState.Ready);
        }

        // ----- layout and refreshing -----

        /// <summary>Puts every anchored piece on the right side of the screen for the layout (GDD §4).</summary>
        private void ApplyLayout()
        {
            float zonePercent = _bodyZoneWidthFraction * 100f;
            Anchor(_bodyStrip, true, 0f, LengthUnit.Pixel);
            _bodyStrip.style.width = new Length(zonePercent, LengthUnit.Percent);

            // The selector column starts at the body zone's edge, so the drawing area stays clear.
            Anchor(_selectorColumn, true, zonePercent, LengthUnit.Percent);
            _selectorRow.style.flexDirection = _mirrored ? FlexDirection.RowReverse : FlexDirection.Row;
            _selectorColumn.style.alignItems = _mirrored ? Align.FlexEnd : Align.FlexStart;
            Anchor(_opponentBox, false, Margin, LengthUnit.Pixel);
            Anchor(_bottomCorner, false, Margin + Margin, LengthUnit.Pixel);
            _bottomCorner.style.flexDirection = _mirrored ? FlexDirection.RowReverse : FlexDirection.Row;
        }

        /// <summary>
        /// Anchors an absolutely placed element to the own edge of the screen (the left one unless mirrored) or to the far edge, at
        /// an offset in pixels or in percent of the screen width.
        /// </summary>
        private void Anchor(VisualElement element, bool ownEdge, float offset, LengthUnit unit)
        {
            bool left = ownEdge != _mirrored;
            var length = new Length(offset, unit);
            element.style.left = left ? new StyleLength(length) : new StyleLength(StyleKeyword.Auto);
            element.style.right = left ? new StyleLength(StyleKeyword.Auto) : new StyleLength(length);
        }

        private void RefreshWeaponButtons()
        {
            foreach (WeaponButton entry in _weaponButtons)
            {
                bool selected = entry.Weapon == _selected;
                Button button = entry.Button;
                button.style.backgroundColor = selected ? SelectedColor : ButtonColor;
                button.style.color = selected ? SelectedTextColor : TextColor;
                button.style.opacity = _switchLocked && !selected ? DisabledOpacity : 1f;
                button.SetEnabled(!_switchLocked);
            }
        }

        // ----- small helpers -----

        private static void Absolute(VisualElement element) => element.style.position = Position.Absolute;

        /// <summary>A layout container that never catches a touch.</summary>
        private static VisualElement Plain(VisualElement parent)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(element);
            return element;
        }

        private static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            parent.Add(label);
            return label;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        private static void StyleButton(Button button, float width, float height, int fontSize)
        {
            IStyle style = button.style;
            style.width = width;
            style.height = height;
            style.fontSize = fontSize;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.color = TextColor;
            style.backgroundColor = ButtonColor;
            style.borderTopLeftRadius = 12;
            style.borderTopRightRadius = 12;
            style.borderBottomLeftRadius = 12;
            style.borderBottomRightRadius = 12;
            style.borderTopWidth = 3;
            style.borderBottomWidth = 3;
            style.borderLeftWidth = 3;
            style.borderRightWidth = 3;
            style.borderTopColor = EdgeColor;
            style.borderBottomColor = EdgeColor;
            style.borderLeftColor = EdgeColor;
            style.borderRightColor = EdgeColor;
            style.marginTop = 0;
            style.marginBottom = 0;
        }

        private readonly struct WeaponButton
        {
            public WeaponId Weapon { get; }
            public Button Button { get; }

            public WeaponButton(WeaponId weapon, Button button)
            {
                Weapon = weapon;
                Button = button;
            }
        }
    }
}
