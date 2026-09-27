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
        public Type[] Types;

        /// <summary>
        /// Case-insensitive substrings matched against the object name. They
        /// catch the objects that carry no known game component: the small
        /// decorative trees, the stubs and the old logs for example only have a
        /// "Destructible", so they would never be found through their type.
        /// </summary>
        public string[] NameContains;

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
            IsNameCategory || (NameContains != null && NameContains.Length > 0);

        /// <summary>
        /// Does that object belong to the category according to its name?
        /// Exact match for the categories created "by name", substring match
        /// for the built-in ones.
        /// </summary>
        public bool MatchesName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (IsNameCategory)
            {
                return string.Equals(objectName, Name, StringComparison.Ordinal);
            }

            if (NameContains == null)
            {
                return false;
            }

            foreach (string pattern in NameContains)
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
        public static readonly Category[] BuiltIn =
        {
            new Category
            {
                Key = "trees",
                Label = "Trees",
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
                Key = "rocks",
                Label = "Rocks / ore",
                Types = new[] { typeof(MineRock5), typeof(MineRock) },
                // a plain boulder only carries a Destructible
                NameContains = new[] { "rock", "boulder", "stone" }
            },
            new Category
            {
                Key = "pickable",
                Label = "Pickable resources",
                Types = new[] { typeof(Pickable), typeof(PickableItem) }
            },
            new Category
            {
                Key = "build",
                Label = "Build pieces",
                Types = new[] { typeof(WearNTear), typeof(Piece) }
            },
            new Category
            {
                Key = "creatures",
                Label = "Creatures",
                Types = new[] { typeof(Character) },
                SkipLocalPlayer = true
            },
            new Category
            {
                Key = "items",
                Label = "Ground items",
                Types = new[] { typeof(ItemDrop) }
            },
            new Category
            {
                Key = "plants",
                Label = "Crops",
                Types = new[] { typeof(Plant) }
            },
            new Category
            {
                Key = "spawners",
                Label = "Spawners",
                Types = new[] { typeof(CreatureSpawner) }
            },
            new Category
            {
                Key = "clutter",
                Label = "Grass / vegetation",
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
            }
        };

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

                // a known game component always decides the category of the
                // object (that is what the picker does): never claim an object
                // through its name when it belongs to a typed category
                if (HasBuiltInComponent(t.gameObject))
                {
                    continue;
                }

                // only the outermost match becomes a root
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

        private static bool HasMatchingParent(Transform transform, Category category)
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (category.MatchesName(parent.name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Does the object carry one of the game components of a known
        /// category? Such an object is never claimed through its name: the
        /// picker would report the component's category instead.
        /// </summary>
        private static bool HasBuiltInComponent(GameObject go)
        {
            foreach (Category category in BuiltIn)
            {
                if (category.Types == null)
                {
                    continue;
                }

                foreach (Type type in category.Types)
                {
                    if (go.GetComponent(type) != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
