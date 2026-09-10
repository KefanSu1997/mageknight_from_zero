using System;
using System.Collections.Generic;
using System.Linq;
using MageKnight.Adventure.Content;
using MK.Logic.Core;
using MK.Logic.Runtime.Adventure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Adventure.Presentation
{
    /// <summary>共享桌面：场景组合、手牌、目标检查器、一个主要行动。无规则计算与直接资源修改。</summary>
    public sealed partial class AdventureTableView
    {
        private readonly RectTransform _root, _stage, _hand, _unitRail, _menu;
        private readonly Action<string> _command;
        private readonly AdventureActorView _actorPrefab;
        private readonly RawImage _background;
        private readonly TextMeshProUGUI _title, _phase, _objective, _message, _contextTitle, _contextBody, _rule;
        private readonly TextMeshProUGUI _primaryLabel, _enhanceLabel, _resourceCaption, _resourceValue;
        private readonly Button _primary, _enhance, _cancel, _sideways;
        private readonly TextMeshProUGUI _mana;
        private readonly Dictionary<string, TextMeshProUGUI> _values = new();
        private readonly Dictionary<string, AdventureActorView> _actors = new();
        private readonly Dictionary<string, Graphic> _sites = new();
        private readonly Dictionary<string, TextMeshProUGUI> _siteLabels = new();
        private readonly Dictionary<int, (RectTransform rect, Image face, TextMeshProUGUI note)> _cards = new();
        private AdventureDefinition _definition;
        private string _stageKey = "", _handKey = "", _unitKey = "";
        private bool _rulesOpen;
        public LocationDefinition Location { get; private set; }

        public AdventureTableView(Canvas canvas, Action<string> command)
        {
            _command = command;
            _actorPrefab = Resources.Load<AdventureActorView>("Adventure/Prefabs/ActorView");
            if (_actorPrefab == null) throw new InvalidOperationException("缺少Adventure角色Prefab，请先生成内容资源。");
            var outer = Panel(canvas.transform, "Background", 0, 0, 1920, 1080, new Color32(7, 19, 18, 255));
            outer.anchorMin = Vector2.zero; outer.anchorMax = Vector2.one; outer.offsetMin = outer.offsetMax = Vector2.zero;
            _root = Rect(canvas.transform, "UIRoot", 0, 0, 1920, 1080);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(.5f, .5f); _root.anchoredPosition = Vector2.zero;
            Text(_root, "Brand", "M A G E   K N I G H T   /   秘法行旅", 48, 24, 850, 25, 15, Gold);
            _title = Text(_root, "Title", "", 46, 62, 700, 61, 42, Cream);
            _objective = Text(_root, "Objective", "", 48, 123, 1260, 33, 19, Muted);
            HeaderValue("hand", "手牌", 780); HeaderValue("fame", "名望", 930);
            HeaderValue("wounds", "伤口", 1080); HeaderValue("redCrystal", "红晶", 1230);
            _mana = Text(_root, "Mana", "", 780, 140, 580, 28, 18, Muted);
            var menuButton = Button(_root, "Btn_Menu", "选择冒险", 1440, 65, 205, 57);
            menuButton.onClick.AddListener(() => _menu.gameObject.SetActive(!_menu.gameObject.activeSelf));
            var reset = Button(_root, "Btn_Reset", "重新开始", 1660, 65, 205, 57);
            reset.onClick.AddListener(() => _command("reset"));

            _stage = Rect(_root, "Stage", 48, 178, 1320, 500);
            _background = Rect(_stage, "Location", 0, 0, 1320, 500).gameObject.AddComponent<RawImage>();
            _background.raycastTarget = false;
            Panel(_stage, "TopShade", 0, 0, 1320, 58, new Color32(7, 22, 22, 190));
            _phase = Text(_stage, "Phase", "", 22, 14, 1230, 35, 22, Cream);
            _unitRail = Rect(_root, "Units", 695, 684, 673, 44);
            Text(_root, "HandTitle", "你的手牌", 49, 686, 400, 43, 22, Gold);
            _hand = Rect(_root, "Hand", 48, 728, 1320, 306);

            var inspector = Panel(_root, "Inspector", 1400, 178, 472, 856, Ink, true);
            Text(inspector, "Heading", "当前行动", 26, 25, 420, 37, 23, Gold);
            _contextTitle = Text(inspector, "ContextTitle", "", 26, 87, 420, 65, 31, Cream);
            _contextBody = Text(inspector, "ContextBody", "", 26, 147, 420, 143, 21, Muted);
            _resourceCaption = Text(inspector, "ResourceCaption", "", 26, 301, 270, 30, 19, Gold);
            _resourceValue = Text(inspector, "ResourceValue", "", 294, 289, 146, 51, 35, Cream, TextAlignmentOptions.Right);
            Panel(inspector, "Line", 26, 352, 420, 1, new Color(Gold.r, Gold.g, Gold.b, .35f));
            _message = Text(inspector, "Feedback", "", 26, 374, 420, 101, 21, Cream);
            _enhance = Button(inspector, "Btn_Enhance", "", 26, 486, 205, 48);
            _enhanceLabel = _enhance.GetComponentInChildren<TextMeshProUGUI>();
            _enhance.onClick.AddListener(() => _command("enhance"));
            _sideways = Button(inspector, "Btn_Sideways", "横置使用 · 1", 241, 486, 205, 48);
            _sideways.onClick.AddListener(() => _command("sideways"));
            var rules = Button(inspector, "Btn_Rules", "规则与行动记录", 26, 548, 420, 41);
            rules.onClick.AddListener(() => { _rulesOpen = !_rulesOpen; _rule.gameObject.SetActive(_rulesOpen); });
            var ruleViewport = Panel(inspector, "RuleViewport", 26, 598, 420, 139, Color.clear);
            ruleViewport.GetComponent<Image>().raycastTarget = true;
            ruleViewport.gameObject.AddComponent<RectMask2D>();
            var scroll = ruleViewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = ruleViewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            _rule = Text(ruleViewport, "RuleDetails", "", 0, 0, 416, 139, 17, Muted);
            _rule.overflowMode = TextOverflowModes.Overflow;
            _rule.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _rule.rectTransform;
            _rule.gameObject.SetActive(false);
            _cancel = Button(inspector, "Btn_Cancel", "取消选择", 26, 746, 136, 39);
            _cancel.onClick.AddListener(() => _command("cancel"));
            _primary = Button(inspector, "Btn_Confirm", "", 178, 744, 268, 79, true);
            _primaryLabel = _primary.GetComponentInChildren<TextMeshProUGUI>(); _primary.onClick.AddListener(() => _command("confirm"));
            Text(_root, "Footer", "选择卡牌或部队  →  点选目标  →  确认行动", 49, 1048, 1780, 24, 16, Muted);

            _menu = Panel(_root, "AdventureMenu", 1395, 130, 480, 542, new Color32(12, 30, 28, 255), true);
            Text(_menu, "Title", "选择冒险", 24, 21, 424, 39, 27, Gold);
            var adventures = Resources.LoadAll<AdventureDefinition>("Adventure/Scenarios").OrderBy(a => a.name).ToArray();
            for (int i = 0; i < adventures.Length; i++)
            {
                var definition = adventures[i];
                var b = Button(_menu, "Btn_" + definition.id, definition.displayName, 24, 76 + i * 76, 430, 61);
                b.onClick.AddListener(() => { _menu.gameObject.SetActive(false); _command("scenario:" + definition.name); });
            }
            _menu.gameObject.SetActive(false);
        }

        private void HeaderValue(string key, string label, float x)
        {
            Text(_root, key + "Caption", label, x, 57, 95, 27, 16, Muted);
            _values[key] = Text(_root, "Value_" + key, "", x, 82, 112, 49, 34, Cream);
        }

        public Dictionary<string, string> ReadDisplayedValues()
        {
            var values = _values.ToDictionary(p => p.Key, p => p.Value.text);
            values["primary"] = _primaryLabel.text;
            values["resource"] = _resourceValue.text;
            values["rulesVisible"] = _rule.gameObject.activeSelf ? "1" : "0";
            values["primaryButtons"] = _primary.gameObject.activeInHierarchy ? "1" : "0";
            values["context"] = _contextBody.text;
            values["printedEffect"] = _contextBody.text.Split('\n')[0];
            values["mana"] = _mana.text;
            foreach (var card in _cards)
            {
                values["cardArt:" + card.Key] = card.Value.rect.Find("Art").GetComponent<Image>().sprite?.name ?? "";
                values["cardNote:" + card.Key] = card.Value.note.text;
            }
            return values;
        }

        public void Refresh(AdventureDefinition definition, AdventureSession session)
        {
            _definition = definition;
            _title.text = definition.displayName; _objective.text = definition.objective;
            _phase.text = PhaseName(session.Phase) + "   /   " + "回合 " + session.Turn;
            var state = session.ReadState();
            foreach (var pair in _values) pair.Value.text = state.GetValueOrDefault(pair.Key, "—");
            _mana.text = "蓝晶 " + state["blueCrystal"] + "   绿晶 " + state["greenCrystal"] + "   白晶 " + state["whiteCrystal"];
            string stageKey = definition.id + ":" + (session.Phase == AdventurePhase.Travel ? "map" :
                session.Phase == AdventurePhase.Interaction ? "offers" : "battle");
            if (_stageKey != stageKey) { BuildStage(session); _stageKey = stageKey; }
            string handKey = definition.id + ":" + string.Join(",", session.Hand.Select(c => c.Serial));
            if (_handKey != handKey) { BuildHand(session); _handKey = handKey; }
            string unitKey = definition.id + ":" + string.Join(",", session.Player.Units.Select(u => u.Card.Id + u.IsReady));
            if (_unitKey != unitKey) { BuildUnits(session); _unitKey = unitKey; }
            UpdateStage(session); UpdateCards(session); UpdateInspector(session);
        }

        private void BuildHand(AdventureSession session)
        {
            Clear(_hand); _cards.Clear();
            if (session.Hand.Count == 0) { Text(_hand, "Empty", "本回合手牌已用尽", 28, 60, 1200, 65, 28, Muted); return; }
            float width = Math.Min(214, (1320 - (session.Hand.Count - 1) * 16) / session.Hand.Count);
            float start = (1320 - session.Hand.Count * (width + 16) + 16) / 2;
            for (int i = 0; i < session.Hand.Count; i++)
            {
                var card = session.Hand[i]; var data = card.Definition;
                var face = Panel(_hand, "Card_" + card.Serial, start + i * (width + 16), 0, width, 306, Ink, true);
                var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face.GetComponent<Image>(); button.targetGraphic.raycastTarget = true;
                button.onClick.AddListener(() => _command("card:" + card.Serial));
                var art = definitionCard(data.Id)?.artwork;
                var cardImage = Sprite(face, "Art", art, 4, 4, width - 8, 274);
                cardImage.enabled = art != null;
                if (data.Source == null) Text(face, "Wound", "伤口\n无法打出", 12, 75, width - 24, 100, 25, Cream, TextAlignmentOptions.Center);
                var note = Text(face, "Note", "", 6, 281, width - 12, 23, 15, Muted, TextAlignmentOptions.Center);
                _cards[card.Serial] = (face, face.GetComponent<Image>(), note);
            }
        }

        private ActionCardDefinition definitionCard(string id) => _definition.deck.FirstOrDefault(c => c.id == id);
        private void UpdateCards(AdventureSession session)
        {
            foreach (var card in session.Hand)
            {
                var view = _cards[card.Serial]; bool selected = card.Serial == session.SelectedCard;
                bool available = session.CardAvailable(card.Definition);
                view.face.color = selected ? new Color32(73, 81, 53, 255) : available ? new Color32(25, 48, 42, 255) : new Color32(15, 28, 27, 255);
                var pos = view.rect.anchoredPosition; pos.y = selected ? 12 : 0; view.rect.anchoredPosition = pos;
                view.note.text = card.Definition.Source == null ? "伤牌 · 占用手牌空间" : !available ? "遭遇已结束" : selected ?
                    (session.Sideways ? "已选横置 · " : session.Enhanced ? "已选强化 · " : "已选基础 · ") + session.Preview(card.Definition, session.Enhanced, session.Sideways) :
                    session.Preview(card.Definition) > 0 ? "基础 · " + AdventureSession.ActionName(OfficialActionAdapter.Action(session.Phase)) + session.Preview(card.Definition) : "本阶段可横置 · 1";
            }
        }

        private void UpdateInspector(AdventureSession s)
        {
            _message.text = s.Message; _message.color = s.Accepted ? Cream : new Color32(235, 153, 125, 255);
            _rule.text = s.Rule + "\n\n" + string.Join("\n", s.Journal.TakeLast(2));
            var card = s.Selected?.Definition;
            _contextTitle.text = card != null ? card.Name : s.SelectedUnit.Length > 0 ? "部队协同" :
                s.TargetSite != null ? s.TargetSite.Name : s.TargetOffer != null ? s.TargetOffer.Name : PhaseName(s.Phase);
            _contextBody.text = TargetDescription(s);
            if (card != null)
            {
                string mode = s.Sideways ? "横置：" + AdventureSession.ActionName(OfficialActionAdapter.Action(s.Phase)) + "1，不耗魔力。" :
                    s.Enhanced ? "强化：" + card.EnhancedText : "基础：" + card.BaseText;
                string cost = "强化耗色：" + string.Join("、", card.Colors.Select(ManaName));
                _contextBody.text = mode + "\n" + (s.Sideways ? "牌面：" + card.BaseText : cost) + "\n" + TargetDescription(s);
            }
            _enhance.gameObject.SetActive(card != null && s.Preview(card, true) > 0);
            _sideways.gameObject.SetActive(card != null);
            if (card != null)
            {
                _enhanceLabel.text = s.Enhanced ? "改回基础效果" : "强化 " + s.Preview(card, true) + " · " + string.Join("/", card.Colors.Select(ManaName));
                _sideways.GetComponentInChildren<TextMeshProUGUI>().text = s.Sideways ? "已选横置 · 1" : "横置使用 · 1";
            }
            _cancel.gameObject.SetActive(card != null || s.SelectedUnit.Length > 0 || s.Target.Length > 0);
            _primaryLabel.text = PrimaryLabel(s);
            _resourceCaption.text = s.Phase == AdventurePhase.Travel ? "可用移动力" : s.Phase == AdventurePhase.Interaction ? "可用影响力" :
                s.Phase == AdventurePhase.Block ? "有效格挡 / 需求" : s.Phase == AdventurePhase.Attack ? "有效攻击 / 护甲" : "获得名望";
            int target = Math.Max(0, s.EnemyIndex);
            _resourceValue.text = s.Phase == AdventurePhase.Travel ? s.Movement.ToString() : s.Phase == AdventurePhase.Interaction ? s.Influence.ToString() :
                s.Phase == AdventurePhase.Block ? s.Battle.EffectiveBlock(target) + " / " + s.Battle.RequiredBlock(target) :
                s.Phase == AdventurePhase.Attack ? s.Battle.EffectiveAttack(target) + " / " + s.Battle.Definition.Enemies[target].Armor : s.Player.Fame.ToString();
        }

        private static string PrimaryLabel(AdventureSession s)
        {
            if (s.Selected != null) return s.Target.Length == 0 ? "先选择目标" : "确认打出";
            if (s.SelectedUnit.Length > 0) return s.EnemyIndex < 0 ? "先选择敌人" : "确认使用部队";
            if (s.Phase == AdventurePhase.Travel) return s.TargetSite == null ? "选择目的地" :
                (s.IsRevealed(s.TargetSite) ? "进入 · " : "探索 · ") + (s.Cost(s.TargetSite) == int.MaxValue ? "不可通行" : "移动" + s.Cost(s.TargetSite));
            if (s.Phase == AdventurePhase.Interaction) return s.TargetOffer != null ? "招募 · 影响" + s.TargetOffer.Cost : "结束回合";
            if (s.Phase == AdventurePhase.Block) return "结束格挡";
            if (s.Phase == AdventurePhase.Attack) return "结算攻击";
            return "再玩一次";
        }

        private static string TargetDescription(AdventureSession s)
        {
            if (s.TargetSite != null) return s.IsRevealed(s.TargetSite) ? s.TargetSite.Name + " · " +
                (s.Cost(s.TargetSite) == int.MaxValue ? "不可通行" : "进入费用" + s.Cost(s.TargetSite)) : "未知地点 · 探索2；揭示后进入还要付地形费用。";
            if (s.TargetOffer != null) return "招募费用 " + s.TargetOffer.Cost + "\n空闲指挥槽 " + (s.Player.CommandSlots - s.Player.Units.Count) +
                "\n" + (s.HasHired(s.TargetOffer.Id) ? "已加入队伍" : "选择交涉牌积累影响力，确认后招募。");
            if (s.EnemyIndex >= 0)
            {
                var enemy = s.Battle.Definition.Enemies[s.EnemyIndex];
                return enemy.Name + "\n护甲 " + enemy.Armor + "   攻击 " + enemy.Attack + "   名望 " + enemy.Fame +
                    (enemy.Abilities.Length > 0 ? "\n" + string.Join(" · ", enemy.Abilities.Select(AbilityName)) : "");
            }
            return s.Phase == AdventurePhase.Travel ? "点击地图地点；需要移动力时，先选择移动牌。" :
                s.Phase == AdventurePhase.Interaction ? "点击想招募的伙伴；选择交涉牌，为招募积累影响力。" :
                s.Phase == AdventurePhase.Block ? "选择格挡牌或就绪部队，再点选要抵挡的敌人。准备好后结束格挡。" :
                s.Phase == AdventurePhase.Attack ? "选择攻击牌并指定敌人。攻击达到护甲时可以击败目标。" : s.Completed ? "遭遇已完成。可以重玩，或从右上选择另一段冒险。" : "仍有敌人存活。重试时调整卡牌与目标的分配。";
        }

        public static string PhaseName(AdventurePhase phase) => phase switch
        { AdventurePhase.Travel => "探索与移动", AdventurePhase.Interaction => "村庄交涉", AdventurePhase.Block => "格挡阶段", AdventurePhase.Attack => "攻击阶段", _ => "遭遇结束" };
        public static string ManaName(ManaColor color) => color switch { ManaColor.Red => "红", ManaColor.Blue => "蓝", ManaColor.Green => "绿", _ => "白" };
        private static string ElementName(Element e) => e == Element.Physical ? "" : e == Element.Ice ? " · 冰" : e == Element.Fire ? " · 火" : " · 冷火";
        public static string AbilityName(Ability a) => a switch { Ability.Swift => "迅捷：格挡需求翻倍", Ability.Brutal => "残暴：未格挡伤害翻倍", Ability.FireResist => "火焰抗性", Ability.IceResist => "冰抗性", _ => a.ToString() };
        private static void Clear(Transform parent)
        { foreach (Transform child in parent) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); } }
    }
}
