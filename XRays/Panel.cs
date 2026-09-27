using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace XRays
{
    /// <summary>
    /// Mod control panel, built with the "vanilla" elements provided by
    /// Jötunn (woodpanel, texts, buttons, toggles).
    /// </summary>
    internal class Panel
    {
        private class CategoryRow
        {
            public Category Category;
            public Toggle Toggle;
            public Text Label;
            public Text Count;
        }

        private const float PanelWidth = 470f;
        private const float LeftMargin = -PanelWidth / 2f + 20f;
        private const float RightMargin = PanelWidth / 2f - 20f;
        private const float ToggleSize = 26f;
        private const float RefreshInterval = 0.4f;
        private const float CountInterval = 1.5f;

        /// <summary>Limits of the "Panel height" option (vertical size).</summary>
        private const float MinPanelHeight = 360f;

        private const float MaxPanelHeight = 1200f;

        /// <summary>Free space kept under the scrolling area.</summary>
        private const float BottomPadding = 8f;

        /// <summary>Smallest usable scrolling area.</summary>
        private const float MinViewportHeight = 160f;

        /// <summary>Scrollbar, in the right margin of the panel.</summary>
        private const float ScrollbarInset = 6f;

        private const float ScrollbarWidth = 10f;

        private readonly XRays plugin;
        private readonly VisibilityManager visibility;
        private readonly List<CategoryRow> categoryRows = new List<CategoryRow>();

        private GameObject root;

        /// <summary>
        /// Parent of the elements being built: the panel itself for the
        /// header, the scrolling content for everything below it.
        /// </summary>
        private Transform container;

        private RectTransform viewportRect;
        private RectTransform contentRect;
        private RectTransform scrollbarRect;

        /// <summary>Height of the header (title + margins), fixed on screen.</summary>
        private float headerHeight;

        /// <summary>Height the rows need, whatever the panel is given.</summary>
        private float contentHeight;

        /// <summary>Height the panel was built with (the option may change).</summary>
        private float builtHeight;

        private Toggle terrainToggle;
        private Toggle waterToggle;
        private Toggle cloudsToggle;
        private Toggle playerToggle;
        private Toggle hudToggle;
        private Button revealButton;
        private Text cursorText;
        private Text objectsText;
        private float refreshTimer;
        private float countTimer;

        private Color BodyColor => new Color(0.93f, 0.88f, 0.79f, 1f);

        /// <summary>Where the next element is created (header or content).</summary>
        private Transform Parent => container != null ? container : root.transform;

        public Panel(XRays owner, VisibilityManager manager)
        {
            plugin = owner;
            visibility = manager;
        }

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>
        /// Is the panel really on screen: opened, part of an active hierarchy
        /// and with its "under the cursor" line actually rendered.
        /// </summary>
        public bool IsVisible =>
            IsOpen && root.activeInHierarchy && cursorText != null && cursorText.isActiveAndEnabled;

        /// <summary>The "under the cursor" text currently shown by the panel.</summary>
        public string CursorText => cursorText != null ? cursorText.text : null;

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (root == null && !Build())
            {
                return;
            }

            if (root == null)
            {
                return;
            }

            root.SetActive(true);
            GUIManager.BlockInput(true);
            Refresh();
            RefreshCounts();
        }

        public void Close()
        {
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
            }

            GUIManager.BlockInput(false);
        }

        /// <summary>The scene changed: the panel was destroyed with the game GUI.</summary>
        public void Invalidate()
        {
            root = null;
            container = null;
            viewportRect = null;
            contentRect = null;
            scrollbarRect = null;
            headerHeight = 0f;
            contentHeight = 0f;
            builtHeight = 0f;
            terrainToggle = null;
            waterToggle = null;
            cloudsToggle = null;
            playerToggle = null;
            hudToggle = null;
            revealButton = null;
            cursorText = null;
            objectsText = null;
            categoryRows.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (!IsOpen)
            {
                return;
            }

            refreshTimer += deltaTime;
            if (refreshTimer >= RefreshInterval)
            {
                refreshTimer = 0f;
                Refresh();
            }

            countTimer += deltaTime;
            if (countTimer >= CountInterval)
            {
                countTimer = 0f;
                RefreshCounts();
            }
        }

        /// <summary>Light refresh (toggles, "under the cursor" line).</summary>
        public void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            // the "Panel height" option may have been changed while the
            // panel is open (config manager), apply it right away
            float wanted = Mathf.Clamp(plugin.PanelHeight.Value, MinPanelHeight, MaxPanelHeight);
            if (!Mathf.Approximately(wanted, builtHeight))
            {
                ApplyHeight(wanted);
            }

            terrainToggle.SetIsOnWithoutNotify(visibility.TerrainHidden);
            waterToggle.SetIsOnWithoutNotify(visibility.WaterHidden);
            cloudsToggle.SetIsOnWithoutNotify(visibility.CloudsHidden);
            playerToggle.SetIsOnWithoutNotify(visibility.PlayerHidden);
            hudToggle.SetIsOnWithoutNotify(visibility.HudHidden);

            foreach (CategoryRow row in categoryRows)
            {
                row.Toggle.SetIsOnWithoutNotify(visibility.IsCategoryHidden(row.Category.Key));
            }

            // the global button says what the next press will do
            SetButtonLabel(
                revealButton,
                visibility.IsRevealing
                    ? "Restore"
                    : (visibility.AnyOptionChecked ? "Reveal" : "Hide")
            );

            objectsText.text =
                "Hidden objects: "
                + visibility.IndividualCount
                + "    Other: "
                + visibility.HiddenNameCategoryCount;

            cursorText.text = PickService.Describe(plugin.PickDistance.Value, visibility);
        }

        /// <summary>
        /// Per-category counters: listing the loaded objects is expensive,
        /// so it is only done periodically (and after an action).
        /// </summary>
        public void RefreshCounts()
        {
            if (!IsOpen)
            {
                return;
            }

            foreach (CategoryRow row in categoryRows)
            {
                row.Label.text = row.Category.Label;
                row.Count.text = Categories.Count(row.Category).ToString();
            }
        }

        // ---------------------------------------------------------------- construction

        private bool Build()
        {
            if (GUIManager.CustomGUIFront == null)
            {
                plugin.Log.LogWarning("XRays panel: Jötunn interface not available yet");
                return false;
            }

            GUIManager gui = GUIManager.Instance;
            float cursor = 14f; // top edge of the next row (negative goes down)

            float Row(float height)
            {
                float center = -(cursor + height / 2f);
                cursor += height;
                return center;
            }

            // pre-compute the panel layout: one single list of options, the
            // world layers first and the categories right after them. Only
            // the title belongs to the fixed header, the rest scrolls.
            float[] rows = new float[10 + Categories.BuiltIn.Length];
            rows[0] = Row(44f); // title (header)
            headerHeight = cursor + 16f; // margins of the header, then the viewport

            // the scrolling content has its own top margin: the height of
            // the panel is an option, so it is not part of this list
            cursor = 14f;

            // under the cursor: the text wraps over several lines (element,
            // game types, hit, coordinates), so it gets a whole extra line
            rows[1] = Row(96f);
            rows[2] = Row(30f); // terrain
            rows[3] = Row(30f); // water
            rows[4] = Row(30f); // clouds
            rows[5] = Row(30f); // player
            rows[6] = Row(30f); // hud
            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                rows[7 + i] = Row(26f);
            }

            rows[7 + Categories.BuiltIn.Length] = Row(30f); // hidden objects
            rows[8 + Categories.BuiltIn.Length] = Row(34f); // global buttons
            rows[9 + Categories.BuiltIn.Length] = Row(100f); // help text (5 lines)
            contentHeight = cursor + 16f;

            builtHeight = Mathf.Clamp(plugin.PanelHeight.Value, MinPanelHeight, MaxPanelHeight);

            root = gui.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(280f, 0f),
                PanelWidth,
                builtHeight,
                true
            );

            if (root == null)
            {
                plugin.Log.LogError("XRays panel: woodpanel creation failed");
                return false;
            }

            categoryRows.Clear();
            container = root.transform;

            // title + close button: they belong to the header, they never
            // scroll out of sight
            MakeText(
                "XRays — Visibility",
                LeftMargin,
                rows[0],
                300f,
                30f,
                22,
                gui.ValheimOrange,
                TextAnchor.MiddleLeft,
                gui.AveriaSerifBold
            );
            Button close = MakeButton("Close", RightMargin, rows[0], 96f, 30f);
            close.onClick.AddListener(Close);

            // everything below the header lives in the scrolling area
            MakeScrollArea();

            // "under the cursor" line
            cursorText = MakeText(
                "Under the cursor: —",
                LeftMargin,
                rows[1],
                430f,
                96f,
                13,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            // world layers and own display:
            // terrain / water / clouds / player / hud
            terrainToggle = MakeToggle(
                LeftMargin,
                rows[2],
                visibility.TerrainHidden,
                v =>
                {
                    visibility.SetTerrain(v);
                    Refresh();
                }
            );
            MakeText(
                "Terrain",
                LeftMargin + 36f,
                rows[2],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            waterToggle = MakeToggle(
                LeftMargin,
                rows[3],
                visibility.WaterHidden,
                v =>
                {
                    visibility.SetWater(v);
                    Refresh();
                }
            );
            MakeText(
                "Water",
                LeftMargin + 36f,
                rows[3],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            cloudsToggle = MakeToggle(
                LeftMargin,
                rows[4],
                visibility.CloudsHidden,
                v =>
                {
                    visibility.SetClouds(v);
                    Refresh();
                }
            );
            MakeText(
                "Clouds",
                LeftMargin + 36f,
                rows[4],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            playerToggle = MakeToggle(
                LeftMargin,
                rows[5],
                visibility.PlayerHidden,
                v =>
                {
                    visibility.SetPlayer(v);
                    Refresh();
                }
            );
            MakeText(
                "Player",
                LeftMargin + 36f,
                rows[5],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            hudToggle = MakeToggle(
                LeftMargin,
                rows[6],
                visibility.HudHidden,
                v =>
                {
                    visibility.SetHud(v);
                    Refresh();
                }
            );
            MakeText(
                "HUD",
                LeftMargin + 36f,
                rows[6],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            // the categories simply continue the list of options
            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                Category category = Categories.BuiltIn[i];
                float y = rows[7 + i];

                CategoryRow row = new CategoryRow
                {
                    Category = category,
                    Toggle = MakeToggle(
                        LeftMargin,
                        y,
                        visibility.IsCategoryHidden(category.Key),
                        v => OnCategoryToggle(category, v)
                    )
                };
                row.Label = MakeText(
                    category.Label,
                    LeftMargin + 36f,
                    y,
                    250f,
                    26f,
                    15,
                    BodyColor,
                    TextAnchor.MiddleLeft,
                    gui.AveriaSerif
                );
                row.Count = MakeText(
                    "0",
                    RightMargin,
                    y,
                    120f,
                    26f,
                    14,
                    BodyColor,
                    TextAnchor.MiddleRight,
                    gui.AveriaSerif,
                    true
                );
                categoryRows.Add(row);
            }

            // the hidden objects counter, then the three global buttons
            int counter = 7 + Categories.BuiltIn.Length;
            int actions = counter + 1;
            int helpRow = actions + 1;

            objectsText = MakeText(
                "Hidden objects: 0",
                LeftMargin,
                rows[counter],
                230f,
                30f,
                14,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            // left to right: the reveal toggle, then the two global buttons
            revealButton = MakeButton("Reveal", RightMargin - 200f, rows[actions], 90f, 30f);
            revealButton.onClick.AddListener(OnToggleReveal);

            Button hideAll = MakeButton("Hide all", RightMargin - 100f, rows[actions], 90f, 30f);
            hideAll.onClick.AddListener(OnHideAll);

            Button restoreAll = MakeButton("Show all", RightMargin, rows[actions], 90f, 30f);
            restoreAll.onClick.AddListener(OnRestoreAll);

            // help text
            string help = PluginHelpText();
            MakeText(
                help,
                LeftMargin,
                rows[helpRow],
                430f,
                100f,
                13,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            container = null;
            return true;
        }

        /// <summary>
        /// Builds the scrolling area: a clipped viewport under the header,
        /// the content as high as the rows need, and the scrollbar in the
        /// right margin of the panel.
        /// </summary>
        private void MakeScrollArea()
        {
            float viewportHeight = GetViewportHeight(builtHeight);

            GameObject viewportGo = new GameObject(
                "XRaysViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect)
            );
            viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(root.transform, false);
            viewportRect.anchorMin = new Vector2(0.5f, 1f);
            viewportRect.anchorMax = new Vector2(0.5f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.anchoredPosition = new Vector2(0f, -headerHeight);
            viewportRect.sizeDelta = new Vector2(PanelWidth, viewportHeight);

            // invisible: it only catches the mouse wheel and the drags that
            // do not start on one of the rows
            Image veil = viewportGo.GetComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0f);
            veil.raycastTarget = true;

            // the content is as high as the rows need, never less than the
            // viewport (a shorter one would be pushed to the bottom)
            contentRect = MakeContentRect(viewportHeight);

            ScrollRect scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 30f;
            scroll.verticalNormalizedPosition = 1f;

            Scrollbar bar = MakeScrollbar(viewportHeight);
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalScrollbarSpacing = 4f;

            container = contentRect;
        }

        /// <summary>Top centred container the rows are created in.</summary>
        private RectTransform MakeContentRect(float viewportHeight)
        {
            GameObject go = new GameObject("XRaysContent", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(viewportRect, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(PanelWidth, Mathf.Max(contentHeight, viewportHeight));
            return rect;
        }

        /// <summary>Hand made scrollbar, matching the panel colors.</summary>
        private Scrollbar MakeScrollbar(float viewportHeight)
        {
            GameObject trackGo = new GameObject(
                "XRaysScrollbar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Scrollbar)
            );
            scrollbarRect = (RectTransform)trackGo.transform;
            scrollbarRect.SetParent(root.transform, false);
            scrollbarRect.anchorMin = new Vector2(1f, 1f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(
                -ScrollbarInset,
                -(headerHeight + viewportHeight / 2f)
            );
            scrollbarRect.sizeDelta = new Vector2(ScrollbarWidth, viewportHeight);

            Image track = trackGo.GetComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.4f);

            GameObject handleGo = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform handle = (RectTransform)handleGo.transform;
            handle.SetParent(scrollbarRect, false);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(1f, 1f);
            handle.offsetMin = new Vector2(1f, 2f);
            handle.offsetMax = new Vector2(-1f, -2f);

            Image handleImage = handleGo.GetComponent<Image>();
            handleImage.color = BodyColor;

            Scrollbar scrollbar = trackGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // the stock color block would paint the handle in plain white
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = BodyColor;
            colors.highlightedColor = new Color(1f, 0.96f, 0.88f, 1f);
            colors.pressedColor = new Color(0.76f, 0.70f, 0.60f, 1f);
            colors.selectedColor = BodyColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            colors.fadeDuration = 0.1f;
            scrollbar.colors = colors;

            return scrollbar;
        }

        /// <summary>Height of the scrolling area for a given panel height.</summary>
        private float GetViewportHeight(float panelHeight)
        {
            return Mathf.Max(MinViewportHeight, panelHeight - headerHeight - BottomPadding);
        }

        /// <summary>
        /// Applies a new value of the "Panel height" option without
        /// rebuilding anything: only the three rectangles are resized.
        /// </summary>
        private void ApplyHeight(float height)
        {
            builtHeight = height;
            float viewportHeight = GetViewportHeight(height);

            RectTransform panel = root.GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(PanelWidth, height);

            viewportRect.anchoredPosition = new Vector2(0f, -headerHeight);
            viewportRect.sizeDelta = new Vector2(PanelWidth, viewportHeight);
            contentRect.sizeDelta = new Vector2(
                PanelWidth,
                Mathf.Max(contentHeight, viewportHeight)
            );

            scrollbarRect.anchoredPosition = new Vector2(
                -ScrollbarInset,
                -(headerHeight + viewportHeight / 2f)
            );
            scrollbarRect.sizeDelta = new Vector2(ScrollbarWidth, viewportHeight);
        }

        private string PluginHelpText()
        {
            return XRays.Instance.KeyPanel.Value
                + " : panel   ·   "
                + XRays.Instance.KeyClose.Value
                + " : close   ·   "
                + XRays.Instance.KeyHud.Value
                + " : hud\n"
                + XRays.Instance.KeyTerrain.Value
                + " : terrain   ·   "
                + XRays.Instance.KeyWater.Value
                + " : water   ·   "
                + XRays.Instance.KeyClouds.Value
                + " : clouds\n"
                + XRays.Instance.KeyPlayer.Value
                + " : player   ·   "
                + XRays.Instance.KeyReveal.Value
                + " : reveal / restore\n"
                + XRays.Instance.KeyPick.Value
                + " : hide object under cursor\n"
                + XRays.Instance.ModifierKey.Value
                + " + "
                + XRays.Instance.KeyPick.Value
                + " : whole category under the cursor";
        }

        private void OnCategoryToggle(Category category, bool hidden)
        {
            visibility.SetCategory(category, hidden);
            Refresh();
        }

        private void OnRestoreAll()
        {
            visibility.RestoreAll();
            Refresh();
        }

        private void OnToggleReveal()
        {
            visibility.ToggleChecked();
            Refresh();
        }

        /// <summary>Puts a new caption on an existing button.</summary>
        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (text != null && text.text != label)
            {
                text.text = label;
            }
        }

        private void OnHideAll()
        {
            visibility.HideAll();
            Refresh();
        }

        // ---------------------------------------------------------------- elements

        private Text MakeText(
            string content,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Font font,
            bool rightAlign = false
        )
        {
            GameObject go = GUIManager.Instance.CreateText(
                content,
                Parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                font,
                fontSize,
                color,
                true,
                Color.black,
                width,
                height,
                false
            );

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.pivot = rightAlign ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);

            Text text = go.GetComponent<Text>();
            text.alignment = alignment;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private Button MakeButton(string label, float x, float y, float width, float height)
        {
            GameObject go = GUIManager.Instance.CreateButton(
                label,
                Parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                width,
                height
            );

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);

            Button button = go.GetComponent<Button>();
            GUIManager.Instance.ApplyButtonStyle(button, 14);
            return button;
        }

        private Toggle MakeToggle(float x, float y, bool initial, System.Action<bool> onChanged)
        {
            GameObject go = GUIManager.Instance.CreateToggle(Parent, ToggleSize, ToggleSize);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(initial);
            if (onChanged != null)
            {
                toggle.onValueChanged.AddListener(value => onChanged(value));
            }

            return toggle;
        }
    }
}
