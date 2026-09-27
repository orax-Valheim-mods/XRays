using System;
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
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
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

        public static string Describe(float maxDistance, VisibilityManager visibility)
        {
            PickResult pick = Pick(maxDistance, visibility);
            if (pick == null)
            {
                return "none";
            }

            switch (pick.Kind)
            {
                case PickKind.Terrain:
                    return "Terrain" + Suffix(visibility.TerrainHidden);
                case PickKind.Water:
                    return "Water" + Suffix(visibility.WaterHidden);
                default:
                    string label = pick.Category != null ? pick.Category.Label : "?";
                    return pick.Name + "  [" + label + "]" + Suffix(pick.Hidden || visibility.IsRootHidden(pick.Root));
            }
        }

        private static string Suffix(bool hidden)
        {
            return hidden ? "  (hidden)" : "";
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
                    return new PickResult { Kind = PickKind.Terrain, Root = current.gameObject, Name = "Terrain" };
                }

                if (current.GetComponent<WaterVolume>() != null)
                {
                    return new PickResult { Kind = PickKind.Water, Root = current.gameObject, Name = "Water" };
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
                                Name = current.name
                            };
                        }
                    }
                }

                // "object root" markers: network view or level of detail
                if (current.GetComponent<ZNetView>() != null || current.GetComponent<LODGroup>() != null)
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

        private static PickResult ObjectResult(Transform current)
        {
            Category category = Category.ForName(current.name);
            return new PickResult
            {
                Kind = PickKind.Object,
                Category = category,
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
