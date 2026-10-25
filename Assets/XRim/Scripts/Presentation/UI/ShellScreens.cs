using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core;
using XRim.Rules;
using static XRim.Presentation.UI.HudElements;
using static XRim.Presentation.UI.UiTheme;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The screens outside a match (Session 18; UI Toolkit built in code, placeholder look until the UI art): the first-launch handedness
    /// pick, the menus and Settings. Opaque and full-screen, so the arena behind never shows through. It draws above every HUD and above the
    /// match screens.
    /// </summary>
    public sealed class ShellScreens : MonoBehaviour, IShellView
    {
        private const int SortingOrder = 210;
        private const float OptionWidth = 760f;
        private const float OptionHeight = 112f;
        private const float ChoiceWidth = 440f;
        private const float ChoiceHeight = 150f;
        private const float BackWidth = 360f;
        private const float BackHeight = 88f;
        private const float StepButtonSize = 84f;
        private const float BarWidth = 380f;
        private const float BarHeight = 26f;
        private const float LabelWidth = 190f;
        private const float PercentWidth = 110f;
        private const float FullPercent = 100f;
        private const float TabWidth = 300f;
        private const float TabHeight = 76f;
        private const float RowWidth = 1180f;
        private const float RowHeight = 92f;
        private const float RowButtonWidth = 230f;
        private const float RowButtonHeight = 68f;
        private const float RowPadding = 22f;

        [SerializeField] private UIDocument _document;

        private PanelSettings _ownedPanelSettings;
        private VisualElement _root;
        private HudSafeArea _safeArea;

        private VisualElement _pick;
        private Label _pickTitle;
        private Label _pickNote;
        private Button _pickRight;
        private Button _pickLeft;

        private VisualElement _menu;
        private Label _menuTitle;
        private Label _menuNote;
        private VisualElement _menuOptions;
        private Button _menuBack;

        private VisualElement _settings;
        private Label _settingsTitle;
        private Label _handednessTitle;
        private Label _handednessNote;
        private Button _settingsRight;
        private Button _settingsLeft;
        private Label _volumesTitle;
        private VisualElement _volumeRows;
        private readonly List<VolumeRow> _rows = new List<VolumeRow>();
        private Button _settingsBack;

        private VisualElement _collection;
        private Label _collectionTitle;
        private Label _collectionHeader;
        private Label _collectionNote;
        private Label _collectionStatus;
        private VisualElement _collectionTabs;
        private VisualElement _collectionRows;
        private Button _collectionBack;
        private VisualElement _collectionTitleGap;

        public event Action<int> OptionPicked;
        public event Action BackPressed;
        public event Action<Handedness> HandednessPicked;
        public event Action<int, int> VolumeStepped;
        public event Action<int> CollectionTabSelected;
        public event Action<int, int, int> CollectionRowPressed;

        /// <summary>Makes the screens on a new GameObject of their own (see <see cref="PlanningHud.Create"/>); all hidden.</summary>
        public static ShellScreens Create(Vector2Int referenceResolution, PanelSettings panelSettingsTemplate = null)
        {
            UIDocument document = CreateDocument("ShellScreens", referenceResolution, SortingOrder, panelSettingsTemplate, out PanelSettings settings);
            var screens = document.gameObject.AddComponent<ShellScreens>();
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
                Debug.LogError("[XRim] ShellScreens has no UI document to build in.", this);
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

        // ----- IShellView -----

        public void ShowHandednessPick(string title, string note, string rightText, string leftText)
        {
            if (_pick == null) return;

            SetText(_pickTitle, title);
            SetText(_pickNote, note);
            _pickRight.text = rightText;
            _pickLeft.text = leftText;
            Show(_pick);
        }

        public void ShowMenu(string title, string note, IReadOnlyList<MenuOption> options, string backText)
        {
            if (_menu == null) return;

            SetText(_menuTitle, title);
            SetText(_menuNote, note);
            _menuOptions.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                MenuOption option = options[i];
                var button = new Button(() => OptionPicked?.Invoke(index)) { text = string.Empty };
                StyleButton(button, OptionWidth, OptionHeight, 34);
                button.style.marginTop = Gap;
                button.style.flexDirection = FlexDirection.Column;
                button.style.justifyContent = Justify.Center;
                button.style.alignItems = Align.Center;
                Label optionTitle = Text(button, option.Title, 36, option.Enabled ? TextColor : DisabledColor);
                optionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                Text(button, option.Detail, 22, option.Enabled ? DimTextColor : DisabledColor);
                button.SetEnabled(option.Enabled);
                _menuOptions.Add(button);
            }

            bool hasBack = !string.IsNullOrEmpty(backText);
            _menuBack.style.display = hasBack ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasBack) _menuBack.text = backText;
            Show(_menu);
        }

        public void ShowSettings(ShellSettingsModel model)
        {
            if (_settings == null) return;

            SetText(_settingsTitle, model.Title);
            SetText(_handednessTitle, model.HandednessTitle);
            SetText(_handednessNote, model.HandednessNote);
            _settingsRight.text = model.RightText;
            _settingsLeft.text = model.LeftText;
            MarkChosen(_settingsRight, model.Handedness == Handedness.Right);
            MarkChosen(_settingsLeft, model.Handedness == Handedness.Left);
            SetText(_volumesTitle, model.VolumesTitle);
            if (_rows.Count != model.Volumes.Count) RebuildRows(model.Volumes.Count);
            for (int i = 0; i < model.Volumes.Count; i++)
            {
                VolumeRow row = _rows[i];
                SetText(row.Name, model.Volumes[i].Label);
                float fraction = XMath.Clamp01(model.Volumes[i].Fraction);
                row.Fill.style.width = Length.Percent(fraction * FullPercent);
                SetText(row.Percent, Mathf.RoundToInt(fraction * FullPercent) + "%");
            }

            _settingsBack.text = model.BackText;
            Show(_settings);
        }

        public void ShowCollection(ShellCollectionModel model)
        {
            if (_collection == null) return;

            int selected = Mathf.Clamp(model.SelectedTab, 0, Mathf.Max(model.Tabs.Count - 1, 0));
            SetText(_collectionTitle, model.Title);
            SetText(_collectionHeader, model.Header);
            SetText(_collectionStatus, model.Status);
            _collectionHeader.style.display = model.Header.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _collectionStatus.style.display = model.Status.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(_collectionNote, model.Tabs.Count > 0 ? model.Tabs[selected].Note : string.Empty);

            _collectionTabs.Clear();
            for (int i = 0; i < model.Tabs.Count; i++)
            {
                int index = i;
                var tab = new Button(() => CollectionTabSelected?.Invoke(index)) { text = model.Tabs[i].Label };
                StyleButton(tab, TabWidth, TabHeight, 30);
                tab.style.marginLeft = Gap * 0.5f;
                tab.style.marginRight = Gap * 0.5f;
                MarkChosen(tab, i == selected);
                _collectionTabs.Add(tab);
            }

            _collectionRows.Clear();
            if (model.Tabs.Count > 0)
            {
                IReadOnlyList<ShellCollectionRow> rows = model.Tabs[selected].Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    AddCollectionRow(selected, i, rows[i]);
                }
            }

            _collectionBack.text = model.BackText;
            Show(_collection);
        }

        public void Hide() => Show(null);

        // ----- building -----

        private void Build(VisualElement root)
        {
            PrepareRoot(root);
            _root = root;
            BuildPick();
            BuildMenu();
            BuildSettings();
            BuildCollection();
            Hide();
            if (_safeArea.Changed(out ScreenInsets insets)) PadScreens(insets);
        }

        /// <summary>Each screen's background still reaches every edge; its content stays inside the safe area.</summary>
        private void PadScreens(ScreenInsets insets)
        {
            foreach (VisualElement screen in new[] { _pick, _menu, _settings, _collection })
            {
                if (screen != null) PadBy(screen, insets);
            }
        }

        private void BuildPick()
        {
            _pick = FullScreen(Plain(_root), ScreenColor);
            _pickTitle = Text(_pick, string.Empty, 72, HighlightColor);
            _pickTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pickNote = Text(_pick, string.Empty, 24, DimTextColor);
            _pickNote.style.marginTop = Gap;
            _pickNote.style.marginBottom = Gap * 3f;

            VisualElement row = Plain(_pick);
            row.style.flexDirection = FlexDirection.Row;
            _pickRight = AddChoice(row, () => HandednessPicked?.Invoke(Handedness.Right));
            _pickLeft = AddChoice(row, () => HandednessPicked?.Invoke(Handedness.Left));
        }

        private void BuildMenu()
        {
            _menu = FullScreen(Plain(_root), ScreenColor);
            _menuTitle = Text(_menu, string.Empty, 96, HighlightColor);
            _menuTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _menuNote = Text(_menu, string.Empty, 22, DimTextColor);
            _menuNote.style.marginBottom = Gap;
            _menuOptions = Plain(_menu);
            _menuOptions.style.alignItems = Align.Center;
            _menuBack = new Button(() => BackPressed?.Invoke()) { text = string.Empty };
            StyleButton(_menuBack, BackWidth, BackHeight, 30);
            _menuBack.style.marginTop = Gap * 2f;
            _menu.Add(_menuBack);
        }

        private void BuildSettings()
        {
            _settings = FullScreen(Plain(_root), ScreenColor);
            _settingsTitle = Text(_settings, string.Empty, 72, HighlightColor);
            _settingsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

            _handednessTitle = Text(_settings, string.Empty, 36, TextColor);
            _handednessTitle.style.marginTop = Gap * 2f;
            _handednessTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _handednessNote = Text(_settings, string.Empty, 22, DimTextColor);
            _handednessNote.style.marginBottom = Gap;
            VisualElement choices = Plain(_settings);
            choices.style.flexDirection = FlexDirection.Row;
            _settingsRight = AddChoice(choices, () => HandednessPicked?.Invoke(Handedness.Right));
            _settingsLeft = AddChoice(choices, () => HandednessPicked?.Invoke(Handedness.Left));
            foreach (Button choice in new[] { _settingsRight, _settingsLeft })
            {
                choice.style.height = BackHeight;
                choice.style.width = ChoiceWidth;
            }

            _volumesTitle = Text(_settings, string.Empty, 36, TextColor);
            _volumesTitle.style.marginTop = Gap * 2f;
            _volumesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _volumeRows = Plain(_settings);
            _volumeRows.style.alignItems = Align.Center;

            _settingsBack = new Button(() => BackPressed?.Invoke()) { text = string.Empty };
            StyleButton(_settingsBack, BackWidth, BackHeight, 30);
            _settingsBack.style.marginTop = Gap * 2f;
            _settings.Add(_settingsBack);
        }

        private void BuildCollection()
        {
            _collection = FullScreen(Plain(_root), ScreenColor);
            _collectionTitle = Text(_collection, string.Empty, 64, HighlightColor);
            _collectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _collectionHeader = Text(_collection, string.Empty, 32, HighlightColor);
            _collectionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            _collectionNote = Text(_collection, string.Empty, 22, DimTextColor);
            _collectionNote.style.marginTop = Gap * 0.5f;
            _collectionStatus = Text(_collection, string.Empty, 26, GoodColor);
            _collectionStatus.style.marginTop = Gap * 0.5f;
            _collectionStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _collectionTitleGap = Plain(_collection);
            _collectionTitleGap.style.height = Gap;

            _collectionTabs = Plain(_collection);
            _collectionTabs.style.flexDirection = FlexDirection.Row;
            _collectionTabs.style.marginBottom = Gap;
            _collectionRows = Plain(_collection);
            _collectionRows.style.alignItems = Align.Center;

            _collectionBack = new Button(() => BackPressed?.Invoke()) { text = string.Empty };
            StyleButton(_collectionBack, BackWidth, BackHeight, 30);
            _collectionBack.style.marginTop = Gap * 1.5f;
            _collection.Add(_collectionBack);
        }

        /// <summary>A card: the name and a line about it on the left, an optional button on the right; a locked one is dimmed.</summary>
        private void AddCollectionRow(int tab, int row, ShellCollectionRow content)
        {
            VisualElement card = Plain(_collectionRows);
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.width = RowWidth;
            card.style.height = RowHeight;
            card.style.marginTop = Gap * 0.5f;
            card.style.paddingLeft = RowPadding;
            card.style.paddingRight = RowPadding;
            card.style.backgroundColor = ButtonColor;
            card.style.borderTopLeftRadius = Gap * 0.6f;
            card.style.borderTopRightRadius = Gap * 0.6f;
            card.style.borderBottomLeftRadius = Gap * 0.6f;
            card.style.borderBottomRightRadius = Gap * 0.6f;

            VisualElement words = Plain(card);
            Label title = Text(words, content.Title, 34, content.Locked ? DisabledColor : TextColor);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(words, content.Detail, 22, content.Locked ? DisabledColor : DimTextColor);

            VisualElement buttons = Plain(card);
            buttons.style.flexDirection = FlexDirection.Row;
            AddRowButton(buttons, tab, row, 0, content.ButtonText, content.ButtonEnabled);
            AddRowButton(buttons, tab, row, 1, content.SecondButtonText, content.SecondButtonEnabled);
        }

        /// <summary>A button at a row's right end; no text, no button (its index is still the one a press reports).</summary>
        private void AddRowButton(VisualElement parent, int tab, int row, int index, string text, bool enabled)
        {
            if (string.IsNullOrEmpty(text)) return;

            var button = new Button(() => CollectionRowPressed?.Invoke(tab, row, index)) { text = text };
            StyleButton(button, RowButtonWidth, RowButtonHeight, 26);
            button.style.marginLeft = Gap * 0.5f;
            button.style.color = enabled ? TextColor : DisabledColor;
            button.SetEnabled(enabled);
            parent.Add(button);
        }

        /// <summary>A big two-choice button (right or left hand).</summary>
        private static Button AddChoice(VisualElement row, Action pressed)
        {
            var button = new Button(pressed) { text = string.Empty };
            StyleButton(button, ChoiceWidth, ChoiceHeight, 38);
            button.style.marginLeft = Gap;
            button.style.marginRight = Gap;
            row.Add(button);
            return button;
        }

        private static void MarkChosen(Button button, bool chosen)
        {
            button.style.backgroundColor = chosen ? ChosenColor : ButtonColor;
            Color edge = chosen ? HighlightColor : EdgeColor;
            button.style.borderTopColor = edge;
            button.style.borderBottomColor = edge;
            button.style.borderLeftColor = edge;
            button.style.borderRightColor = edge;
        }

        /// <summary>A volume is a name, a minus button, a bar that fills, a plus button and a percentage: no dragging, so every press is a big target.</summary>
        private void RebuildRows(int count)
        {
            _volumeRows.Clear();
            _rows.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                VisualElement line = Plain(_volumeRows);
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.style.marginTop = Gap * 0.5f;

                Label name = Text(line, string.Empty, 30, TextColor);
                name.style.width = LabelWidth;
                Button minus = AddStep(line, "-", () => VolumeStepped?.Invoke(index, -1));

                VisualElement track = Plain(line);
                track.style.width = BarWidth;
                track.style.height = BarHeight;
                track.style.backgroundColor = BarTrackColor;
                track.style.marginLeft = Gap;
                track.style.marginRight = Gap;
                VisualElement fill = Plain(track);
                fill.style.height = BarHeight;
                fill.style.backgroundColor = HighlightColor;

                Button plus = AddStep(line, "+", () => VolumeStepped?.Invoke(index, 1));
                Label percent = Text(line, string.Empty, 28, DimTextColor);
                percent.style.width = PercentWidth;
                percent.style.marginLeft = Gap;
                _rows.Add(new VolumeRow(name, fill, percent, minus, plus));
            }
        }

        private static Button AddStep(VisualElement line, string text, Action pressed)
        {
            var button = new Button(pressed) { text = text };
            StyleButton(button, StepButtonSize, StepButtonSize, 44);
            line.Add(button);
            return button;
        }

        private void Show(VisualElement screen)
        {
            foreach (VisualElement candidate in new[] { _pick, _menu, _settings, _collection })
            {
                if (candidate != null) candidate.style.display = candidate == screen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private readonly struct VolumeRow
        {
            public readonly Label Name;
            public readonly VisualElement Fill;
            public readonly Label Percent;
            public readonly Button Minus;
            public readonly Button Plus;

            public VolumeRow(Label name, VisualElement fill, Label percent, Button minus, Button plus)
            {
                Name = name;
                Fill = fill;
                Percent = percent;
                Minus = minus;
                Plus = plus;
            }
        }
    }
}
