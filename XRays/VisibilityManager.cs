using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRays
{
    /// <summary>
    /// Core of the mod: keeps the state (terrain, water, categories, individual
    /// objects) and applies the hiding by disabling renderers.
    ///
    /// Renderers are never destroyed: their original state (enabled /
    /// forceRenderingOff) is memorised so everything can be restored exactly
    /// as it was. The hiding is re-applied periodically to pick up newly loaded
    /// objects and to counter the LODGroups that re-enable renderers.
    /// </summary>
    internal class VisibilityManager
    {
        private struct RendererState
        {
            public bool Enabled;
            public bool ForceOff;

            public RendererState(bool enabled, bool forceOff)
            {
                Enabled = enabled;
                ForceOff = forceOff;
            }
        }

        private const float RefreshInterval = 0.75f;

        private readonly Dictionary<int, Renderer> hiddenRenderers =
            new Dictionary<int, Renderer>();
        private readonly Dictionary<int, RendererState> rendererStates =
            new Dictionary<int, RendererState>();

        /// <summary>
        /// The instanced grass is not drawn by a Renderer but by an
        /// InstanceRenderer, which is disabled the same way (its original
        /// state is memorised, then restored).
        /// </summary>
        private readonly Dictionary<int, InstanceRenderer> hiddenInstances =
            new Dictionary<int, InstanceRenderer>();
        private readonly Dictionary<int, bool> instanceStates = new Dictionary<int, bool>();

        private readonly Dictionary<int, GameObject> individualObjects =
            new Dictionary<int, GameObject>();
        private readonly HashSet<string> hiddenCategories = new HashSet<string>();
        private readonly HashSet<int> hiddenRoots = new HashSet<int>();
        private float refreshTimer;

        public bool TerrainHidden { get; private set; }
        public bool WaterHidden { get; private set; }
        public bool CloudsHidden { get; private set; }

        /// <summary>Your own character (the other players belong to the Creatures category).</summary>
        public bool PlayerHidden { get; private set; }

        public bool AnyActive =>
            TerrainHidden
            || WaterHidden
            || CloudsHidden
            || PlayerHidden
            || hiddenCategories.Count > 0
            || individualObjects.Count > 0;

        public int IndividualCount => individualObjects.Count;

        public int HiddenNameCategoryCount
        {
            get
            {
                int count = 0;
                foreach (string key in hiddenCategories)
                {
                    if (key.StartsWith(Category.NameKeyPrefix))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // ---------------------------------------------------------------- terrain / water

        public void SetTerrain(bool hidden)
        {
            if (TerrainHidden == hidden)
            {
                return;
            }

            TerrainHidden = hidden;
            Reconcile();
        }

        public void ToggleTerrain() => SetTerrain(!TerrainHidden);

        public void SetWater(bool hidden)
        {
            if (WaterHidden == hidden)
            {
                return;
            }

            WaterHidden = hidden;
            Reconcile();
        }

        public void ToggleWater() => SetWater(!WaterHidden);

        // ---------------------------------------------------------------- clouds

        public void SetClouds(bool hidden)
        {
            if (CloudsHidden == hidden)
            {
                return;
            }

            CloudsHidden = hidden;
            Reconcile();
        }

        public void ToggleClouds() => SetClouds(!CloudsHidden);

        // ---------------------------------------------------------------- player

        /// <summary>
        /// Hides your own character (body, armor and held item) so it does not
        /// block the view. Nothing else is touched: the colliders, the camera
        /// and the other players are unaffected.
        /// </summary>
        public void SetPlayer(bool hidden)
        {
            if (PlayerHidden == hidden)
            {
                return;
            }

            PlayerHidden = hidden;
            Reconcile();
        }

        public void TogglePlayer() => SetPlayer(!PlayerHidden);

        // ---------------------------------------------------------------- HUD

        /// <summary>
        /// The whole HUD (health, stamina, hot bar, mini map, crosshair,
        /// hints, ...) as one piece.
        /// </summary>
        public bool HudHidden { get; private set; }

        /// <summary>
        /// Hides or shows the HUD by deactivating the very object the game
        /// drags off screen for its own Ctrl+F3 toggle (Hud.m_rootObject).
        /// Deactivating it instead of using that toggle keeps the pause menu
        /// on screen (the game moves it along with the HUD) and leaves the
        /// rest of the HUD code running as usual.
        /// </summary>
        public void SetHud(bool hidden)
        {
            if (HudHidden == hidden)
            {
                return;
            }

            HudHidden = hidden;
            ApplyHud();
        }

        public void ToggleHud() => SetHud(!HudHidden);

        // ---------------------------------------------------------------- categories

        public bool IsCategoryHidden(string key)
        {
            return !string.IsNullOrEmpty(key) && hiddenCategories.Contains(key);
        }

        /// <summary>Hides or shows a category. Returns the number of objects involved.</summary>
        public int SetCategory(Category category, bool hidden)
        {
            if (category == null)
            {
                return 0;
            }

            Categories.Register(category);
            Categories.Invalidate(category.Key);
            int count = Categories.CollectRoots(category).Count;

            if (hidden)
            {
                hiddenCategories.Add(category.Key);
            }
            else
            {
                hiddenCategories.Remove(category.Key);
            }

            Reconcile();
            return count;
        }

        // ---------------------------------------------------------------- individual objects

        /// <summary>Hides or shows an object. Returns true when it is hidden.</summary>
        public bool ToggleObject(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            int id = go.GetInstanceID();
            bool hidden = !individualObjects.ContainsKey(id);

            if (hidden)
            {
                individualObjects[id] = go;
            }
            else
            {
                individualObjects.Remove(id);
            }

            Reconcile();
            return hidden;
        }

        /// <summary>Is the object (or one of its parents) hidden individually?</summary>
        public bool IsObjectHidden(GameObject go)
        {
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent)
            {
                if (individualObjects.ContainsKey(t.gameObject.GetInstanceID()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Is the object (or one of its parents) hidden (individually or through a category)?</summary>
        public bool IsRootHidden(GameObject go)
        {
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent)
            {
                if (hiddenRoots.Contains(t.gameObject.GetInstanceID()))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- global

        public void RestoreAll()
        {
            TerrainHidden = false;
            WaterHidden = false;
            CloudsHidden = false;
            PlayerHidden = false;
            hiddenCategories.Clear();
            individualObjects.Clear();
            SetHud(false);
            Reconcile();
        }

        /// <summary>
        /// Hides everything the panel can hide (the reverse of RestoreAll):
        /// terrain, water, clouds, your own character, the HUD and every
        /// known category. The objects picked one by one are already hidden,
        /// they need nothing.
        /// </summary>
        public void HideAll()
        {
            TerrainHidden = true;
            WaterHidden = true;
            CloudsHidden = true;
            PlayerHidden = true;

            foreach (Category category in Categories.All)
            {
                hiddenCategories.Add(category.Key);
            }

            // a global command replaces the setup remembered by the reveal
            // toggle, keeping it would only restore "everything" anyway
            revealedFrom = null;

            SetHud(true);
            Reconcile();
        }

        // ---------------------------------------------------------------- reveal toggle

        /// <summary>How many of the options are plain world / own display flags.</summary>
        private const int WorldOptionCount = 5;

        /// <summary>
        /// The options checked when the reveal started (the flags then one
        /// slot per category, in panel order), null when there is nothing to
        /// bring back.
        /// </summary>
        private bool[] revealedFrom;

        /// <summary>Is a reveal waiting to be undone by the next call?</summary>
        public bool IsRevealing => revealedFrom != null;

        /// <summary>Is at least one option of the panel list checked?</summary>
        public bool AnyOptionChecked
        {
            get
            {
                if (TerrainHidden || WaterHidden || CloudsHidden || PlayerHidden || HudHidden)
                {
                    return true;
                }

                foreach (Category category in Categories.BuiltIn)
                {
                    if (hiddenCategories.Contains(category.Key))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Shows everything the checked options were hiding, then brings that
        /// exact setup back on the next call. Only the options of the panel
        /// take part: the objects hidden one by one and the categories only
        /// known under their name are left alone. With nothing checked, the
        /// call checks every option instead, so it is never a no-op.
        /// </summary>
        public void ToggleChecked()
        {
            bool[] current = CaptureOptions();

            if (revealedFrom == null)
            {
                bool anything = AnyOptionChecked;
                revealedFrom = current;
                ApplyOptions(Filled(current, !anything));
                return;
            }

            // the remembered setup plus whatever was ticked while it was
            // shown: a choice made by hand must never be dropped
            bool[] target = (bool[])revealedFrom.Clone();
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = target[i] || current[i];
            }

            revealedFrom = null;
            ApplyOptions(target);
        }

        /// <summary>Current state of every panel option, in panel order.</summary>
        private bool[] CaptureOptions()
        {
            bool[] state = new bool[WorldOptionCount + Categories.BuiltIn.Length];
            state[0] = TerrainHidden;
            state[1] = WaterHidden;
            state[2] = CloudsHidden;
            state[3] = PlayerHidden;
            state[4] = HudHidden;

            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                state[WorldOptionCount + i] = IsCategoryHidden(Categories.BuiltIn[i].Key);
            }

            return state;
        }

        /// <summary>Puts the panel options in the given state, in one pass.</summary>
        private void ApplyOptions(bool[] state)
        {
            TerrainHidden = state[0];
            WaterHidden = state[1];
            CloudsHidden = state[2];
            PlayerHidden = state[3];

            if (HudHidden != state[4])
            {
                HudHidden = state[4];
                ApplyHud();
            }

            for (int i = 0; i < Categories.BuiltIn.Length; i++)
            {
                string key = Categories.BuiltIn[i].Key;
                if (state[WorldOptionCount + i])
                {
                    hiddenCategories.Add(key);
                }
                else
                {
                    hiddenCategories.Remove(key);
                }
            }

            Reconcile();
        }

        private static bool[] Filled(bool[] from, bool value)
        {
            bool[] state = new bool[from.Length];
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = value;
            }

            return state;
        }

        public void Tick(float deltaTime)
        {
            if (HudHidden)
            {
                // re-asserted every frame: the HUD must never flash back on
                // screen, whatever the game code runs in between
                ApplyHud();
            }

            if (!AnyActive)
            {
                return;
            }

            refreshTimer += deltaTime;
            if (refreshTimer < RefreshInterval)
            {
                return;
            }

            refreshTimer = 0f;
            Reconcile();
        }

        /// <summary>
        /// Recomputes the whole set of renderers that must be hidden and applies
        /// the difference against the current state.
        /// </summary>
        public void Reconcile()
        {
            HashSet<int> waterIds = CollectWaterSurfaceIds();
            Dictionary<int, Renderer> wanted = new Dictionary<int, Renderer>();
            Dictionary<int, InstanceRenderer> wantedInstances =
                new Dictionary<int, InstanceRenderer>();
            HashSet<int> roots = new HashSet<int>();

            if (TerrainHidden)
            {
                foreach (Heightmap heightmap in CollectHeightmaps())
                {
                    AddRenderers(
                        heightmap != null ? heightmap.gameObject : null,
                        wanted,
                        wantedInstances,
                        waterIds
                    );
                }

                // Distant terrain (LOD), handled by a dedicated component
                foreach (
                    TerrainLod lod in UnityEngine.Object.FindObjectsByType<TerrainLod>(
                        FindObjectsSortMode.None
                    )
                )
                {
                    AddRenderers(
                        lod != null ? lod.gameObject : null,
                        wanted,
                        wantedInstances,
                        waterIds
                    );
                }
            }

            if (WaterHidden)
            {
                foreach (WaterVolume volume in CollectWaterVolumes())
                {
                    if (volume != null && volume.m_waterSurface != null)
                    {
                        wanted[volume.m_waterSurface.GetInstanceID()] = volume.m_waterSurface;
                    }
                }

                // the sea drawn beyond the loaded zones (the environment's ocean plane)
                foreach (GameObject root in CollectOceanRoots())
                {
                    AddRenderers(root, wanted, wantedInstances, waterIds);
                }
            }

            if (CloudsHidden)
            {
                foreach (GameObject root in CollectCloudRoots())
                {
                    AddRenderers(root, wanted, wantedInstances, null);
                }

                // the renderers the environment keeps a direct reference on
                EnvMan env = EnvMan.instance;
                if (env != null)
                {
                    AddWanted(env.m_clouds, wanted);
                    AddWanted(env.m_rainClouds, wanted);
                    AddWanted(env.m_rainCloudsDownside, wanted);
                }
            }

            if (PlayerHidden)
            {
                // your own character: body, armor and the held item. Resolved
                // on every pass, the game rebuilds that visual when the
                // equipment or the character itself changes.
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    AddRenderers(player.gameObject, wanted, wantedInstances, waterIds);
                }
            }

            foreach (string key in hiddenCategories)
            {
                Category category;
                if (!Categories.TryGet(key, out category))
                {
                    continue;
                }

                foreach (GameObject root in Categories.CollectRoots(category))
                {
                    if (root == null)
                    {
                        continue;
                    }

                    roots.Add(root.GetInstanceID());
                    AddRenderers(root, wanted, wantedInstances, waterIds);
                }
            }

            foreach (KeyValuePair<int, GameObject> entry in individualObjects)
            {
                GameObject root = entry.Value;
                if (root == null)
                {
                    continue;
                }

                roots.Add(entry.Key);
                AddRenderers(root, wanted, wantedInstances, waterIds);
            }

            hiddenRoots.Clear();
            hiddenRoots.UnionWith(roots);

            Apply(wanted, wantedInstances);
        }

        // ---------------------------------------------------------------- internals

        /// <summary>
        /// The threshold the game itself uses to tell the HUD is off screen
        /// (see Hud.IsVisible).
        /// </summary>
        private const float NotVisibleThreshold = 1000f;

        /// <summary>
        /// Applies the HUD state: the root object deactivated or reactivated,
        /// plus whatever the game moved off screen with it.
        /// </summary>
        private void ApplyHud()
        {
            Hud hud = Hud.instance;
            GameObject root = hud != null ? hud.m_rootObject : null;
            if (root == null)
            {
                return;
            }

            if (HudHidden)
            {
                if (root.activeSelf)
                {
                    root.SetActive(false);
                }

                KeepPauseMenuOnScreen();
                return;
            }

            // showing the HUD again also undoes the game's own toggle
            // (Ctrl + F3): that toggle only drags the root off screen, which
            // would leave it invisible even though it is active again
            if (hud.m_userHidden)
            {
                hud.m_userHidden = false;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            if (root.transform.localPosition.x >= NotVisibleThreshold)
            {
                root.transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// The game drags the pause menu off screen along with the HUD (its
        /// own Ctrl + F3 toggle): the menu would then open out of view, which
        /// is very confusing when Escape also closes the panel. It is put
        /// back as soon as it leaves the screen.
        /// </summary>
        private static void KeepPauseMenuOnScreen()
        {
            Menu menu = Menu.instance;
            if (menu == null || menu.m_root == null)
            {
                return;
            }

            if (menu.m_root.localPosition.x >= NotVisibleThreshold)
            {
                menu.m_root.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// The loaded Heightmaps. The game's static list is preferred (when it
        /// is filled) and we fall back on an object search otherwise.
        /// </summary>
        private static IEnumerable<Heightmap> CollectHeightmaps()
        {
            var instances = Heightmap.Instances;
            if (instances != null && instances.Count > 0)
            {
                List<Heightmap> result = new List<Heightmap>(instances.Count);
                foreach (var entry in instances)
                {
                    Heightmap heightmap = entry as Heightmap;
                    if (heightmap != null)
                    {
                        result.Add(heightmap);
                    }
                }

                if (result.Count > 0)
                {
                    return result;
                }
            }

            return UnityEngine.Object.FindObjectsByType<Heightmap>(FindObjectsSortMode.None);
        }

        private static IEnumerable<WaterVolume> CollectWaterVolumes()
        {
            List<WaterVolume> instances = WaterVolume.Instances;
            if (instances != null && instances.Count > 0)
            {
                return instances;
            }

            return UnityEngine.Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None);
        }

        private static HashSet<int> CollectWaterSurfaceIds()
        {
            HashSet<int> ids = new HashSet<int>();

            foreach (WaterVolume volume in CollectWaterVolumes())
            {
                if (volume != null && volume.m_waterSurface != null)
                {
                    ids.Add(volume.m_waterSurface.GetInstanceID());
                }
            }

            return ids;
        }

        /// <summary>
        /// The sea drawn outside of the loaded zones: the ocean plane of the
        /// environment (the object named "WaterPlane" below "_Environment").
        /// The water toggle only hides the surfaces of the loaded zones
        /// without it, which leaves the horizon visible.
        /// </summary>
        private static IEnumerable<GameObject> CollectOceanRoots()
        {
            return CollectEnvironmentRoots("water");
        }

        /// <summary>The clouds of the sky ("Clouds", "CloudCylinder", ...).</summary>
        private static IEnumerable<GameObject> CollectCloudRoots()
        {
            return CollectEnvironmentRoots("cloud");
        }

        /// <summary>
        /// The children of the environment object whose name contains that
        /// word: only the dedicated objects are picked, never the environment
        /// itself (it also holds the weather, the light and the mist).
        /// </summary>
        private static IEnumerable<GameObject> CollectEnvironmentRoots(string namePart)
        {
            List<GameObject> result = new List<GameObject>();
            EnvMan env = EnvMan.instance;

            if (env == null)
            {
                return result;
            }

            foreach (Transform child in env.transform)
            {
                if (
                    child != null
                    && child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    result.Add(child.gameObject);
                }
            }

            return result;
        }

        private static void AddWanted(Renderer renderer, Dictionary<int, Renderer> wanted)
        {
            if (renderer != null)
            {
                wanted[renderer.GetInstanceID()] = renderer;
            }
        }

        /// <summary>
        /// Registers everything that must be hidden below that object: the
        /// renderers (never the water surfaces, they belong to the water
        /// toggle) and the InstanceRenderers drawing the instanced grass.
        /// </summary>
        private static void AddRenderers(
            GameObject go,
            Dictionary<int, Renderer> wanted,
            Dictionary<int, InstanceRenderer> wantedInstances,
            HashSet<int> excluded
        )
        {
            if (go == null)
            {
                return;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                int id = renderer.GetInstanceID();
                if (excluded != null && excluded.Contains(id))
                {
                    // the water surface is only hidden by the "water" toggle
                    continue;
                }

                wanted[id] = renderer;
            }

            InstanceRenderer[] instances = go.GetComponentsInChildren<InstanceRenderer>(true);
            foreach (InstanceRenderer instance in instances)
            {
                if (instance != null)
                {
                    wantedInstances[instance.GetInstanceID()] = instance;
                }
            }
        }

        private void Apply(
            Dictionary<int, Renderer> wanted,
            Dictionary<int, InstanceRenderer> wantedInstances
        )
        {
            // 1) restore what should no longer be hidden
            List<int> toRestore = null;
            foreach (KeyValuePair<int, Renderer> entry in hiddenRenderers)
            {
                if (!wanted.ContainsKey(entry.Key))
                {
                    if (toRestore == null)
                    {
                        toRestore = new List<int>();
                    }

                    toRestore.Add(entry.Key);
                }
            }

            if (toRestore != null)
            {
                foreach (int id in toRestore)
                {
                    RestoreRenderer(id);
                }
            }

            List<int> instancesToRestore = null;
            foreach (KeyValuePair<int, InstanceRenderer> entry in hiddenInstances)
            {
                if (!wantedInstances.ContainsKey(entry.Key))
                {
                    if (instancesToRestore == null)
                    {
                        instancesToRestore = new List<int>();
                    }

                    instancesToRestore.Add(entry.Key);
                }
            }

            if (instancesToRestore != null)
            {
                foreach (int id in instancesToRestore)
                {
                    RestoreInstanceRenderer(id);
                }
            }

            // 2) hide the new ones
            foreach (KeyValuePair<int, Renderer> entry in wanted)
            {
                if (hiddenRenderers.ContainsKey(entry.Key))
                {
                    continue;
                }

                Renderer renderer = entry.Value;
                hiddenRenderers[entry.Key] = renderer;
                rendererStates[entry.Key] = new RendererState(
                    renderer.enabled,
                    renderer.forceRenderingOff
                );
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }

            foreach (KeyValuePair<int, InstanceRenderer> entry in wantedInstances)
            {
                if (hiddenInstances.ContainsKey(entry.Key))
                {
                    continue;
                }

                InstanceRenderer instance = entry.Value;
                hiddenInstances[entry.Key] = instance;
                instanceStates[entry.Key] = instance.enabled;
                instance.enabled = false;
            }

            // 3) re-apply the hiding (some systems re-enable renderers) + purge destroyed objects
            List<int> dead = null;
            foreach (KeyValuePair<int, Renderer> entry in hiddenRenderers)
            {
                Renderer renderer = entry.Value;
                if (renderer == null)
                {
                    if (dead == null)
                    {
                        dead = new List<int>();
                    }

                    dead.Add(entry.Key);
                    continue;
                }

                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }

            if (dead != null)
            {
                foreach (int id in dead)
                {
                    hiddenRenderers.Remove(id);
                    rendererStates.Remove(id);
                }
            }

            List<int> deadInstances = null;
            foreach (KeyValuePair<int, InstanceRenderer> entry in hiddenInstances)
            {
                if (entry.Value == null)
                {
                    if (deadInstances == null)
                    {
                        deadInstances = new List<int>();
                    }

                    deadInstances.Add(entry.Key);
                    continue;
                }

                entry.Value.enabled = false;
            }

            if (deadInstances != null)
            {
                foreach (int id in deadInstances)
                {
                    hiddenInstances.Remove(id);
                    instanceStates.Remove(id);
                }
            }

            // 4) purge destroyed individual objects
            List<int> deadObjects = null;
            foreach (KeyValuePair<int, GameObject> entry in individualObjects)
            {
                if (entry.Value == null)
                {
                    if (deadObjects == null)
                    {
                        deadObjects = new List<int>();
                    }

                    deadObjects.Add(entry.Key);
                }
            }

            if (deadObjects != null)
            {
                foreach (int id in deadObjects)
                {
                    individualObjects.Remove(id);
                }
            }
        }

        private void RestoreRenderer(int id)
        {
            Renderer renderer;
            if (hiddenRenderers.TryGetValue(id, out renderer))
            {
                RendererState state;
                if (renderer != null && rendererStates.TryGetValue(id, out state))
                {
                    renderer.enabled = state.Enabled;
                    renderer.forceRenderingOff = state.ForceOff;
                }

                hiddenRenderers.Remove(id);
                rendererStates.Remove(id);
            }
        }

        private void RestoreInstanceRenderer(int id)
        {
            InstanceRenderer instance;
            if (hiddenInstances.TryGetValue(id, out instance))
            {
                bool enabled;
                if (instance != null && instanceStates.TryGetValue(id, out enabled))
                {
                    instance.enabled = enabled;
                }

                hiddenInstances.Remove(id);
                instanceStates.Remove(id);
            }
        }
    }
}
