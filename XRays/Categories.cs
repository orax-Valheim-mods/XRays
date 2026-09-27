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
                Types = new[] { typeof(MineRock5), typeof(MineRock) }
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
                Types = new[] { typeof(ClutterSystem) }
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

        // ---------------------------------------------------------------- internals

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
