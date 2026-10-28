using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    // Procedural chamfered silhouette: native geometry, no generated/imported art.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatControlPlate : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float cut = Mathf.Min(12, r.height * .25f);
            var points = new[] {
                new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax),
                new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut) };
            mesh.AddVert(r.center, color, Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddVert(points[i], color, Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % 8 + 1);
        }
    }
    public sealed partial class PveBattleController
    {
        // Presentation state only: knowledge navigation never calls Act or a rule command.
        private bool _combatWikiOpen, _combatLogOpen;
        private bool _knowledgeBattleShell;
        private RectTransform _combatKnowledgeRoot;
        private int _combatWikiTopic;
        private readonly List<string> _combatJournal = new();
        private readonly Dictionary<string, float> _combatReadPosition = new();
        private static readonly string[] CombatWikiTopics =
            { "行动目标", "阶段与推进", "格挡与伤害", "费用与来源", "实体详情", "当前阶段规则", "战斗符号图鉴", "战场图标" };

        private void AppendCombatJournal(string operation, string before)
        {
            _combatJournal.Add("操作 " + _inputSequence + " · " + operation +
                (_inputRejected ? " · 未完成" : " · 已响应") + "\n操作前\n" + before +
                "\n操作反馈\n" + _feedback + "\n正式事件快照（可能沿用前次事件，不单独证明本次消费）\n" + (_battle?.LastEvent ?? "当前无战斗会话") +
                "\n操作后\n" + CombatJournalState());
        }

        private string CombatJournalState()
        {
            string state = InputSelectionState() + "\n卡牌模式 " + (_choice ?? "未选") +
                " · 技能选项 " + _skillOption + " · 部队模式 " + _unitMode + " · 攻击序号 " + (_attackSlot + 1);
            state += "\n付款技能 " + (_paymentSelection?.Skill?.Id ?? "未选") +
                " · 需色 " + _paymentRequired + " · 源色 " + _paymentSource +
                " · 材料 " + (_paymentMaterial?.Id ?? "无") + " · 已选源骰 " + (_paymentDie != null);
            var player = _battle?.Player ?? _journey?.Player;
            if (player == null) return state + "\n尚未建立玩家会话";
            state += "\n" + player.Name + " · 护甲 " + player.Armor + " · 名望 " + player.Fame + " · 创伤 " + player.Wounds;
            state += "\n晶体 " + string.Join(" / ", player.Mana.Crystals.Select(p => p.Key + " " + p.Value));
            state += "\n魔力 " + string.Join(" / ", player.Mana.Tokens.Select(p => p.Key + " " + p.Value));
            state += "\n手牌（实际顺序） " + string.Join(" / ", player.Deck.Hand.Select((c, i) => (i + 1) + ":" + CardName(c) + " [" + c.Id + "]"));
            state += "\n已打出 " + string.Join(" / ", player.Deck.PlayedCards.Select(c => c.Id));
            state += "\n部队 " + string.Join(" / ", player.Units.Select(u => u.Card.NameCn + " · 可发动 " + u.CanActivate + " · 创伤 " + u.Wounds));
            if (_battle != null)
            {
                state += "\n实体规则\n" + CombatEntityDetails();
                state += "\n目标实体 " + string.Join(" / ", _targets.Where(i => i >= 0 && i < _battle.Enemies.Count).OrderBy(i => i)
                    .Select(i => "#" + (i + 1) + " " + EnemyView(_battle.Enemies[i].Id).displayName));
                state += "\n阶段 " + PhaseIndicatorName(_battle.Phase) + "\n" + PoolText();
                state += "\n攻击来源 " + string.Join(" / ", _battle.Context.CombatPower.Attacks.Select(a => AttackSource(a) + " " + a.Element + " " + a.Value));
                state += "\n格挡来源 " + string.Join(" / ", _battle.Context.CombatPower.Blocks.Select(b => BlockSource(b) + " " + b.Element + " " + b.Value));
            }
            return state;
        }

        private string CombatEntityDetails()
        {
            if (_battle == null) return "当前处于战外。进入战斗并选中实体后可在此回查对应全文。";
            if (_selectedSkill != null) return _selectedSkill.Id + "\n" + _selectedSkill.PrintedRule + "\n" + SkillUsage(_selectedSkill);
            if (_selectedUnit != null) return _selectedUnit.Card.NameCn + "\n" + UnitText(_selectedUnit) + "\n\n原能力与费用\n" +
                string.Join("\n\n", (_selectedUnit.Card.Actions?.Select(a => a.PrintedRule + "\n费用：" +
                    (a.Cost.Length == 0 ? "免费" : string.Join("、", a.Cost.Select(ColorName)))) ?? Enumerable.Empty<string>()));
            if (_selected != null) return CardName(_selected) + "\n" + CardText(_selected);
            return "当前未选中手牌、技能或部队。悬停手牌查看原卡；选中实体后，本主题提供对应规则全文。";
        }

        private static void StyleCombatCaption(Button button, string id, float width, float height)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null || string.IsNullOrEmpty(text.text)) return;
            var oldGraphic = button.targetGraphic;
            // Graphic disallows a second Graphic on the Image-backed Button object.
            // A separate child preserves the Button/handlers and its selectable tint.
            var plateRect = W.Rect(button.transform, "ControlPlate", 0, 0, width, height);
            plateRect.SetAsFirstSibling();
            plateRect.gameObject.AddComponent<CanvasRenderer>();
            var plate = plateRect.gameObject.AddComponent<CombatControlPlate>();
            plate.color = oldGraphic.color;
            oldGraphic.enabled = false;
            button.targetGraphic = plate;
            foreach (Transform edge in button.transform)
                if (edge.name == "Top" || edge.name == "Bottom" || edge.name == "Left" || edge.name == "Right")
                    edge.gameObject.SetActive(false);
            // W.Button removes ten vertical pixels. A 35px control then has only 25px
            // for a 21pt msyh line (~28px), causing an entirely blank Ellipsis label.
            // Fix the actual glyph rectangle, preserving the original hit target/handler.
            text.rectTransform.anchoredPosition = new Vector2(8, 0);
            text.rectTransform.sizeDelta = new Vector2(width - 16, height);
            // Only short, fitting captions get a single line. Long rule/entity labels
            // retain wrapping and any caller-specific sizing instead of spilling out.
            float captionWidth = text.GetPreferredValues(text.text, float.PositiveInfinity, 0).x;
            bool singleLine = !text.text.Contains("\n") && captionWidth <= width - 16;
            text.textWrappingMode = singleLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            if (singleLine && height >= 29) text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            if (id == "Btn_BlockParts")
            {
                text.margin = Vector4.zero; text.enableAutoSizing = false;
                text.fontSize = 21; text.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void DrawCombatTacticalRail()
        {
            if (optimizedInteractionUi && _fluentSourceOpen) return;
            W.Text(_root, "HeroName", _battle.Player.Name, 187, 234, 238, 38, 25, W.Gold);
            W.Text(_root, "HeroIdentity", "等级 " + _battle.Player.Level + " · 护甲 " + _battle.Player.Armor +
                "\n创伤 " + _battle.Player.Wounds, 187, 278, 238, 64, 22, W.Cream);
            var rail = InformationSurface(_root, "TacticalRail", 58, 366, 367, 267, new Color32(26, 45, 38, 232));
            W.Panel(rail, "Accent", 0, 0, 4, 267, W.Gold);
            W.Text(rail, "DecisionHeading", "当前战术", 18, 14, 330, 36, 24, W.Gold);
            string demand = "目标 " + (_targets.Count == 0 ? "未选" : string.Join("、", _targets.OrderBy(i => i).Select(i => "#" + (i + 1)))) +
                "\n" + (_battle.Phase == MK.Logic.Core.Phase.Block ? "格挡资源 " + _battle.Context.BlockPool + " · 攻击 #" + (_attackSlot + 1) :
                _battle.Phase == MK.Logic.Core.Phase.AssignDamage ? "英雄护甲 " + _battle.Player.Armor + " · 待确认承伤" :
                "远程 " + _battle.AttackTotal(MK.Logic.Core.AttackType.Ranged) + " · 攻城 " + _battle.AttackTotal(MK.Logic.Core.AttackType.Siege) + "\n近战 " + _battle.AttackTotal(MK.Logic.Core.AttackType.Melee));
            W.Text(rail, "ImmediateDemand", demand, 18, 60, 330, 130, 22, W.Cream);
            KnowledgeButton(rail, "Btn_EntityKnowledge", "所选实体 · 全文详情", 18, 204, 330,
                () => { _combatWikiOpen = true; _combatLogOpen = false; _combatWikiTopic = 4; });
        }

        private Button KnowledgeButton(Transform parent, string id, string label,
            float x, float y, float width, System.Action action)
        {
            var button = W.Button(parent, id, label, x, y, width, 46);
            StyleCombatCaption(button, id, width, 46);
            button.onClick.AddListener(() => { action(); DrawCombatKnowledgeLayer(_knowledgeBattleShell); });
            return button;
        }

        private void DrawCombatKnowledgeLayer(bool battleShell = true)
        {
            _knowledgeBattleShell = battleShell;
            if (_combatKnowledgeRoot != null)
            { _combatKnowledgeRoot.gameObject.SetActive(false); Destroy(_combatKnowledgeRoot.gameObject); }
            _combatKnowledgeRoot = W.Rect(_root, "CombatKnowledge", 0, 0, 1920, 1080);
            // Old menu entry remains discoverable, but now routes into the same Wiki.
            if (_rules) { _rules = false; _combatWikiOpen = true; _combatWikiTopic = 5; }
            var wikiButton = KnowledgeButton(_combatKnowledgeRoot, "Btn_GlobalWiki", battleShell && embeddedBattleUi ? "规则" : "规则 Wiki", battleShell ? embeddedBattleUi ? 1410 : 950 : 1450, battleShell ? embeddedBattleUi ? 24 : 135 : 24, battleShell && embeddedBattleUi ? 100 : 180,
                () => { _combatWikiOpen = !_combatWikiOpen; _combatLogOpen = false; });
            var logButton = KnowledgeButton(_combatKnowledgeRoot, "Btn_CombatLog", (battleShell && embeddedBattleUi ? "日志 " : "战斗日志 ") + _combatJournal.Count, battleShell ? embeddedBattleUi ? 1525 : 1140 : 1645, battleShell ? embeddedBattleUi ? 24 : 135 : 24, battleShell && embeddedBattleUi ? 130 : 197,
                () => { _combatLogOpen = !_combatLogOpen; _combatWikiOpen = false; });
            if (battleShell && embeddedBattleUi)
            {
                StyleUtilityShortcut(wikiButton, BattleUtilityGlyph.Kind.Book);
                StyleUtilityShortcut(logButton, BattleUtilityGlyph.Kind.Journal);
                HoverInfo(wikiButton.gameObject, "操作与规则", "悬浮手牌放大原卡；悬浮角色查看详情。\n点击规则查看行动目标、阶段、费用和实体全文。\n英雄技能与部队入口在左侧，手牌在下方。\n查看说明不会推进阶段或消费资源。");
                HoverInfo(logButton.gameObject, "完整战斗日志", "打开或关闭按顺序保留的操作记录。\n包含操作前后状态、完整反馈与正式事件。\n查看日志不会清除当前选择或消费资源。");
                wikiButton.GetComponent<BattleHoverInfo>().compactGuidance = true;
                logButton.GetComponent<BattleHoverInfo>().compactGuidance = true;
                var symbols = KnowledgeButton(_combatKnowledgeRoot, "Btn_CombatSymbols", "图鉴", 1285, 90, 110,
                    () => { _combatWikiTopic = 6; _combatWikiOpen = true; _combatLogOpen = false; });
                StyleUtilityShortcut(symbols, BattleUtilityGlyph.Kind.Book);
                HoverInfo(symbols.gameObject, "战斗符号图鉴", "闪电=就绪，沙漏=耗竭，血滴数字=实际创伤。\n就绪不代表一定能激活。悬浮每个状态符号查看当前真实限制。\n点击查看全部符号定义；不会改变战斗选择或消费资源。");
                symbols.GetComponent<BattleHoverInfo>().compactGuidance = true;
            }
            if (battleShell && embeddedBattleUi && (_selected != null || _selectedUnit != null || _selectedSkill != null || _targets.Count > 0))
                KnowledgeButton(_combatKnowledgeRoot, "Btn_SelectedDetails", "所选详情", 1410, 90, 110,
                    () => { _combatWikiTopic = 4; _combatWikiOpen = true; _combatLogOpen = false; });
            if (!_combatWikiOpen && !_combatLogOpen) return;
            var shade = W.Panel(_combatKnowledgeRoot, "KnowledgeShade", 0, 0, 1920, 1080, new Color(0, .025f, .02f, .88f));
            shade.GetComponent<Image>().raycastTarget = true;
            var panel = InformationSurface(shade, "KnowledgePanel", 140, 92, 1640, 896, new Color32(29, 47, 40, 255), true);
            W.Text(panel, "Heading", _combatWikiOpen ? "全局 Wiki · 选择主题" : "战斗日志 · 正式事件与操作上下文", 35, 24, 1250, 52, 31, W.Gold);
            KnowledgeButton(panel, "Btn_ReturnCombat", "返回战斗", 1390, 22, 210,
                () => { _combatWikiOpen = false; _combatLogOpen = false; });
            string key, content;
            float left;
            if (_combatWikiOpen)
            {
                for (int i = 0; i < CombatWikiTopics.Length; i++)
                {
                    int topic = i;
                    KnowledgeButton(panel, "Btn_WikiTopic_" + i, (_combatWikiTopic == i ? "◆ " : "") + CombatWikiTopics[i],
                        30, 110 + i * 65, 275, () => _combatWikiTopic = topic);
                }
                left = 340; key = "wiki:" + _combatWikiTopic; content = CombatWikiText();
            }
            else
            {
                left = 35; key = "journal";
                content = _combatJournal.Count == 0 ? "尚无操作记录。返回战斗后，实际输入、目标、资源与正式事件将在此按顺序保留。"
                    : string.Join("\n\n━━━━━━━━━━━━━━━━━━━━\n\n", _combatJournal);
            }
            float width = 1600 - left;
            var viewport = W.Rect(panel, "ReadingViewport", left, 108, width, 682);
            viewport.gameObject.AddComponent<Image>().color = new Color32(216, 198, 155, 255);
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = W.Text(viewport, "FullText", content, 18, 0, width - 55, 682, 26, new Color32(54, 43, 26, 255));
            body.enableAutoSizing = false; body.overflowMode = TextOverflowModes.Overflow;
            float height = Mathf.Max(682, body.GetPreferredValues(content, width - 55, 0).y + 36);
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body.rectTransform;
            if (_combatWikiOpen && _combatWikiTopic == 7)
            {
                string[] marks = { "Attack", "Fire", "Ice", "ColdFire", "Summon", "Armor", "Fame", "Ranged", "Siege", "Wounds", "Influence", "Reputation", "Fortified", "Swift", "Brutal", "Poison", "Paralyze", "Regenerate", "FireResist", "IceResist", "ColdFireResist", "PhysicalResist", "MagicResist", "Assassination", "Elusive", "Defend", "Cumbersome", "Vampirism", "IgnoreSiteFortification", "Rule", "Success", "Alert" };
                string[] labels = { "物理攻击 / 近战", "火焰攻击", "寒冰攻击", "冰火攻击", "召唤攻击", "护甲 / 格挡资源", "名望", "远程资源", "攻城资源", "创伤", "影响力", "声誉", "城防", "迅捷", "残暴", "毒素", "麻痹", "再生", "火焰抗性", "寒冰抗性", "冰火抗性", "物理抗性", "魔法抗性", "刺杀", "闪避", "防护", "笨重", "吸血", "不受地点城防", "完整效果 / 费用 / 预览", "操作已有响应", "操作未完成" };
                int rows = (marks.Length + 2) / 3;
                var gallery = W.Rect(viewport,"FieldSymbolGuideContent",0,0,width,rows*76+height);
                float cellWidth=(width-36)/3;
                for(int i=0;i<marks.Length;i++)
                {
                    var cell=W.Rect(gallery,"FieldSymbolDefinition_"+i,18+i%3*cellWidth,i/3*76,cellWidth,76);
                    var glyph=W.Rect(cell,"Glyph",0,16,38,38).gameObject.AddComponent<BattleFieldMark>();
                    glyph.mark=marks[i];glyph.color=new Color32(125,86,33,255);glyph.raycastTarget=false;
                    W.Text(cell,"Name",labels[i],52,10,cellWidth-60,60,23,new Color32(54,43,26,255));
                }
                body.rectTransform.SetParent(gallery,false);body.rectTransform.anchoredPosition=new Vector2(18,-rows*76);scroll.content=gallery;
            }
            if (_combatWikiOpen && _combatWikiTopic == 6)
            {
                var gallery = W.Rect(viewport, "SymbolGuideContent", 0, 0, width, height + 244);
                int[] kinds = { 3, 4, 5, 2, 0, 1, 6, 7, 8 };
                string[] names = { "英雄", "等级", "护甲", "创伤", "就绪", "耗竭", "可选0", "可选N", "名望" };
                string[] meanings = { "冠徽 · 领袖身份", "军衔 · 原卡等级", "盾牌 · 当前有效值", "血滴 · 实际伤势", "闪电 · 非激活保证", "沙漏 · 已耗竭", "闭锁 · 无启用行动", "开锁 · 启用行动数", "星章 · 正式名望值" };
                float cellWidth = (width - 36) / 4;
                for (int i = 0; i < kinds.Length; i++)
                {
                    var cell = W.Rect(gallery, "SymbolDefinition_" + i, 18 + i % 4 * cellWidth, i / 4 * 76, cellWidth, 76);
                    var glyph = W.Rect(cell, "Glyph", 0, 10, 38, 38).gameObject.AddComponent<BattlePartyStateGlyph>();
                    glyph.kind = kinds[i]; glyph.color = new Color32(89, 60, 27, 255); glyph.raycastTarget = false;
                    W.Text(cell, "Name", names[i], 50, 0, cellWidth - 56, 34, 24, new Color32(89, 60, 27, 255));
                    W.Text(cell, "Meaning", meanings[i], 50, 36, cellWidth - 56, 38, 21, new Color32(54, 43, 26, 255));
                }
                body.rectTransform.SetParent(gallery, false);
                body.rectTransform.anchoredPosition = new Vector2(18, -244);
                scroll.content = gallery;
            }
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 42;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.verticalNormalizedPosition = _combatReadPosition.TryGetValue(key, out var position) ? position : 1;
            scroll.onValueChanged.AddListener(value => _combatReadPosition[key] = value.y);
            KnowledgeButton(panel, "Btn_ReadStart", "回到开头", left, 818, 190,
                () => _combatReadPosition[key] = 1);
            KnowledgeButton(panel, "Btn_ReadEnd", "阅读末行", left + 210, 818, 190,
                () => _combatReadPosition[key] = 0);
            W.Text(panel, "ReadingHint", "滚轮阅读全文 · 返回后保留卡牌、模式、目标与付款选择", left + 435, 822, width - 435, 40, 21, W.Muted);
        }

        private string CombatWikiText()
        {
            switch (_combatWikiTopic)
            {
                case 0: return "行动目标\n\n打出手牌、发动英雄技能或使用部队准备资源。点击战场敌人指定目标；攻击、格挡的有效数值与需求显示在战场和右侧决策处。核对费用、来源及目标后使用正式确认按钮。\n\n" +
                    (_knowledgeBattleShell && embeddedBattleUi ? "手牌位于下方，英雄技能与部队入口位于左侧。悬浮手牌放大原卡；悬浮角色查看详情。顶部日志可回查完整操作反馈。" : "手牌、英雄技能与部队入口位于底部。") +
                    "牌库及弃牌入口保留原有功能。查看此页不替你选择目标，也不消费资源。";
                case 1: return "阶段与推进\n\n远程／攻城 → 格挡 → 分配伤害 → 近战 → 战斗结束。当前阶段始终显示在顶部，右下方的情境按钮执行正式推进。跳过和终止条件由原规则决定。\n\n远程／攻城：准备资源，选择目标与来源，结算攻击。\n格挡：选择敌人的一次攻击，准备格挡，核对来源并正式分配。\n分配伤害：核对护甲，按需要选择部队承伤，再确认。\n近战：准备攻击并击败目标，最后结束战斗。\n战斗结束后仍可能需要战后治疗、地点奖励或结束回合补牌。";
                case 2: return "格挡与伤害\n\n每次攻击须完整格挡；迅捷需要双倍。对元素攻击的无效格挡按正式规则减半。未完全格挡的攻击会造成完整伤害。\n\n部队按顺序承伤，同场最多一次；抗性先吸收护甲值，剩余伤害造成部队创伤，再由英雄承受余量。\n\n城防会阻挡普通远程攻击；攻击不足不会累积削减护甲。实际有效攻击、格挡和需求应以当前目标旁的战术预览为准。";
                case 3: return "费用与来源\n\n魔力、晶体、公共源骰与卡牌／部队贡献是不同来源。顶部保留当前库存，战斗资源条保留可用贡献。右侧付款入口继续选择实际付款方式。\n\n选择来源或颜色不等于正式消费；确认出牌、能力或结算时由原规则执行。费用、选定来源与攻击／格挡需求无需查 Wiki 即可核对。操作失败会保留即时反馈；完整输入前后资源与正式事件保存在战斗日志。";
                case 4: return "所选实体 · 全文详情\n\n" + CombatEntityDetails() + "\n\n实体查阅\n\n悬停手牌查看原卡；敌人附近保留威胁、防御与状态。技能和部队原入口及全部原规则能力保留。关闭 Wiki 或日志不会清除当前卡牌、技能、部队、目标、模式和付款选择。";
                case 6: return "战斗符号图鉴\n\n冠徽：英雄/领袖身份。\n星章：名望，数值以英雄累计或敌人正式奖励读数为准。\n军衔：部队等级，数字为原卡等级。\n盾牌：当前有效护甲，含正式回合与旌旗加成。\n血滴与数字：实际创伤，0表示无创伤；和就绪状态独立。\n闪电：部队就绪，不保证当前阶段、费用或伤势允许激活。\n沙漏：部队耗竭；承伤资格及特殊能力依原规则独立判断。\n闭锁／可选0：当前已展开的能力、承伤或旌旗按钮均未启用。\n开锁／可选N：数字是以上最终已启用行动按钮的数量；不保证费用、目标已满足或必定结算成功。预选模式、切换目标或展开其他能力不计入。\n\n使用方法\n悬浮角色脚边或右侧的符号，读取当前数值、阶段限制和行动资格。右侧就绪/耗竭与可选N的短标注用于帮助建立符号含义。\n\n资源\n顶部六色魔力为临时库存，晶体为可存储资源；颜色名称及数量保留。公共魔力入口显示实际源骰；选择不等于正式消费。\n\n查阅图鉴不会消费资源、推进阶段或清除选择。";
                case 7: return "战场图标\n\n同一符号在不同位置的含义由上下文和悬浮标题明确区分：敌人上方是攻击，脚下是有效护甲与奖励，手牌上方是已经生成的战斗资源；英雄左上是护甲、创伤与累计名望。\n\n敌人特性逐项显示。红色斜杠表示当前已移除或已忽略，悬浮以正式状态和规则全文为准。攻击与护甲的原始数值、额外名望、复杂条件均保留在对应悬浮详情。\n\n书页表示可查阅完整效果、费用、规则或目标结算预览；反馈勾号仅表示已有响应，不能据此判断费用消费或战斗成功。\n\n这些信息入口不会消费魔力或推进阶段；真正的发动、出牌、付款和结算仍使用原行动按钮。";
                default: return _battle == null ? "当前处于战外或回合准备。\n\n依当前战术、补牌、升级或地点选择继续；战斗开始后本主题显示实际阶段规则。\n\n战斗结束与回合结束是不同边界，查看 Wiki 不会推进任何阶段。" : "当前阶段 · " + PhaseIndicatorName(_battle.Phase) + "\n\n" + RulesText() +
                    "\n\n当前真实选择\n" + InputSelectionState() + "\n\n" + PoolText() +
                    "\n\n规则信息用于解释，所有实际可执行操作仍在战斗界面。";
            }
        }
    }
}
