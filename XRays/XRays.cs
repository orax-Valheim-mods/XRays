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

        /// <summary>Opens / closes the panel, in the "General hotkeys" section.</summary>
        internal ConfigEntry<KeyboardShortcut> KeyPanel;

        // Hotkeys shown in the panel help text, each one a key combination.
        internal ConfigEntry<KeyboardShortcut> KeyClose;
        internal ConfigEntry<KeyboardShortcut> KeyTerrain;
        internal ConfigEntry<KeyboardShortcut> KeyWater;
        internal ConfigEntry<KeyboardShortcut> KeyClouds;
        internal ConfigEntry<KeyboardShortcut> KeyMist;
        internal ConfigEntry<KeyboardShortcut> KeyPlayer;
        internal ConfigEntry<KeyboardShortcut> KeyHud;
        internal ConfigEntry<KeyboardShortcut> KeyPick;
        internal ConfigEntry<KeyboardShortcut> KeyPickCategory;
        internal ConfigEntry<KeyboardShortcut> KeyReveal;

        internal ConfigEntry<float> PickDistance;

        internal ConfigEntry<float> PanelHeight;

        internal ConfigEntry<bool> ShowTooltips;

        /// <summary>Seconds between two passes recomputing what must stay hidden.</summary>
        internal ConfigEntry<float> UpdateInterval;

        /// <summary>Seconds an object-name scan is kept before a new one.</summary>
        internal ConfigEntry<float> NameScanInterval;

        internal ConfigEntry<bool> LogCursorInfo;

        /// <summary>Whether the phases of the reconcile pass are timed to the log.</summary>
        internal ConfigEntry<bool> LogReconcileTiming;

        /// <summary>
        /// Panel toggle: keep the checked options for the next launch (the
        /// setup itself lives in the entry below).
        /// </summary>
        internal ConfigEntry<bool> RememberSetup;

        /// <summary>The options kept for the next launch, as a list of names.</summary>
        internal ConfigEntry<string> SavedSetup;

        /// <summary>Name of the YAML file holding the name rules of the categories.</summary>
        internal ConfigEntry<string> NameRulesFileName;

        /// <summary>Master switch: unchecked, nothing of the mod runs.</summary>
        internal ConfigEntry<bool> Enable;

        /// <summary>How often the "Under the cursor" info is sampled for logging.</summary>
        private const float CursorLogInterval = 0.4f;

        /// <summary>How often the remembered setup is written back (while it is on).</summary>
        private const float SetupSyncInterval = 0.5f;

        /// <summary>The config section the panel key, Close control panel and the pick category are listed under.</summary>
        private const string GeneralHotkeysSection = "General hotkeys";

        /// <summary>Section still read when the panel key and the pick category are bound, to carry their values over.</summary>
        private const string FormerGeneralSection = "General";

        /// <summary>The config section the plain hotkeys are listed under, in the game's config window.</summary>
        private const string HotkeySection = "Show/hide hotkeys";

        /// <summary>Section still read when the hotkeys are bound, to carry their values over.</summary>
        private const string FormerHotkeySection = "Hotkeys";

        /// <summary>Names of the world options, one per slot of the remembered setup.</summary>
        private static readonly string[] WorldOptionNames =
        {
            "terrain",
            "water",
            "clouds",
            "mist",
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

        /// <summary>Whether the mod runs: read from the option each frame, a change goes through SetModEnabled.</summary>
        private bool modEnabled;

        /// <summary>The panel options when the mod was switched off, to bring them back on.</summary>
        private bool[] disabledSetup;

        private void Awake()
        {
            Instance = this;

            // Nothing written below: the config file is saved once at the end
            // of the binds, when every entry is bound. A description is only
            // written for the entries bound at that moment, so a save in the
            // middle of the block would strip the comments of the others.
            bool saveOnConfigSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;

            const KeyCode defaultPanelKey = KeyCode.F8;
            KeyboardShortcut defaultPanelShortcut = new KeyboardShortcut(defaultPanelKey);

            KeyPanel = BindShortcut(
                GeneralHotkeysSection,
                "Open/close control panel",
                defaultPanelShortcut,
                "Open / close the XRays control panel. Click it here and press a key combination to record a new one.",
                new ConfigDefinition(GeneralHotkeysSection, "Show/hide panel"),
                new ConfigDefinition(FormerGeneralSection, "Show/hide panel"),
                new ConfigDefinition(FormerHotkeySection, "Panel")
            );

            KeyClose = BindShortcut(
                GeneralHotkeysSection,
                "Close control panel",
                new KeyboardShortcut(KeyCode.Escape),
                "Close the XRays panel (Escape by default). Click it here and press a key combination to record a new one.",
                new ConfigDefinition(GeneralHotkeysSection, "Close GUI"),
                new ConfigDefinition(HotkeySection, "Close"),
                new ConfigDefinition(FormerHotkeySection, "Close")
            );

            // No key of their own: each one starts "Not set", so a fresh
            // install presses nothing. A config file that already holds a
            // combination keeps it, only a missing entry takes the default.
            KeyTerrain = BindHotkey(
                "Terrain",
                KeyboardShortcut.Empty,
                "Hide / show the terrain in order to see through it."
            );
            KeyWater = BindHotkey(
                "Water",
                KeyboardShortcut.Empty,
                "Hide / show the water surface."
            );
            KeyClouds = BindHotkey(
                "Clouds",
                KeyboardShortcut.Empty,
                "Hide / show the clouds in the sky (planes, horizon cylinder, particle clouds)."
            );
            KeyMist = BindHotkey(
                "Mist",
                KeyboardShortcut.Empty,
                "Hide / show the mist and the fog (ground mist, weather fog, distant fog planes, ocean mist, Mistlands mist) and the distance fog of the game. The game itself already takes F1, F2, F3, F9 and F11, and F5 opens its console: record a key it does not use."
            );
            KeyPlayer = BindHotkey(
                "Player",
                KeyboardShortcut.Empty,
                "Hide / show your own character (body, armor and held item). The other players belong to the Creatures category."
            );
            KeyHud = BindHotkey(
                "HUD",
                KeyboardShortcut.Empty,
                "Hide / show the HUD (bars, hot bar, mini map, crosshair, hints). The game's own Ctrl+F3 toggle does the same thing."
            );
            KeyPick = BindHotkey(
                "Pick",
                KeyboardShortcut.Empty,
                "Hide / show the object under the cursor. Pressing it with the Pick category combination acts on the whole category."
            );
            KeyReveal = BindHotkey(
                "Reveal",
                KeyboardShortcut.Empty,
                "Show every checked option at once, then bring that setup back on the next press. With nothing checked it checks them all."
            );

            const KeyCode defaultCategoryKey = KeyCode.F9;
            const KeyCode defaultCategoryModifier = KeyCode.LeftControl;
            KeyboardShortcut defaultCategoryShortcut = new KeyboardShortcut(
                defaultCategoryKey,
                defaultCategoryModifier
            );
            KeyCode formerCategoryModifier = Config
                .Bind(
                    FormerHotkeySection,
                    "Category modifier",
                    defaultCategoryModifier,
                    "Old name of "
                        + GeneralHotkeysSection
                        + " -> Pick category, read one moment to carry its key over."
                )
                .Value;
            Config.Remove(new ConfigDefinition(FormerHotkeySection, "Category modifier"));

            KeyPickCategory = BindShortcut(
                GeneralHotkeysSection,
                "Pick category",
                defaultCategoryShortcut,
                "Hide / show the whole category of the object under the cursor: Pick pressed with that combination. Click it here and press a key combination to record a new one.",
                new ConfigDefinition(FormerGeneralSection, "Pick category"),
                new ConfigDefinition(HotkeySection, "Pick category"),
                new ConfigDefinition(FormerHotkeySection, "Pick category")
            );

            if (
                formerCategoryModifier != defaultCategoryModifier
                && KeyPickCategory.Value.Equals(defaultCategoryShortcut)
            )
            {
                KeyPickCategory.Value =
                    formerCategoryModifier == KeyCode.None
                        ? KeyboardShortcut.Empty
                        : new KeyboardShortcut(defaultCategoryKey, formerCategoryModifier);
            }

            PickDistance = Config.Bind(
                "Selection",
                "Max distance",
                100f,
                "Maximum picking distance under the cursor (in meters)."
            );
            PanelHeight = Config.Bind(
                "Display",
                "Panel height",
                750f,
                new ConfigDescription(
                    "Vertical size of the control panel in pixels. The rows scroll under the title when they do not fit in it.",
                    new AcceptableValueRange<float>(360f, 1200f)
                )
            );
            ShowTooltips = Config.Bind(
                "Display",
                "Show tooltips",
                true,
                "Show the little box describing a row of the panel while the pointer rests on it: it appears after a short delay and follows the pointer. Turning this off takes every box away."
            );
            UpdateInterval = Config.Bind(
                "Refresh",
                "Update interval",
                VisibilityManager.DefaultUpdateInterval,
                new ConfigDescription(
                    "Seconds between two passes recomputing everything that must stay hidden (the periodic sweep of the scene). A longer wait costs less CPU, and objects appearing later (a spawn, a new construction) are picked up later. The renderers below a hidden root are collected again once every ten passes.",
                    new AcceptableValueRange<float>(0.1f, 60f)
                )
            );
            NameScanInterval = Config.Bind(
                "Refresh",
                "Name scan interval",
                Categories.DefaultNameScanInterval,
                new ConfigDescription(
                    "Seconds the result of an object-name scan is kept before the scene is swept again for the name rules. A longer wait costs less CPU, and the objects a name rule catches later are picked up at the next scan.",
                    new AcceptableValueRange<float>(0.1f, 60f)
                )
            );
            LogCursorInfo = Config.Bind(
                "Logging",
                "Log cursor info",
                false,
                "Write the \"Under the cursor\" info (the exact text shown in the panel) to the BepInEx log while the panel is open, whenever it changes."
            );
            LogReconcileTiming = Config.Bind(
                "Logging",
                "Log reconcile timing",
                false,
                "Write the time spent in each phase of the periodic hiding pass to the BepInEx log: one line per second while options are hidden, with the average, the smallest and the biggest value of the passes covered. Nothing is written while this is off."
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
                "The options kept for the next launch, written as their names (terrain, water, clouds, mist, player, hud and the category keys, comma separated). Filled in by the panel: edited by hand only to force a setup."
            );
            NameRulesFileName = Config.Bind(
                "Files",
                "Name rules file",
                NameRulesFile.DefaultFileName,
                "Name of the YAML file holding the name rules of the categories. The mod looks for it in BepInEx\\plugins, beside its own assembly and in BepInEx\\scripts, and copies it into BepInEx\\config when it is missing there or written for another version of the mod: the copy of BepInEx\\config is the file read, and read again every time it changes while the game runs."
            );

            Enable = Config.Bind(
                "General",
                "Enable",
                true,
                "Unchecked, the mod stops completely: everything hidden is shown again, the panel closes, and the hotkeys and the periodic work do nothing until it is checked again. Checked back, the panel options come back as they were (the objects picked one by one and the categories taken by name are forgotten); started switched off, the mod comes back with the remembered setup instead, when \"Remember setup\" is on."
            );

            Config.SaveOnConfigSet = saveOnConfigSet;

            Config.Save();

            // the categories are read with the rules of the file from here on
            NameRulesFile.Load();

            Visibility = new VisibilityManager();
            Panel = new Panel(this, Visibility);

            modEnabled = Enable.Value;
            if (modEnabled)
            {
                RestoreRememberedSetup();
            }

            GUIManager.OnCustomGUIAvailable += OnCustomGuiAvailable;

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        /// <summary>
        /// Binds a key combination under its section, keeping the value the
        /// config file still holds for it under the sections it used to have:
        /// an entry that is not bound anymore is kept aside by BepInEx, so
        /// binding it once reads that value back, and dropping it right after
        /// takes that old line out of the file. The old locations are listed
        /// from the newest to the oldest, the first one with a value of its
        /// own passes it on, and the value only comes over while the setting
        /// being bound still has its default: a setting of its own always
        /// wins.
        /// </summary>
        private ConfigEntry<KeyboardShortcut> BindShortcut(
            string section,
            string key,
            KeyboardShortcut defaultValue,
            string description,
            params ConfigDefinition[] formerLocations
        )
        {
            KeyboardShortcut formerValue = defaultValue;

            foreach (ConfigDefinition formerLocation in formerLocations)
            {
                KeyboardShortcut value = Config
                    .Bind(
                        formerLocation.Section,
                        formerLocation.Key,
                        defaultValue,
                        "Old location of "
                            + section
                            + " -> "
                            + key
                            + ", read one moment to carry its value over."
                    )
                    .Value;
                Config.Remove(formerLocation);

                if (formerValue.Equals(defaultValue) && !value.Equals(defaultValue))
                {
                    formerValue = value;
                }
            }

            ConfigEntry<KeyboardShortcut> entry = Config.Bind(
                section,
                key,
                defaultValue,
                description
            );

            if (!formerValue.Equals(defaultValue) && entry.Value.Equals(defaultValue))
            {
                entry.Value = formerValue;
            }

            return entry;
        }

        /// <summary>
        /// Binds a plain hotkey under the hotkey section, keeping the value
        /// the config file still holds for it under the section it used to
        /// have.
        /// </summary>
        private ConfigEntry<KeyboardShortcut> BindHotkey(
            string key,
            KeyboardShortcut defaultValue,
            string description
        )
        {
            return BindShortcut(
                HotkeySection,
                key,
                defaultValue,
                description,
                new ConfigDefinition(FormerHotkeySection, key)
            );
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

            if (Enable.Value != modEnabled)
            {
                SetModEnabled(Enable.Value);
            }

            if (!modEnabled)
            {
                // switched off: not one frame of the mod runs
                return;
            }

            ReconcileTiming.Enabled = LogReconcileTiming.Value;

            if (Visibility != null)
            {
                Visibility.UpdateInterval = UpdateInterval.Value;
                Visibility.Tick(deltaTime);
                SyncRememberedSetup(deltaTime);
            }

            NameRulesFile.Tick(deltaTime);

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

                    if (Hotkey(KeyMist, blocked))
                    {
                        ToggleMist();
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

                    if (Hotkey(KeyPickCategory, blocked))
                    {
                        ToggleCategoryUnderCursor();
                    }
                    else if (Hotkey(KeyPick, blocked))
                    {
                        ToggleObjectUnderCursor();
                    }
                }
            }

            // logged last so it reflects the panel state of this very frame
            if (Visibility != null)
            {
                TickCursorLog(deltaTime);
            }
        }

        /// <summary>
        /// Switches the mod on or off with the "General" → "Enable" option.
        /// Off: the setup of the panel is put aside, everything is shown
        /// again, the panel closes and the timing window of the passes is
        /// dropped. On: the setup put aside comes back, or the remembered
        /// one when the mod was started switched off.
        /// </summary>
        private void SetModEnabled(bool enabled)
        {
            if (enabled)
            {
                if (disabledSetup != null)
                {
                    Visibility.ApplyOptions(disabledSetup);
                    disabledSetup = null;
                }
                else
                {
                    RestoreRememberedSetup();
                }

                Notify("XRays enabled");
            }
            else
            {
                disabledSetup = Visibility.CaptureOptions();

                if (Panel != null && Panel.IsOpen)
                {
                    Panel.Close();
                }

                Visibility.RestoreAll();

                // the timing window of the passes goes with them
                ReconcileTiming.Enabled = false;
                ReconcileTiming.Tick(0f);

                Notify("XRays disabled");
            }

            modEnabled = enabled;
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

        internal void ToggleMist()
        {
            Visibility.ToggleMist();
            Notify(Visibility.MistHidden ? "Mist hidden" : "Mist shown");
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
        /// Builds the option state back from those names: the world flags
        /// first, then one slot per built-in category. A name that no
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
        /// Name of the option at that slot (the world flags, then one slot
        /// per built-in category), null when the slot does not exist.
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

        // A shortcut fires for the exact combination it holds: the modifier
        // keys must be down with the main key, and any other key held at the
        // same time cancels the press — except the modifier keys themselves
        // (Shift, Ctrl, Alt), which are always free. An unset combination
        // never fires.
        private static bool Hotkey(ConfigEntry<KeyboardShortcut> entry, bool blocked)
        {
            return entry != null && !blocked && entry.Value.IsDown();
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
