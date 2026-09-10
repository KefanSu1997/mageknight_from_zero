using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.Adventure.Presentation
{
    // 可供后续正式UI复用的基础组件；不含任何场景或规则分支。
    public static class AdventureWidgets
    {
        public static readonly Color Gold = new Color32(206, 174, 116, 255);
        public static readonly Color Cream = new Color32(239, 237, 222, 255);
        public static readonly Color Muted = new Color32(164, 185, 177, 255);
        public static readonly Color Ink = new Color32(12, 29, 28, 250);
        public static TMP_FontAsset Font => Resources.Load<TMP_FontAsset>("Fonts/msyh TMP Dynamic") ?? TMP_Settings.defaultFontAsset;

        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }
        public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color tint, bool border = false)
        {
            var r = Rect(parent, name, x, y, w, h); var image = r.gameObject.AddComponent<Image>();
            image.color = tint; image.raycastTarget = false;
            if (border)
            {
                var color = new Color(Gold.r, Gold.g, Gold.b, .45f);
                Panel(r, "Top", 0, 0, w, 1, color); Panel(r, "Bottom", 0, h - 1, w, 1, color);
                Panel(r, "Left", 0, 0, 1, h, color); Panel(r, "Right", w - 1, 0, 1, h, color);
            }
            return r;
        }
        public static TextMeshProUGUI Text(Transform p, string name, string text, float x, float y, float w, float h, int size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Rect(p, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font; t.fontSize = size; t.color = color; t.text = text; t.alignment = align;
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }
        public static Button Button(Transform p, string name, string label, float x, float y, float w, float h, bool primary = false)
        {
            var r = Panel(p, name, x, y, w, h, primary ? new Color32(121, 94, 49, 255) : Ink, true);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>(); b.targetGraphic.raycastTarget = true;
            Text(r, "Label", label, 10, 5, w - 20, h - 10, 21, Cream, TextAlignmentOptions.Center); return b;
        }
        public static Image Sprite(Transform p, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var image = Rect(p, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
        }
    }
}
