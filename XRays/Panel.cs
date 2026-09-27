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

        private readonly XRays plugin;
        private readonly VisibilityManager visibility;
        private readonly List<CategoryRow> categoryRows = new List<CategoryRow>();

        private GameObject root;
        private Toggle terrainToggle;
        private Toggle waterToggle;
        private Toggle cloudsToggle;
        private Toggle playerToggle;
        private Toggle hudToggle;
        private Text cursorText;
        private Text objectsText;
        private float refreshTimer;
        private float countTimer;

        private Color BodyColor => new Color(0.93f, 0.88f, 0.79f, 1f);

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
            terrainToggle = null;
            waterToggle = null;
            cloudsToggle = null;
            playerToggle = null;
            hudToggle = null;
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

            terrainToggle.SetIsOnWithoutNotify(visibility.TerrainHidden);
            waterToggle.SetIsOnWithoutNotify(visibility.WaterHidden);
            cloudsToggle.SetIsOnWithoutNotify(visibility.CloudsHidden);
            playerToggle.SetIsOnWithoutNotify(visibility.PlayerHidden);
            hudToggle.SetIsOnWithoutNotify(visibility.HudHidden);

            foreach (CategoryRow row in categoryRows)
            {
                row.Toggle.SetIsOnWithoutNotify(visibility.IsCategoryHidden(row.Category.Key));
            }

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

            // pre-compute the panel layout
            float[] rows = new float[10 + Categories.BuiltIn.Length];
            rows[0] = Row(44f); // title

            // under the cursor: the text wraps over several lines (element,
            // game types, hit, coordinates), so it gets a whole extra line
            rows[1] = Row(96f);
            rows[2] = Row(30f); // terrain
            rows[3] = Row(30f); // water
            rows[4] = Row(30f); // clouds
            rows[5] = Row(30f); // player
            rows[6] = Row(30f); // hud
            rows[7] = Row(26f); // section header
            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                rows[8 + i] = Row(26f);
            }

            rows[8 + Categories.BuiltIn.Length] = Row(34f); // hidden objects
            rows[9 + Categories.BuiltIn.Length] = Row(82f); // help text (4 lines)
            float panelHeight = cursor + 16f;

            root = gui.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(280f, 0f),
                PanelWidth,
                panelHeight,
                true
            );

            if (root == null)
            {
                plugin.Log.LogError("XRays panel: woodpanel creation failed");
                return false;
            }

            categoryRows.Clear();

            // title + close button
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
                "Hide terrain",
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
                "Hide water",
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
                "Hide clouds",
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
                "Hide player",
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
                "Hide HUD",
                LeftMargin + 36f,
                rows[6],
                300f,
                26f,
                15,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            // categories section
            MakeText(
                "Categories (hide / show)",
                LeftMargin,
                rows[7],
                350f,
                24f,
                16,
                gui.ValheimOrange,
                TextAnchor.MiddleLeft,
                gui.AveriaSerifBold
            );

            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                Category category = Categories.BuiltIn[i];
                float y = rows[8 + i];

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

            // hidden objects + the two global buttons on its right
            int last = 8 + Categories.BuiltIn.Length;
            objectsText = MakeText(
                "Hidden objects: 0",
                LeftMargin,
                rows[last],
                230f,
                30f,
                14,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            Button hideAll = MakeButton("Hide all", RightMargin - 100f, rows[last], 90f, 30f);
            hideAll.onClick.AddListener(OnHideAll);

            Button restoreAll = MakeButton("Show all", RightMargin, rows[last], 90f, 30f);
            restoreAll.onClick.AddListener(OnRestoreAll);

            // help text
            string help = PluginHelpText();
            MakeText(
                help,
                LeftMargin,
                rows[last + 1],
                430f,
                80f,
                13,
                BodyColor,
                TextAnchor.MiddleLeft,
                gui.AveriaSerif
            );

            return true;
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
                root.transform,
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
                root.transform,
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
            GameObject go = GUIManager.Instance.CreateToggle(
                root.transform,
                ToggleSize,
                ToggleSize
            );

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
