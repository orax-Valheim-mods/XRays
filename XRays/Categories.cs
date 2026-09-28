using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRays
{
    /// <summary>
    /// A group of game objects that can be hidden / shown as a whole
    /// (all trees for example). "Name" categories are created on the fly when
    /// the aimed object does not belong to any known group.
    /// </summary>
    internal class Category
    {
        public const string NameKeyPrefix = "name:";

        public string Key;
        public string Label;

        /// <summary>
        /// What the panel shows in its little box when the pointer stays on
        /// the row of that category. Categories created from an object name
        /// have none, they never get a row of their own.
        /// </summary>
        public string Description;

        public Type[] Types;

        /// <summary>
        /// Case-insensitive substrings matched against the object name. They
        /// catch the objects that carry no known game component: the small
        /// decorative trees, the stubs and the old logs for example only have a
        /// "Destructible", so they would never be found through their type.
        /// </summary>
        public string[] NameContains;

        /// <summary>
        /// Case-insensitive prefixes matched against the object name. Every
        /// spawner prefab is named "Spawner_...", which catches the ones
        /// carrying no known game component (a modded spawner for example)
        /// without claiming the effects and the model pieces whose name
        /// merely contains that word.
        /// </summary>
        public string[] NameStartsWith;

        /// <summary>
        /// Case-insensitive substrings given away to another category: an ore
        /// vein carries the same game components as a plain rock, so Rocks
        /// hands every ore name over to Ores. Checked first, both by the name
        /// rules and by the objects claimed through a component, so one object
        /// never ends up in the two categories at once.
        /// </summary>
        public string[] NameExcludes;

        /// <summary>
        /// Roots the mod finds on its own, for the objects that carry no game
        /// component at all: the clutter below the grass root of the
        /// ClutterSystem for example.
        /// </summary>
        public Func<List<GameObject>> RootProvider;

        public bool SkipLocalPlayer;

        public bool IsNameCategory =>
            Key != null && Key.StartsWith(NameKeyPrefix, StringComparison.Ordinal);

        public string Name => IsNameCategory ? Key.Substring(NameKeyPrefix.Length) : Label;

        /// <summary>Does the category need an object-name scan to list its objects?</summary>
        public bool HasNameMatch =>
            IsNameCategory
            || (NameContains != null && NameContains.Length > 0)
            || (NameStartsWith != null && NameStartsWith.Length > 0);

        /// <summary>
        /// Does that object belong to the category according to its name?
        /// Exact match for the categories created "by name", prefix match and
        /// substring match for the built-in ones.
        /// </summary>
        public bool MatchesName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            // the name belongs to another category: an ore is no rock
            if (MatchesExclude(objectName))
            {
                return false;
            }

            if (IsNameCategory)
            {
                return string.Equals(objectName, Name, StringComparison.Ordinal);
            }

            if (HasPrefix(objectName))
            {
                return true;
            }

            return ContainsAny(objectName, NameContains);
        }

        /// <summary>Is that name given away to another category?</summary>
        public bool MatchesExclude(string objectName)
        {
            return ContainsAny(objectName, NameExcludes);
        }

        private static bool ContainsAny(string objectName, string[] patterns)
        {
            if (string.IsNullOrEmpty(objectName) || patterns == null)
            {
                return false;
            }

            foreach (string pattern in patterns)
            {
                if (
                    !string.IsNullOrEmpty(pattern)
                    && objectName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPrefix(string objectName)
        {
            if (NameStartsWith == null)
            {
                return false;
            }

            foreach (string prefix in NameStartsWith)
            {
                if (
                    !string.IsNullOrEmpty(prefix)
                    && objectName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return true;
                }
            }

            return false;
        }

        public static Category ForName(string objectName)
        {
            return new Category
            {
                Key = NameKeyPrefix + objectName,
                Label = objectName,
                Types = Array.Empty<Type>()
            };
        }
    }

    /// <summary>
    /// Registry of the known categories + enumeration of a category's objects.
    /// </summary>
    internal static class Categories
    {
        /// <summary>
        /// Key of the category that follows the animals and the monsters. The
        /// visibility code checks it again: the bars the game draws over a
        /// creature belong to the creature and are taken away with it.
        /// </summary>
        public const string CreaturesKey = "creatures";

        /// <summary>
        /// The names of the ore veins. An ore vein and a plain stone are
        /// marked with the same components (MineRock5 / MineRock), so only
        /// the name tells them apart: they belong to Ores, never to Rocks
        /// (see Category.NameExcludes). Both categories share this very
        /// array, so the two lists can never drift apart. "tin" carries its
        /// underscores: alone it would also claim "ShipSetting" and "Chitin".
        /// </summary>
        private static readonly string[] OreNames =
        {
            "copper",
            "iron",
            "silver",
            "obsidian",
            "meteor",
            "flametal",
            "sulfur",
            "sulphur",
            "gold",
            "coal",
            "_tin",
            "tin_"
        };

        /// <summary>
        /// The categories in claim order: when an object is picked or
        /// hidden, the first one holding a known component (or matching the
        /// object name) decides its category. The panel lists them sorted
        /// by label instead, which is what DisplayOrder holds.
        /// </summary>
        public static readonly Category[] BuiltIn =
        {
            new Category
            {
                Key = "trees",
                Label = "Trees",
                Description =
                    "Every tree, plus the stubs, roots and branches that only carry a Destructible, matched by name.",
                Types = new[] { typeof(TreeBase), typeof(TreeLog) },
                // small trees, stubs, roots and branches only have a Destructible
                NameContains = new[]
                {
                    "tree",
                    "birch",
                    "beech",
                    "oak",
                    "stump",
                    "stub",
                    "root",
                    "branch"
                }
            },
            new Category
            {
                Key = "ores",
                Label = "Ores",
                Description =
                    "The ore veins themselves: copper, tin, iron, silver, obsidian, meteorite, flametal, sulfur, gold and coal. The name decides, because the game marks an ore and a plain stone with the same components.",
                // only the name tells an ore vein from a mineable stone
                NameContains = OreNames
            },
            new Category
            {
                Key = "rocks",
                Label = "Rocks",
                Description =
                    "Boulders, stones and cliffs, plus every mineable rock that is no ore (MineRock5 and MineRock), matched by component and by name.",
                Types = new[] { typeof(MineRock5), typeof(MineRock) },
                // a plain boulder or a cliff only carries a Destructible
                NameContains = new[] { "rock", "boulder", "stone", "cliff" },
                // the ore veins carry those very components: they are Ores
                NameExcludes = OreNames
            },
            new Category
            {
                Key = "pickable",
                Label = "Pickable resources",
                Description =
                    "What can be gathered by hand: berries, mushrooms, feathers and the rest (Pickable).",
                Types = new[] { typeof(Pickable), typeof(PickableItem) }
            },
            new Category
            {
                Key = "build",
                Label = "Build pieces",
                Description =
                    "Everything that can be built by hand: walls, floors, furniture and workstations (Piece and WearNTear).",
                Types = new[] { typeof(WearNTear), typeof(Piece) }
            },
            new Category
            {
                Key = CreaturesKey,
                Label = "Creatures",
                Description =
                    "The animals and the monsters (Character), plus the fish and the birds, with the bars the game draws over them.",
                // Character covers the animals and the monsters; a swimming
                // fish carries no Character at all, only Fish + ItemDrop,
                // and would be reported as a ground item instead. The ambient
                // birds are the same story: Crow, Seagal and AshCrow carry no
                // Character either, only RandomFlyingBird (plus a Destructible),
                // so without it they survive the whole category.
                Types = new[] { typeof(Character), typeof(Fish), typeof(RandomFlyingBird) },
                SkipLocalPlayer = true
            },
            new Category
            {
                Key = "items",
                Label = "Ground items",
                Description =
                    "What lies on the ground: every dropped item and resource (ItemDrop).",
                Types = new[] { typeof(ItemDrop) }
            },
            new Category
            {
                Key = "plants",
                Label = "Crops",
                Description =
                    "The plants you grow: carrots, turnips, onions, anything carrying a Plant component.",
                Types = new[] { typeof(Plant) }
            },
            new Category
            {
                Key = "spawners",
                Label = "Spawners",
                Description =
                    "The one-shot spawners, the nests and piles, the evil hearts and the holes that open on approach.",
                // the one-shot spawners, the nests / piles / evil hearts
                // (SpawnArea) and the holes that open on approach
                // (TriggerSpawner). SpawnSystem is on the zone controller,
                // never on a spawner itself, and the fire / smoke spawners
                // are effects: neither belongs here.
                Types = new[]
                {
                    typeof(CreatureSpawner),
                    typeof(SpawnArea),
                    typeof(TriggerSpawner)
                },
                // a spawner carrying no known component (a modded one): every
                // one of those prefabs is named "Spawner...", taken from the
                // start so the model pieces and the effects whose name only
                // contains that word stay out
                NameStartsWith = new[] { "spawner" }
            },
            new Category
            {
                Key = "clutter",
                Label = "Grass / vegetation",
                Description =
                    "The clutter growing in the world: bushes, grass patches, ferns, reeds, moss, mushrooms and flowers.",
                // the grass patches are drawn by InstanceRenderer, not by a Renderer
                Types = new[] { typeof(InstanceRenderer) },
                // bushes and isolated plants only carry a Destructible
                NameContains = new[]
                {
                    "bush",
                    "shrub",
                    "grass",
                    "fern",
                    "reed",
                    "moss",
                    "mushroom",
                    "flower"
                },
                // the clutter spawned by the ClutterSystem lives under "grassroot"
                RootProvider = GrassRoots
            },
            new Category
            {
                Key = "locations",
                Label = "Locations",
                Description =
                    "The structures the game places on its own: ruins, dungeon entrances and everything a Location carries.",
                // the structures the game places on its own: a dungeon
                // entrance sits below a LocationProxy (the object that spawns
                // the location when the player comes close), and the location
                // itself carries Location. Neither of them belongs to any
                // other category, so without this one they would survive
                // everything, Hide all included.
                // Last in this list: a known component decides the
                // category, so the trees, the rocks and the build pieces
                // claim their own objects before these two.
                Types = new[] { typeof(LocationProxy), typeof(Location) }
            }
        };

        /// <summary>
        /// The same categories sorted by label: the order the panel shows
        /// them in. Only the panel reads it, the claim order above is the
        /// one everything else keeps.
        /// </summary>
        public static readonly Category[] DisplayOrder = SortByLabel(BuiltIn);

        private static Category[] SortByLabel(Category[] source)
        {
            Category[] sorted = (Category[])source.Clone();
            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Label, b.Label));
            return sorted;
        }

        private static readonly Dictionary<string, Category> Registry =
            new Dictionary<string, Category>();

        /// <summary>
        /// A name match needs a scan of every loaded object, which is far too
        /// expensive to run on each refresh: the result is kept for a moment.
        /// </summary>
        private const float NameScanDuration = 2f;

        private class NameScan
        {
            public float Time;
            public readonly List<GameObject> Roots = new List<GameObject>();
        }

        private static readonly Dictionary<string, NameScan> NameScans =
            new Dictionary<string, NameScan>();

        static Categories()
        {
            foreach (Category category in BuiltIn)
            {
                Registry[category.Key] = category;
            }
        }

        public static bool TryGet(string key, out Category category)
        {
            return Registry.TryGetValue(key, out category);
        }

        public static void Register(Category category)
        {
            if (category != null && !string.IsNullOrEmpty(category.Key))
            {
                Registry[category.Key] = category;
            }
        }

        /// <summary>Every known category: the built-in ones + the ones created on the fly.</summary>
        public static IEnumerable<Category> All => Registry.Values;

        /// <summary>Drops the cached name scan of a category (called when it is toggled).</summary>
        public static void Invalidate(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                NameScans.Remove(key);
            }
        }

        /// <summary>
        /// Lists the currently loaded objects belonging to the category.
        /// Every returned "root" is the GameObject below which all renderers
        /// are hidden.
        /// </summary>
        public static List<GameObject> CollectRoots(Category category)
        {
            List<GameObject> roots = new List<GameObject>();
            HashSet<int> seen = new HashSet<int>();

            if (category == null)
            {
                return roots;
            }

            AddProvidedRoots(category, roots, seen);
            AddTypedRoots(category, roots, seen);

            if (category.HasNameMatch)
            {
                foreach (GameObject go in NameScanOf(category))
                {
                    if (go != null && seen.Add(go.GetInstanceID()))
                    {
                        roots.Add(go);
                    }
                }
            }

            return roots;
        }

        public static int Count(Category category)
        {
            return CollectRoots(category).Count;
        }

        /// <summary>
        /// The grass root of the ClutterSystem: below it sit the clutter
        /// patches (the instanced grass) and the clutter props. The manager
        /// itself lives on the root of the scene, so it is never a root: it
        /// would hide the clouds, the sea and the weather with the grass.
        /// </summary>
        public static List<GameObject> GrassRoots()
        {
            List<GameObject> roots = new List<GameObject>();
            Transform grassRoot = GrassRoot();
            if (grassRoot != null)
            {
                roots.Add(grassRoot.gameObject);
            }

            return roots;
        }

        /// <summary>The "grassroot" object of the ClutterSystem, if it is loaded.</summary>
        public static Transform GrassRoot()
        {
            ClutterSystem clutter = ClutterSystem.instance;
            if (clutter == null)
            {
                foreach (
                    UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(
                        typeof(ClutterSystem),
                        FindObjectsSortMode.None
                    )
                )
                {
                    clutter = (obj as Component) as ClutterSystem;
                    if (clutter != null)
                    {
                        break;
                    }
                }
            }

            return clutter != null ? clutter.transform.Find("grassroot") : null;
        }

        /// <summary>
        /// The clutter item holding that object: the direct child of the grass
        /// root (the whole patch or prop, not just one of its parts). Null
        /// when the object does not belong to the grass root.
        /// </summary>
        public static Transform GrassItem(Transform transform)
        {
            Transform grassRoot = GrassRoot();
            if (
                grassRoot == null
                || transform == null
                || (transform != grassRoot && !transform.IsChildOf(grassRoot))
            )
            {
                return null;
            }

            Transform item = transform;
            while (item != grassRoot && item.parent != null && item.parent != grassRoot)
            {
                item = item.parent;
            }

            return item;
        }

        // ---------------------------------------------------------------- internals

        /// <summary>The roots a category computes itself (see RootProvider).</summary>
        private static void AddProvidedRoots(
            Category category,
            List<GameObject> roots,
            HashSet<int> seen
        )
        {
            if (category.RootProvider == null)
            {
                return;
            }

            List<GameObject> provided = category.RootProvider();
            if (provided == null)
            {
                return;
            }

            foreach (GameObject go in provided)
            {
                if (go != null && seen.Add(go.GetInstanceID()))
                {
                    roots.Add(go);
                }
            }
        }

        /// <summary>The objects holding one of the game components of the category.</summary>
        private static void AddTypedRoots(
            Category category,
            List<GameObject> roots,
            HashSet<int> seen
        )
        {
            if (category.Types == null)
            {
                return;
            }

            foreach (Type type in category.Types)
            {
                foreach (
                    UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(
                        type,
                        FindObjectsSortMode.None
                    )
                )
                {
                    Component comp = obj as Component;
                    if (comp == null)
                    {
                        continue;
                    }

                    Player player = comp as Player;
                    if (
                        category.SkipLocalPlayer
                        && player != null
                        && player == Player.m_localPlayer
                    )
                    {
                        continue;
                    }

                    GameObject go = comp.gameObject;

                    // the name belongs to another category: an ore vein
                    // carries MineRock5 like a stone does, but it is Ores
                    if (category.MatchesExclude(go.name))
                    {
                        continue;
                    }

                    if (seen.Add(go.GetInstanceID()))
                    {
                        roots.Add(go);
                    }
                }
            }
        }

        /// <summary>
        /// The objects matching the category through their name. The scan of
        /// every loaded object is cached for a short while (see NameScans).
        /// </summary>
        private static List<GameObject> NameScanOf(Category category)
        {
            NameScan scan;
            if (
                NameScans.TryGetValue(category.Key, out scan)
                && Time.realtimeSinceStartup - scan.Time < NameScanDuration
            )
            {
                return scan.Roots;
            }

            scan = new NameScan { Time = Time.realtimeSinceStartup };
            AddNamedRoots(category, scan.Roots);
            NameScans[category.Key] = scan;
            return scan.Roots;
        }

        private static void AddNamedRoots(Category category, List<GameObject> roots)
        {
            HashSet<int> seen = new HashSet<int>();
            HashSet<Transform> foreignRoots = ForeignRoots(category);

            foreach (
                UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(
                    typeof(Transform),
                    FindObjectsSortMode.None
                )
            )
            {
                Transform t = obj as Transform;
                if (t == null || !category.MatchesName(t.name))
                {
                    continue;
                }

                // the roots the other categories find on their own (the grass
                // root of the ClutterSystem) and everything below them: the
                // rule "root" of the Trees category also matches "grassroot",
                // which would hide the whole grass with the trees
                if (IsUnderAny(t, foreignRoots))
                {
                    continue;
                }

                // a known game component decides the category of the object
                // (that is what the picker does): never claim an object
                // through its name when a category claims it by component —
                // except for a name that category gives away (NameExcludes),
                // which stays free for the name rules
                if (TypedOwner(t.gameObject) != null)
                {
                    continue;
                }

                // a same-named ancestor takes the whole subtree, unless that
                // ancestor belongs to another category
                if (HasMatchingParent(t, category))
                {
                    continue;
                }

                if (seen.Add(t.gameObject.GetInstanceID()))
                {
                    roots.Add(t.gameObject);
                }
            }
        }

        /// <summary>
        /// The roots the other categories provide themselves (see
        /// RootProvider): a name rule never claims one of them, nor anything
        /// below it, whatever the two names have in common.
        /// </summary>
        private static HashSet<Transform> ForeignRoots(Category category)
        {
            HashSet<Transform> result = null;

            foreach (Category other in All)
            {
                if (other == category || other.RootProvider == null)
                {
                    continue;
                }

                List<GameObject> provided = other.RootProvider();
                if (provided == null)
                {
                    continue;
                }

                foreach (GameObject root in provided)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    if (result == null)
                    {
                        result = new HashSet<Transform>();
                    }

                    result.Add(root.transform);
                }
            }

            return result;
        }

        /// <summary>Is that object one of those roots, or below one of them?</summary>
        private static bool IsUnderAny(Transform transform, HashSet<Transform> roots)
        {
            if (roots == null || transform == null)
            {
                return false;
            }

            for (Transform current = transform; current != null; current = current.parent)
            {
                if (roots.Contains(current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Is there an ancestor taking that object with its own name? An
        /// ancestor carrying a known component does not count: it belongs to
        /// another category, it is hidden with that one and it never claims
        /// the objects of the same name sitting inside it (the picker stops
        /// on the object it hits, so those report the object, not the
        /// container).
        /// </summary>
        private static bool HasMatchingParent(Transform transform, Category category)
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (category.MatchesName(parent.name) && TypedOwner(parent.gameObject) == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The category claiming that object through one of its game
        /// components, null when no category does. Such an object is never
        /// claimed through its name: the picker would report the component's
        /// category instead. A category giving that name away claims it
        /// neither (Rocks leaves the ore names to Ores), which keeps the name
        /// rules free to pick the object up.
        /// </summary>
        private static Category TypedOwner(GameObject go)
        {
            foreach (Category category in BuiltIn)
            {
                if (category.Types == null || category.MatchesExclude(go.name))
                {
                    continue;
                }

                foreach (Type type in category.Types)
                {
                    if (go.GetComponent(type) != null)
                    {
                        return category;
                    }
                }
            }

            return null;
        }
    }
}
