using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class BattleHoverInfo
    {
        private void ShowInspection()
        {
            var panel = CreateFolio(surface, "BattleHoverInfo", 1532, 140, 352, 560);
            preview = panel.gameObject;
            InspectionGlyph(panel, "InspectionIdentity", 18, 12, 26, inspection.identityGlyph);
            InspectionText(panel, "InspectionRole", "正在查看 · " + inspection.role, 54, 9, 280, 34, 21, FolioHeading);
            var title = InspectionText(panel, "Heading", heading, 18, 45, 316, 68, 24, FolioHeading);
            title.ForceMeshUpdate();
            float titleHeight = Mathf.Max(40, title.preferredHeight + 8);
            title.rectTransform.sizeDelta = new Vector2(316, titleHeight);

            // The return cue is a separate operation context, never another
            // entity's full stat/rule paragraph beneath the inspected entity.
            var context = InspectionText(panel, "InspectionReturnContext", "当前操作 · " + selectedContext + "\n移开回到操作", 18, 0, 316, 90, 21, FolioHeading);
            context.ForceMeshUpdate();
            float contextHeight = Mathf.Max(54, context.preferredHeight + 8);
            context.rectTransform.sizeDelta = new Vector2(316, contextHeight);
            float top = 45 + titleHeight + 12;
            float viewportHeight = 560 - top - contextHeight - 32 - 24;
            var viewport = W.Rect(panel, "EntityHoverViewport", 18, top, 316, viewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = W.Rect(viewport, "InspectionContent", 0, 0, 316, viewportHeight);
            float y = 4;

            if (inspection.facts.Count > 0)
            {
                float width = 316f / inspection.facts.Count;
                for (int i = 0; i < inspection.facts.Count; i++)
                {
                    var fact = inspection.facts[i];
                    var cell = W.Rect(content, fact.id, i * width, y, width, 72);
                    InspectionGlyph(cell, "FactGlyph", 8, 0, 23, fact.glyph);
                    InspectionText(cell, "FactLabel", fact.label, 36, 0, width - 38, 30, 21, FolioHeading);
                    InspectionText(cell, "FactValue", fact.value, 4, 32, width - 8, 38, 28, FolioInk, TextAlignmentOptions.Center);
                }
                y += 82;
            }
            if (inspection.resources.Count > 0)
            {
                ResourceGrid(content, "Mana", "魔力 · 回合结束清空", inspection.resources.ToArray(), false, ref y);
                ResourceGrid(content, "Crystals", "晶体 · 金/黑不可储存", inspection.resources.Where(r => r.crystal != null).ToArray(), true, ref y);
            }
            foreach (var section in inspection.sections)
            {
                if (string.IsNullOrWhiteSpace(section.text)) continue;
                var group = W.Rect(content, section.id, 0, y, 316, 80);
                InspectionGlyph(group, "SectionGlyph", 4, 1, 23, section.glyph);
                InspectionText(group, "SectionTitle", section.title, 38, 0, 274, 32, 21, FolioHeading);
                var text = InspectionText(group, "SectionBody", section.text, 4, 36, 308, 100, Mathf.Max(22, bodyFontSize), FolioInk);
                text.margin = new Vector4(2, 2, 2, 2);
                text.ForceMeshUpdate();
                float height = Mathf.Max(text.preferredHeight, text.GetRenderedValues(false).y) + 12;
                text.rectTransform.sizeDelta = new Vector2(308, height);
                group.sizeDelta = new Vector2(316, height + 36);
                y += height + 48;
            }
            if (!string.IsNullOrWhiteSpace(inspection.hint))
            {
                var hint = InspectionText(content, "InspectionHint", inspection.hint, 4, y, 308, 80, 21, FolioHeading);
                hint.ForceMeshUpdate();
                float height = hint.preferredHeight + 10;
                hint.rectTransform.sizeDelta = new Vector2(308, height); y += height;
            }
            content.sizeDelta = new Vector2(316, Mathf.Max(viewportHeight, y + 18));
            _entityScroll = panel.gameObject.AddComponent<ScrollRect>();
            _entityScroll.viewport = viewport; _entityScroll.content = content;
            _entityScroll.horizontal = false; _entityScroll.vertical = true;
            _entityScroll.inertia = false; _entityScroll.scrollSensitivity = 32;
            _entityScroll.movementType = ScrollRect.MovementType.Clamped;
            float progressTop = top + viewportHeight + 4;
            var progress = InspectionText(panel, "EntityReadingProgress", "", 18, progressTop, 316, 32, 21, FolioHeading);
            context.rectTransform.anchoredPosition = new Vector2(18, -(progressTop + 36));
            W.Panel(panel, "InspectionContextDivider", 18, progressTop + 33, 316, 1, new Color32(121, 93, 49, 140));
            void UpdateProgress(Vector2 value) => progress.text = y + 18 > viewportHeight
                ? "在角色上滚轮 · " + Mathf.RoundToInt((1 - value.y) * 100) + "%" : "信息已显示";
            _entityScroll.onValueChanged.AddListener(UpdateProgress);
            _entityScroll.verticalNormalizedPosition = 1;
            UpdateProgress(new Vector2(0, 1));
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }

        private static TMP_Text InspectionText(Transform parent, string id, string text, float x, float y, float width, float height,
            int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = W.Text(parent, id, text, x, y, width, height, size, color, alignment);
            label.fontStyle = FontStyles.Bold; label.enableAutoSizing = false; label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static void InspectionGlyph(Transform parent, string id, float x, float y, float size, int kind)
        {
            var rect = W.Rect(parent, id, x, y, size, size);
            if (kind == 9)
            {
                var g = rect.gameObject.AddComponent<BattleThreatSwords>(); g.color = FolioHeading; g.raycastTarget = false;
            }
            else if (kind == 10)
            {
                var g = rect.gameObject.AddComponent<BattleUtilityGlyph>(); g.kind = BattleUtilityGlyph.Kind.Book; g.color = FolioHeading; g.raycastTarget = false;
            }
            else
            {
                var g = rect.gameObject.AddComponent<BattlePartyStateGlyph>(); g.kind = kind; g.color = FolioHeading; g.raycastTarget = false;
            }
        }

        private static void ResourceGrid(RectTransform content, string id, string heading, BattleInspectionResource[] resources, bool crystals, ref float y)
        {
            InspectionText(content, id + "Heading", heading, 4, y, 308, 32, 21, FolioHeading); y += 34;
            int columns = resources.Length;
            var measure = InspectionText(content, "ResourceMeasure", "", 0, 0, 100, 40, 24, FolioInk);
            measure.gameObject.SetActive(false);
            if (resources.Any(r => measure.GetPreferredValues(crystals ? r.crystal : r.mana, float.PositiveInfinity, 0).x + 8 > 316f / columns))
                columns = Mathf.Min(3, columns);
            Color[] tints = { new Color32(222,94,76,255), new Color32(91,166,226,255), new Color32(112,182,115,255),
                new Color32(224,230,216,255), W.Gold, new Color32(66,61,73,255) };
            float width = 316f / columns;
            for (int i = 0; i < resources.Length; i++)
            {
                var resource = resources[i];
                var cell = W.Rect(content, id + "_" + resource.id, i % columns * width, y + i / columns * 70, width, 68);
                var g = W.Rect(cell, "ResourceGlyph", 2, 1, 23, 23).gameObject.AddComponent<BattleResourceGlyph>();
                g.element = resource.element; g.crystal = crystals; g.color = tints[resource.element]; g.raycastTarget = false;
                InspectionText(cell, "ResourceColor", resource.label, 28, 0, width - 28, 28, 21, FolioHeading);
                InspectionText(cell, "ResourceValue", crystals ? resource.crystal : resource.mana, 4, 28, width - 8, 38, 24, FolioInk, TextAlignmentOptions.Center);
            }
            y += Mathf.CeilToInt((float)resources.Length / columns) * 70 + 12;
        }
    }
}
