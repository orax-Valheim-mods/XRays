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
        public bool SkipLocalPlayer;

        public bool IsNameCategory => Key != null && Key.StartsWith(NameKeyPrefix, StringComparison.Ordinal);

        public string Name => IsNameCategory ? Key.Substring(NameKeyPrefix.Length) : Label;

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
            new Category { Key = "trees",     Label = "Trees",                 Types = new[] { typeof(TreeBase), typeof(TreeLog) } },
            new Category { Key = "rocks",     Label = "Rocks / ore",           Types = new[] { typeof(MineRock5), typeof(MineRock) } },
            new Category { Key = "pickable",  Label = "Pickable resources",    Types = new[] { typeof(Pickable), typeof(PickableItem) } },
            new Category { Key = "build",     Label = "Build pieces",          Types = new[] { typeof(WearNTear), typeof(Piece) } },
            new Category { Key = "creatures", Label = "Creatures",             Types = new[] { typeof(Character) }, SkipLocalPlayer = true },
            new Category { Key = "items",     Label = "Ground items",          Types = new[] { typeof(ItemDrop) } },
            new Category { Key = "plants",    Label = "Crops",                 Types = new[] { typeof(Plant) } },
            new Category { Key = "spawners",  Label = "Spawners",              Types = new[] { typeof(CreatureSpawner) } },
            new Category { Key = "clutter",   Label = "Grass / vegetation",    Types = new[] { typeof(ClutterSystem) } }
        };

        private static readonly Dictionary<string, Category> Registry = new Dictionary<string, Category>();

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

            // "Name" category: simply compare GameObject names
            if (category.IsNameCategory)
            {
                string wanted = category.Name;
                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(typeof(Transform), FindObjectsSortMode.None))
                {
                    Transform t = obj as Transform;
                    if (t == null || t.name != wanted)
                    {
                        continue;
                    }

                    if (seen.Add(t.gameObject.GetInstanceID()))
                    {
                        roots.Add(t.gameObject);
                    }
                }

                return roots;
            }

            if (category.Types == null)
            {
                return roots;
            }

            foreach (Type type in category.Types)
            {
                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None))
                {
                    Component comp = obj as Component;
                    if (comp == null)
                    {
                        continue;
                    }

                    Player player = comp as Player;
                    if (category.SkipLocalPlayer && player != null && player == Player.m_localPlayer)
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

            return roots;
        }

        public static int Count(Category category)
        {
            return CollectRoots(category).Count;
        }
    }
}
