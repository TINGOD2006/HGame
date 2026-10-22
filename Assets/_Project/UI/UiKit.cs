using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HGame.Unity
{
    public static class UiKit
    {
        public static TMP_FontAsset Font;
        public static readonly Color Background = Hex("111829"), Panel = Hex("1B263B"), PanelLight = Hex("26344D");
        public static readonly Color Ink = Hex("F1EEE8"), Muted = Hex("A4B2C9"), Gold = Hex("E4BF7D"), Teal = Hex("7CC9BB"), Pink = Hex("D994B9");
        public static Color Hex(string hex) { Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c; }
        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        public static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        public static Image PanelAt(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = color;
            image.raycastTarget = false; return image;
        }
        public static TextMeshProUGUI Text(string value, Transform parent, float x, float y, float w, float h, float size = 20, Color? color = null)
        {
            var r = Rect("Text", parent, x, y, w, h); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font; t.text = value; t.fontSize = size; t.color = color ?? Ink;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            t.margin = Vector4.zero; return t;
        }
        public static Button Button(string name, string label, Transform parent, float x, float y, float w, float h, Action click, bool enabled = true, Color? accent = null)
        {
            var r = Rect(name, parent, x, y, w, h);
            var image = r.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>("Art/Kenney/button");
            if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; }
            image.color = accent ?? PanelLight;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.interactable = enabled;
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = Teal; colors.disabledColor = new Color(0.55f, 0.57f, 0.63f, 0.65f); b.colors = colors;
            var t = Text(label, r, 14, 8, w - 28, h - 16, 18, enabled ? Ink : Muted); t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = 18;
            t.overflowMode = TextOverflowModes.Ellipsis;
            string iconPath = name == "SaveManual" ? "save" : name == "Help" ? "gear" : "";
            if (iconPath != "")
            {
                var icon = Rect("Icon", r, 12, (h - 20) / 2, 20, 20).gameObject.AddComponent<Image>();
                icon.sprite = Resources.Load<Sprite>("Art/Kenney/" + iconPath); icon.color = Muted; icon.raycastTarget = false;
                t.rectTransform.anchoredPosition = new Vector2(42, -8); t.rectTransform.sizeDelta = new Vector2(w - 52, h - 16);
            }
            b.onClick.AddListener(() => click?.Invoke()); return b;
        }
        public static void Bar(Transform parent, float x, float y, float width, string label, int value, int max, Color color)
        {
            PanelAt("Bar", parent, x, y, width, 8, Background);
            PanelAt("Fill", parent, x, y, width * Mathf.Clamp01(max > 0 ? (float)value / max : 0), 8, color);
            Text(label + "  " + value + " / " + max, parent, x, y - 27, width, 24, 16, Muted);
        }
        public static RectTransform GridScroll(Transform parent, float x, float y, float w, float h, float cellW, float cellH)
        {
            var root = Rect("ActionScroll", parent, x, y, w, h);
            root.gameObject.AddComponent<Image>().color = Color.clear;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root, 0, 0, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, w - 10, 0);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(12, 10); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2; grid.padding = new RectOffset(1, 1, 1, 1);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            viewport.sizeDelta = new Vector2(w - 10, h);
            var track = Rect("Scrollbar", root, w - 6, 0, 6, h); var trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = Background;
            var handle = Rect("Handle", track, 0, 0, 6, h); var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.49f, 0.79f, 0.73f, 0.6f);
            Stretch(handle);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.value = 1;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) { var go = parent.GetChild(i).gameObject; go.SetActive(false); UnityEngine.Object.Destroy(go); }
        }
        public static void Artwork(Transform parent, float x, float y, float w, float h)
        {
            var texture = Resources.Load<Texture2D>("Art/heroine-reference");
            if (texture == null) return;
            var frame = Rect("ArtworkFrame", parent, x, y, w, h);
            var r = Rect("HeroineArtwork", frame, 0, 0, w, h); var raw = r.gameObject.AddComponent<RawImage>(); raw.texture = texture;
            var ratio = r.gameObject.AddComponent<AspectRatioFitter>(); ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ratio.aspectRatio = (float)texture.width / texture.height; raw.raycastTarget = false;
        }
    }
}
