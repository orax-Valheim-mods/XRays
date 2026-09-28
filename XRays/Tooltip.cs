using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XRays
{
    /// <summary>
    /// The little box describing the element under the mouse: it waits for
    /// the pointer to stay on an element for a moment, then follows it
    /// until the pointer has been nowhere for another moment. The rows of
    /// the panel are made of separate objects (toggle, label, count) and
    /// the pointer crosses the space between them without leaving the row,
    /// which that second moment covers.
    /// </summary>
    internal class Tooltip
    {
        /// <summary>Hover needed before the box shows itself.</summary>
        private const float ShowDelay = 0.4f;

        /// <summary>Hover without an element before the box goes away.</summary>
        private const float HideDelay = 0.15f;

        /// <summary>Space kept between the pointer and the box.</summary>
        private const float Gap = 10f;

        /// <summary>Space kept between the box and the edges of the panel.</summary>
        private const float Margin = 6f;

        /// <summary>Width of the text: the box follows the height it needs.</summary>
        private const float TextWidth = 320f;

        private const float PaddingX = 9f;
        private const float PaddingY = 6f;
        private const int FontSize = 13;

        /// <summary>The panel rectangle the box stays inside.</summary>
        private readonly RectTransform area;

        private readonly RectTransform box;
        private readonly Text label;

        private GameObject hovered;
        private System.Func<string> source;
        private float wait;
        private bool shown;

        public Tooltip(RectTransform panel)
        {
            area = panel;

            GameObject go = new GameObject(
                "XRaysTip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            box = go.GetComponent<RectTransform>();
            box.SetParent(area, false);
            box.anchorMin = new Vector2(0.5f, 0.5f);
            box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0f, 1f);

            Image background = go.GetComponent<Image>();
            background.color = new Color(0.09f, 0.07f, 0.05f, 0.96f);

            // the box sits beside the pointer: taking the pointer from the
            // element below it would make it blink away and back
            background.raycastTarget = false;

            label = MakeText(go.transform);
            label.raycastTarget = false;
            go.SetActive(false);
        }

        /// <summary>Puts a description on an element of the panel.</summary>
        public void Attach(GameObject target, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Attach(target, () => text);
            }
        }

        /// <summary>
        /// Same, with the text read when the box is about to show itself:
        /// the reveal button says something else depending on what it
        /// would do on the next press.
        /// </summary>
        public void Attach(GameObject target, System.Func<string> text)
        {
            if (target == null || text == null)
            {
                return;
            }

            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<EventTrigger>();
            }

            EventTrigger.Entry enter = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(_ => Hover(target, text));
            trigger.triggers.Add(enter);

            EventTrigger.Entry leave = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            leave.callback.AddListener(_ => Leave(target));
            trigger.triggers.Add(leave);
        }

        /// <summary>Forgets the pointer, the box goes back to hidden.</summary>
        public void Reset()
        {
            hovered = null;
            source = null;
            wait = 0f;
            Hide();
        }

        /// <summary>Follows the pointer while the panel is on screen.</summary>
        public void Tick(float deltaTime)
        {
            string text = hovered != null && source != null ? source() : null;

            if (string.IsNullOrEmpty(text))
            {
                if (shown)
                {
                    wait += deltaTime;
                    if (wait >= HideDelay)
                    {
                        Hide();
                    }
                }

                return;
            }

            wait += deltaTime;
            if (!shown)
            {
                if (wait < ShowDelay)
                {
                    return;
                }

                Show();
            }

            SetText(text);
            Place();
        }

        private void Hover(GameObject target, System.Func<string> text)
        {
            hovered = target;
            source = text;
            wait = 0f;
        }

        private void Leave(GameObject target)
        {
            if (hovered != target)
            {
                return;
            }

            hovered = null;
            source = null;
            wait = 0f;
        }

        private void Show()
        {
            shown = true;

            // over everything else of the panel, wherever it was created
            box.SetAsLastSibling();
            box.gameObject.SetActive(true);
        }

        private void Hide()
        {
            shown = false;
            box.gameObject.SetActive(false);
            label.text = string.Empty;
        }

        /// <summary>Sets the text and the size of the box, only when it changed.</summary>
        private void SetText(string text)
        {
            if (text == label.text)
            {
                return;
            }

            label.text = text;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, TextWidth);

            float height = Mathf.Ceil(label.preferredHeight);
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            box.sizeDelta = new Vector2(TextWidth + PaddingX * 2f, height + PaddingY * 2f);
        }

        /// <summary>
        /// Next to the pointer, on its other side when the panel has no
        /// room there: the box never leaves the rectangle of the panel.
        /// </summary>
        private void Place()
        {
            Vector2 point;
            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    Input.mousePosition,
                    null,
                    out point
                )
            )
            {
                return;
            }

            Rect rect = area.rect;
            float width = box.sizeDelta.x;
            float height = box.sizeDelta.y;

            // on the right of the pointer, on its left when the panel is too narrow
            float x = point.x + Gap;
            if (x + width > rect.xMax - Margin)
            {
                x = point.x - Gap - width;
            }

            // under the pointer, above it when the panel is too short
            float y = point.y - Gap;
            if (y - height < rect.yMin + Margin)
            {
                y = point.y + Gap;
            }

            x = Mathf.Clamp(x, rect.xMin + Margin, rect.xMax - Margin - width);
            y = Mathf.Clamp(y, rect.yMin + Margin + height, rect.yMax - Margin);

            box.anchoredPosition = new Vector2(x - rect.center.x, y - rect.center.y);
        }

        /// <summary>Label of the box, wrapped on the width of the text.</summary>
        private Text MakeText(Transform parent)
        {
            GameObject go = GUIManager.Instance.CreateText(
                string.Empty,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                GUIManager.Instance.AveriaSerif,
                FontSize,
                Panel.BodyColor,
                true,
                Color.black,
                TextWidth,
                FontSize + 6f,
                false
            );

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(PaddingX, -PaddingY);

            Text text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }
    }
}
