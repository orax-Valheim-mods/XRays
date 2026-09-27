using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Jotunn.Managers;
using UnityEngine;

namespace XRays
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class XRays : BaseUnityPlugin
    {
        public const string PluginGUID = "orax.XRays";
        public const string PluginName = "XRays";
        public const string PluginVersion = "0.1.0";

        internal static XRays Instance;

        /// <summary>Plugin logger, exposed for the satellite classes.</summary>
        internal ManualLogSource Log => Logger;

        internal VisibilityManager Visibility { get; private set; }
        internal Panel Panel { get; private set; }

        // Hotkeys (labels shown in the panel help text)
        internal ConfigEntry<KeyCode> KeyPanel;
        internal ConfigEntry<KeyCode> KeyClose;
        internal ConfigEntry<KeyCode> KeyTerrain;
        internal ConfigEntry<KeyCode> KeyWater;
        internal ConfigEntry<KeyCode> KeyClouds;
        internal ConfigEntry<KeyCode> KeyPlayer;
        internal ConfigEntry<KeyCode> KeyHud;
        internal ConfigEntry<KeyCode> KeyPick;
        internal ConfigEntry<KeyCode> KeyReveal;
        internal ConfigEntry<KeyCode> ModifierKey;

        internal ConfigEntry<float> PickDistance;

        internal ConfigEntry<float> PanelHeight;

        internal ConfigEntry<bool> LogCursorInfo;

        /// <summary>
        /// Panel toggle: keep the checked options for the next launch (the
        /// setup itself lives in the entry below).
        /// </summary>
        internal ConfigEntry<bool> RememberSetup;

        /// <summary>The options kept for the next launch, as a list of names.</summary>
        internal ConfigEntry<string> SavedSetup;

        /// <summary>How often the "Under the cursor" info is sampled for logging.</summary>
        private const float CursorLogInterval = 0.4f;

        /// <summary>How often the remembered setup is written back (while it is on).</summary>
        private const float SetupSyncInterval = 0.5f;

        /// <summary>Names of the five world options, in panel order.</summary>
        private static readonly string[] WorldOptionNames =
        {
            "terrain",
            "water",
            "clouds",
            "player",
            "hud"
        };

        /// <summary>
        /// The game opens its own pause menu on Escape: for a short moment
        /// after the panel has been closed with that key, the menu is kept
        /// closed so the key only does what the user asked for.
        /// </summary>
        private const float MenuSuppressDuration = 0.25f;

        private float menuSuppressTimer;
        private float cursorLogTimer;
        private float setupSyncTimer;
        private string lastCursorLog;

        private void Awake()
        {
            Instance = this;

            KeyPanel = Config.Bind(
                "Hotkeys",
                "Panel",
                KeyCode.F8,
                "Open / close the XRays control panel."
            );
            KeyClose = Config.Bind(
                "Hotkeys",
                "Close",
                KeyCode.Escape,
                "Close the XRays panel (Escape by default)."
            );
            KeyTerrain = Config.Bind(
                "Hotkeys",
                "Terrain",
                KeyCode.F7,
                "Hide / show the terrain in order to see through it."
            );
            KeyWater = Config.Bind(
                "Hotkeys",
                "Water",
                KeyCode.F10,
                "Hide / show the water surface."
            );
            KeyClouds = Config.Bind(
                "Hotkeys",
                "Clouds",
                KeyCode.F6,
                "Hide / show the clouds in the sky."
            );
            KeyPlayer = Config.Bind(
                "Hotkeys",
                "Player",
                KeyCode.F4,
                "Hide / show your own character (body, armor and held item). The other players belong to the Creatures category."
            );
            KeyHud = Config.Bind(
                "Hotkeys",
                "HUD",
                KeyCode.F3,
                "Hide / show the HUD (bars, hot bar, mini map, crosshair, hints). The game's own Ctrl+F3 toggle does the same thing."
            );
            KeyPick = Config.Bind(
                "Hotkeys",
                "Pick",
                KeyCode.F9,
                "Hide / show the object under the cursor."
            );
            KeyReveal = Config.Bind(
                "Hotkeys",
                "Reveal",
                KeyCode.F2,
                "Show every checked option at once, then bring that setup back on the next press. With nothing checked it checks them all."
            );
            ModifierKey = Config.Bind(
                "Hotkeys",
                "Category modifier",
                KeyCode.LeftControl,
                "Hold together with Pick to act on the whole category."
            );
            PickDistance = Config.Bind(
                "Selection",
                "Max distance",
                100f,
                "Maximum picking distance under the cursor (in meters)."
            );
            PanelHeight = Config.Bind(
                "Display",
                "Panel height",
                640f,
                new ConfigDescription(
                    "Vertical size of the control panel in pixels. The rows scroll under the title when they do not fit in it.",
                    new AcceptableValueRange<float>(360f, 1200f)
                )
            );
            LogCursorInfo = Config.Bind(
                "Logging",
                "Log cursor info",
                false,
                "Write the \"Under the cursor\" info (the exact text shown in the panel) to the BepInEx log while the panel is open, whenever it changes."
            );
            RememberSetup = Config.Bind(
                "Setup",
                "Remember setup",
                false,
                "Keep the options checked in the panel and check them again the next time the game starts. The \"Remember this setup\" toggle of the panel does the same thing."
            );
            SavedSetup = Config.Bind(
                "Setup",
                "Saved setup",
                "",
                "The options kept for the next launch, written as their names (terrain, water, clouds, player, hud and the category keys, comma separated). Filled in by the panel: edited by hand only to force a setup."
            );

            Visibility = new VisibilityManager();
            Panel = new Panel(this, Visibility);

            RestoreRememberedSetup();

            GUIManager.OnCustomGUIAvailable += OnCustomGuiAvailable;

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            GUIManager.OnCustomGUIAvailable -= OnCustomGuiAvailable;

            if (Panel != null && Panel.IsOpen)
            {
                Panel.Close();
            }

            if (Visibility != null)
            {
                Visibility.RestoreAll();
            }

            Instance = null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (Visibility != null)
            {
                Visibility.Tick(deltaTime);
                SyncRememberedSetup(deltaTime);
            }

            bool consoleBlocked = Blocked();
            bool blocked = consoleBlocked || (Panel != null && Panel.IsOpen);

            if (Hotkey(KeyPanel, consoleBlocked))
            {
                Panel.Toggle();
            }

            // closing the panel with Escape. The game reads that very key in
            // its own Menu.Update, which can run before this one: it then
            // shows its pause menu first, which makes Menu.IsVisible block
            // the hotkey above. The pause menu appearing while the panel is
            // on screen is therefore also taken for a press of Escape, and
            // it is put back right away.
            if (Panel != null && Panel.IsOpen)
            {
                if (PauseMenuVisible())
                {
                    Panel.Close();
                    Menu.instance.Hide();
                    menuSuppressTimer = MenuSuppressDuration;
                }
                else if (Hotkey(KeyClose, consoleBlocked))
                {
                    Panel.Close();
                    menuSuppressTimer = MenuSuppressDuration;
                }
            }

            // the game menu may be shown after this Update (same frame)
            if (menuSuppressTimer > 0f)
            {
                menuSuppressTimer -= deltaTime;
                if (PauseMenuVisible())
                {
                    Menu.instance.Hide();
                }
            }

            if (Panel != null)
            {
                Panel.Tick(deltaTime);

                if (!blocked)
                {
                    if (Hotkey(KeyTerrain, blocked))
                    {
                        ToggleTerrain();
                    }

                    if (Hotkey(KeyWater, blocked))
                    {
                        ToggleWater();
                    }

                    if (Hotkey(KeyClouds, blocked))
                    {
                        ToggleClouds();
                    }

                    if (Hotkey(KeyPlayer, blocked))
                    {
                        TogglePlayer();
                    }

                    if (Hotkey(KeyHud, blocked))
                    {
                        ToggleHud();
                    }

                    if (Hotkey(KeyReveal, blocked))
                    {
                        ToggleReveal();
                    }

                    if (Hotkey(KeyPick, blocked))
                    {
                        if (ModifierHeld())
                        {
                            ToggleCategoryUnderCursor();
                        }
                        else
                        {
                            ToggleObjectUnderCursor();
                        }
                    }
                }
            }

            // logged last so it reflects the panel state of this very frame
            if (Visibility != null)
            {
                TickCursorLog(deltaTime);
            }
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        internal void ToggleTerrain()
        {
            Visibility.ToggleTerrain();
            Notify(Visibility.TerrainHidden ? "Terrain hidden" : "Terrain shown");
        }

        internal void ToggleWater()
        {
            Visibility.ToggleWater();
            Notify(Visibility.WaterHidden ? "Water hidden" : "Water shown");
        }

        internal void ToggleClouds()
        {
            Visibility.ToggleClouds();
            Notify(Visibility.CloudsHidden ? "Clouds hidden" : "Clouds shown");
        }

        internal void TogglePlayer()
        {
            Visibility.TogglePlayer();
            Notify(Visibility.PlayerHidden ? "Player hidden" : "Player shown");
        }

        internal void ToggleHud()
        {
            Visibility.ToggleHud();
            Notify(Visibility.HudHidden ? "HUD hidden" : "HUD shown");
        }

        internal void ToggleReveal()
        {
            bool wasRevealing = Visibility.IsRevealing;
            bool wasChecked = Visibility.AnyOptionChecked;

            Visibility.ToggleChecked();

            if (wasRevealing)
            {
                Notify("Options restored");
            }
            else
            {
                Notify(wasChecked ? "All options shown" : "All options hidden");
            }
        }

        internal void ToggleObjectUnderCursor()
        {
            PickResult pick = Pick();
            if (pick == null)
            {
                Notify("Nothing under the cursor");
                return;
            }

            if (pick.Kind == PickKind.Terrain)
            {
                ToggleTerrain();
                return;
            }

            if (pick.Kind == PickKind.Water)
            {
                ToggleWater();
                return;
            }

            if (pick.Root == null)
            {
                Notify("Nothing to hide on this object");
                return;
            }

            bool hidden = Visibility.ToggleObject(pick.Root);
            Notify(pick.Name + ": " + (hidden ? "hidden" : "shown"));
        }

        internal void ToggleCategoryUnderCursor()
        {
            PickResult pick = Pick();
            if (pick == null)
            {
                Notify("Nothing under the cursor");
                return;
            }

            if (pick.Kind == PickKind.Terrain)
            {
                ToggleTerrain();
                return;
            }

            if (pick.Kind == PickKind.Water)
            {
                ToggleWater();
                return;
            }

            if (pick.Category == null)
            {
                Notify("Unknown category for " + pick.Name);
                return;
            }

            bool wasHidden = Visibility.IsCategoryHidden(pick.Category.Key);
            int count = Visibility.SetCategory(pick.Category, !wasHidden);
            Notify(
                pick.Category.Label
                    + ": "
                    + (!wasHidden ? "hidden (" + count + ")" : "shown (" + count + ")")
            );
        }

        // ------------------------------------------------------------------
        // Remembered setup
        // ------------------------------------------------------------------

        /// <summary>
        /// Called by the panel toggle (and by the config entry it mirrors):
        /// while it is on, the options checked in the panel are written back
        /// to the config so the next launch starts from them. Turning it on
        /// stores the setup of the moment right away, so leaving the game
        /// just after that keeps it too.
        /// </summary>
        internal void SetRememberSetup(bool remember)
        {
            RememberSetup.Value = remember;

            if (remember)
            {
                StoreRememberedSetup();
            }
        }

        /// <summary>
        /// Puts the stored options back at startup, when the toggle was left
        /// on. The state is rebuilt name by name, so the options and the
        /// categories that did not exist when it was stored simply start
        /// unchecked, and the ones that were removed are dropped.
        /// </summary>
        private void RestoreRememberedSetup()
        {
            if (!RememberSetup.Value || string.IsNullOrEmpty(SavedSetup.Value))
            {
                return;
            }

            Visibility.ApplyOptions(DeserializeSetup(SavedSetup.Value));
            Logger.LogInfo("Remembered setup restored: " + SavedSetup.Value);
        }

        /// <summary>
        /// Keeps the stored setup in step with the panel while the toggle is
        /// on. The state is sampled rather than written on every change, and
        /// only when the list of names really differs: BepInEx writes the
        /// config file itself as soon as an entry gets a new value, so a
        /// quiet option never touches the disk.
        /// </summary>
        private void SyncRememberedSetup(float deltaTime)
        {
            if (!RememberSetup.Value)
            {
                setupSyncTimer = 0f;
                return;
            }

            setupSyncTimer += deltaTime;
            if (setupSyncTimer < SetupSyncInterval)
            {
                return;
            }

            setupSyncTimer = 0f;
            StoreRememberedSetup();
        }

        private void StoreRememberedSetup()
        {
            string value = SerializeSetup();
            if (!string.Equals(value, SavedSetup.Value, StringComparison.Ordinal))
            {
                SavedSetup.Value = value;
            }
        }

        /// <summary>The checked options, as the names the config stores.</summary>
        private string SerializeSetup()
        {
            bool[] state = Visibility.CaptureOptions();
            List<string> names = new List<string>(state.Length);

            for (int i = 0; i < state.Length; i++)
            {
                if (!state[i])
                {
                    continue;
                }

                string name = OptionNameAt(i);
                if (name != null)
                {
                    names.Add(name);
                }
            }

            return string.Join(",", names.ToArray());
        }

        /// <summary>
        /// Builds the option state back from those names: the five world
        /// flags first, then one slot per built-in category. A name that no
        /// longer means anything here is ignored, a category without a name
        /// in the list starts unchecked.
        /// </summary>
        private bool[] DeserializeSetup(string value)
        {
            bool[] state = new bool[VisibilityManager.WorldOptionCount + Categories.BuiltIn.Length];

            if (string.IsNullOrEmpty(value))
            {
                return state;
            }

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in value.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }

            for (int i = 0; i < state.Length; i++)
            {
                string name = OptionNameAt(i);
                state[i] = name != null && names.Contains(name);
            }

            return state;
        }

        /// <summary>
        /// Name of the option at that slot (the world flags, then the
        /// categories in panel order), null when the slot does not exist.
        /// </summary>
        private string OptionNameAt(int index)
        {
            if (index < VisibilityManager.WorldOptionCount)
            {
                return index < WorldOptionNames.Length ? WorldOptionNames[index] : null;
            }

            int category = index - VisibilityManager.WorldOptionCount;
            return category < Categories.BuiltIn.Length ? Categories.BuiltIn[category].Key : null;
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        private PickResult Pick()
        {
            return PickService.Pick(PickDistance.Value, Visibility);
        }

        /// <summary>
        /// Optional logging of the "Under the cursor" info: the exact text the
        /// panel is showing, sampled periodically and written only when it
        /// changes so the log stays readable. Nothing is ever written while
        /// the panel is closed.
        /// </summary>
        private void TickCursorLog(float deltaTime)
        {
            // the displayed text is null unless the panel is on screen
            string displayed = Panel != null && Panel.IsVisible ? Panel.CursorText : null;

            if (LogCursorInfo == null || !LogCursorInfo.Value || displayed == null)
            {
                // reset so the current target is logged again when logging resumes
                lastCursorLog = null;
                cursorLogTimer = 0f;
                return;
            }

            cursorLogTimer += deltaTime;
            if (cursorLogTimer < CursorLogInterval)
            {
                return;
            }

            cursorLogTimer = 0f;

            if (displayed == lastCursorLog)
            {
                return;
            }

            lastCursorLog = displayed;
            Logger.LogInfo(displayed.Replace("\n", "  |  "));
        }

        private bool ModifierHeld()
        {
            return ModifierKey.Value != KeyCode.None && Input.GetKey(ModifierKey.Value);
        }

        private static bool Hotkey(ConfigEntry<KeyCode> entry, bool blocked)
        {
            if (entry == null || entry.Value == KeyCode.None || blocked)
            {
                return false;
            }

            return Input.GetKeyDown(entry.Value);
        }

        private static bool Blocked()
        {
            if (Console.IsVisible() || Menu.IsVisible())
            {
                return true;
            }

            if (Chat.instance != null && Chat.instance.HasFocus())
            {
                return true;
            }

            Player player = Player.m_localPlayer;
            return player != null && player.IsDead();
        }

        /// <summary>
        /// The pause menu is really on screen. Menu.IsVisible also returns
        /// true during the frames following a popup, which has nothing to do
        /// with the menu itself.
        /// </summary>
        private static bool PauseMenuVisible()
        {
            return Menu.IsVisible() && !UnifiedPopup.WasVisibleThisFrame();
        }

        private void Notify(string text)
        {
            Logger.LogInfo(text);

            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft, text, 0, null, false);
            }
        }

        /// <summary>
        /// Jötunn (re)creates its in-game GUI: the existing panel is no longer
        /// part of the hierarchy, so it will be rebuilt on the next open.
        /// </summary>
        private void OnCustomGuiAvailable()
        {
            if (Panel != null)
            {
                Panel.Invalidate();
            }
        }
    }
}
