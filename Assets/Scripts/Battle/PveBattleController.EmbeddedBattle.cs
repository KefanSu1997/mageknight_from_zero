using System;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    // Presentation geometry only. The existing buttons retain their formal handlers,
    // enabledness, original card instances, choices, payment and target semantics.
    public sealed partial class PveBattleController
    {
        public bool embeddedBattleUi = true;
        private CanvasGroup _embeddedToast;
        private readonly Dictionary<Texture2D, Material> _characterAtmospheres = new();
        private void UpdateEmbeddedFeedback()
        {
            if (_embeddedToast == null) return;
            float age = Time.unscaledTime - _inputAt;
            bool reading = _root != null && _root.Find("CardHoverPreview")?.gameObject.activeInHierarchy == true;
            // Keep feedback legible for its lifetime. A half-faded sentence is
            // harder to read than either a visible receipt or an absent one.
            _embeddedToast.alpha = reading || age >= 4 ? 0 : 1;
            _embeddedToast.blocksRaycasts = false;
        }

        private static void Place(Transform target, float x, float y, float width, float height)
        {
            if (target == null) return;
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private void HideBattlePart(string path)
        { var part = _root.Find(path); if (part != null) part.gameObject.SetActive(false); }

        private static void QuietGraphics(Transform node)
        {
            foreach (var graphic in node.GetComponentsInChildren<Graphic>(true))
                if (!(graphic is TMP_Text)) graphic.enabled = false;
        }

        private void HoverInfo(GameObject target, string title, string body, bool entityDetails = false)
        {
            var graphic = target.GetComponent<Graphic>(); if (graphic != null) graphic.raycastTarget = true;
            var hover = target.AddComponent<BattleHoverInfo>();
            hover.surface = _root; hover.heading = title; hover.body = body;
            hover.entityDetails = entityDetails;
            hover.selectedContext = _selected != null ? CardName(_selected) : _selectedUnit?.Card.NameCn
                ?? _selectedSkill?.Id ?? (_targets.Count > 0 ? "敌人目标" : "无");
        }

        private void DrawEmbeddedEnemy(Transform parent, string id, Texture2D token, float width, bool targeted, bool killed)
        {
            width = Mathf.Min(330, 810f / Math.Max(1, _battle.Enemies.Count)) - 18;
            string key = "embedded:" + id;
            if (!_enemyIllustrations.TryGetValue(key, out var texture))
            {
                texture = Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v5/" + id)
                    ?? Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v4/" + id)
                    ?? Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v3/" + id)
                    ?? Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v2/" + id)
                    ?? Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v1/" + id);
                _enemyIllustrations[key] = texture;
            }
            // Unconverted IDs remain explicit fallbacks, never a fabricated replacement.
            var art = texture ?? Resources.Load<Texture2D>("UI/EnemyIllustrations/r13/" + id) ?? token;
            float artX = _battle.Player.Units.Count == 4 && _battle.Enemies.Count == 3 ? 0 : 6;
            DrawGroundContact(parent, "GroundShadow", art, artX, 425, width - 12, texture == null ? 338 : 310);
            var tint = killed ? new Color(.4f, .4f, .4f, .65f) : Color.white;
            DrawCharacterTexture(parent, "EnemyIllustration", art, artX, 425, width - 12,
                texture == null ? 338 : 310, tint);
            DrawActorHoverFocus(parent, artX, 425, width - 12, targeted);
            if (targeted)
            {
                var ring = W.Rect(parent, "TargetRing", width * .1f, 420, width * .8f, 22).gameObject.AddComponent<BattleRoundGraphic>();
                ring.color = W.Gold; ring.hollow = true; ring.raycastTarget = false;
            }
        }

        private void ComposeEmbeddedBattle()
        {
            foreach (string path in new[] { "Header", "HeroStatusPlate", "EnemyStage", "HandSurface", "HandToolbar", "PowerPlate", "TacticalRail", "HeroIdentity", "Mode", "Brand" }) HideBattlePart(path);
            var shade = _root.Find("Shade")?.GetComponent<Image>();
            if (shade != null) shade.color = new Color(.015f, .025f, .03f, .13f);
            var headerShade = W.Rect(_root, "CompactHeaderShade", 5, 4, 1210, 112).gameObject.AddComponent<BattleRoundGraphic>();
            headerShade.color = new Color(0, 0, 0, .68f); headerShade.feathered = true; headerShade.raycastTarget = false;
            headerShade.transform.SetSiblingIndex((_root.Find("Shade")?.GetSiblingIndex() ?? 0) + 1);
            var title = _root.Find("Title"); Place(title, 38, 20, 445, 46);
            var titleText = title.GetComponent<TMP_Text>(); titleText.fontSize = 26;
            // Small, steady identity/resources. Knowledge remains optional.
            Place(_status.transform, 38, 68, 555, 40);
            _status.text = "护甲 " + _battle.Player.Armor + "    创伤 " + _battle.Player.Wounds + "    名望 " + _battle.Player.Fame;
            _status.fontSize = 22;
            HoverInfo(title.gameObject, Encounter.displayName,
                (Encounter.dayPart == DayPart.Day ? "白昼" : "黑夜") + "\n" + StatusText() + "\n" + PoolText());
            var phase = _root.Find("PhaseTrack");
            if (phase != null)
            {
                QuietGraphics(phase); Place(phase, 700, 112, 470, 46);
                phase.Find("PhaseDecision")?.gameObject.SetActive(false);
                Place(_phaseLabel.transform, 0, 0, 470, 45);
                _phaseLabel.text = PhaseIndicatorName(_battle.Phase); _phaseLabel.fontSize = 26;
                if (_battle.Phase == Phase.Block && _targets.Count == 0 && _battle.Context.BlockPool > 0)
                    _phaseLabel.text += " · 选择一次攻击";
                _phaseLabel.alignment = TextAlignmentOptions.Center;
                string hint = _targets.Count > 0 ? "已选目标 · 核对需求后结算" : _selectedUnit != null ? "已选部队 · 查看能力与状态" : _selectedSkill != null ? "已选技能 · 核对费用与目标" : _selected != null ? "选择模式 · 确认出牌" :
                    _battle.Phase == Phase.Block ? "未选敌人 · 准备格挡或选择一次攻击" :
                    _battle.Phase == Phase.AssignDamage ? "选择攻击 · 分配承伤" :
                    _battle.Phase == Phase.End ? "回查战果 · 继续回合" : "选择手牌或敌人";
                // Keep the current phase visible; tutorials and repeated selection
                // instructions belong to deliberate hover instead of a second rail.
                var help = W.Panel(phase, "PhaseHelp", 0, 0, 470, 45, Color.clear);
                help.GetComponent<Image>().raycastTarget = true;
                float helpX = Mathf.Min(435, 235 + _phaseLabel.GetPreferredValues(_phaseLabel.text).x / 2 + 12);
                var helpBadge = W.Rect(help, "HelpBadge", helpX, 6, 32, 32).gameObject.AddComponent<BattleRoundGraphic>();
                helpBadge.color = new Color32(14, 28, 27, 245); helpBadge.raycastTarget = false;
                var helpMark = W.Text(help, "HelpAffordance", "?", helpX, 3, 32, 38, 24, W.Cream, TextAlignmentOptions.Center);
                helpMark.fontStyle = FontStyles.Bold;
                HoverInfo(help.gameObject, PhaseIndicatorName(_battle.Phase), hint + "\n" +
                    (_battle.Phase == Phase.Block ? "每次攻击须完整格挡；不足仍承受完整伤害。\n迅捷需双倍；元素攻击的无效格挡减半。" : RulesText()));
                help.GetComponent<BattleHoverInfo>().compactGuidance = true;
                var backdrop = W.Rect(phase, "PhaseBackdrop", 75, -2, 320, 49).gameObject.AddComponent<BattleRoundGraphic>();
                backdrop.color = new Color(0, 0, 0, .92f); backdrop.feathered = true; backdrop.raycastTarget = false;
                backdrop.transform.SetAsFirstSibling();
            }
            DrawEmbeddedManaInventory();
            Place(_pools.transform, 700, 702, 800, 34); _pools.fontSize = 22; _pools.alignment = TextAlignmentOptions.Center;
            _pools.text = PoolText().Replace("战斗资源    ", "");
            var resourceShade = W.Rect(_root, "ResourceReadability", 690, 698, 820, 40).gameObject.AddComponent<BattleRoundGraphic>();
            resourceShade.color = new Color(0, 0, 0, .9f); resourceShade.feathered = true; resourceShade.raycastTarget = false;
            resourceShade.transform.SetSiblingIndex(_pools.transform.GetSiblingIndex());
            var heroHeight = (_root.Find("Hero") as RectTransform)?.rect.height ?? 104;
            Place(_root.Find("HeroPortraitFrame"), 86, 372, 112, heroHeight + 8);
            var hero = _root.Find("Hero"); if (hero != null) { Place(hero, 90, 376, 104, ((RectTransform)hero).rect.height); HoverInfo(hero.gameObject, _battle.Player.Name, StatusText()); }
            Place(_root.Find("HeroName"), 50, 382 + heroHeight, 300, 45);
            var heroName = _root.Find("HeroName")?.GetComponent<TMP_Text>();
            if (heroName == null)
                heroName = W.Text(_root,"HeroName",_battle.Player.Name,50,382+heroHeight,300,45,25,W.Gold);
            if (heroName != null) heroName.text = _battle.Player.Name + " · " + _battle.Player.Level + "级";
            DrawEmbeddedParty();
            for (int i = 0; i < _battle.Enemies.Count; i++)
            {
                var target = _root.Find("Enemy_" + i); if (target == null) continue;
                float width = Mathf.Min(330, 810f / Math.Max(1, _battle.Enemies.Count));
                bool fourAllies = _battle.Player.Units.Count == 4;
                // Preserve each full-sized figure. In the four-ally case the
                // three enemy illustrations occupy their actual 240px silhouettes
                // with only the old transparent tile gutters removed.
                float stride = fourAllies && _battle.Enemies.Count == 3 ? 240 : width;
                float enemyX = fourAllies ? 805 + (720 - stride * _battle.Enemies.Count) / 2 + i * stride
                    : 690 + (810 - width * _battle.Enemies.Count) / 2 + i * width;
                Place(target, enemyX, 195, width - 18, 505);
                var face = target.GetComponent<Image>(); if (face != null) face.color = Color.clear;
                foreach (Transform trim in target)
                    if (trim.name == "Top" || trim.name == "Bottom" || trim.name == "Left" || trim.name == "Right") trim.gameObject.SetActive(false);
                var button = target.Find("Btn_Target"); Place(button, 0, 0, fourAllies && _battle.Enemies.Count == 3 ? stride : width - 18, 505); QuietGraphics(button);
                // Restore only actual character art and ground indication after removing generic card chrome.
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                    if (graphic is RawImage || graphic is BattleRoundGraphic) graphic.enabled = true;
                var targetButton = button.GetComponent<Button>();
                targetButton.targetGraphic.enabled = true; targetButton.targetGraphic.color = Color.clear;
                // Keep full-size figures on the courtyard ground. Identity/state
                // move above the silhouette so full-size text does not collide
                // with the hand or resource line beneath their feet.
                Place(target.Find("Name"), 0, 46, width - 18, 40);
                var name = target.Find("Name")?.GetComponent<TMP_Text>(); if (name != null) name.fontSize = 23;
                Place(target.Find("Numbers"), 0, 433, width - 18, 70);
                var numbers = target.Find("Numbers")?.GetComponent<TMP_Text>(); if (numbers != null) numbers.fontSize = 20;
                string fullNumbers = numbers?.text ?? "";
                var enemy = _battle.Enemies[i];
                // Keep the canonical native readout, including conditional fame
                // bonuses and the original armor reference after a modifier.
                // Only the attack line moves to the above-actor intent HUD.
                if (numbers != null) numbers.text = string.Join("\n", fullNumbers.Split('\n').Skip(1));
                var intent = W.Text(target, "EnemyIntent", fullNumbers.Split('\n')[0], 0, -20, width - 18, 58, 23, W.Cream, TextAlignmentOptions.Center);
                // Text receives one local readability backing below. Avoid stacking
                // extra oversized ellipses behind the same name and attack line.
                // One text-sized backing is added below; do not stack a second
                // wide black plate behind these same numbers.
                target.Find("EnemyIndex")?.gameObject.SetActive(false); target.Find("TargetRibbon")?.gameObject.SetActive(false);
                Place(target.Find("State"), 0, 87, width - 18, 30);
                var state = target.Find("State")?.GetComponent<TMP_Text>();
                if (state != null && state.text == "点击卡面选择目标") state.text = "";
                PlaceEnemyThreat(target,button,intent,name,state,width-18);
                HoverInfo(button.gameObject, EnemyView(_battle.Enemies[i].Id).displayName,
                    fullNumbers + "\n点击该实体选择正式目标。\n" + RulesText(), true);
                button.GetComponent<BattleHoverInfo>().inspection = EnemyInspection(fullNumbers);
                DrawEnemySymbolReadout(target, intent, numbers, enemy, i, fullNumbers, width - 18);
            }
            var hand = _root.Find("Hand"); Place(hand, 220, 740, 1320, 287);
            if (hand != null)
            {
                int count = hand.childCount;
                for (int i = 0; i < count; i++)
                {
                    var card = hand.GetChild(i); float position = count <= 1 ? 0 : (i - (count - 1) / 2f) / Math.Max(1, count - 1);
                    card.localRotation = Quaternion.Euler(0, 0, -position * 10);
                    var r = (RectTransform)card; r.anchoredPosition += new Vector2(0, -Mathf.Abs(position) * 30);
                    var hover = card.GetComponent<BattleCardHover>(); if (hover != null) { hover.previewY = 178; hover.dockInInspector = true; hover.readingWell = true; }
                }
            }
            var handLabel = _root.Find("HandLabel"); Place(handLabel, 54, 805, 150, 36);
            if (handLabel != null) { var label = handLabel.GetComponent<TMP_Text>(); label.text = "手牌 " + _battle.Player.Deck.Hand.Count; label.alignment = TextAlignmentOptions.Center; }
            DrawPileCount("DeckCount", "牌库", _battle.Player.Deck.DrawPileCount, 66);
            DrawPileCount("DiscardCount", "弃牌", _battle.Player.Deck.DiscardPile.Count, 1720);
            MoveShortcut("Btn_Skills", 40, 650, _showSkills ? "返回手牌" : "技能 " + _battle.Player.Skills.Count); MoveShortcut("Btn_Roster", 40, 702, _showUnits ? "返回手牌" : "部队 " + _battle.Player.Units.Count);
            MoveShortcut("Btn_Recovery", 40, 750, "战后治疗"); MoveShortcut("Btn_BowChoices", 40, 750, "星辰之弓");
            if (_showSkills)
            {
                Place(_root.Find("Skills"), 250, 805, 1320, 275);
                Place(_root.Find("SkillLabel"), 250, 752, 1320, 36);
                var label = _root.Find("SkillLabel")?.GetComponent<TMP_Text>();
                if (label != null) AddReadableLabelBacking(label);
            }
            if (_showUnits)
            {
                Place(_root.Find("Units"), 250, 805, 1320, 287);
                Place(_root.Find("UnitLabel"), 250, 752, 1320, 36);
                var label = _root.Find("UnitLabel")?.GetComponent<TMP_Text>();
                if (label != null) AddReadableLabelBacking(label);
            }
            ComposeActionDock();
            MoveShortcut("Btn_ResetPhase", 1670, 24, "回退", 110); MoveShortcut("Btn_Menu", 1795, 24, "菜单", 85);
            MoveShortcut("Btn_QuickSource", 1240, 24, "公共魔力", 150); MoveShortcut("Btn_QuickMana", 1240, 24, "公共魔力", 150);
            StyleUtilityShortcut(_root.Find("Btn_QuickSource")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Mana);
            StyleUtilityShortcut(_root.Find("Btn_QuickMana")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Mana);
            StyleUtilityShortcut(_root.Find("Btn_ResetPhase")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Undo);
            StyleUtilityShortcut(_root.Find("Btn_Menu")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Menu);
            StyleUtilityShortcut(_root.Find("Btn_Skills")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Skill);
            StyleUtilityShortcut(_root.Find("Btn_Roster")?.GetComponent<Button>(), BattleUtilityGlyph.Kind.Party);
            foreach (var text in _root.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.outlineWidth = .16f; text.outlineColor = new Color32(0, 0, 0, 230);
                text.extraPadding = true;
                if(text.name=="ManaTokenAmount" || text.name=="ManaCrystalAmount")
                {text.fontStyle|=FontStyles.Bold;text.outlineWidth=.25f;}
                var ownerButton = text.GetComponentInParent<Button>();
                if (ownerButton != null && ownerButton.interactable && ownerButton.targetGraphic != null
                    && ownerButton.GetComponent<BattleCommandFocus>() == null
                    && ownerButton.targetGraphic.color.grayscale > .5f && text.name == "Label")
                {
                    ownerButton.targetGraphic.color = new Color32(20, 43, 36, 255);
                    text.color = W.Cream; text.outlineWidth = 0; text.fontStyle |= FontStyles.Bold;
                    var buttonRect = (RectTransform)ownerButton.transform;
                    W.Panel(buttonRect, "PrimaryActionAccent", 0, 8, 5, buttonRect.rect.height - 16, W.Gold);
                }
                bool enemyCaption = text.transform.parent.name.StartsWith("Enemy_", StringComparison.Ordinal)
                    && (text.name == "Name" || text.name == "Numbers" || text.name == "EnemyIntent");
                if (enemyCaption || text == _status || text == _pools || text.name == "Title"
                    || text.name == "HeroName" || text.name == "MinimalDecisionCue" || text.name == "BlockRiskCue"
                    || text.name == "HandReadingCue" || text.name == "ContextTitle" || text.name == "UnitAvailability"
                    || text.name == "EffectiveTarget" || text.name == "ManaLegend" || text.name == "BattleToast")
                    if (!(text.name == "EnemyIntent" && text.transform.parent.Find("ThreatBadge") != null)) AddReadableLabelBacking(text);
            }
            FinishSymbolicBattlefield();
        }

        private void StyleUtilityShortcut(Button button, BattleUtilityGlyph.Kind kind)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            // Retain the native hit rectangle, caption and handler. Utility
            // controls use an illustrated mark rather than another solid plate.
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                if (!(graphic is TMP_Text)) graphic.enabled = false;
            var hit = button.GetComponent<Image>();
            if (hit == null) hit = button.gameObject.AddComponent<Image>();
            hit.enabled = true; hit.color = Color.clear; hit.raycastTarget = true;
            button.targetGraphic = hit;
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption != null)
            {
                Place(caption.transform, 37, 0, rect.rect.width - 39, rect.rect.height);
                caption.enableAutoSizing = false;
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.overflowMode = TextOverflowModes.Overflow;
                caption.alignment = TextAlignmentOptions.MidlineLeft;
                caption.color = W.Cream;
                EmbossFieldText(caption);
            }
            var icon = W.Rect(rect, "UtilityIcon", 1, 8, 29, 29).gameObject.AddComponent<BattleUtilityGlyph>();
            icon.kind = kind; icon.color = W.Cream; icon.raycastTarget = false;
            RuneMount(rect,"UtilityIconRuneMount",-1,6,33,33,icon.transform.GetSiblingIndex());
            var focus = button.gameObject.AddComponent<BattleUtilityFocus>(); focus.icon = icon; focus.caption = caption; focus.button = button;
            focus.Refresh(false);
            if (kind == BattleUtilityGlyph.Kind.Mana || kind == BattleUtilityGlyph.Kind.Undo || kind == BattleUtilityGlyph.Kind.Menu
                || kind == BattleUtilityGlyph.Kind.Skill || kind == BattleUtilityGlyph.Kind.Party)
            {
                string description = kind == BattleUtilityGlyph.Kind.Mana ? "打开公共魔力源，选择实际骰子并正式取用。" :
                    kind == BattleUtilityGlyph.Kind.Undo ? "回退本阶段操作。是否可回退以当前按钮状态和正式规则为准。" :
                    kind == BattleUtilityGlyph.Kind.Skill ? "查看英雄技能。点击英雄实体也可打开；返回手牌保留在同一战场。" :
                    kind == BattleUtilityGlyph.Kind.Party ? "查看部队能力。也可直接点击战场上的佣兵选择实际能力。" : "打开菜单，查看本局与游戏选项。";
                HoverInfo(button.gameObject, caption != null ? caption.text : "战斗操作", description);
                button.GetComponent<BattleHoverInfo>().compactGuidance = true;
            }
        }

        private static void AddReadableLabelBacking(TMP_Text text)
        {
            text.outlineColor = new Color32(9, 20, 16, 255);
            text.outlineWidth = .22f; text.extraPadding = true;
        }

        private void DrawEmbeddedParty()
        {
            bool fourAllies = _battle.Player.Units.Count == 4;
            float partyInset = 0;
            var heroArt = Resources.Load<Texture2D>("UI/HeroCharacters/embedded_v1/" + _battle.Player.HeroId);
            if (heroArt != null)
            {
                HideBattlePart("Hero"); HideBattlePart("HeroPortraitFrame");
                // The layout frame uses alpha>=64 for consistent body scale,
                // but the full original texture includes translucent cloaks and
                // weapons outside that frame. Reserve their left margin too.
                float heroLeft = 8;
                var heroFrame = CharacterFrameFor(heroArt);
                if (heroFrame != null && heroFrame.width > 0 && heroFrame.height > 0
                    && heroFrame.textureWidth == heroArt.width && heroFrame.textureHeight == heroArt.height)
                {
                    float fit = Mathf.Min(245 / heroFrame.width, 350 / heroFrame.height);
                    float frameX = MirrorCharacter(heroArt) ? heroArt.width - heroFrame.x - heroFrame.width : heroFrame.x;
                    heroLeft += Mathf.Max(0, frameX * fit - (245 - heroFrame.width * fit) / 2);
                }
                partyInset = heroLeft - 8;
                var hero = Button(_root, "Btn_HeroActor", "", heroLeft, 255, 245, 350,
                    () => { _fluentDiscardMode = false; _showSkills = !_showSkills; _showUnits = false; _selectedSkill = null; _selected = null; _selectedUnit = null; _selectMaterials = false; });
                QuietGraphics(hero.transform);
                hero.targetGraphic.enabled = true; hero.targetGraphic.color = Color.clear;
                DrawPartyFigure(hero.transform, heroArt, 245, 350, Color.white);
                var crest = W.Rect(hero.transform, "HeroLeadershipCrest", 99.5f, -23, 46, 36).gameObject.AddComponent<BattlePartyStateGlyph>();
                crest.kind = 3; crest.color = W.Gold;
                HoverInfo(crest.gameObject, "英雄 · 领袖", _battle.Player.Name + "\n英雄立绘较大，冠徽标识领袖。\n" + StatusText(), true);
                HoverInfo(hero.gameObject, _battle.Player.Name + " · 英雄", StatusText() + "\n点击查看英雄技能。", true);
                hero.GetComponent<BattleHoverInfo>().inspection = HeroInspection();
                crest.GetComponent<BattleHoverInfo>().inspection = HeroInspection();
                Place(_root.Find("HeroName"), heroLeft, 606, 245, 40);
                var captionShade = W.Rect(_root, "HeroCaptionShade", 0, 600, 255, 48).gameObject.AddComponent<BattleRoundGraphic>();
                captionShade.color = new Color(0, 0, 0, .94f); captionShade.feathered = true; captionShade.raycastTarget = false;
                captionShade.transform.SetSiblingIndex(_root.Find("HeroName").GetSiblingIndex());
            }
            for (int i = 0; i < _battle.Player.Units.Count; i++)
            {
                var unit = _battle.Player.Units[i];
                var art = Resources.Load<Texture2D>("UI/UnitCharacters/embedded_v2/" + unit.Card.Id)
                    ?? Resources.Load<Texture2D>("UI/UnitCharacters/embedded_v1/" + unit.Card.Id);
                if (art == null) continue;
                // Keep the three ordinary command slots on the foreground ground
                // line instead of placing allies in a floating upper HUD row.
                int column = fourAllies ? i : i % 3, row = fourAllies ? 0 : i / 3;
                float top = row == 0 ? 385 : 109;
                // Preserve every ally's illustration and type size. Reclaim only
                // the empty inter-figure gutters when all four slots are filled.
                // Translate allies with the hero so restoring a translucent
                // edge never pushes a weapon or cloak into the first ally.
                float left = (fourAllies ? 255 + column * 135 : 260 + column * 160) + partyInset;
                var button = Button(_root, "Btn_UnitActor_" + i, "", left, top, fourAllies ? 135 : 145, 316,
                    () => { _recovery = false; _selectedUnit = unit; _selected = null; _choice = null; _bannerMode = false; _unitUtilityPanel = false; });
                QuietGraphics(button.transform);
                button.targetGraphic.enabled = true; button.targetGraphic.color = Color.clear;
                DrawPartyFigure(button.transform, art, 145, 220, unit.IsReady ? Color.white : new Color(.65f, .65f, .65f, 1), unit == _selectedUnit);
                var captionShade = W.Rect(button.transform, "CaptionShade", -6, 215, 157, 70).gameObject.AddComponent<BattleLabelBacking>();
                captionShade.color = new Color(0, 0, 0, .9f); captionShade.raycastTarget = false;
                string unitName = unit.Card.NameCn.Length > 6 ? unit.Card.NameCn.Insert(4, "\n") : unit.Card.NameCn;
                W.Text(button.transform, "Identity", unitName, 0, 224, 145, 60, 21, W.Cream, TextAlignmentOptions.Center);
                var readiness = W.Rect(button.transform, "ReadinessSymbol", 23, 286, 30, 30).gameObject.AddComponent<BattlePartyStateGlyph>();
                readiness.kind = unit.IsReady ? 0 : 1; readiness.color = unit.IsReady ? W.Gold : W.Cream;
                HoverInfo(readiness.gameObject, unit.IsReady ? "就绪" : "耗竭",
                    (unit.IsReady ? "闪电：部队就绪。能否激活仍取决于当前阶段、伤势、费用和原能力限制。" : "沙漏：部队已耗竭，普通激活不可重复。承伤资格与特殊能力按原规则独立判断。") + "\n\n" + UnitText(unit), true);
                var wounds = W.Rect(button.transform, "WoundSymbol", 72, 286, 28, 30).gameObject.AddComponent<BattlePartyStateGlyph>();
                wounds.kind = 2; wounds.color = unit.Wounds > 0 ? new Color32(231, 116, 100, 255) : W.Cream;
                HoverInfo(wounds.gameObject, "部队创伤 " + unit.Wounds,
                    "血滴与数字表示该部队的实际创伤数量；0表示没有创伤。创伤与就绪/耗竭是独立状态。\n\n" + UnitText(unit), true);
                W.Text(button.transform, "WoundCount", unit.Wounds.ToString(), 102, 281, 35, 40, 23, W.Cream, TextAlignmentOptions.MidlineLeft);
                if (unit == _selectedUnit)
                {
                    var ring = W.Rect(button.transform, "Selection", 7, 208, 131, 16).gameObject.AddComponent<BattleRoundGraphic>();
                    ring.hollow = true; ring.color = W.Gold; ring.raycastTarget = false;
                }
                var actionDescriptions = unit.Card.Actions?.Select(action => action.PrintedRule +
                    "\n印刷费用：" + (action.Cost.Length == 0 ? "免费" : string.Join("、", action.Cost.Select(ColorName)))) ?? Enumerable.Empty<string>();
                string originalActions = string.Join("\n\n", actionDescriptions);
                HoverInfo(button.gameObject, unit.Card.NameCn, UnitText(unit) + "\n点击查看并选择本部队的原能力。" +
                    "\n\n原能力（印刷费用）：\n" + originalActions + "\n\n当前可用项与实际费用以选中后的动作区为准。", true);
                button.GetComponent<BattleHoverInfo>().inspection = UnitInspection(unit, originalActions);
            }
        }

        private void DrawPartyFigure(Transform parent, Texture2D art, float width, float height, Color tint, bool selected = false)
        {
            DrawGroundContact(parent, "ContactShadow", art, 0, height, width, height);
            DrawCharacterTexture(parent, "PartyFigure", art, 0, height, width, height, tint);
            DrawActorHoverFocus(parent, 0, height, width, selected);
        }

        private static void DrawActorHoverFocus(Transform parent, float x, float foot, float width, bool selected = false)
        {
            // A local ground marker acknowledges the actual hovered hit target.
            // It does not claim the entity has a legal activation in this phase.
            var ring = W.Rect(parent, "ActorHoverFocus", x + width * .08f, foot - 7, width * .84f, 18)
                .gameObject.AddComponent<BattleRoundGraphic>();
            ring.hollow = true; ring.color = new Color(.94f, .93f, .87f, .82f); ring.raycastTarget = false;
            ring.transform.SetAsFirstSibling(); ring.gameObject.SetActive(false);
            var focus = parent.gameObject.AddComponent<BattleActorFocus>();
            focus.indicator = ring.gameObject; focus.selected = selected;
        }

        private void DrawGroundContact(Transform parent, string name, Texture2D art, float x, float foot, float width, float height)
        {
            // Match the visible silhouette rather than stamping the full button
            // width beneath a narrow figure. Artwork and its full UV are unchanged.
            var frame = CharacterFrameFor(art);
            float visibleWidth = frame != null && frame.width > 0 && frame.height > 0
                ? frame.width * Mathf.Min(width / frame.width, height / frame.height)
                : Mathf.Min(width, art.width * height / art.height);
            float spread = Mathf.Clamp(visibleWidth * .72f, 28, width * .84f);
            var soft = W.Rect(parent, name, x + (width - spread) / 2, foot - 7, spread, 14).gameObject.AddComponent<BattleRoundGraphic>();
            soft.color = new Color(.025f, .025f, .018f, .36f); soft.feathered = true; soft.raycastTarget = false;
            float coreWidth = spread * .64f;
            var core = W.Rect(parent, name + "Core", x + (width - coreWidth) / 2, foot - 3, coreWidth, 6).gameObject.AddComponent<BattleRoundGraphic>();
            core.color = new Color(.012f, .015f, .012f, .65f); core.feathered = true; core.raycastTarget = false;
        }

        private void DrawCharacterTexture(Transform parent, string name, Texture2D art, float x, float foot,
            float width, float height, Color tint)
        {
            var frame = CharacterFrameFor(art);
            bool mirror = MirrorCharacter(art);
            if (frame != null && frame.textureWidth == art.width && frame.textureHeight == art.height
                && frame.width > 0 && frame.height > 0 && frame.x >= 0 && frame.y >= 0
                && frame.x + frame.width <= art.width && frame.y + frame.height <= art.height)
            {
                // Transparent generation margins must not make one hero a miniature.
                // Bounds determine scale and foot placement only. Keep the FULL UV
                // and full image, including every translucent edge; never crop art.
                float fit = Mathf.Min(width / frame.width, height / frame.height);
                float frameX = mirror ? art.width - frame.x - frame.width : frame.x;
                Texture(parent, name, art, x + (width - frame.width * fit) / 2 - frameX * fit,
                    foot - (frame.y + frame.height) * fit, art.width * fit, art.height * fit, tint);
                parent.Find(name).GetComponent<RawImage>().uvRect = new Rect(mirror ? 1 : 0, 0, mirror ? -1 : 1, 1);
                ApplyCharacterAtmosphere(parent.Find(name).GetComponent<RawImage>(), art, frame);
                AddCharacterProjection(parent.Find(name).GetComponent<RawImage>(), art, frame, foot);
                return;
            }
            float scale = Mathf.Min(width / art.width, height / art.height);
            float w = art.width * scale, h = art.height * scale;
            Texture(parent, name, art, x + (width - w) / 2, foot - h, w, h, tint);
            parent.Find(name).GetComponent<RawImage>().uvRect = new Rect(mirror ? 1 : 0, 0, mirror ? -1 : 1, 1);
            ApplyCharacterAtmosphere(parent.Find(name).GetComponent<RawImage>(), art, null);
            AddCharacterProjection(parent.Find(name).GetComponent<RawImage>(), art, null, foot);
        }

        private void ApplyCharacterAtmosphere(RawImage image, Texture2D art, CharacterLayoutFrame frame)
        {
            if (!_characterAtmospheres.TryGetValue(art, out var material))
            {
                var shader = Resources.Load<Shader>("UI/BattleCharacterAtmosphere");
                if (shader == null || !shader.isSupported) return;
                material = new Material(shader) { name = "ForestAtmosphere_" + art.name, hideFlags = HideFlags.DontSave };
                material.SetColor("_TopLight", new Color(1, .98f, .94f, 1));
                material.SetColor("_GroundLight", new Color(.88f, .92f, .96f, 1));
                material.SetFloat("_Saturation", .9f);
                material.SetVector("_BodyUV", frame == null ? new Vector4(0, 1, 0, 0)
                    : new Vector4(1-(frame.y+frame.height)/art.height, 1-frame.y/art.height, 0, 0));
                _characterAtmospheres.Add(art, material);
            }
            image.material = material;
        }

        [Serializable]
        private sealed class CharacterFacingEntry
        {
            public string id, category, resourceVersion, sha256;
            public bool mirrorHorizontal;
        }

        [Serializable]
        private sealed class CharacterFacingRegistry { public CharacterFacingEntry[] items; }
        private static CharacterFacingRegistry _characterFacing;

        private static bool MirrorCharacter(Texture art)
        {
            if (art == null) return false;
            if (_characterFacing == null)
            {
                var resource = Resources.Load<TextAsset>("UI/CharacterFacing/mirrors_v1");
                _characterFacing = resource == null ? new CharacterFacingRegistry()
                    : JsonUtility.FromJson<CharacterFacingRegistry>(resource.text);
            }
            if (_characterFacing.items == null) return false;
            // Match the actual versioned texture object, not just its shared name.
            // New or repainted versions must receive a new independent review.
            foreach (var entry in _characterFacing.items)
                if (entry.mirrorHorizontal && entry.id == art.name && art == Resources.Load<Texture2D>(
                    "UI/" + entry.category + "/" + entry.resourceVersion + "/" + entry.id)) return true;
            return false;
        }

        [Serializable]
        private sealed class CharacterLayoutFrame
        {
            public float x = 0, y = 0, width = 0, height = 0;
            public int textureWidth = 0, textureHeight = 0;
        }

        private static string CharacterAssetVersion(Texture displayed, string category, string id)
        {
            if (displayed == null) return "none";
            foreach (string version in new[] { "embedded_v5", "embedded_v4", "embedded_v3", "embedded_v2", "embedded_v1" })
                if (displayed == Resources.Load<Texture2D>("UI/" + category + "/" + version + "/" + id)) return version;
            return "fallback";
        }

        private static CharacterLayoutFrame CharacterFrameFor(Texture art)
        {
            // Versioned artwork has its own layout metadata. Never overwrite the
            // older frame: it must still describe the old PNG after rollback.
            bool v5 = art == Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v5/" + art.name);
            bool v4 = art == Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v4/" + art.name);
            bool v3 = art == Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v3/" + art.name);
            var asset = Resources.Load<TextAsset>("UI/CharacterFrames/" + (v5 ? "embedded_v5/" : v4 ? "embedded_v4/" : v3 ? "embedded_v3/" : "") + art.name);
            return asset == null ? null : JsonUtility.FromJson<CharacterLayoutFrame>(asset.text);
        }

        private void DrawPileCount(string id, string name, int count, float x)
        {
            var stack = W.Rect(_root, id, x, 858, 125, 156);
            for (int i = 2; i >= 0; i--)
            {
                var card = W.Panel(stack, "CardBack" + i, i * 4, -i * 4, 106, 140, new Color32(21, 43, 39, 240), true);
                card.localRotation = Quaternion.Euler(0, 0, i * 3 - 3);
            }
            W.Text(stack, "Count", count.ToString(), 0, 23, 108, 60, 34, W.Gold, TextAlignmentOptions.Center);
            W.Text(stack, "Label", name, 0, 90, 108, 40, 23, W.Cream, TextAlignmentOptions.Center);
            var hit = stack.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            HoverInfo(stack.gameObject, name + " " + count, name == "牌库" ? "剩余牌数。未揭示牌序不在此公开。" : string.Join("\n", _battle.Player.Deck.DiscardPile.Select(CardName)));
        }

        private void MoveShortcut(string id, float x, float y, string label, float width = 190)
        {
            var node = _root.Find(id); if (node == null) return;
            Place(node, x, y, width, 46);
            var text = node.GetComponentInChildren<TMP_Text>();
            if (label != null) text.text = label;
            Place(text.transform, 5, 0, width - 10, 46);
            Place(node.Find("ControlPlate"), 0, 0, width, 46);
            foreach (Transform edge in node)
                if (edge.name == "ControlTopEdge" || edge.name == "ControlBottomEdge") edge.gameObject.SetActive(false);
        }

        private string CardActionPreview()
        {
            var current = _battle.Choices(_selected).FirstOrDefault(c => c.Id == _choice);
            // Remove only repeated generic submission instructions from the
            // immediate preview; the unchanged full CardText remains in details.
            string text = string.Join("\n", CardText(_selected).Split('\n')
                .Where(line => line != "确认后支付费用并移入已打出区。"));
            if (_selected.Type == CardType.Action && current?.Enhanced == false)
                text += "\n印刷魔力：无";
            return text;
        }

        private void ComposeActionDock()
        {
            var dock = _root.Find("Inspector"); if (dock == null) return;
            Place(dock, 1532, 230, 352, 810);
            // The top combat-log button opens this same complete journal.
            // A second shortcut below the action dock covered the discard pile.
            // Keep one stable entry; no feedback or journal content is removed.
            var repeatedJournal = dock.Find("Btn_ResolutionRecall");
            if (repeatedJournal != null) repeatedJournal.gameObject.SetActive(false);
            // Opening the retained full result is an explicit reading mode. Keep its
            // real ScrollRect and complete text instead of reducing it to a tooltip.
            if (_showResolutionRecall && !string.IsNullOrEmpty(_lastResolutionText))
            { Place(dock, 1408, 230, 472, 810); return; }
            var existing = dock.Cast<Transform>().ToArray();
            var buttons = existing.Where(t => t.name != "Btn_ResolutionRecall").Select(t => t.GetComponent<Button>()).Where(b => b != null).ToArray();
            // Full descriptions remain on demand; target/effective requirement is kept immediately visible.
            string details = string.Join("\n\n", existing.Select(t => t.GetComponent<TMP_Text>()).Where(t => t != null).Select(t => t.text));
            foreach (var child in existing) if (child.GetComponent<Button>() == null) child.gameObject.SetActive(false);
            var background = dock.GetComponent<Image>(); if (background != null) background.enabled = false;
            bool decision = _selected != null || _selectedUnit != null || _selectedSkill != null || _targets.Count > 0 || _battle.Phase == Phase.AssignDamage || _battle.Phase == Phase.End || _recovery || _bowPanel;
            var content = W.Rect(dock, "Actions", 0, 0, 352, 670);
            float y = 0;
            string selectedUnitStatus = null, selectedUnitFullStatus = null, selectedUnitActionHelp = null;
            RectTransform selectedCommandFact = null;
            bool IsUnitCommand(Button b) => b.name.StartsWith("Btn_UnitAbility_", StringComparison.Ordinal)
                || b.name == "Btn_AssignUnit" || b.name == "Btn_Fortitude" || b.name == "Btn_FearBanner" || b.name == "Btn_UseFear"
                || b.name == "Btn_Courage" || b.name == "Btn_HealWound";
            if (decision)
            {
                string heading = _selected != null ? CardName(_selected) : _selectedSkill?.Id ?? _selectedUnit?.Card.NameCn ?? PhaseIndicatorName(_battle.Phase);
                var title = W.Text(content, "ContextTitle", heading, 8, 0, 330, 44, 26, W.Gold);
                var hit = title.gameObject.AddComponent<CanvasGroup>(); hit.blocksRaycasts = false;
                var info = W.Panel(content, "ContextInfoHover", 0, 0, 352, 45, Color.clear);
                info.GetComponent<Image>().raycastTarget = true; HoverInfo(info.gameObject, heading, details);
                y = 52;
                // Keep exact short decision text beside the selected skill's
                // confirm choices. Longer original rules use the full reader;
                // never invent or truncate a numerical/conditional summary.
                if (_selectedSkill != null && !string.IsNullOrWhiteSpace(_selectedSkill.PrintedRule)
                    && _selectedSkill.PrintedRule.Length <= 90)
                {
                    var rule = W.Text(content,"SkillDecisionRule",_selectedSkill.PrintedRule,8,y,330,160,22,W.Cream);
                    rule.enableAutoSizing=false;
                    float height=44;
                    rule.rectTransform.sizeDelta=new Vector2(330,height);y+=height+10;
                }
                if (_selectedUnit != null)
                {
                    string status = string.Join("\n", UnitText(_selectedUnit).Split('\n').Take(2));
                    bool hasPhaseAction = buttons.Any(b => b.name.StartsWith("Btn_Unit", StringComparison.Ordinal)
                        || b.name == "Btn_AssignUnit" || b.name == "Btn_Fortitude" || b.name == "Btn_FearBanner" || b.name == "Btn_UseFear");
                    if (!hasPhaseAction)
                    {
                        status = status.Replace("可以激活", "本阶段无适用能力");
                        var restriction = existing.FirstOrDefault(t => t.name == "UnitSiteRestriction")?.GetComponent<TMP_Text>();
                        status += "\n" + (restriction != null ? restriction.text : "当前" + PhaseIndicatorName(_battle.Phase) + "阶段没有适用能力。");
                    }
                    string guidance = _battle.Phase == Phase.Block && buttons.Any(b => b.name.StartsWith("Btn_UnitAbility_", StringComparison.Ordinal))
                        ? (_selectedUnit.CanActivate ? "激活能力生成格挡资源；再选敌人一次攻击确认格挡。" : "核对格挡资源，再选敌人一次攻击确认格挡。") : "";
                    var statusHelp = info.GetComponent<BattleHoverInfo>();
                    selectedUnitStatus = status;
                    selectedUnitActionHelp = status + "\n\n激活后耗竭。"
                        + (string.IsNullOrEmpty(guidance) ? "" : "\n" + guidance + "\n每次攻击须完整格挡；不足仍承受完整伤害。");
                    string fullStatus = status + "\n\n" + string.Join("\n", UnitText(_selectedUnit).Split('\n').Skip(2)) +
                        (string.IsNullOrEmpty(guidance) ? "" : "\n\n" + guidance + "\n每次攻击须完整格挡；不足仍承受完整伤害。");
                    statusHelp.body = fullStatus;
                    statusHelp.entityDetails = true;
                    statusHelp.bodyFontSize = 22;
                    statusHelp.compactGuidance = false;
                    int armor = _selectedUnit.Card.Armor + _battle.Context.UnitArmorBonus
                        + BannerActions.HonorBonus(_battle.Context, _selectedUnit) + BannerActions.ProtectionArmor(_selectedUnit);
                    RectTransform State(int column, string id, int kind, string caption, string headingText, string explanation)
                    {
                        float tileWidth = column == 4 ? 76 : 60;
                        var tile = W.Rect(content, id, 8 + column * 66, y, tileWidth, 78);
                        var glyph = W.Rect(tile, "Glyph", (tileWidth - 32) / 2, 0, 32, 32).gameObject.AddComponent<BattlePartyStateGlyph>();
                        glyph.kind = kind; glyph.color = W.Gold; glyph.raycastTarget = false;
                        var value = W.Text(tile, "Value", caption, 0, 34, tileWidth, 40, column < 3 ? 24 : 21, W.Cream, TextAlignmentOptions.Center);
                        value.raycastTarget = false; value.outlineColor = new Color32(11, 23, 20, 255); value.outlineWidth = .18f;
                        var surface = tile.gameObject.AddComponent<Image>(); surface.color = Color.clear;
                        HoverInfo(tile.gameObject, headingText, fullStatus + "\n\n" + explanation, true);
                        tile.GetComponent<BattleHoverInfo>().bodyFontSize = 22;
                        tile.GetComponent<BattleHoverInfo>().symbolFact = true;
                        return tile;
                    }
                    State(0, "UnitRankFact", 4, _selectedUnit.Card.Level.ToString(), "部队等级", "军衔表示原卡等级；数字为本部队实际等级。");
                    State(1, "UnitArmorFact", 5, armor.ToString(), "当前护甲", "盾牌表示当前有效护甲，包含本回合和旌旗加成。");
                    State(2, "UnitWoundFact", 2, _selectedUnit.Wounds.ToString(), "实际创伤", "血滴数字表示创伤；与就绪/耗竭独立。");
                    State(3, "UnitReadinessFact", _selectedUnit.IsReady ? 0 : 1, _selectedUnit.IsReady ? "就绪" : "耗竭",
                        "就绪状态", "闪电=就绪，沙漏=耗竭。就绪不保证当前阶段、费用和伤势允许激活。");
                    selectedUnitFullStatus = fullStatus;
                    selectedCommandFact = State(4, "UnitCommandFact", 6, "可选0", "当前可选行动", "按最终行动按钮的启用状态计数。");
                    y += 88;
                    string restrictionReason = !UnitParticipation.CanUse(_battle.Player, _battle.Context, _selectedUnit) ? "地点禁止"
                        : !buttons.Any(IsUnitCommand) ? "本阶段无能力"
                        : _leadershipSkill == null && !DelphanaMastersActions.IsMasters(_selectedUnit) && !_selectedUnit.CanActivate
                            ? (_selectedUnit.Wounds > 0 ? "伤势限制" : !_selectedUnit.IsReady ? "已耗竭" : "当前受限") : "";
                    if (!string.IsNullOrEmpty(restrictionReason))
                    {
                        var reason = W.Rect(content, "UnitRestrictionReason", 8, y, 330, 38);
                        reason.gameObject.AddComponent<Image>().color = Color.clear;
                        var lockGlyph = W.Rect(reason, "RestrictionGlyph", 0, 3, 28, 28).gameObject.AddComponent<BattlePartyStateGlyph>();
                        lockGlyph.kind = 6; lockGlyph.color = W.Gold; lockGlyph.raycastTarget = false;
                        W.Text(reason, "RestrictionLabel", restrictionReason, 40, 0, 284, 38, 22, W.Gold).raycastTarget = false;
                        HoverInfo(reason.gameObject, restrictionReason, fullStatus, true);
                        reason.GetComponent<BattleHoverInfo>().symbolFact = true;
                        reason.GetComponent<BattleHoverInfo>().bodyFontSize = 22;
                        y += 44;
                    }
                }
                if (_targets.Count > 0 || _battle.Phase == Phase.AssignDamage)
                {
                    string summary = _battle.Phase == Phase.AssignDamage ? DamagePlanText() : TargetPreview();
                    var preview = W.Text(content, "EffectiveTarget", summary, 8, y, 330, 140, 22, W.Cream);
                    preview.overflowMode = TextOverflowModes.Overflow;
                    float height = _battle.Phase == Phase.AssignDamage || _battle.Phase == Phase.Block ? 78 : 156;
                    preview.rectTransform.sizeDelta = new Vector2(330, height); y += height;
                }
            }
            // Mode and short branch choices form small groups, while the
            // selected printed effect is readable beside the card. Long labels
            // keep a full-width row; no automatic font reduction is used.
            bool cardChoices = _selected != null && buttons.Any(b => b.name == "Btn_Choice_base")
                && buttons.Any(b => b.name == "Btn_Choice_strong");
            var choiceRects = new Dictionary<Button, Rect>();
            if (cardChoices)
            {
                var modes = buttons.Where(b => b.name == "Btn_Choice_base" || b.name == "Btn_Choice_strong").ToArray();
                var branches = buttons.Where(b => b.name.StartsWith("Btn_Choice_", StringComparison.Ordinal) && !modes.Contains(b)).ToArray();
                void LayoutGroup(Button[] group)
                {
                    bool shortLabels = group.All(b => {
                        var label = b.GetComponentInChildren<TMP_Text>(); label.enableAutoSizing = false; label.fontSize = 21;
                        return label.GetPreferredValues(label.text, float.PositiveInfinity, 0).x <= 140;
                    });
                    int columns = shortLabels ? 2 : 1; float rowWidth = shortLabels ? 160 : 330;
                    for (int start = 0; start < group.Length; start += columns)
                    {
                        float height = 46;
                        for (int col = 0; col < columns && start + col < group.Length; col++)
                        {
                            var label = group[start + col].GetComponentInChildren<TMP_Text>(); label.enableAutoSizing = false; label.fontSize = 21;
                            height = Mathf.Max(height, label.GetPreferredValues(label.text, rowWidth - 20, 0).y + 16);
                        }
                        for (int col = 0; col < columns && start + col < group.Length; col++)
                            choiceRects[group[start + col]] = new Rect(8 + col * 170, y, rowWidth, height);
                        y += height + 9;
                    }
                    if (group.Length > 0) y += 7;
                }
                var selectedChoice = _battle.Choices(_selected).FirstOrDefault(c => c.Id == _choice);
                var fees = CardActionPreview().Split('\n').Where(line => line.StartsWith("印刷", StringComparison.Ordinal)
                    || line.StartsWith("免费强化：", StringComparison.Ordinal));
                string summary = (selectedChoice?.Label ?? "尚未选定效果") + "\n" + string.Join("\n", fees);
                var effect = W.Text(content, "CurrentCardEffect", summary, 8, y, 330, 160, 22, W.Cream);
                effect.textWrappingMode = TextWrappingModes.Normal; effect.overflowMode = TextOverflowModes.Overflow;
                effect.ForceMeshUpdate();
                float effectHeight = 50;
                effect.rectTransform.sizeDelta = new Vector2(330, effectHeight); y += effectHeight;
                AddReadableLabelBacking(effect);
                var hint = W.Text(content, "CardEffectHint", "完整效果与条件 · 悬浮查看", 8, y, 330, 34, 21, W.Gold);
                AddReadableLabelBacking(hint);
                // The full printed effect is reached through the symbol reader.
                HoverInfo(effect.gameObject, CardName(_selected) + " · 完整效果", CardActionPreview(), true);
                effect.GetComponent<BattleHoverInfo>().cardRuleDetail = true;
                HoverInfo(hint.gameObject, CardName(_selected) + " · 完整效果", CardActionPreview(), true);
                hint.GetComponent<BattleHoverInfo>().cardRuleDetail = true;
                // Disabled sideways remains discoverable with its real reason,
                // but does not consume a primary-command row in this phase.
                var disabledSideways = buttons.FirstOrDefault(b => b.name == "Btn_Sideways" && !b.interactable);
                if (disabledSideways != null)
                {
                    var label = disabledSideways.GetComponentInChildren<TMP_Text>();
                    label.text = "横置使用 · 本阶段不可用"; label.enableAutoSizing = false; label.fontSize = 21;
                    float height = Mathf.Max(34, label.GetPreferredValues(label.text, 310, 0).y + 8);
                    choiceRects[disabledSideways] = new Rect(8, y, 330, height); y += height + 9;
                }
                var titleHelp = content.Find("ContextInfoHover")?.GetComponent<BattleHoverInfo>();
                if (titleHelp != null) { titleHelp.entityDetails = true; titleHelp.cardRuleDetail = true; titleHelp.body = CardActionPreview(); }
                LayoutGroup(modes);
                float branchTop = y; LayoutGroup(branches);
                float reserve = y - branchTop;
                var measure = modes[0].GetComponentInChildren<TMP_Text>(); measure.enableAutoSizing = false; measure.fontSize = 21;
                foreach (bool enhanced in new[] { false, true })
                {
                    var choices = _battle.Choices(_selected).Where(c => c.Enhanced == enhanced).Take(4)
                        .Where(c => c.Id != "base" && c.Id != "strong").ToArray();
                    bool small = choices.All(c => measure.GetPreferredValues("● " + c.Label, float.PositiveInfinity, 0).x <= 140);
                    int columns = small ? 2 : 1; float width = small ? 140 : 310; float height = 0;
                    for (int first = 0; first < choices.Length; first += columns)
                    {
                        float row = 46;
                        for (int col = 0; col < columns && first + col < choices.Length; col++)
                            row = Mathf.Max(row, measure.GetPreferredValues("● " + choices[first + col].Label, width, 0).y + 16);
                        height += row + 9;
                    }
                    if (choices.Length > 0) height += 7;
                    reserve = Mathf.Max(reserve, height);
                }
                y = branchTop + reserve;
                foreach (var command in buttons.Where(b => b != disabledSideways
                    && !b.name.StartsWith("Btn_Choice_", StringComparison.Ordinal)
                    && b.name != "Btn_Next" && b.name != "Btn_EndTurn" && b.name != "Btn_Restart"
                    && b.name != "Btn_FinishSite" && b.name != "Btn_SiteReward"))
                {
                    var label = command.GetComponentInChildren<TMP_Text>(); label.enableAutoSizing = false; label.fontSize = 21;
                    float height = Mathf.Max(46, label.GetPreferredValues(label.text, 310, 0).y + 16);
                    choiceRects[command] = new Rect(8, y, 330, height); y += height + 9;
                }
                // Move the complete available-command group toward the hand.
                // Rich/long cases keep ordinary scroll flow instead of shrinking.
                if (y <= 470)
                {
                    float shift = 470 - y;
                    foreach (var key in choiceRects.Keys.Where(b => b != disabledSideways).ToArray())
                    { var rect = choiceRects[key]; rect.y += shift; choiceRects[key] = rect; }
                    y = 470;
                }
                // A single edge-faded action well separates the interactive group
                // from busy scenery. It is not a plate on every choice: selected
                // modes, faceted resources and the execution arrow remain distinct.
                var availableRects = choiceRects.Where(p => p.Key != disabledSideways).Select(p => p.Value).ToArray();
                if (availableRects.Length > 0)
                {
                    float top = availableRects.Min(r => r.y), bottom = availableRects.Max(r => r.yMax);
                    var veil = W.Rect(content, "ActionShelfShade", -8, top - 12, 370, bottom - top + 24).gameObject.AddComponent<BattleLabelBacking>();
                    veil.color = new Color(.012f, .022f, .022f, .92f); veil.raycastTarget = false;
                    veil.transform.SetAsFirstSibling();
                }
            }
            foreach (var button in buttons)
            {
                bool next = button.name == "Btn_Next" || button.name == "Btn_EndTurn" || button.name == "Btn_Restart" || button.name == "Btn_FinishSite" || button.name == "Btn_SiteReward";
                if (next)
                {
                    Place(button.transform, 70, 490, 280, 86);
                    var text = button.GetComponentInChildren<TMP_Text>(); Place(text.transform, 10, 0, 260, 86);
                    Place(button.transform.Find("ControlPlate"), 0, 0, 280, 86);
                    foreach (Transform edge in button.transform)
                        if (edge.name == "ControlTopEdge" || edge.name == "ControlBottomEdge") edge.gameObject.SetActive(false);
                    StylePhaseCommand(button, text, _selected == null && _selectedUnit == null && _selectedSkill == null && _targets.Count == 0);
                    continue;
                }
                if (!decision && button.name != "Btn_Conversions" && button.name != "Btn_ReduceAttack" && button.name != "Btn_ExplosiveArmor" && button.name != "Btn_SkipExplosiveArmor") { button.gameObject.SetActive(false); continue; }
                button.transform.SetParent(content, false);
                var caption = button.GetComponentInChildren<TMP_Text>(); caption.enableAutoSizing = false; caption.fontSize = 21;
                if (cardChoices && button.name == "Btn_Sideways" && !button.interactable)
                {
                    caption.text = "横置使用 · 本阶段不可用";
                    QuietGraphics(button.transform);
                    var hit = button.GetComponent<Graphic>(); if (hit != null) { hit.enabled = true; hit.color = Color.clear; }
                }
                // Match the ordinary native readiness guard. Masters have their
                // own continuation rules, and leadership has a separate consumer.
                if (button.name.StartsWith("Btn_UnitAbility_", StringComparison.Ordinal))
                    caption.text = "激活 · " + caption.text;
                if (_selectedUnit != null && _leadershipSkill == null && !DelphanaMastersActions.IsMasters(_selectedUnit)
                    && button.name.StartsWith("Btn_UnitAbility_", StringComparison.Ordinal) && !_selectedUnit.CanActivate)
                {
                    button.interactable = false;
                    caption.text += " · 当前不能激活";
                    // Disabled controls retain readable text, without a ready-looking
                    // colored plate. Enabledness remains governed by native readiness.
                    var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
                    caption.color = W.Cream;
                    var disabledPlate = button.transform.Find("ControlPlate")?.GetComponent<Graphic>();
                    if (disabledPlate != null) disabledPlate.color = new Color32(28, 36, 35, 245);
                }
                string fullUnitCaption = caption.text;
                bool unitCombatSymbol = TryUnitCombatSummary(button, out string unitCombatSummary);
                if (unitCombatSymbol) { caption.text = unitCombatSummary; caption.fontSize = 22; }
                // The dock narrows controls after their original caption styling.
                // Appended activation/readiness text can invalidate its old NoWrap
                // decision. Measure the final text in its actual wrapped width.
                caption.textWrappingMode = TextWrappingModes.Normal;
                caption.overflowMode = TextOverflowModes.Overflow;
                bool grouped = choiceRects.TryGetValue(button, out var choiceRect);
                float width = grouped ? choiceRect.width : 330;
                float h = grouped ? choiceRect.height : Mathf.Max(unitCombatSymbol ? 96 : 46, caption.GetPreferredValues(caption.text, width - (unitCombatSymbol ? 65 : 20), 0).y + 16);
                Place(button.transform, grouped ? choiceRect.x : 8, grouped ? choiceRect.y : y, width, h);
                Place(caption.transform, 10, 0, width - 20, h);
                var plate = button.transform.Find("ControlPlate"); Place(plate, 0, 0, width, h);
                foreach (Transform child in button.transform)
                    if (child.name == "ControlTopEdge" || child.name == "ControlBottomEdge") child.gameObject.SetActive(false);
                if (cardChoices) StyleCardCommand(button, caption, width, h);
                if (unitCombatSymbol)
                {
                    StyleUnitCombatSymbol(button, caption, width, h);
                    HoverInfo(button.gameObject, _selectedUnit.Card.NameCn + " · 原能力与费用", fullUnitCaption + "\n\n" + selectedUnitActionHelp, true);
                    var unitHelp = button.GetComponent<BattleHoverInfo>(); unitHelp.symbolFact = true; unitHelp.compactRule = true; unitHelp.bodyFontSize = 22;
                    button.gameObject.AddComponent<BattleUnitCommandEvidence>().fullCaption = fullUnitCaption;
                }
                else HoverInfo(button.gameObject, caption.text, details); if (!grouped) y += h + 9;
            }
            // Long option lists scroll within the same contextual dock without reducing text.
            if (y > 470)
            {
                var viewport = W.Rect(dock, "ActionViewport", 0, 0, 352, 470);
                viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .12f);
                viewport.gameObject.AddComponent<RectMask2D>(); content.SetParent(viewport, false);
                content.sizeDelta = new Vector2(352, y);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
                scroll.horizontal = false; scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            // Compare against the actual final action captions and enabledness,
            // rather than recomputing effects or payment rules in a tooltip.
            var selectedButtons = content.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            if (selectedCommandFact != null)
            {
                var commands = selectedButtons.Where(IsUnitCommand).ToArray();
                int enabledCount = commands.Count(b => b.IsInteractable());
                selectedCommandFact.GetComponentInChildren<BattlePartyStateGlyph>().kind = enabledCount > 0 ? 7 : 6;
                selectedCommandFact.Find("Value").GetComponent<TMP_Text>().text = "可选" + enabledCount;
                selectedCommandFact.GetComponent<BattleHoverInfo>().body = selectedUnitFullStatus
                    + "\n\n可选" + enabledCount + "：当前已展开并启用的能力、承伤或旌旗行动数量。"
                    + "不保证费用、目标已满足或必定结算成功；预选模式、切换目标和展开入口不计入。\n"
                    + string.Join("\n", commands.Select(b => (b.IsInteractable() ? "可选：" : "不可选：") + (b.GetComponent<BattleUnitCommandEvidence>()?.fullCaption ?? b.GetComponentInChildren<TMP_Text>().text)));
            }
            var selectedStatus = selectedUnitStatus;
            string selectedBody = string.Join("\n", selectedButtons.Select(b =>
                (b.name.StartsWith("Btn_SkillChoice_",StringComparison.Ordinal)
                    ? (b.interactable ? "可预选：" : "不可预选：")
                    : (b.interactable ? "可用：" : "不可用：")) + (b.GetComponent<BattleUnitCommandEvidence>()?.fullCaption ?? b.GetComponentInChildren<TMP_Text>().text)));
            if (!string.IsNullOrEmpty(selectedStatus)) selectedBody = selectedStatus + "\n" + selectedBody;
            if (cardChoices)
            {
                var selectedChoice = _battle.Choices(_selected).FirstOrDefault(c => c.Id == _choice);
                selectedBody = (selectedChoice?.Enhanced == true ? "强化 · " : "基础 · ") + (selectedChoice?.Label ?? "尚未选定效果") + "\n" + CardActionPreview();
                var effectText = content.GetComponentsInChildren<TMP_Text>().First(t => t.name == "CurrentCardEffect");
                if (effectText.GetPreferredValues(selectedBody, 316, 0).y + 12 > 215)
                {
                    // Native branch caption + exact printed-fee lines form the
                    // comparison summary. The complete printed rule remains in
                    // CurrentCardEffect and the existing selected-details entry.
                    var fees = CardActionPreview().Split('\n').Where(line => line.StartsWith("印刷", StringComparison.Ordinal)
                        || line.StartsWith("免费强化：", StringComparison.Ordinal));
                    selectedBody = (selectedChoice?.Enhanced == true ? "强化 · " : "基础 · ") + (selectedChoice?.Label ?? "尚未选定效果") + "\n" + string.Join("\n", fees) + "\n全文：所选详情";
                }
            }
            foreach (var hover in _root.GetComponentsInChildren<BattleHoverInfo>())
                if (hover.entityDetails && !hover.symbolFact) { hover.selectedActionBody = selectedBody; hover.selectedActionCount = selectedButtons.Length; hover.selectedCardEffect = cardChoices; }
            string feedback = string.IsNullOrWhiteSpace(LastRule) ? "" : LastRule.Split('\n').LastOrDefault(s => !string.IsNullOrWhiteSpace(s));
            _embeddedToast = null;
            if (_inputSequence > 0 && Time.unscaledTime - _inputAt < 4 && !string.IsNullOrEmpty(feedback))
            {
                FieldFact(_root, "FieldFact_LastFeedback", _inputRejected ? "Alert" : "Success", "",
                    _inputRejected ? "操作未完成" : "操作反馈", LastRule, 1840, 118, 44);
            }
        }

        private void StylePhaseCommand(Button button, TMP_Text caption, bool primary)
        {
            primary = primary && button.interactable;
            QuietGraphics(button.transform);
            var hit = button.GetComponent<Graphic>(); hit.enabled = true; hit.color = Color.clear; button.targetGraphic = hit;
            var seal = W.Rect(button.transform, "PhaseExecutionSeal", 0, 0, 280, 86).gameObject.AddComponent<BattleExecutionSeal>();
            seal.color = primary && button.interactable ? W.Gold : new Color32(22, 38, 35, 250); seal.raycastTarget = false;
            seal.transform.SetAsFirstSibling();
            var arrow = W.Rect(button.transform, "PhaseForwardGlyph", 238, 26, 30, 34).gameObject.AddComponent<BattleCardCommandGlyph>();
            arrow.play = true; arrow.color = primary ? W.Ink : W.Gold; arrow.raycastTarget = false;
            Place(caption.transform, 14, 0, 220, 86); caption.color = primary ? W.Ink : W.Cream;
            caption.textWrappingMode = TextWrappingModes.Normal;
            var glow = W.Rect(button.transform, "PhaseHoverGlow", 0, 0, 280, 86).gameObject.AddComponent<BattleRoundGraphic>();
            glow.color = Color.clear; glow.feathered = true; glow.raycastTarget = false;
            glow.transform.SetSiblingIndex(caption.transform.GetSiblingIndex());
            var focus = button.gameObject.AddComponent<BattleCommandFocus>(); focus.glow = glow;
        }

        private void StyleCardCommand(Button button, TMP_Text caption, float width, float height)
        {
            bool mode = button.name == "Btn_Choice_base" || button.name == "Btn_Choice_strong";
            string branch = button.name.StartsWith("Btn_Choice_", StringComparison.Ordinal) ? button.name.Substring(11) : "";
            bool gem = branch == "blue" || branch == "white" || branch == "red" || branch == "green_crystal";
            bool play = button.name == "Btn_Play" && button.interactable;
            if (!mode && !gem && !play) return;
            // Retain the original Button and full hit rectangle; decorations never
            // receive raycasts, and the native choice/cost handlers are unchanged.
            QuietGraphics(button.transform);
            var hit = button.GetComponent<Graphic>();
            if (hit != null) { hit.enabled = true; hit.color = Color.clear; button.targetGraphic = hit; }
            var shade = W.Rect(button.transform, "ActionReadability", 0, 0, width, height).gameObject.AddComponent<BattleRoundGraphic>();
            shade.color = new Color(.01f, .02f, .02f, .88f); shade.feathered = true; shade.raycastTarget = false;
            shade.transform.SetAsFirstSibling();
            bool chosen = caption.text.StartsWith("●", StringComparison.Ordinal);
            var glow = W.Rect(button.transform, "ActionHoverGlow", 0, 0, width, height).gameObject.AddComponent<BattleRoundGraphic>();
            glow.feathered = true; glow.color = new Color(.85f, .68f, .3f, 0); glow.raycastTarget = false;
            glow.transform.SetSiblingIndex(caption.transform.GetSiblingIndex());
            var focus = button.gameObject.AddComponent<BattleCommandFocus>(); focus.glow = glow;
            if (mode)
            {
                var line = W.Panel(button.transform, "ModeSelectionUnderline", 12, height - 4, width - 24, chosen ? 3 : 1,
                    chosen ? W.Gold : new Color(.64f, .66f, .61f, .45f));
                line.GetComponent<Graphic>().raycastTarget = false;
                caption.color = chosen ? W.Gold : W.Cream;
                return;
            }
            Color tint = play ? W.Gold : branch == "blue" ? new Color32(87, 175, 245, 255)
                : branch == "red" ? new Color32(244, 111, 91, 255) : branch == "white" ? new Color32(232, 241, 240, 255)
                : new Color32(105, 219, 132, 255);
            if (play)
            {
                var seal = W.Rect(button.transform, "PrimaryExecutionSeal", 0, 0, width, height).gameObject.AddComponent<BattleExecutionSeal>();
                seal.color = W.Gold; seal.raycastTarget = false;
                seal.transform.SetSiblingIndex(caption.transform.GetSiblingIndex());
                tint = W.Ink;
            }
            var icon = W.Rect(button.transform, "ActionObjectGlyph", play ? 8 : 2, (height - 34) / 2, 34, 34).gameObject.AddComponent<BattleCardCommandGlyph>();
            icon.color = tint; icon.play = play; icon.crystal = branch == "green_crystal"; icon.raycastTarget = false;
            Place(caption.transform, play ? 50 : 38, 0, width - (play ? 54 : 40), height);
            caption.alignment = TextAlignmentOptions.MidlineLeft; caption.color = play ? W.Ink : chosen ? W.Gold : W.Cream;
            if (chosen)
            {
                var ring = W.Rect(button.transform, "SelectedResourceRing", 0, (height - 38) / 2, 38, 38).gameObject.AddComponent<BattleRoundGraphic>();
                ring.hollow = true; ring.color = W.Gold; ring.raycastTarget = false;
            }
        }
    }

    // A single primary operation has a bronze/gold game control silhouette.
    // Choices remain secondary: this isn't another identical row of plates.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleExecutionSeal : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r=GetPixelAdjustedRect(); const int steps=16;
            float radius=r.height/2;
            var points=new List<Vector2>();
            for(int i=0;i<=steps;i++)
            {
                float a=(-90+i*180f/steps)*Mathf.Deg2Rad;
                points.Add(new Vector2(r.xMax-radius+Mathf.Cos(a)*radius,r.center.y+Mathf.Sin(a)*radius));
            }
            for(int i=0;i<=steps;i++)
            {
                float a=(90+i*180f/steps)*Mathf.Deg2Rad;
                points.Add(new Vector2(r.xMin+radius+Mathf.Cos(a)*radius,r.center.y+Mathf.Sin(a)*radius));
            }
            mesh.AddVert(r.center,color,Vector2.zero);
            foreach(var p in points)
            {
                float light=Mathf.InverseLerp(r.yMin,r.yMax,p.y);
                var tint=Color.Lerp(Color.Lerp(color,Color.black,.27f),Color.Lerp(color,Color.white,.2f),light);
                mesh.AddVert(p,tint,Vector2.zero);
            }
            for(int i=0;i<points.Count;i++)mesh.AddTriangle(0,i+1,(i+1)%points.Count+1);
        }
    }

    // Local text contrast, with a flat opaque reading area and soft outer edges.
    // Unlike an ellipse, the backdrop does not fade under the text's end lines.
    public sealed class BattleCommandFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Graphic glow;
        public void OnPointerEnter(PointerEventData data) { if (glow != null) glow.color = new Color(.85f, .68f, .3f, .28f); }
        public void OnPointerExit(PointerEventData data) { if (glow != null) glow.color = new Color(.85f, .68f, .3f, 0); }
    }

    // Mana choices are colored, faceted objects; execution has a distinct arrow.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleCardCommandGlyph : MaskableGraphic
    {
        public bool play, crystal;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 P(float x, float y) => new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
            void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
            { int i=mesh.currentVertCount; mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddTriangle(i,i+1,i+2); }
            if (play)
            {
                Triangle(P(.24f,.16f),P(.86f,.5f),P(.24f,.84f),color);
                Triangle(P(.24f,.16f),P(.42f,.5f),P(.86f,.5f),Color.Lerp(color,Color.black,.2f));
                return;
            }
            var center=P(.5f,.48f);int sides=crystal?4:6;
            for(int i=0;i<sides;i++)
            {
                float a=(90+i*360f/sides)*Mathf.Deg2Rad,b=(90+(i+1)*360f/sides)*Mathf.Deg2Rad;
                var p=center+new Vector2(Mathf.Cos(a)*r.width*.34f,Mathf.Sin(a)*r.height*.43f);
                var q=center+new Vector2(Mathf.Cos(b)*r.width*.34f,Mathf.Sin(b)*r.height*.43f);
                Triangle(center,p,q,Color.Lerp(color,i<2?Color.white:Color.black,i<2?.3f:.25f));
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleLabelBacking : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            float[] xs = { r.xMin, r.xMin + 12, r.xMax - 12, r.xMax };
            float[] ys = { r.yMin, r.yMin + 5, r.yMax - 5, r.yMax };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                mesh.AddVert(new Vector2(xs[x], ys[y]), x == 0 || x == 3 || y == 0 || y == 3 ? new Color(color.r, color.g, color.b, 0) : color, Vector2.zero);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
            { int i = y * 4 + x; mesh.AddTriangle(i, i + 4, i + 1); mesh.AddTriangle(i + 1, i + 4, i + 5); }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleRoundGraphic : MaskableGraphic
    {
        public bool hollow, feathered;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = GetPixelAdjustedRect(); const int segments = 64;
            if (feathered && !hollow)
            {
                mesh.AddVert(rect.center, color, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    var radius = new Vector2(Mathf.Cos(angle) * rect.width / 2, Mathf.Sin(angle) * rect.height / 2);
                    mesh.AddVert(rect.center + radius * .68f, color, Vector2.zero);
                    mesh.AddVert(rect.center + radius, new Color(color.r, color.g, color.b, 0), Vector2.zero);
                }
                for (int i = 0; i < segments; i++)
                {
                    mesh.AddTriangle(0, i * 2 + 1, i * 2 + 3);
                    mesh.AddTriangle(i * 2 + 1, i * 2 + 2, i * 2 + 3);
                    mesh.AddTriangle(i * 2 + 2, i * 2 + 4, i * 2 + 3);
                }
                return;
            }
            if (!hollow) mesh.AddVert(rect.center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var v = new Vector2(Mathf.Cos(a) * rect.width / 2, Mathf.Sin(a) * rect.height / 2);
                mesh.AddVert(rect.center + v, color, Vector2.zero);
                if (hollow) mesh.AddVert(rect.center + v * .85f, color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                if (!hollow) mesh.AddTriangle(0, i + 1, i + 2);
                else { mesh.AddTriangle(i * 2, i * 2 + 1, i * 2 + 2); mesh.AddTriangle(i * 2 + 1, i * 2 + 3, i * 2 + 2); }
        }
    }

    // Code-native line art stays crisp at the fixed canvas size and does not
    // depend on optional font symbols or card artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleUtilityGlyph : MaskableGraphic
    {
        public enum Kind { Mana, Book, Journal, Undo, Menu, Skill, Party }
        public Kind kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 P(float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);
            void Line(float ax, float ay, float bx, float by)
            {
                var a = P(ax, ay); var b = P(bx, by); var d = b - a;
                var n = new Vector2(-d.y, d.x).normalized * 1.35f; int i = mesh.currentVertCount;
                mesh.AddVert(a + n, color, Vector2.zero); mesh.AddVert(a - n, color, Vector2.zero);
                mesh.AddVert(b + n, color, Vector2.zero); mesh.AddVert(b - n, color, Vector2.zero);
                mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i + 1, i + 3, i + 2);
            }
            if (kind == Kind.Skill)
            {
                Line(.2f,.14f,.75f,.78f);Line(.48f,.85f,.68f,.68f);Line(.82f,.98f,.82f,.8f);
                Line(.67f,.9f,.97f,.9f);Line(.1f,.65f,.28f,.65f);Line(.19f,.56f,.19f,.74f);
            }
            else if (kind == Kind.Party)
            {
                Line(.12f,.57f,.12f,.88f);Line(.12f,.88f,.42f,.88f);Line(.42f,.88f,.42f,.57f);
                Line(.12f,.57f,.27f,.45f);Line(.27f,.45f,.42f,.57f);Line(.03f,.13f,.1f,.35f);Line(.1f,.35f,.44f,.35f);
                Line(.56f,.57f,.56f,.88f);Line(.56f,.88f,.86f,.88f);Line(.86f,.88f,.86f,.57f);
                Line(.56f,.57f,.71f,.45f);Line(.71f,.45f,.86f,.57f);Line(.56f,.35f,.9f,.35f);Line(.9f,.35f,.97f,.13f);
            }
            else if (kind == Kind.Menu)
            { Line(.1f,.76f,.9f,.76f); Line(.1f,.5f,.9f,.5f); Line(.1f,.24f,.9f,.24f); }
            else if (kind == Kind.Book)
            {
                Line(.5f,.12f,.5f,.86f); Line(.08f,.24f,.08f,.92f); Line(.08f,.92f,.5f,.82f);
                Line(.08f,.24f,.5f,.12f); Line(.92f,.24f,.92f,.92f); Line(.92f,.92f,.5f,.82f); Line(.92f,.24f,.5f,.12f);
            }
            else if (kind == Kind.Journal)
            {
                Line(.15f,.08f,.15f,.92f); Line(.15f,.92f,.85f,.92f); Line(.85f,.92f,.85f,.08f); Line(.85f,.08f,.15f,.08f);
                Line(.3f,.7f,.7f,.7f); Line(.3f,.5f,.7f,.5f); Line(.3f,.3f,.6f,.3f);
            }
            else if (kind == Kind.Undo)
            {
                Line(.1f,.68f,.38f,.91f); Line(.1f,.68f,.4f,.45f); Line(.1f,.68f,.62f,.68f);
                Line(.62f,.68f,.85f,.53f); Line(.85f,.53f,.85f,.27f); Line(.85f,.27f,.66f,.1f); Line(.66f,.1f,.34f,.1f);
            }
            else
            {
                // Shared-source dice are an operation, distinct from owned gems.
                Line(.15f,.22f,.85f,.22f);Line(.85f,.22f,.85f,.86f);Line(.85f,.86f,.15f,.86f);Line(.15f,.86f,.15f,.22f);
                Line(.29f,.37f,.36f,.37f);Line(.47f,.54f,.54f,.54f);Line(.65f,.71f,.72f,.71f);
            }
        }
    }
    public sealed class BattleUtilityFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Graphic icon; public TMP_Text caption; public Button button;
        private static readonly Color cream = new Color32(231, 225, 201, 255), gold = new Color32(219, 188, 124, 255);
        public void Refresh(bool hovered)
        {
            Color tint = button != null && !button.interactable ? new Color32(160, 164, 155, 255) : hovered ? gold : cream;
            Color iconTint=tint;
            if(icon!=null&&icon.transform.parent.Find(icon.name+"RuneMount")!=null)
                iconTint=((Color)(hovered?new Color32(116,65,18,255):new Color32(35,43,34,255))).linear;
            if (icon != null) icon.color = iconTint; if (caption != null) caption.color = tint;
        }
        public void OnPointerEnter(PointerEventData data) => Refresh(true);
        public void OnPointerExit(PointerEventData data) => Refresh(false);
    }

    public sealed class BattleActorFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject indicator;
        public bool selected;
        public void OnPointerEnter(PointerEventData data) { if (indicator != null) indicator.SetActive(!selected); }
        public void OnPointerExit(PointerEventData data) { if (indicator != null) indicator.SetActive(false); }
        private void OnDisable() { if (indicator != null) indicator.SetActive(false); }
    }

    public sealed partial class BattleHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        public RectTransform surface;
        public string heading, body;
        public bool compactGuidance;
        public bool entityDetails;
        public string selectedContext;
        public string selectedActionBody;
        public bool symbolFact;
        public int selectedActionCount;
        public bool selectedCardEffect;
        public bool cardRuleDetail;
        public bool compactRule;
        public BattleInspectionData inspection;
        public int bodyFontSize = 21;
        private GameObject preview;
        private ScrollRect _entityScroll;
        public void OnPointerEnter(PointerEventData data)
        {
            if (preview != null || surface == null || string.IsNullOrEmpty(body)) return;
            if (inspection != null) { ShowInspection(); return; }
            if (entityDetails) { ShowEntityDetails(); return; }
            // Short decision help uses the unused band below the HUD and above
            // the party, instead of covering the character that is being explained.
            float width = compactGuidance ? 640 : 425;
            float textWidth = width - 36;
            float bodyTop = compactGuidance ? 44 : 87;
            var panel = CreateFolio(surface, "BattleHoverInfo", 28, compactGuidance ? 118 : 150, width, 600);
            preview = panel.gameObject;
            W.Text(panel, "Heading", heading, 18, compactGuidance ? 8 : 15, textWidth, compactGuidance ? 32 : 65, 24, FolioHeading).fontStyle = FontStyles.Bold;
            var text = W.Text(panel, "Body", body, 18, bodyTop, textWidth, 485, bodyFontSize, FolioInk);
            text.fontStyle |= FontStyles.Bold;
            text.overflowMode = TextOverflowModes.Ellipsis;
            float bodyHeight = text.GetPreferredValues(body, textWidth, 0).y;
            if (bodyHeight > 485)
            {
                PlaceReadingFooter(panel);
                text.rectTransform.sizeDelta = new Vector2(textWidth, 423);
            }
            else
            {
                text.rectTransform.sizeDelta = new Vector2(textWidth, bodyHeight + 10);
                panel.sizeDelta = new Vector2(width, compactGuidance ? bodyTop + bodyHeight + 12 : Mathf.Max(170, bodyHeight + 115));
            }
            // Read-only hover information must not steal clicks from the party or
            // hand underneath it, or hold pointer focus after leaving the entity.
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }
        private static readonly Color FolioInk = ((Color)new Color32(48, 41, 29, 255)).linear;
        private static readonly Color FolioHeading = ((Color)new Color32(99, 66, 31, 255)).linear;
        private static RectTransform CreateFolio(Transform parent, string name, float x, float y, float width, float height)
        {
            var page = W.Rect(parent, name, x, y, width, height);
            page.gameObject.AddComponent<BattleFieldFolio>().raycastTarget = false;
            return page;
        }
        private static void PlaceReadingFooter(RectTransform panel)
        { W.Text(panel, "FullReadingHint", "全文：规则 Wiki → 实体详情 / 当前阶段规则", 18, 525, 389, 58, 20, FolioHeading).fontStyle = FontStyles.Bold; }
        private void ShowEntityDetails()
        {
            // A temporary inspection mode occupies the existing right-hand well,
            // above the phase action. Leaving the entity reveals the same selected
            // action again; the native selection is never changed by inspection.
            var panel = CreateFolio(surface, "BattleHoverInfo", 1532, 140, 352, 560);
            var book = W.Rect(panel, "FolioBookSigil", 18, 12, 25, 25).gameObject.AddComponent<BattleUtilityGlyph>();
            book.kind = BattleUtilityGlyph.Kind.Book; book.color = FolioHeading; book.raycastTarget = false;
            W.Text(panel, "FolioEyebrow", "规则 · 悬浮查阅", 54, 9, 270, 32, 21, FolioHeading).fontStyle = FontStyles.Bold;
            preview = panel.gameObject;
            var title = W.Text(panel, "Heading", heading, 18, 45, 316, 68, 24, FolioHeading);
            title.fontStyle |= FontStyles.Bold;
            title.ForceMeshUpdate();
            float titleHeight = Mathf.Max(40, title.preferredHeight + 8);
            title.rectTransform.sizeDelta = new Vector2(316, titleHeight);
            var context = W.Text(panel, "HoverContext", (selectedCardEffect ? "已选效果 · " : "已选动作 · ") + selectedContext, 18, 0, 316, 56, 22, FolioHeading);
            context.fontStyle |= FontStyles.Bold;
            context.ForceMeshUpdate();
            float contextHeight = Mathf.Max(32, context.preferredHeight + 8);
            context.rectTransform.sizeDelta = new Vector2(316, contextHeight);
            string comparison = symbolFact ? "符号释义；移开查看当前操作。" : string.IsNullOrWhiteSpace(selectedActionBody) ? "当前没有已选操作；移开查看战斗动作。" : selectedActionBody;
            var selected = W.Text(panel, "SelectedActionComparison", comparison, 18, 0, 316, 200, 22, FolioInk);
            selected.fontStyle |= FontStyles.Bold;
            selected.ForceMeshUpdate();
            float selectedHeight = Mathf.Max(selected.preferredHeight, selected.GetRenderedValues(false).y) + 12;
            float comparisonBudget = Mathf.Min(selectedCardEffect ? 240 : 190, 560 - (45 + titleHeight + 8) - 160 - 56 - 8 - contextHeight - 8 - 12);
            // Never silently truncate a long set of options or shrink type. Those
            // contexts retain the full original action list on pointer exit.
            if (selectedHeight > comparisonBudget)
            {
                selected.text = "已选有 " + selectedActionCount + " 项操作。\n移开后查看全部操作与费用。";
                selected.ForceMeshUpdate();
                selectedHeight = Mathf.Max(selected.preferredHeight, selected.GetRenderedValues(false).y) + 12;
            }
            if (symbolFact)
            {
                // The inspected fact/rule already names the entity and carries
                // its exact current state. Do not repeat that in a second prose
                // footer at the expense of the actual rule-reading viewport.
                context.gameObject.SetActive(false); selected.gameObject.SetActive(false);
                contextHeight = 0; selectedHeight = 0;
            }
            selected.rectTransform.sizeDelta = new Vector2(316, selectedHeight);
            float viewportTop = 45 + titleHeight + 12;
            float viewportHeight = symbolFact ? 560 - viewportTop - 72 : 560 - viewportTop - 56 - 8 - contextHeight - 8 - selectedHeight - 12;
            var viewport = W.Rect(panel, "EntityHoverViewport", 18, viewportTop, 316, viewportHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = W.Text(viewport, "Body", body, 0, 0, 316, viewportHeight, Mathf.Max(cardRuleDetail ? 22 : 21, bodyFontSize), FolioInk);
            text.fontStyle |= FontStyles.Bold;
            text.margin = new Vector4(4, 4, 4, 4);
            text.overflowMode = TextOverflowModes.Overflow;
            // Measure after applying the real text margin and generating wrapped
            // glyphs. Measuring an unpadded width underestimated long content and
            // left the final lines outside ScrollRect's reachable content bounds.
            text.ForceMeshUpdate();
            float textHeight = Mathf.Max(text.preferredHeight, text.GetRenderedValues(false).y) + 24;
            if (compactRule)
            {
                float leafHeight = Mathf.Clamp(viewportTop + textHeight + 72, 270, 560);
                panel.sizeDelta = new Vector2(352, leafHeight);
                viewportHeight = leafHeight - viewportTop - 72;
                viewport.sizeDelta = new Vector2(316, viewportHeight);
            }
            float fullHeight = Mathf.Max(viewportHeight, textHeight);
            text.rectTransform.sizeDelta = new Vector2(316, fullHeight);
            _entityScroll = panel.gameObject.AddComponent<ScrollRect>();
            _entityScroll.viewport = viewport; _entityScroll.content = text.rectTransform;
            _entityScroll.horizontal = false; _entityScroll.vertical = true;
            _entityScroll.movementType = ScrollRect.MovementType.Clamped;
            _entityScroll.inertia = false; _entityScroll.scrollSensitivity = 32;
            float progressTop = viewportTop + viewportHeight + 4;
            var progress = W.Text(panel, "EntityReadingProgress", "", 18, progressTop, 316, 56, 21, FolioHeading);
            progress.fontStyle |= FontStyles.Bold;
            context.rectTransform.anchoredPosition = new Vector2(18, -(progressTop + 56 + 4));
            selected.rectTransform.anchoredPosition = new Vector2(18, -(progressTop + 56 + 4 + contextHeight + 8));
            W.Panel(panel, "ComparisonDivider", 18, progressTop + 56 + 1, 316, 1, new Color32(121, 93, 49, 140));
            void UpdateProgress(Vector2 value) => progress.text = fullHeight > viewportHeight
                ? "在角色上滚轮阅读 · " + Mathf.RoundToInt((1 - value.y) * 100) + "%\n移开返回已选项"
                : "全文已显示 · 移开返回已选项";
            _entityScroll.onValueChanged.AddListener(UpdateProgress);
            _entityScroll.verticalNormalizedPosition = 1;
            UpdateProgress(new Vector2(0, 1));
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }
        public void OnScroll(PointerEventData data)
        { if (_entityScroll != null && preview != null) _entityScroll.OnScroll(data); }
        public void OnPointerExit(PointerEventData data) => Hide();
        private void OnDisable() => Hide();
        private void Hide() { if (preview != null) { preview.SetActive(false); Destroy(preview); } preview = null; _entityScroll = null; }
    }
}
