using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core;
using static XRim.Presentation.UI.HudElements;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The match HUD (UI Toolkit built in code, placeholder look): an HP bar under each dummy at the bottom of the screen, the turn
    /// counter between them, a banner under the planning timer (sudden death), and a signature move's name shouted over its fighter's
    /// bar while the turn plays (§8). Nothing in it catches a touch, so drawing and
    /// swiping work right through it. It sits under the planning HUD and between the body zone and the Ready corner (D23 layout).
    /// </summary>
    public sealed class MatchHud : MonoBehaviour, IMatchHudView
    {
        private const int SortingOrder = 90;
        private const float BlockWidth = 420f;
        private const float CentreWidth = 260f;
        private const float BarHeight = 14f;
        private const float BottomMargin = 10f;
        private const float BannerTop = 160f;
        private const float PercentScale = 100f;
        private const float ShoutBottom = 96f;

        private static readonly Color BannerColor = new Color(1f, 0.35f, 0.2f, 1f);

        [SerializeField] private UIDocument _document;

        private PanelSettings _ownedPanelSettings;
        private VisualElement _root;
        private HudSafeArea _safeArea;
        private FighterBlock _screenLeft;
        private FighterBlock _screenRight;
        private Label _turnLabel;
        private VisualElement _banner;
        private Label _bannerTitle;
        private Label _bannerDetail;
        private Label _shoutLeft;
        private Label _shoutRight;
        private bool _visible = true;

        /// <summary>Makes the HUD on a new GameObject of its own (see <see cref="PlanningHud.Create"/>).</summary>
        public static MatchHud Create(Vector2Int referenceResolution, PanelSettings panelSettingsTemplate = null)
        {
            UIDocument document = CreateDocument("MatchHud", referenceResolution, SortingOrder, panelSettingsTemplate, out PanelSettings settings);
            var hud = document.gameObject.AddComponent<MatchHud>();
            hud._document = document;
            hud._ownedPanelSettings = settings;
            hud._safeArea = new HudSafeArea(referenceResolution);
            document.gameObject.SetActive(true);
            return hud;
        }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError("[XRim] MatchHud has no UI document to build in.", this);
                return;
            }

            Build(_document.rootVisualElement);
        }

        private void Update()
        {
            if (_root != null && _safeArea.Changed(out ScreenInsets insets)) InsetEdges(_root, insets);
        }

        private void OnDestroy()
        {
            if (_ownedPanelSettings != null) Destroy(_ownedPanelSettings);
        }

        // ----- IMatchHudView -----

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowFighter(Side side, string name, float hp, float maxHp, bool onScreenLeft)
        {
            FighterBlock block = onScreenLeft ? _screenLeft : _screenRight;
            if (block == null) return;

            float share = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            int shown = hp <= 0f ? 0 : Mathf.CeilToInt(hp); // alive shows at least 1
            SetText(block.Name, name);
            SetText(block.Hp, "HP " + shown.ToString(CultureInfo.InvariantCulture));
            block.Fill.style.width = new Length(share * PercentScale, LengthUnit.Percent);
            block.Fill.style.backgroundColor = Color.Lerp(BadColor, GoodColor, share);
        }

        public void ShowTurn(string text) => SetText(_turnLabel, text);

        public void ShowBanner(string title, string detail)
        {
            if (_banner == null) return;
            bool show = !string.IsNullOrEmpty(title);
            _banner.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(_bannerTitle, title ?? string.Empty);
            SetText(_bannerDetail, detail ?? string.Empty);
        }

        public void ShowSignatureShout(Side side, string text, bool onScreenLeft)
        {
            Label shout = onScreenLeft ? _shoutLeft : _shoutRight;
            if (shout == null) return;
            bool show = !string.IsNullOrEmpty(text);
            shout.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            SetText(shout, text ?? string.Empty);
        }

        // ----- building -----

        private void Build(VisualElement root)
        {
            PrepareRoot(root);
            _root = root;
            _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_safeArea.Changed(out ScreenInsets insets)) InsetEdges(_root, insets); // nothing here may hide under a notch

            VisualElement row = Plain(_root);
            Absolute(row);
            row.style.left = 0;
            row.style.right = 0;
            row.style.bottom = BottomMargin;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.FlexEnd;

            _screenLeft = BuildBlock(row, false);
            VisualElement centre = Plain(row);
            centre.style.width = CentreWidth;
            centre.style.alignItems = Align.Center;
            _turnLabel = Text(centre, string.Empty, 26, TextColor);
            _turnLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _screenRight = BuildBlock(row, true);

            BuildBanner();
            BuildShouts();
        }

        /// <summary>A row over the HP bars, laid out like them, where a signature move's name is shouted over its fighter's bar (§8).</summary>
        private void BuildShouts()
        {
            VisualElement row = Plain(_root);
            Absolute(row);
            row.style.left = 0;
            row.style.right = 0;
            row.style.bottom = ShoutBottom;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            _shoutLeft = BuildShout(row);
            VisualElement gap = Plain(row);
            gap.style.width = CentreWidth;
            _shoutRight = BuildShout(row);
        }

        private static Label BuildShout(VisualElement row)
        {
            VisualElement cell = Plain(row);
            cell.style.width = BlockWidth;
            cell.style.alignItems = Align.Center;
            Label shout = Text(cell, string.Empty, 32, HighlightColor);
            shout.style.unityFontStyleAndWeight = FontStyle.Bold;
            shout.style.whiteSpace = WhiteSpace.Normal;
            shout.style.unityTextAlign = TextAnchor.MiddleCenter;
            shout.style.backgroundColor = PanelColor;
            Pad(shout, 16f, 6f);
            shout.style.visibility = Visibility.Hidden;
            return shout;
        }

        /// <summary>A name, the HP and a bar that empties toward the outer edge of the screen.</summary>
        private static FighterBlock BuildBlock(VisualElement parent, bool onTheRight)
        {
            VisualElement block = Plain(parent);
            block.style.width = BlockWidth;
            block.style.backgroundColor = PanelColor;
            Pad(block, 10f, 6f);

            VisualElement header = Plain(block);
            header.style.flexDirection = onTheRight ? FlexDirection.RowReverse : FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            Label name = Text(header, string.Empty, 24, TextColor);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label hp = Text(header, string.Empty, 24, TextColor);

            VisualElement track = Plain(block);
            track.style.height = BarHeight;
            track.style.marginTop = 4;
            track.style.backgroundColor = ButtonColor;
            track.style.flexDirection = onTheRight ? FlexDirection.RowReverse : FlexDirection.Row;
            VisualElement fill = Plain(track);
            fill.style.height = new Length(PercentScale, LengthUnit.Percent);
            fill.style.width = new Length(PercentScale, LengthUnit.Percent);
            fill.style.backgroundColor = GoodColor;
            return new FighterBlock(name, hp, fill);
        }

        private void BuildBanner()
        {
            VisualElement row = Plain(_root);
            Absolute(row);
            row.style.left = 0;
            row.style.right = 0;
            row.style.top = BannerTop;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;

            _banner = Plain(row);
            _banner.style.backgroundColor = PanelColor;
            _banner.style.alignItems = Align.Center;
            Pad(_banner, 24f, 8f);
            _bannerTitle = Text(_banner, string.Empty, 34, BannerColor);
            _bannerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bannerDetail = Text(_banner, string.Empty, 20, DimTextColor);
            _banner.style.display = DisplayStyle.None;
        }

        private sealed class FighterBlock
        {
            public Label Name { get; }
            public Label Hp { get; }
            public VisualElement Fill { get; }

            public FighterBlock(Label name, Label hp, VisualElement fill)
            {
                Name = name;
                Hp = hp;
                Fill = fill;
            }
        }
    }
}
