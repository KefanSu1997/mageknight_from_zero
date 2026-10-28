using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    /// <summary>Read the existing original card without selecting or playing it.</summary>
    public sealed class BattleCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        public Sprite artwork;
        public RectTransform surface;
        public float previewX;
        public float previewY = 270;
        // The inspector is the card-reading surface in the fluent layout.
        // Keep the entire enemy area, resource strip and mana tray unobstructed.
        public bool dockInInspector;
        public bool readingWell;
        public string cardTitle, printedRules;
        private GameObject _preview;
        private ScrollRect _readingScroll;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (surface == null || _preview != null || (artwork == null && string.IsNullOrEmpty(printedRules))) return;
            if (readingWell)
            {
                // A transient reading mode replaces the dock cleanly, rather than
                // leaving fragments of its old text visible around the enlarged card.
                var well = W.Panel(surface, "CardHoverPreview", 1532, 178, 352, 540, new Color32(12, 29, 28, 255));
                _preview = well.gameObject;
                var identity = W.Panel(well, "ReadingIdentity", 0, -38, 352, 38, new Color32(12, 29, 28, 255));
                var title = W.Text(identity, "ReadingTitle", "阅读 · " + cardTitle, 8, 3, 336, 32, 22, W.Gold);
                title.enableAutoSizing = false;
                var viewport = W.Rect(well, "ReadingViewport", 10, 8, 332, 438);
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = W.Rect(viewport, "Content", 0, 0, 332, 438);
                if (artwork != null) W.Sprite(content, "OriginalCard", artwork, 16, 0, 300, 438);
                float rulesTop = artwork != null ? 450 : 0;
                var rules = W.Text(content, "PrintedRules", cardTitle + "\n\n" + printedRules,
                    6, rulesTop, 320, 438, 22, W.Cream);
                rules.richText = false; rules.enableAutoSizing = false; rules.overflowMode = TextOverflowModes.Overflow;
                float rulesHeight = rules.GetPreferredValues(rules.text, 320, 0).y + 24;
                rules.rectTransform.sizeDelta = new Vector2(320, rulesHeight);
                content.sizeDelta = new Vector2(332, Mathf.Max(438, rulesTop + rulesHeight));
                _readingScroll = viewport.gameObject.AddComponent<ScrollRect>();
                _readingScroll.viewport = viewport; _readingScroll.content = content;
                _readingScroll.horizontal = false; _readingScroll.vertical = true;
                _readingScroll.inertia = false; _readingScroll.scrollSensitivity = 35;
                _readingScroll.movementType = ScrollRect.MovementType.Clamped;
                _readingScroll.verticalNormalizedPosition = 1;
                W.Text(well, "ReadingHint", "在手牌上滚轮阅读全文\n移出手牌恢复操作", 10, 449, 332, 60, 21, W.Cream, TextAlignmentOptions.Center);
                var progress = W.Text(well, "ReadingProgress", "", 10, 510, 332, 28, 21, W.Gold, TextAlignmentOptions.Center);
                progress.enableAutoSizing = false;
                var track = W.Rect(well, "ReadingTrack", 347, 8, 3, 438);
                track.gameObject.AddComponent<Image>().color = new Color32(78, 96, 88, 255);
                float thumbHeight = Mathf.Max(24, 438 * 438 / content.sizeDelta.y);
                var thumb = W.Rect(track, "ReadingPosition", 0, 0, 3, thumbHeight);
                thumb.gameObject.AddComponent<Image>().color = W.Gold;
                void UpdateReadingPosition(Vector2 ignored)
                {
                    float position = 1 - _readingScroll.verticalNormalizedPosition;
                    thumb.anchoredPosition = new Vector2(0, -position * (438 - thumbHeight));
                    progress.text = position < .001f ? "原卡 · 向下阅读规则" :
                        position > .999f ? "规则末尾 · 向上回看原卡" : "阅读进度 " + Mathf.RoundToInt(position * 100) + "%";
                }
                _readingScroll.onValueChanged.AddListener(UpdateReadingPosition);
                UpdateReadingPosition(Vector2.zero);
                foreach (var graphic in well.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                return;
            }
            if (artwork == null) return;
            var panel = W.Panel(surface, "CardHoverPreview", dockInInspector ? 1478 : Mathf.Clamp(previewX, 38, 1020), previewY, 316, 454, W.Ink, true);
            _preview = panel.gameObject;
            W.Sprite(panel, "OriginalCard", artwork, 8, 8, 300, 438);
        }
        public void OnPointerExit(PointerEventData eventData) => Hide();
        // The pointer stays on the hand card: scrolling reads the transient panel
        // without selecting/playing the card or covering another input surface.
        public void OnScroll(PointerEventData eventData)
        { if (readingWell && _readingScroll != null) _readingScroll.OnScroll(eventData); }
        private void OnDisable() => Hide();
        private void Hide() { if (_preview != null) { _preview.SetActive(false); Destroy(_preview); } _preview = null; _readingScroll = null; }
    }
}
