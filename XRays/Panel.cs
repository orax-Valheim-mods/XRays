using System.Collections.Generic;
using BepInEx.Configuration;
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

        /// <summary>One option of the compact world layer line.</summary>
        private class LayerOption
        {
            public Toggle Toggle;
            public Text Label;
            public float Width; // toggle, gap and label together
        }

        private const float PanelWidth = 470f;
        private const float LeftMargin = -PanelWidth / 2f + 20f;
        private const float RightMargin = PanelWidth / 2f - 20f;
        private const float ToggleSize = 26f;
        private const float RefreshInterval = 0.4f;
        private const float CountInterval = 1.5f;

        /// <summary>What the "Remember this setup" row describes itself with.</summary>
        private const string RememberTip =
            "Keeps the ticked options for the next launch: they are checked again when the game starts. Turning it off starts the next launch with nothing checked.";

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

        /// <summary>One compact line of world layer options.</summary>
        private const float LayerRowHeight = 30f;

        /// <summary>Gap between a toggle and its own label.</summary>
        private const float LayerLabelGap = 6f;

        /// <summary>Gap between two options of the same line.</summary>
        private const float LayerItemGap = 14f;

        /// <summary>Width the line of options has to fit in.</summary>
        private const float LayerLineWidth = RightMargin - LeftMargin;

        /// <summary>Type size of the compact line.</summary>
        private const int LayerFontSize = 14;

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
        private Toggle mistToggle;
        private Toggle playerToggle;
        private Toggle hudToggle;

        /// <summary>The "Remember this setup" row (persisted options).</summary>
        private Toggle rememberToggle;

        private Button revealButton;
        private Text cursorText;
        private Text objectsText;

        /// <summary>The little box describing the element under the mouse.</summary>
        private Tooltip tip;

        private float refreshTimer;
        private float countTimer;

        internal static readonly Color BodyColor = new Color(0.93f, 0.88f, 0.79f, 1f);

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

            if (tip != null)
            {
                tip.Reset();
            }
        }

        /// <summary>The scene changed: the panel was destroyed with the game GUI.</summary>
        public void Invalidate()
        {
            root = null;
            container = null;
            tip = null;
            viewportRect = null;
            contentRect = null;
            scrollbarRect = null;
            headerHeight = 0f;
            contentHeight = 0f;
            builtHeight = 0f;
            terrainToggle = null;
            waterToggle = null;
            cloudsToggle = null;
            mistToggle = null;
            playerToggle = null;
            hudToggle = null;
            rememberToggle = null;
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

            tip.Tick(deltaTime);
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
            mistToggle.SetIsOnWithoutNotify(visibility.MistHidden);
            playerToggle.SetIsOnWithoutNotify(visibility.PlayerHidden);
            hudToggle.SetIsOnWithoutNotify(visibility.HudHidden);

            // the config entry may have been changed from outside (config
            // manager), the row follows it
            rememberToggle.SetIsOnWithoutNotify(plugin.RememberSetup.Value);

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

            // the summary line: what is invisible right now, then what is
            // hidden without a row of its own in the list
            objectsText.text =
                "Hidden objects: "
                + visibility.HiddenRootCount
                + "    Other: "
                + visibility.OffListCount;

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
            // the title belongs to the fixed header, the rest scrolls. The
            // rows below the world layers are computed once that block has
            // measured itself, since it takes one line or two.
            float[] rows = new float[6 + Categories.DisplayOrder.Length];
            rows[0] = Row(44f); // title (header)
            headerHeight = cursor + 8f; // margins of the header, then the viewport

            // the scrolling content has its own top margin: the height of
            // the panel is an option, so it is not part of this list
            cursor = 6f;

            // under the cursor: the text wraps over several lines (element,
            // game types, hit, coordinates), so it gets a whole block. The
            // block is centred on its text: four lines is what it usually
            // holds, six is the longest it can reach.
            rows[1] = Row(80f);

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
            tip = new Tooltip(root.GetComponent<RectTransform>());

            // title + close button: they belong to the header, they never
            // scroll out of sight
            MakeText(
                "XRays",
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
            tip.Attach(close.gameObject, "Closes the panel (Escape by default).");

            // everything below the header lives in the scrolling area
            MakeScrollArea();

            // "under the cursor" line
            cursorText = MakeText(
                "Under the cursor: —",
                LeftMargin,
                rows[1],
                430f,
                80f,
                13,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            // a little air between that block and the options
            cursor += 10f;
            cursor = MakeLayerOptions(gui, cursor);

            // the rows below that block, now that its height is known
            for (int i = 0; i < Categories.DisplayOrder.Length; i++)
            {
                rows[2 + i] = Row(26f);
            }

            rows[2 + Categories.DisplayOrder.Length] = Row(30f); // hidden objects
            rows[3 + Categories.DisplayOrder.Length] = Row(26f); // remembered setup
            rows[4 + Categories.DisplayOrder.Length] = Row(34f); // global buttons
            rows[5 + Categories.DisplayOrder.Length] = Row(100f); // help text (5 lines)
            contentHeight = cursor + 16f;

            // the rows are known: the scrolling content gets its height
            ApplyHeight(builtHeight);

            // the categories simply continue the list of options
            for (int i = 0; i < Categories.DisplayOrder.Length; i++)
            {
                Category category = Categories.DisplayOrder[i];
                float y = rows[2 + i];

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
                tip.Attach(row.Toggle.gameObject, category.Description);
                tip.Attach(row.Label.gameObject, category.Description);
                tip.Attach(row.Count.gameObject, category.Description);
                categoryRows.Add(row);
            }

            // the hidden objects counter, the remembered setup, then the
            // three global buttons
            int counter = 2 + Categories.DisplayOrder.Length;
            int remember = counter + 1;
            int actions = remember + 1;
            int helpRow = actions + 1;

            objectsText = MakeText(
                "Hidden objects: 0    Other: 0",
                LeftMargin,
                rows[counter],
                230f,
                30f,
                14,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );
            tip.Attach(
                objectsText.gameObject,
                "The object roots currently invisible (ticked categories, objects picked one by one, Hide all), and what is hidden without a row of its own in this list."
            );

            // the setup kept for the next launch: shaped like a category row
            // so the whole list stays one single list of options
            rememberToggle = MakeToggle(
                LeftMargin,
                rows[remember],
                plugin.RememberSetup.Value,
                OnRememberToggle
            );
            Text rememberLabel = MakeText(
                "Remember this setup for the next launch",
                LeftMargin + 36f,
                rows[remember],
                380f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );
            tip.Attach(rememberToggle.gameObject, RememberTip);
            tip.Attach(rememberLabel.gameObject, RememberTip);

            // left to right: the reveal toggle, then the two global buttons
            revealButton = MakeButton("Reveal", RightMargin - 200f, rows[actions], 90f, 30f);
            revealButton.onClick.AddListener(OnToggleReveal);
            tip.Attach(revealButton.gameObject, RevealTip);

            Button hideAll = MakeButton("Hide all", RightMargin - 100f, rows[actions], 90f, 30f);
            hideAll.onClick.AddListener(OnHideAll);
            tip.Attach(
                hideAll.gameObject,
                "Checks every option of the list at once: terrain, water, clouds, mist, character, HUD and every category. It also replaces the setup the reveal would bring back."
            );

            Button restoreAll = MakeButton("Show all", RightMargin, rows[actions], 90f, 30f);
            restoreAll.onClick.AddListener(OnRestoreAll);
            tip.Attach(
                restoreAll.gameObject,
                "Everything back on screen: every option unchecked and the objects picked one by one shown again. It also replaces the setup the reveal would bring back."
            );

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
        /// The world layer options (clouds, hud, mist, player, terrain,
        /// water), in label order: they share one compact line and drop on
        /// a second one when their labels do not all fit in the width of
        /// the panel. Returns the cursor, moved past the block.
        /// </summary>
        private float MakeLayerOptions(GUIManager gui, float top)
        {
            // created with their label and description, measured, then placed
            LayerOption[] options =
            {
                MakeLayerOption(
                    gui,
                    "Clouds",
                    "The clouds of the sky: the big planes and the horizon cylinder, the particle clouds and the distant mist clouds.",
                    visibility.CloudsHidden,
                    v =>
                    {
                        visibility.SetClouds(v);
                        Refresh();
                    }
                ),
                MakeLayerOption(
                    gui,
                    "HUD",
                    "The whole in-game HUD: health, stamina and eitr bars, hot bar, mini map, crosshair, hints and panels.",
                    visibility.HudHidden,
                    v =>
                    {
                        visibility.SetHud(v);
                        Refresh();
                    }
                ),
                MakeLayerOption(
                    gui,
                    "Mist",
                    "The mist and the fog: ground mist, weather fog, distant fog planes, ocean mist, the Mistlands mist and the distance fog of the game.",
                    visibility.MistHidden,
                    v =>
                    {
                        visibility.SetMist(v);
                        Refresh();
                    }
                ),
                MakeLayerOption(
                    gui,
                    "Player",
                    "Your own character — body, armor and held item — out of the picture. Renderers only; the camera and the colliders stay.",
                    visibility.PlayerHidden,
                    v =>
                    {
                        visibility.SetPlayer(v);
                        Refresh();
                    }
                ),
                MakeLayerOption(
                    gui,
                    "Terrain",
                    "The ground itself: the loaded zones, the sea bed and the horizon, taken away so you can see through it.",
                    visibility.TerrainHidden,
                    v =>
                    {
                        visibility.SetTerrain(v);
                        Refresh();
                    }
                ),
                MakeLayerOption(
                    gui,
                    "Water",
                    "The water: the surface of the loaded zones and the sea drawn beyond them.",
                    visibility.WaterHidden,
                    v =>
                    {
                        visibility.SetWater(v);
                        Refresh();
                    }
                )
            };

            // the light refresh keeps its hands on those six toggles, taken
            // from the array in the same order as above
            cloudsToggle = options[0].Toggle;
            hudToggle = options[1].Toggle;
            mistToggle = options[2].Toggle;
            playerToggle = options[3].Toggle;
            terrainToggle = options[4].Toggle;
            waterToggle = options[5].Toggle;

            float total = 0f;
            for (int i = 0; i < options.Length; i++)
            {
                total += options[i].Width + (i > 0 ? LayerItemGap : 0f);
            }

            // one line when everything fits, otherwise half of the options
            // per line (three then three with the six of them)
            int firstLine = total <= LayerLineWidth ? options.Length : (options.Length + 1) / 2;

            float line = -(top + LayerRowHeight / 2f);
            PlaceLayers(options, 0, firstLine, line);
            top += LayerRowHeight;

            if (firstLine < options.Length)
            {
                line = -(top + LayerRowHeight / 2f);
                PlaceLayers(options, firstLine, options.Length - firstLine, line);
                top += LayerRowHeight;
            }

            return top;
        }

        /// <summary>
        /// One toggle and its label, measured for the flow layout. Both of
        /// them carry the description given here, shown while the pointer
        /// stays on either.
        /// </summary>
        private LayerOption MakeLayerOption(
            GUIManager gui,
            string label,
            string tipText,
            bool shown,
            System.Action<bool> onChanged
        )
        {
            LayerOption option = new LayerOption
            {
                Toggle = MakeToggle(LeftMargin, 0f, shown, onChanged),
                Label = MakeText(
                    label,
                    LeftMargin,
                    0f,
                    300f,
                    26f,
                    LayerFontSize,
                    BodyColor,
                    TextAnchor.MiddleLeft,
                    gui.AveriaSerif
                )
            };

            // measured without wrapping, so the box of the label ends where
            // its words do instead of covering the next option
            HorizontalWrapMode overflow = option.Label.horizontalOverflow;
            option.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float words = option.Label.preferredWidth;
            option.Label.horizontalOverflow = overflow;
            option.Label.rectTransform.sizeDelta = new Vector2(words + 6f, 26f);

            option.Width = ToggleSize + LayerLabelGap + words;
            tip.Attach(option.Toggle.gameObject, tipText);
            tip.Attach(option.Label.gameObject, tipText);
            return option;
        }

        /// <summary>Places a run of options from left to right on one line.</summary>
        private static void PlaceLayers(LayerOption[] options, int from, int count, float y)
        {
            float x = LeftMargin;
            for (int i = from; i < from + count; i++)
            {
                LayerOption option = options[i];
                RectTransform toggleRect = (RectTransform)option.Toggle.transform;
                toggleRect.anchoredPosition = new Vector2(x, y);
                option.Label.rectTransform.anchoredPosition = new Vector2(
                    x + ToggleSize + LayerLabelGap,
                    y
                );
                x += option.Width + LayerItemGap;
            }
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

        /// <summary>
        /// What the reveal button describes itself with: it says what the
        /// next press would do, which changes with the state of the panel.
        /// </summary>
        private string RevealTip()
        {
            if (visibility.IsRevealing)
            {
                return "Brings back the setup that was on before the reveal; anything ticked while the reveal was on is kept.";
            }

            return visibility.AnyOptionChecked
                ? "Shows everything the checked options are hiding; the next press brings that exact setup back."
                : "Nothing is checked: the first press checks every option of the list instead.";
        }

        /// <summary>
        /// The help line of the panel: the hotkeys that have a key, as
        /// "key : label", three per line and the two long ones on a line of
        /// their own. A hotkey left "Not set" is left out of the line, there
        /// is no key to show for it.
        /// </summary>
        private string PluginHelpText()
        {
            List<string> lines = new List<string>();

            AddHelpLine(
                lines,
                (XRays.Instance.KeyPanel, "panel"),
                (XRays.Instance.KeyClose, "close"),
                (XRays.Instance.KeyHud, "hud")
            );
            AddHelpLine(
                lines,
                (XRays.Instance.KeyTerrain, "terrain"),
                (XRays.Instance.KeyWater, "water"),
                (XRays.Instance.KeyClouds, "clouds")
            );
            AddHelpLine(
                lines,
                (XRays.Instance.KeyMist, "mist"),
                (XRays.Instance.KeyPlayer, "player"),
                (XRays.Instance.KeyReveal, "reveal / restore")
            );
            AddHelpLine(lines, (XRays.Instance.KeyPick, "hide object under cursor"));
            AddHelpLine(lines, (XRays.Instance.KeyPickCategory, "whole category under the cursor"));

            return string.Join("\n", lines);
        }

        /// <summary>
        /// One row of the help line: "key : label" for each hotkey of that
        /// row which has a key, joined by a dot. Nothing is added when none
        /// of them has one, which keeps that row out of the text.
        /// </summary>
        private static void AddHelpLine(
            List<string> lines,
            params (ConfigEntry<KeyboardShortcut> key, string label)[] hotkeys
        )
        {
            List<string> parts = new List<string>();

            foreach ((ConfigEntry<KeyboardShortcut> key, string label) in hotkeys)
            {
                if (!key.Value.Equals(KeyboardShortcut.Empty))
                {
                    parts.Add(key.Value.ToString() + " : " + label);
                }
            }

            if (parts.Count > 0)
            {
                lines.Add(string.Join("   ·   ", parts));
            }
        }

        private void OnCategoryToggle(Category category, bool hidden)
        {
            visibility.SetCategory(category, hidden);
            Refresh();
        }

        /// <summary>
        /// Keeps (or drops) the checked options for the next launch: the row
        /// only writes the config entry, the plugin does the storing.
        /// </summary>
        private void OnRememberToggle(bool remember)
        {
            plugin.SetRememberSetup(remember);
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
