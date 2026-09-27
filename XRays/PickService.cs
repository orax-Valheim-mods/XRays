using System;
using System.Globalization;
using UnityEngine;

namespace XRays
{
    internal enum PickKind
    {
        None,
        Terrain,
        Water,
        Object
    }

    internal class PickResult
    {
        public PickKind Kind;
        public Category Category;
        public GameObject Root;
        public string Name;
        public bool Hidden;

        /// <summary>World position of the picked element (the hit point for terrain / water).</summary>
        public Vector3 Position;

        /// <summary>Distance between the camera and the picked element, in meters.</summary>
        public float Distance;

        /// <summary>
        /// The game component type that matched the category (TreeBase, Pickable,
        /// Heightmap, ...). Null for categories created "by name".
        /// </summary>
        public Type MatchedType;

        /// <summary>
        /// The object name that matched the category (when the category also
        /// matches names, e.g. the small trees in "Trees"). Null otherwise.
        /// </summary>
        public string MatchedName;
    }

    /// <summary>
    /// Selection of an element under the cursor (or under the crosshair when the
    /// pointer is locked) through a ray cast starting from the camera.
    /// </summary>
    internal static class PickService
    {
        public static PickResult Pick(float maxDistance, VisibilityManager visibility)
        {
            Camera camera = GetCamera();
            if (camera == null || visibility == null)
            {
                return null;
            }

            Ray ray = camera.ScreenPointToRay(GetPointerPosition());
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            if (hits.Length == 0)
            {
                return null;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Player localPlayer = Player.m_localPlayer;
            PickResult hiddenFallback = null;

            foreach (RaycastHit hit in hits)
            {
                Transform transform = hit.collider != null ? hit.collider.transform : null;
                if (transform == null)
                {
                    continue;
                }

                // never select ourselves
                if (localPlayer != null && transform.IsChildOf(localPlayer.transform))
                {
                    continue;
                }

                PickResult result = Classify(transform);
                if (result == null)
                {
                    continue;
                }

                // where it is: the object position, or the aimed point for terrain / water
                result.Distance = hit.distance;
                result.Position =
                    result.Kind == PickKind.Object && result.Root != null
                        ? result.Root.transform.position
                        : hit.point;

                if (result.Kind == PickKind.Terrain)
                {
                    // terrain already hidden: look for what is behind it
                    if (visibility.TerrainHidden)
                    {
                        continue;
                    }

                    return result;
                }

                if (result.Kind == PickKind.Water)
                {
                    if (visibility.WaterHidden)
                    {
                        continue;
                    }

                    return result;
                }

                // already hidden object: kept as a fallback so it can be shown again
                if (visibility.IsRootHidden(result.Root))
                {
                    result.Hidden = true;
                    if (hiddenFallback == null)
                    {
                        hiddenFallback = result;
                    }

                    continue;
                }

                return result;
            }

            return hiddenFallback;
        }

        /// <summary>
        /// Detailed description of the picked element, used by the panel:
        /// name, category, hidden state, then the game types used by that
        /// category, then coordinates and distance.
        /// </summary>
        public static string Describe(float maxDistance, VisibilityManager visibility)
        {
            PickResult pick = Pick(maxDistance, visibility);
            if (pick == null)
            {
                return "Under the cursor: none";
            }

            string title;
            switch (pick.Kind)
            {
                case PickKind.Terrain:
                    title = "Terrain";
                    break;
                case PickKind.Water:
                    title = "Water";
                    break;
                default:
                    title = pick.Name;
                    break;
            }

            string line = "Under the cursor: " + title;

            if (pick.Category != null)
            {
                line += "  ·  Category: " + pick.Category.Label;
            }

            if (pick.Hidden || (pick.Root != null && visibility.IsRootHidden(pick.Root)))
            {
                line += "  ·  hidden";
            }

            return line + "\n" + TypesLine(pick) + "\n" + Details(pick);
        }

        /// <summary>
        /// Debug line: the game component types backing the category, plus the
        /// one that actually matched the picked object.
        /// </summary>
        private static string TypesLine(PickResult pick)
        {
            if (pick.Kind == PickKind.Terrain)
            {
                return "Game type: Heightmap";
            }

            if (pick.Kind == PickKind.Water)
            {
                return "Game type: WaterVolume";
            }

            Category category = pick.Category;
            if (category == null)
            {
                return "Game type: none";
            }

            if (category.IsNameCategory)
            {
                return "Game type: none  ·  category created by name";
            }

            string list = "none";
            if (category.Types != null && category.Types.Length > 0)
            {
                string[] names = new string[category.Types.Length];
                for (int i = 0; i < category.Types.Length; i++)
                {
                    names[i] = category.Types[i].Name;
                }

                list = string.Join(", ", names);
            }

            if (category.NameContains != null && category.NameContains.Length > 0)
            {
                list += " + names";
            }

            string matched;
            if (pick.MatchedType != null)
            {
                matched = pick.MatchedType.Name;
            }
            else if (pick.MatchedName != null)
            {
                matched = "name (\"" + pick.MatchedName + "\")";
            }
            else
            {
                matched = "?";
            }

            return "Game types: " + list + "  ·  match: " + matched;
        }

        /// <summary>Coordinates + distance line (invariant culture, English unit).</summary>
        private static string Details(PickResult pick)
        {
            Vector3 position = pick.Position;
            string prefix = pick.Kind == PickKind.Object ? "Pos" : "Hit";

            return prefix
                + " ("
                + Format(position.x)
                + ", "
                + Format(position.y)
                + ", "
                + Format(position.z)
                + ")"
                + "  ·  "
                + Format(pick.Distance)
                + " m";
        }

        private static string Format(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Walks up the hierarchy to find: the terrain, the water, or the object
        /// (root + category) matching the touched collider.
        /// </summary>
        private static PickResult Classify(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.GetComponent<Heightmap>() != null)
                {
                    return new PickResult
                    {
                        Kind = PickKind.Terrain,
                        Root = current.gameObject,
                        Name = "Terrain",
                        MatchedType = typeof(Heightmap)
                    };
                }

                if (current.GetComponent<WaterVolume>() != null)
                {
                    return new PickResult
                    {
                        Kind = PickKind.Water,
                        Root = current.gameObject,
                        Name = "Water",
                        MatchedType = typeof(WaterVolume)
                    };
                }

                foreach (Category category in Categories.BuiltIn)
                {
                    if (category.Types == null)
                    {
                        continue;
                    }

                    foreach (Type type in category.Types)
                    {
                        if (current.GetComponent(type) != null)
                        {
                            return new PickResult
                            {
                                Kind = PickKind.Object,
                                Category = category,
                                Root = current.gameObject,
                                Name = current.name,
                                MatchedType = type
                            };
                        }
                    }
                }

                // "object root" markers: network view or level of detail
                if (
                    current.GetComponent<ZNetView>() != null
                    || current.GetComponent<LODGroup>() != null
                )
                {
                    return ObjectResult(current);
                }

                if (current.parent == null)
                {
                    return ObjectResult(current);
                }
            }

            return null;
        }

        /// <summary>
        /// Result for an object root (network view / LOD / top of the hierarchy):
        /// the category matching that object name when there is one (the small
        /// trees, the stubs, ...), a category created from the name otherwise.
        /// </summary>
        private static PickResult ObjectResult(Transform current)
        {
            foreach (Category category in Categories.BuiltIn)
            {
                if (category.MatchesName(current.name))
                {
                    return new PickResult
                    {
                        Kind = PickKind.Object,
                        Category = category,
                        Root = current.gameObject,
                        Name = current.name,
                        MatchedName = current.name
                    };
                }
            }

            return new PickResult
            {
                Kind = PickKind.Object,
                Category = Category.ForName(current.name),
                Root = current.gameObject,
                Name = current.name
            };
        }

        private static Camera GetCamera()
        {
            Camera camera = Camera.main;
            if (camera == null && GameCamera.instance != null)
            {
                camera = GameCamera.instance.GetComponentInChildren<Camera>(true);
            }

            return camera;
        }

        /// <summary>
        /// Pointer position: the screen center (= crosshair) when the cursor is
        /// locked, otherwise the real mouse position.
        /// </summary>
        private static Vector2 GetPointerPosition()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return Input.mousePosition;
        }
    }
}
