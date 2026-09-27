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
        internal ConfigEntry<KeyCode> KeyTerrain;
        internal ConfigEntry<KeyCode> KeyWater;
        internal ConfigEntry<KeyCode> KeyPick;
        internal ConfigEntry<KeyCode> ModifierKey;

        internal ConfigEntry<float> PickDistance;

        private void Awake()
        {
            Instance = this;

            KeyPanel = Config.Bind("Hotkeys", "Panel", KeyCode.F8,
                "Open / close the XRays control panel.");
            KeyTerrain = Config.Bind("Hotkeys", "Terrain", KeyCode.F7,
                "Hide / show the terrain in order to see through it.");
            KeyWater = Config.Bind("Hotkeys", "Water", KeyCode.F10,
                "Hide / show the water surface.");
            KeyPick = Config.Bind("Hotkeys", "Pick", KeyCode.F9,
                "Hide / show the object under the cursor.");
            ModifierKey = Config.Bind("Hotkeys", "Category modifier", KeyCode.LeftControl,
                "Hold together with Pick to act on the whole category.");
            PickDistance = Config.Bind("Selection", "Max distance", 100f,
                "Maximum picking distance under the cursor (in meters).");

            Visibility = new VisibilityManager();
            Panel = new Panel(this, Visibility);

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
            }

            bool consoleBlocked = Blocked();
            bool blocked = consoleBlocked || (Panel != null && Panel.IsOpen);

            if (Hotkey(KeyPanel, consoleBlocked))
            {
                Panel.Toggle();
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
            Notify(pick.Category.Label + ": " + (!wasHidden ? "hidden (" + count + ")" : "shown (" + count + ")"));
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        private PickResult Pick()
        {
            return PickService.Pick(PickDistance.Value, Visibility);
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
