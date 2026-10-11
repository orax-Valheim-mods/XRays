using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XRays
{
    /// <summary>
    /// The XRays line of the pause menu: one more entry in the list Escape draws, opening
    /// the panel the hotkey opens. The "General" option "Enable" decides whether the line
    /// is there at all: a disabled mod runs no code able to open the panel, so it shows
    /// no line either.
    ///
    /// The entry is a clone of the game's own Settings button - same look, same layout,
    /// same place in the list (right after it) - relabelled, and given the click that
    /// opens the panel. That click is not added to the one the clone was born with: a
    /// button carries the listeners its prefab wired into it, and RemoveAllListeners
    /// leaves those alone, so the whole UnityEvent is thrown away and a new one put in
    /// its place. Without that, the entry would open Settings behind the panel.
    ///
    /// The option is read every time the pause menu opens, and applied at once when it
    /// changes (the handler bound to the config entry): a line the option takes away is
    /// destroyed rather than switched off, and the gamepad chain is rewound first - Unity
    /// follows an explicit selectOnDown whether the button it points at still exists or
    /// not, so a D-pad sent to a hidden entry would simply go nowhere.
    /// </summary>
    internal static class PauseMenuEntry
    {
        /// <summary>What the line says, in English like every label of this mod.</summary>
        private const string EntryLabel = "XRays";

        /// <summary>What the clone is called, so it is never mistaken for a line of the game.</summary>
        private const string EntryName = "XRaysEntry";

        /// <summary>The clone on screen: null until the first opening, and after a scene change.</summary>
        private static Button entryButton;

        /// <summary>The menu the clone lives in, kept for the removals and the click.</summary>
        private static Menu pauseMenu;

        /// <summary>
        /// What Settings pointed down to while the entry is not in the gamepad's chain: the
        /// link the game itself wrote, kept so that taking the entry out can put it back.
        /// </summary>
        private static Selectable belowSettings;

        /// <summary>True once something went wrong building the entry: the log is then silent.</summary>
        private static bool failed;

        /// <summary>
        /// Makes sure the pause menu that was just opened holds the entry the mod asks
        /// for, and that the gamepad's up / down chain runs through it. Called from the
        /// postfix on Menu.Show, which runs once per opening: no polling anywhere.
        /// </summary>
        internal static void Add(Menu menu)
        {
            try
            {
                pauseMenu = menu;

                XRays plugin = XRays.Instance;
                if (plugin == null)
                {
                    // the plugin is going away: the patch itself leaves with it
                    return;
                }

                if (!plugin.Enable.Value)
                {
                    Forget("Enable is off");
                    return;
                }

                bool known = entryButton != null;
                if (!known)
                {
                    entryButton = Build(menu);
                }

                if (entryButton != null)
                {
                    WireNavigation();

                    if (known)
                    {
                        // an entry left from a previous opening: what the menu did to it since
                        plugin.Log.LogInfo(
                            "Pause menu entry: reused - active "
                                + entryButton.gameObject.activeInHierarchy
                                + ", interactable "
                                + entryButton.interactable
                                + "."
                        );
                    }
                }
            }
            catch (Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    XRays.Instance?.Log.LogError("Pause menu entry: " + e);
                }
            }
        }

        /// <summary>
        /// The option moved (ConfigurationManager, the .cfg by hand): the line follows
        /// it at once, which is one build or one removal. Nothing to do before the first
        /// Show - it reads the option itself, and there is no menu to draw in yet.
        /// </summary>
        internal static void OnSettingChanged()
        {
            Menu menu = pauseMenu;
            if (menu != null)
            {
                Add(menu);
            }
        }

        /// <summary>
        /// Puts the entry into the gamepad's chain, right after Settings - the place it
        /// has in the list. The menu rewires its own buttons every time it opens
        /// (Menu.UpdateNavigation, which knows nothing of this one), so the two links it
        /// breaks are set again rather than set once: after every Show (this call) and
        /// after that method too (its own postfix). Rewiring is idempotent - the chain
        /// that already holds the entry is left as it is.
        /// </summary>
        internal static void WireNavigation()
        {
            Button current = entryButton;
            if (current == null)
            {
                // the world changed and took the entry with it: the next Show rebuilds
                return;
            }

            Button settings = SettingsButton();
            if (settings == null || settings.navigation.selectOnDown == current)
            {
                // no Settings to stand after, or the chain already holds the entry
                return;
            }

            belowSettings = settings.navigation.selectOnDown;

            Navigation here = current.navigation;
            here.mode = Navigation.Mode.Explicit;
            here.selectOnUp = settings;
            here.selectOnDown = belowSettings;
            current.navigation = here;

            Navigation after = settings.navigation;
            after.selectOnDown = current;
            settings.navigation = after;
        }

        /// <summary>The click: the pause menu goes, then the panel opens as the hotkey opens it.</summary>
        private static void OnClicked()
        {
            XRays plugin = XRays.Instance;
            plugin?.Log.LogInfo("Pause menu entry: clicked.");

            if (plugin == null || !plugin.Enable.Value || plugin.Panel == null)
            {
                plugin?.Log.LogWarning(
                    "Pause menu entry: click ignored - instance "
                        + (XRays.Instance != null)
                        + ", Enable "
                        + (plugin != null && plugin.Enable.Value)
                        + ", panel "
                        + (plugin != null && plugin.Panel != null)
                        + "."
                );
                return;
            }

            if (pauseMenu != null)
            {
                // through the game's own hand: unpause and the cursor back, so the
                // panel starts on a running game like it does on the hotkey press
                pauseMenu.Hide();
                plugin.Log.LogInfo("Pause menu entry: pause menu hidden.");
            }
            else
            {
                plugin.Log.LogWarning("Pause menu entry: no menu to hide.");
            }

            plugin.Panel.Open();
            plugin.Log.LogInfo("Pause menu entry: panel open = " + plugin.Panel.IsOpen + ".");
        }

        /// <summary>One clone of the game's own Settings button, or null (and one log line).</summary>
        private static Button Build(Menu menu)
        {
            Button settings = menu.m_settingsButton;
            Transform parent = settings != null ? settings.transform.parent : null;
            if (settings == null || parent == null)
            {
                return Fail("Menu carries no Settings button to take the shape of.");
            }

            GameObject entry = UnityEngine.Object.Instantiate(settings.gameObject, parent);
            entry.name = EntryName;
            entry.SetActive(true); // Settings is hidden in some modes: the entry is not Settings

            Button created = entry.GetComponent<Button>();
            if (created == null)
            {
                UnityEngine.Object.Destroy(entry);
                return Fail("The button taken as a shape carries no Button.");
            }

            created.onClick = new Button.ButtonClickedEvent(); // the prefab's own clicks go with it
            created.onClick.AddListener(OnClicked);
            created.interactable = true;
            SetLabel(entry);

            // right after Settings, which is the place the chain above is wired for as
            // well: the list is laid out by sibling index
            entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

            try
            {
                entry.AddComponent<Probe>();
            }
            catch (Exception e)
            {
                XRays.Instance?.Log.LogWarning("Pause menu entry: probe refused: " + e.Message);
            }

            XRays.Instance?.Log.LogInfo("Pause menu: entry added (" + EntryLabel + ").");
            return created;
        }

        /// <summary>
        /// Takes the line OUT of the pause menu: its two links leave the gamepad's chain
        /// first (a link to a destroyed button is still followed, and a D-pad then goes
        /// nowhere), then the clone itself. `why` only says in the log which of the two
        /// callers brought it here.
        /// </summary>
        private static void Forget(string why)
        {
            Button gone = entryButton;
            entryButton = null;
            if (gone == null)
            {
                // nothing on screen: the option and this copy have nothing to undo
                return;
            }

            Button settings = SettingsButton();
            if (settings != null && settings.navigation.selectOnDown == gone)
            {
                Navigation after = settings.navigation;
                after.selectOnDown = belowSettings;
                settings.navigation = after;
            }

            Navigation here = gone.navigation;
            here.selectOnUp = null;
            here.selectOnDown = null;
            gone.navigation = here;

            XRays.Instance?.Log.LogInfo("Pause menu: entry removed (" + why + ").");
            UnityEngine.Object.Destroy(gone.gameObject);
            belowSettings = null;
        }

        /// <summary>
        /// The plugin is unloading (ScriptEngine reloads it into a NEW assembly, statics
        /// and all): the line has to go with this copy, or the menu would keep a button
        /// nobody owns - nothing in the game ever destroys it - and the copy loaded next
        /// would draw a second one right beside it, because its own entryButton is null
        /// and it knows nothing of the one already standing there. Called from
        /// XRays.OnDestroy, before the instance goes with the logger.
        /// </summary>
        internal static void Cleanup()
        {
            try
            {
                Forget("plugin unloaded");
            }
            catch (Exception e)
            {
                XRays.Instance?.Log.LogError("Pause menu cleanup: " + e);
            }
        }

        /// <summary>The Settings line of the open menu, the shape the entry clones.</summary>
        private static Button SettingsButton()
        {
            return pauseMenu != null ? pauseMenu.m_settingsButton : null;
        }

        /// <summary>
        /// The word on the entry. The game's localizer walks both kinds of text it has
        /// (Text and TMP_Text alike, every thirty frames while the menu is on screen), so
        /// both are answered here: whichever the button really carries takes the word, and
        /// the one it does not carry is simply not found. The word has no "$" in it, so
        /// the localizer leaves it alone - it only ever translates what it is given as a
        /// key.
        /// </summary>
        private static void SetLabel(GameObject entry)
        {
            TMP_Text mesh = entry.GetComponentInChildren<TMP_Text>(true);
            if (mesh != null && !string.Equals(mesh.text, EntryLabel, StringComparison.Ordinal))
            {
                mesh.text = EntryLabel;
            }

            Text text = entry.GetComponentInChildren<Text>(true);
            if (text != null && !string.Equals(text.text, EntryLabel, StringComparison.Ordinal))
            {
                text.text = EntryLabel;
            }
        }

        /// <summary>One line in the log, and no entry at all.</summary>
        private static Button Fail(string reason)
        {
            if (!failed)
            {
                failed = true;
                XRays.Instance?.Log.LogWarning("Pause menu: no XRays entry - " + reason);
            }

            return null;
        }

        /// <summary>
        /// One frame of diagnosis: by the time Start runs, the menu layout has had its
        /// pass, so the raycast below asks the entry's REAL place who would take the
        /// click standing there.
        /// </summary>
        private sealed class Probe : MonoBehaviour
        {
            private void Start()
            {
                Diagnose();
            }
        }

        /// <summary>Where a click at the center of the entry would land, and what state it is in.</summary>
        private static void Diagnose()
        {
            try
            {
                Button current = entryButton;
                EventSystem system = EventSystem.current;
                RectTransform rect = current != null ? current.transform as RectTransform : null;
                if (current == null || system == null || rect == null)
                {
                    XRays.Instance?.Log.LogWarning(
                        "Pause menu entry: diagnosis - button "
                            + (current != null)
                            + ", event system "
                            + (system != null)
                            + "."
                    );
                    return;
                }

                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.position);
                List<RaycastResult> hits = new List<RaycastResult>();
                system.RaycastAll(new PointerEventData(system) { position = position }, hits);

                string top = "nothing";
                for (int i = 0; i < hits.Count && i < 3; i++)
                {
                    top += (i == 0 ? " -> " : " | ") + hits[i].gameObject.name;
                }

                XRays.Instance?.Log.LogInfo(
                    "Pause menu entry: raycast at own center "
                        + top
                        + " (hits "
                        + hits.Count
                        + "); state: active "
                        + current.gameObject.activeInHierarchy
                        + ", interactable "
                        + current.interactable
                        + ", graphic "
                        + (current.targetGraphic != null && current.targetGraphic.raycastTarget)
                        + "."
                );
            }
            catch (Exception e)
            {
                XRays.Instance?.Log.LogWarning("Pause menu entry: diagnosis failed: " + e.Message);
            }
        }
    }
}
