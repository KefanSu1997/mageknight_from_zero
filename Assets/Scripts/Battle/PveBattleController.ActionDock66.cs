using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Presentation only: keep native selectable objects, listeners and captions.
        private void RestyleActionDock66()
        {
            var dock = _root.Find("Inspector");
            if (dock == null || dock.Find("ActionDock66Installed") != null) return;
            W.Rect(dock, "ActionDock66Installed", 0, 0, 0, 0);
            var all = dock.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            foreach (var b in all.Where(b => b.name == "Btn_Next" || b.name == "Btn_EndTurn" || b.name == "Btn_Restart" || b.name == "Btn_FinishSite" || b.name == "Btn_SiteReward"))
                Dock66Control(b, 2);
            if (_selected == null)
            {
                foreach (var resolve in all.Where(b => b.name == "Btn_Resolve")) Dock66Control(resolve, 3);
                return;
            }
            var modes = all.Where(b => b.name == "Btn_Choice_base" || b.name == "Btn_Choice_strong").ToArray();
            if (modes.Length != 2) return;
            var content = modes[0].transform.parent as RectTransform;
            if (content == null) return;
            var choices = all.Where(b => b.transform.parent == content && b.name.StartsWith("Btn_Choice_", StringComparison.Ordinal) && !modes.Contains(b)).ToArray();
            var commands = all.Where(b => b.transform.parent == content && !b.name.StartsWith("Btn_Choice_", StringComparison.Ordinal)).ToArray();
            // The title and existing full-rule hover reader remain at the top.
            float y = 64;
            var ruleReader = content.Find("FieldFact_CurrentCardEffect");
            if (ruleReader != null) Place(ruleReader, 294, 54, 44, 44);
            var feeLines = CardActionPreview().Split('\n').Where(s => s.StartsWith("印刷", StringComparison.Ordinal) || s.StartsWith("免费强化：", StringComparison.Ordinal)).ToArray();
            Dock66Heading(content, "Dock66ModeHeading", "效果模式", ref y);
            float modeHeight = 50;
            foreach (var b in modes)
            {
                var t = b.GetComponentInChildren<TMP_Text>();
                modeHeight = Mathf.Max(modeHeight, t.GetPreferredValues(t.text, 118, 0).y + 14);
            }
            for (int i = 0; i < modes.Length; i++)
            {
                Place(modes[i].transform, 8 + 170 * i, y, 160, modeHeight);
                Dock66Control(modes[i], 0);
            }
            y += modeHeight + 8;
            if (feeLines.Length > 0)
            {
                var fee = W.Text(content, "Dock66ActualFees", string.Join("\n", feeLines), 14, y, 318, 40, 21, W.Cream);
                fee.enableAutoSizing = false; fee.textWrappingMode = TextWrappingModes.Normal;
                fee.overflowMode = TextOverflowModes.Overflow; fee.raycastTarget = false;
                float h = Mathf.Max(32, fee.GetPreferredValues(fee.text, 318, 0).y + 8);
                Place(fee.transform, 14, y, 318, h); y += h + 6;
            }
            if (choices.Length > 0)
            {
                // These native choices may be an effect output, not a payment.
                Dock66Heading(content, "Dock66SourceHeading", "效果选项", ref y);
                bool compact = choices.All(b => b.GetComponentInChildren<TMP_Text>().GetPreferredValues(b.GetComponentInChildren<TMP_Text>().text, float.PositiveInfinity, 0).x <= 116);
                int columns = compact ? 2 : 1;
                float width = compact ? 160 : 330;
                for (int row = 0; row < choices.Length; row += columns)
                {
                    float h = 48;
                    for (int j = row; j < Mathf.Min(row + columns, choices.Length); j++)
                    {
                        var t = choices[j].GetComponentInChildren<TMP_Text>();
                        h = Mathf.Max(h, t.GetPreferredValues(t.text, width - 46, 0).y + 12);
                    }
                    for (int j = row; j < Mathf.Min(row + columns, choices.Length); j++)
                    { Place(choices[j].transform, 8 + (j - row) * 170, y, width, h); Dock66Control(choices[j], 1); }
                    y += h + 8;
                }
            }
            // Prerequisites/alternatives precede the primary execution. Keep
            // the committing action at the hand-facing end of the same group.
            foreach (var b in commands.OrderBy(b => b.name == "Btn_Play" ? 1 : 0))
            {
                var t = b.GetComponentInChildren<TMP_Text>(); if (t == null) continue;
                float h = Mathf.Max(48, t.GetPreferredValues(t.text, 274, 0).y + 16);
                Place(b.transform, 8, y + 6, 330, h);
                Dock66Control(b, b.name == "Btn_Play" ? 3 : 1); y += h + 14;
            }
            StatusFolio66(content, "Dock66SharedFolio", -2, -4, 354, y + 12, 0);
            var heading = content.Find("ContextTitle")?.GetComponent<TMP_Text>();
            if (heading != null) { heading.color = W.Cream; EmbossFieldText(heading); }
            var viewport = dock.Find("ActionViewport") as RectTransform;
            if (y > 470 && viewport == null)
            {
                viewport = W.Rect(dock, "ActionViewport", 0, 0, 352, 470);
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>(); content.SetParent(viewport, false);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
                scroll.horizontal = false; scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            content.sizeDelta = new Vector2(352, Mathf.Max(470, y + 8));
        }

        private static void Dock66Heading(Transform parent, string id, string label, ref float y)
        {
            var t = W.Text(parent, id, label, 16, y, 270, 30, 21, new Color32(185, 205, 213, 255));
            t.raycastTarget = false; t.fontStyle = FontStyles.Bold; y += 34;
        }

        private static void Dock66Control(Button button, int role)
        {
            var r = (RectTransform)button.transform;
            var caption = button.GetComponentInChildren<TMP_Text>(); if (caption == null) return;
            bool selected = caption.text.StartsWith("●", StringComparison.Ordinal);
            string[] retired = { "ChoiceRuneMount", "ChoiceModeGlyph", "ControlPlate", "PrimaryExecutionSeal", "PhaseExecutionSeal", "PhaseForwardGlyph", "ActionReadability", "ModeSelectionUnderline", "SelectedResourceRing", "RuneMount", "ControlTopEdge", "ControlBottomEdge" };
            foreach (Transform child in button.transform)
                if (retired.Contains(child.name)) child.gameObject.SetActive(false);
            var hit = button.GetComponent<Graphic>();
            if (hit != null) { hit.enabled = true; hit.color = Color.clear; button.targetGraphic = hit; }
            float w = r.rect.width, h = r.rect.height;
            caption.enableAutoSizing = false; caption.fontSize = Mathf.Max(21, caption.fontSize);
            caption.textWrappingMode = TextWrappingModes.Normal; caption.overflowMode = TextOverflowModes.Overflow;
            caption.color = !button.interactable ? new Color32(173, 182, 184, 255) : selected ? new Color32(249, 217, 143, 255) : W.Cream;
            caption.outlineColor = new Color32(9, 20, 28, 255); caption.outlineWidth = .16f;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            if (role == 0)
            {
                var tab = W.Rect(r, "Dock66ModeTab", 0, 0, w, h).gameObject.AddComponent<BattleDock66Shape>();
                tab.role = 0; tab.color = selected ? new Color32(67, 94, 108, 255) : new Color32(26, 45, 57, 255); tab.raycastTarget = false; tab.transform.SetAsFirstSibling();
                Place(caption.transform, 16, 0, w - 28, h);
                var line = W.Panel(r, "Dock66ModeState", 12, h - 4, w - 24, selected ? 4 : 1, selected ? W.Gold : new Color32(110, 137, 149, 255));
                line.GetComponent<Graphic>().raycastTarget = false;
            }
            else if (role >= 2)
            {
                var body = W.Rect(r, "Dock66Execution", 0, 0, w, h).gameObject.AddComponent<BattleDock66Shape>();
                body.role = role; body.color = role == 3 && button.interactable ? new Color32(129, 92, 40, 255) : new Color32(31, 58, 66, 255);
                body.raycastTarget = false; body.transform.SetAsFirstSibling();
                Place(caption.transform, 18, 0, w - 52, h);
                var glyph = button.transform.Find("ActionObjectGlyph"); if (glyph != null) glyph.gameObject.SetActive(false);
                var arrow = W.Rect(r, "Dock66ExecuteArrow", w - 39, (h - 28) / 2, 28, 28).gameObject.AddComponent<BattleCardCommandGlyph>();
                arrow.play = true; arrow.color = W.Gold; arrow.raycastTarget = false;
            }
            else
            {
                bool glyph = button.transform.Find("ActionObjectGlyph") != null;
                Place(caption.transform, glyph ? 40 : 14, 0, w - (glyph ? 46 : 24), h);
                var rule = W.Panel(r, "Dock66ChoiceRule", 10, h - 2, w - 20, selected ? 3 : 1, selected ? W.Gold : new Color32(81, 110, 120, 255));
                rule.GetComponent<Graphic>().raycastTarget = false;
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleDock66Shape : MaskableGraphic
    {
        public int role;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            Vector2[] points = role == 0
                ? new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax - 10), new Vector2(r.xMax - 12, r.yMax), new Vector2(r.xMin, r.yMax) }
                : new[] { new Vector2(r.xMin + 6, r.yMin), new Vector2(r.xMax - 24, r.yMin), new Vector2(r.xMax, r.center.y), new Vector2(r.xMax - 24, r.yMax), new Vector2(r.xMin + 6, r.yMax), new Vector2(r.xMin, r.center.y) };
            mesh.AddVert(r.center, color, Vector2.zero);
            foreach (var p in points) mesh.AddVert(p, Color.Lerp(color, Color.white, Mathf.InverseLerp(r.yMin, r.yMax, p.y) * .16f), Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % points.Length + 1);
        }
    }
}


