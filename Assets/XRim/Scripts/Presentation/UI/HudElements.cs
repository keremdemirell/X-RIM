using UnityEngine;
using UnityEngine.UIElements;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// What every HUD here shares (UI Toolkit built in code: no UXML, stylesheet or panel asset): panel settings made at runtime that
    /// scale with the screen height, the built-in font, the placeholder palette, and the plain containers, labels and buttons they are
    /// made of. Placeholder look until the UI art arrives (Session 18).
    /// </summary>
    internal static class HudElements
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        private const float ButtonCornerRadius = 12f;
        private const float ButtonEdgeWidth = 3f;

        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color TextColor = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color DimTextColor = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color ButtonColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        public static readonly Color EdgeColor = new Color(1f, 1f, 1f, 0.3f);
        public static readonly Color HighlightColor = new Color(1f, 0.85f, 0.1f, 1f);
        public static readonly Color GoodColor = new Color(0.25f, 0.8f, 0.3f);
        public static readonly Color BadColor = new Color(0.85f, 0.2f, 0.15f);

        /// <summary>
        /// A UI document on a new, inactive GameObject, with panel settings created in code: they scale with the screen height from
        /// <paramref name="referenceResolution"/>, so a wider screen shows more but nothing grows (like the arena view). The caller adds
        /// its component, keeps <paramref name="settings"/> to destroy it later, then activates the object.
        /// </summary>
        public static UIDocument CreateDocument(string name, Vector2Int referenceResolution, int sortingOrder, out PanelSettings settings)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            var document = gameObject.AddComponent<UIDocument>();
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = referenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f; // by height
            settings.sortingOrder = sortingOrder;
            document.panelSettings = settings;
            return document;
        }

        /// <summary>Makes the document's root fill the screen without catching touches, with the built-in font for every label below.</summary>
        public static void PrepareRoot(VisualElement root)
        {
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            Absolute(root);
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;

            // Without a theme the panel has no default font: set one, inherited by every label and button below.
            Font font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        public static void Absolute(VisualElement element) => element.style.position = Position.Absolute;

        /// <summary>A layout container that never catches a touch.</summary>
        public static VisualElement Plain(VisualElement parent)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            parent.Add(element);
            return element;
        }

        public static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            parent.Add(label);
            return label;
        }

        public static void SetText(Label label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        public static void StyleButton(Button button, float width, float height, int fontSize)
        {
            IStyle style = button.style;
            style.width = width;
            style.height = height;
            style.fontSize = fontSize;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.color = TextColor;
            style.backgroundColor = ButtonColor;
            style.borderTopLeftRadius = ButtonCornerRadius;
            style.borderTopRightRadius = ButtonCornerRadius;
            style.borderBottomLeftRadius = ButtonCornerRadius;
            style.borderBottomRightRadius = ButtonCornerRadius;
            style.borderTopWidth = ButtonEdgeWidth;
            style.borderBottomWidth = ButtonEdgeWidth;
            style.borderLeftWidth = ButtonEdgeWidth;
            style.borderRightWidth = ButtonEdgeWidth;
            style.borderTopColor = EdgeColor;
            style.borderBottomColor = EdgeColor;
            style.borderLeftColor = EdgeColor;
            style.borderRightColor = EdgeColor;
            style.marginTop = 0;
            style.marginBottom = 0;
        }

        /// <summary>Inner space on all four sides.</summary>
        public static void Pad(VisualElement element, float horizontal, float vertical)
        {
            element.style.paddingLeft = horizontal;
            element.style.paddingRight = horizontal;
            element.style.paddingTop = vertical;
            element.style.paddingBottom = vertical;
        }
    }
}
