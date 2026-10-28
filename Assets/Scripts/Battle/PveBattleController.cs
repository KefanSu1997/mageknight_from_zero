using System;
using System.Collections.Generic;
using System.Linq;
using MageKnight.Adventure.Content;
using MageKnight.SceneAutomation;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    /// <summary>Card/target UI only. All mutations are commands to the reusable formal battle session.</summary>
    public sealed partial class PveBattleController : MonoBehaviour, ISceneAutomationStateSource
    {
        public BattleLoadout[] loadouts;
        public BattleEncounter[] encounters;
        public BattleUnitDefinition[] unitCatalog = Array.Empty<BattleUnitDefinition>();
        private BattleUnitDefinition[] _activeUnits;
        private List<BattleUnitDefinition> _draftUnits;
        private PveBattleSession _battle;
        private int _loadout, _encounter, _draftLoadout, _draftEncounter, _attackSlot, _hornBonus;
        private readonly HashSet<int> _targets = new();
        private DeedCard _selected;
        private UnitState _selectedUnit;
        private bool _showUnits;
        private bool _showSkills;
        private SkillCard _selectedSkill;
        private int _skillOption, _skillPage;
        private bool _artifactBurn, _selectMaterials, _shieldSplit;
        private int _artifactUnit;
        private int _supportTarget = -1;
        private int _unitMode, _unitBoost, _unitConversion;
        private ManaColor _unitManaFirst = ManaColor.Red, _unitManaSecond = ManaColor.Blue;
        private bool _unitUtilityPanel;
        private bool _bowPanel;
        private int _bowContribution;
        private readonly List<DeedCard> _materials = new();
        private string _choice;
        private string _feedback;
        private bool _inputRejected;
        private int _inputSequence;
        private CanvasGroup _inputPulse;
        private float _inputAt = -10;
        // Presentation only: never delays or performs a rule command.
        private float _resolutionAt = -10;
        private string _resolutionText;
        private string _lastResolutionText, _lastResolutionHeading;
        private string _lastActionFeedback;
        private PveBattleSession _resolutionHistoryBattle;
        private bool _showResolutionRecall;
        private bool _resolutionAttack;
        private readonly HashSet<int> _impactTargets = new();
        private readonly List<CanvasGroup> _impactGraphics = new();
        private CanvasGroup _resolutionReceipt;
        private PveBattleSession _phasePresentationBattle;
        private Phase _presentedPhase;
        private float _phaseChangedAt = -10;
        private string _phaseTransitionText;
        private CanvasGroup _phaseTransitionCue;
        private CanvasGroup _phaseHighlight;
        private readonly Dictionary<string, Texture2D> _enemyIllustrations = new();
        private Texture2D _informationGrain;
        private Sprite _informationSurface;
        private bool _camp, _rules, _recovery;
        private int _campPicker, _pickerPage;
        private const int PickerPageSize = 18;
        private bool _grailFirst = true;
        private int _rejections;
        private bool _bannerMode;
        private RectTransform _root;
        private Canvas _canvas;
        private bool _previousBackground;
        private TextMeshProUGUI _status, _pools, _phaseLabel;
        public string LastRule => _feedback ?? _battle?.LastEvent ?? "";
        private BattleLoadout Loadout => loadouts[_loadout];
        private BattleEncounter Encounter => _siteEncounter??encounters[_encounter];
        private OriginalEnemyView EnemyView(string originalId)
        {
            var view = Encounter.enemies.Concat(worldEnemyCatalog).FirstOrDefault(e => e.originalId == originalId);
            if (view == null || view.originalToken == null)
                throw new InvalidOperationException("缺少实际原敌人素材：" + originalId);
            return view;
        }
        private BattleDeedDefinition Binding(DeedCard card) => deedCatalog.FirstOrDefault(b => b.id == card.Id);
        private BattleArtifactDefinition ArtifactBinding(DeedCard card) => artifactCatalog.FirstOrDefault(b => b.source.Id == card.Id);
        private string CardName(DeedCard card) => Binding(card)?.source.NameCn ?? ArtifactBinding(card)?.source.NameCn ?? "创伤";

        private void Start()
        {
            // The Editor process directory may be the Unity install directory. Formal
            // effects load original rule data relative to the project, never that cwd.
            CardJsonLoader.SetBaseDirectory(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../resources/text_json")));
            _previousBackground = Application.runInBackground;
            if (SceneAutomationRuntimeState.PendingRequest != null) Application.runInBackground = true;
            _canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Begin();
            if (SceneAutomationRuntimeState.PendingRequest == null)
            {
                OpenCamp(); _campPicker = 5; Draw();
            }
        }

        private void OnDestroy()
        {
            Application.runInBackground = _previousBackground;
            if (_informationSurface != null) Destroy(_informationSurface);
            if (_informationGrain != null) Destroy(_informationGrain);
            foreach (var material in _characterAtmospheres.Values) if (material != null) Destroy(material);
            _characterAtmospheres.Clear();
            foreach (var material in _projectedShadowMaterials.Values) if (material != null) Destroy(material);
            _projectedShadowMaterials.Clear();
        }

        private void Begin()
        {
            ClearAttackSelection(); _handPage = 0;
            ClearManaPayment();
            ClearSiteEncounter();
            _originalStartHero=null;
            _journey = null; _journeyTools = false;
            _fluentSourceOpen = _fluentDiscardMode = false;
            _battleSourceOpen=false;_battleSourcePage=0;
            _loadout = _draftLoadout; _encounter = _draftEncounter;
            var originals = OriginalDeeds();
            var selectedUnits = _draftUnits?.ToArray() ?? Loadout.units ?? Array.Empty<BattleUnitDefinition>();
            var player = CreateConfiguredPlayer(selectedUnits);
            _activeUnits = selectedUnits;
            _battle = new PveBattleSession(player, Encounter.LoadEnemies(), originals,
                context: new ActionContext { DayPart = Encounter.dayPart, EnemySupply = CreateWorldSupply(), EffectRandom = Loadout.replayEffectRandom ? new System.Random(Loadout.effectRandomSeed) : null },
                artifacts: artifactCatalog.Select(a => a.Snapshot()), spells: OriginalSpells(), enemyRewardMode: EnemyRewardMode.TezlaJustFame);
            _targets.Clear(); _selected = null; _choice = null; _feedback = null; _camp = false; _attackSlot = 0;
            _lastResolutionText = null; _lastResolutionHeading = null; _showResolutionRecall = false;
            _lastActionFeedback = null;
            _selectedUnit = null; _showUnits = false; _bowPanel = false; _bowContribution = 0;
            _unitMode = _unitBoost = _unitConversion = 0; _unitManaFirst = ManaColor.Red; _unitManaSecond = ManaColor.Blue; _unitUtilityPanel = false;
            _selectedSkill = null; _showSkills = false; _skillOption = 0; _skillPage = 0;
            _artifactBurn = false; _hornBonus = 0; _supportTarget = -1; _recovery = false; _grailFirst = true; _selectMaterials = false; _artifactUnit = 0; _materials.Clear();
            Draw();
        }
        private static ActionCardData Original(ActionCardDefinition binding)
        {
            var s = binding.source;
            return new ActionCardData(s.Id, s.NameCn, s.Set, s.ImagePath, s.EnImagePath,
                s.BaseEffect, s.EnhancedEffect, s.RequiredCrystals);
        }
        private void Act(Action action, string inputLabel, string controlPath)
        {
            EnsureResolutionPresentationSession();
            var session = _battle;
            bool combat = session != null && (session.Phase == Phase.Ranged || session.Phase == Phase.Block || session.Phase == Phase.Melee);
            var enemies = combat ? session.Enemies.ToArray() : Array.Empty<Monster>();
            var states = combat ? Enumerable.Range(0, enemies.Length).Select(EnemyResolutionState).ToArray() : Array.Empty<string>();
            var hand = combat ? session.Player.Deck.Hand.ToArray() : Array.Empty<DeedCard>();
            var attacks = combat ? session.Context.CombatPower.Attacks.ToArray() : Array.Empty<AttackProfile>();
            var targets = _targets.ToArray();
            var phase = session?.Phase;
            string previousEvent = session?.LastEvent;
            string journalBefore = CombatJournalState();
            _inputRejected = false;
            _showResolutionRecall = false;
            try
            {
                _feedback = null; action();
                if (!ReferenceEquals(session, _battle) || phase != _battle?.Phase)
                {
                    _resolutionAt = -10; _resolutionText = null; _impactTargets.Clear();
                    if (!ReferenceEquals(session, _battle))
                    { _lastResolutionText = null; _lastResolutionHeading = null; _lastActionFeedback = null; }
                }
                else if (combat && enemies.Length == _battle.Enemies.Count
                    && enemies.Select((e, i) => ReferenceEquals(e, _battle.Enemies[i])).All(same => same))
                {
                    var changed = Enumerable.Range(0, enemies.Length).Where(i => states[i] != EnemyResolutionState(i)).ToArray();
                    var used = hand.Where(c => !_battle.Player.Deck.Hand.Any(h => ReferenceEquals(c, h))).ToArray();
                    bool attackSpent = _battle.LastEvent.StartsWith("攻击结算：", StringComparison.Ordinal)
                        && attacks.Any(a => !_battle.Context.CombatPower.Attacks.Any(p => ReferenceEquals(a, p)));
                    if (changed.Length > 0 || used.Length > 0 || attackSpent)
                    {
                        _impactTargets.Clear();
                        foreach (int i in changed) _impactTargets.Add(i);
                        if (attackSpent) foreach (int i in targets.Where(i => i >= 0 && i < enemies.Length)) _impactTargets.Add(i);
                        _resolutionAttack = attackSpent;
                        _resolutionAt = Time.unscaledTime;
                        // Pool-building cards are not hits. Flash only changed enemies or a resolved attack.
                        _resolutionText = _impactTargets.Count > 0
                            ? _battle.LastEvent + " · " + string.Join(" / ", _impactTargets.OrderBy(i => i).Select(i => "#" + (i + 1) + " " + EnemyView(enemies[i].Id).displayName))
                            : string.Join(" / ", used.Select(CardName)) + " · " + _battle.LastEvent;
                        // The physical card may have left the hand. Retain the complete
                        // formal result separately from the short-lived visual receipt.
                        _lastResolutionText = _resolutionText + (string.IsNullOrEmpty(_lastResolutionText) ? "" : "\n\n—— 较早结算（历史记录）——\n" + _lastResolutionText);
                        _lastResolutionHeading = used.Length > 0 ? string.Join(" / ", used.Select(CardName)) + " · 已结算" : "最近结算";
                        _showResolutionRecall = false;
                        _resolutionText = "效果已结算 · 战斗日志保留完整结果与适用条件";
                    }
                }
            }
            catch (InvalidOperationException e) { _feedback = e.Message; _rejections++; _inputRejected = true; }
            // A selection is an input, not a successful rule resolution. Only show
            // a new formal event when the session actually produced one.
            if (_feedback == null)
            {
                string result = !ReferenceEquals(session, _battle) || previousEvent != _battle?.LastEvent
                    ? _battle?.LastEvent : InputSelectionState();
                if (controlPath == "Btn_HandPage")
                {
                    inputLabel = "手牌翻页";
                    int count = _battle.Player.Deck.Hand.Count;
                    result = $"当前显示手牌 {_handPage + 1}/{Math.Max(1, (count + HandPageSize - 1) / HandPageSize)} 页。";
                }
                _feedback = controlPath == "Btn_HandPage" ? result : inputLabel + "\n" + result;
            }
            _feedback = "操作 " + (++_inputSequence) + (_inputRejected ? " · 未完成\n" : " · 已响应\n") + _feedback;
            if (!controlPath.EndsWith("Btn_ResolutionRecall", StringComparison.Ordinal)) _lastActionFeedback = _feedback;
            AppendCombatJournal(inputLabel, journalBefore);
            Draw();
            PulseInput(controlPath);
        }
        private string InputSelectionState()
        {
            string selection = _selected != null ? "手牌：" + CardName(_selected)
                : _selectedUnit != null ? "部队：" + _selectedUnit.Card.NameCn
                : _selectedSkill != null ? "技能：" + _selectedSkill.Id : "未选手牌 / 部队 / 技能";
            string targets = _targets.Count == 0 ? "未选敌人" : "目标：" + string.Join("、", _targets.OrderBy(i => i).Select(i => "#" + (i + 1)));
            return selection + " · " + targets;
        }
        private string ControlPath(Transform control)
        {
            var names = new List<string>();
            for (var t = control; t != null && t != _root; t = t.parent) names.Add(t.name);
            names.Reverse(); return string.Join("/", names);
        }
        private void PulseInput(string controlPath)
        {
            _inputAt = Time.unscaledTime;
            // Follow the rebuilt control (including a raised hand card), rather
            // than drawing a flash over its old screen position or the artwork.
            var target = _root.Find(controlPath) as RectTransform;
            if (target == null) target = _root.GetComponentsInChildren<TextMeshProUGUI>()
                .LastOrDefault(t => t.name.EndsWith("Feedback", StringComparison.Ordinal))?.rectTransform;
            if (target == null) return;
            if (embeddedBattleUi && target.GetComponent<Button>() is Button rebuiltButton && !rebuiltButton.interactable) return;
            var r = W.Rect(target, "InputResponse", 0, 0, target.rect.width, target.rect.height);
            _inputPulse = r.gameObject.AddComponent<CanvasGroup>();
            _inputPulse.blocksRaycasts = false; _inputPulse.interactable = false;
            Color color = _inputRejected ? new Color32(255, 139, 112, 255) : new Color32(123, 232, 195, 255);
            float w = target.rect.width, h = target.rect.height;
            if (embeddedBattleUi && (target.name == "Btn_HeroActor" || target.name.StartsWith("Btn_UnitActor_", StringComparison.Ordinal) || target.name == "Btn_Target"))
            {
                // Reuse the actual actor's ground marker. Enemy artwork and hit
                // regions have different heights; an old fixed Y crosses its body.
                var ground = target.Find("ActorHoverFocus") as RectTransform;
                float groundX = ground != null ? ground.anchoredPosition.x : w * .08f;
                float groundY = ground != null ? -ground.anchoredPosition.y : h - 12;
                float groundWidth = ground != null ? ground.rect.width : w * .84f;
                float groundHeight = ground != null ? ground.rect.height : 18;
                var ring = W.Rect(r, "ContactResponse", groundX, groundY, groundWidth, groundHeight).gameObject.AddComponent<BattleRoundGraphic>();
                ring.color = color; ring.hollow = true; ring.raycastTarget = false;
                return;
            }
            W.Panel(r, "Top", 0, 0, w, 3, color); W.Panel(r, "Bottom", 0, h - 3, w, 3, color);
            W.Panel(r, "Left", 0, 0, 3, h, color); W.Panel(r, "Right", w - 3, 0, 3, h, color);
            foreach (var text in _root.GetComponentsInChildren<TextMeshProUGUI>())
                if (text.name.EndsWith("Feedback", StringComparison.Ordinal)) text.color = color;
        }
        private string EnemyResolutionState(int index)
        {
            var enemy = _battle.Enemies[index];
            return _battle.Context.DefeatedEnemyIndices.Contains(index) + ":" + CombatMath.EffectiveArmor(enemy, index, _battle.Context)
                + ":" + Abilities(enemy, index) + ":" + string.Join("/", Enumerable.Range(0, enemy.AttackCount).Select(a =>
                    CardCombatActions.DefenseProfile(enemy, index, _battle.Context, a).Attack + ":" + _battle.IsBlocked(index, a)
                    + ":" + _battle.Context.IsAttackCancelled(index, a) + ":" + _battle.Context.ColdStareSkips(enemy)));
        }
        private void Update()
        {
            UpdateEmbeddedFeedback();
            UpdatePhaseCue();
            if (_inputPulse != null) _inputPulse.alpha = Mathf.Clamp01((1.2f - (Time.unscaledTime - _inputAt)) / .5f);
            float age = Time.unscaledTime - _resolutionAt;
            foreach (var graphic in _impactGraphics)
                if (graphic != null) graphic.alpha = Mathf.Clamp01(1 - age / .7f);
            if (_resolutionReceipt != null)
                _resolutionReceipt.alpha = Mathf.Clamp01((3.5f - age) / .5f);
        }
        private Button Button(Transform parent, string id, string label, float x, float y, float w, float h, Action click, bool primary = false)
        {
            var button = W.Button(parent, id, label, x, y, w, h, primary);
            StyleCombatCaption(button, id, w, h);
            if (_battle != null && !string.IsNullOrEmpty(label))
            {
                // Solid, tactile controls sit above the translucent forest surfaces.
                // Empty-label hand/target buttons keep their existing artwork treatment.
                bool emphasized = primary || id == "Btn_Next";
                button.targetGraphic.color = emphasized ? W.Gold : new Color32(37, 59, 51, 255);
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.18f, 1.18f, 1.12f, 1);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(.72f, .78f, .74f, 1);
                colors.disabledColor = new Color(.58f, .62f, .60f, 1);
                colors.fadeDuration = .08f;
                button.colors = colors;
                var caption = button.GetComponentInChildren<TextMeshProUGUI>();
                caption.color = emphasized ? new Color32(20, 35, 29, 255) : W.Cream;
                W.Panel(button.transform, "ControlTopEdge", 1, 1, w - 2, 2,
                    emphasized ? new Color32(242, 216, 167, 255) : new Color32(138, 156, 130, 255));
                W.Panel(button.transform, "ControlBottomEdge", 1, h - 3, w - 2, 2,
                    new Color32(19, 33, 28, 255));
            }
            string path = ControlPath(button.transform);
            string inputLabel = string.IsNullOrEmpty(label) ? (id.StartsWith("Btn_Card_", StringComparison.Ordinal) ? "手牌选择" : id == "Btn_Target" ? "敌人选择" : "选择输入") : label;
            button.onClick.AddListener(() => Act(click, inputLabel, ControlPath(button.transform)));
            // Unity Button silently ignores disabled controls. Keep their rule
            // gate intact, but acknowledge a real pointer click without executing
            // their action. Use the explicit flag so a redraw cannot double-fire.
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                if (button.interactable || ((PointerEventData)data).button != PointerEventData.InputButton.Left) return;
                _inputRejected = true; _rejections++;
                _feedback = "操作 " + (++_inputSequence) + " · 未执行\n" + inputLabel + "\n此控件当前不可用；未调用规则操作。";
                AppendCombatJournal(inputLabel, CombatJournalState());
                string actualPath = ControlPath(button.transform);
                Draw(); PulseInput(actualPath);
            });
            trigger.triggers.Add(entry);
            return button;
        }
        private static void Texture(Transform parent, string id, Texture2D texture, float x, float y, float w, float h, Color? tint = null)
        {
            if (texture != null && texture.name.StartsWith("hero_skill_pool_", StringComparison.Ordinal))
            {
                float fit = Mathf.Min(w / texture.width, h / texture.height);
                float iw = texture.width * fit, ih = texture.height * fit;
                x += (w - iw) / 2; y += (h - ih) / 2; w = iw; h = ih;
            }
            var image = W.Rect(parent, id, x, y, w, h).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false; image.color = tint ?? Color.white;
        }
        private RectTransform InformationSurface(Transform parent, string id, float x, float y, float w, float h, Color tint, bool border = false)
        {
            if (_informationSurface == null)
            {
                // A small, reusable linen grain, independent of gameplay RNG. Tile at
                // native UI scale so large panels never stretch the fine surface detail.
                const int size = 128;
                _informationGrain = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Battle information linen", wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                for (int row = 0; row < size; row++)
                    for (int column = 0; column < size; column++)
                    {
                        uint hash = (uint)(column + row * size + 1);
                        hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15;
                        float grain = (hash & 255) / 255f;
                        float fiber = .5f + .5f * Mathf.Sin(row * Mathf.PI / 4 + Mathf.Sin(column * Mathf.PI / 32));
                        byte value = (byte)Mathf.RoundToInt(255 * (.90f + .065f * grain + .035f * fiber));
                        pixels[row * size + column] = new Color32(value, value, value, 255);
                    }
                _informationGrain.SetPixels32(pixels);
                _informationGrain.Apply(false, true);
                _informationSurface = Sprite.Create(_informationGrain, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                _informationSurface.hideFlags = HideFlags.HideAndDontSave;
            }
            var panel = W.Panel(parent, id, x, y, w, h, tint, border);
            var image = panel.GetComponent<Image>();
            image.sprite = _informationSurface; image.type = Image.Type.Tiled;
            return panel;
        }
        private void Draw()
        {
            EnsureResolutionPresentationSession();
            // Resolve the legacy rule-menu request before laying out legal actions.
            if (_rules) { _rules = false; _combatWikiOpen = true; _combatWikiTopic = 5; }
            _impactGraphics.Clear(); _resolutionReceipt = null; _inputPulse = null;
            _phaseTransitionCue = null; _phaseHighlight = null;
            RememberDemoPhase();
            PrepareHeadbandInput();
            if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); }
            _root = W.Rect(_canvas.transform, "BattleTable", 0, 0, 1920, 1080);
            if(_siteEndOpen){DrawSiteEndChoice();DrawCombatKnowledgeLayer(false);return;}
            if(_journey?.Game.IsEndingTurn==true){DrawLevelReward();DrawCombatKnowledgeLayer(false);return;}
            if (_journey != null && (!_journey.Game.HasActiveTurn || _battle == null)) { DrawJourneyMenu(); DrawCombatKnowledgeLayer(false); return; }
            Texture(_root, "Environment", Encounter.background, 0, 0, 1920, 1080);
            W.Panel(_root, "Shade", 0, 0, 1920, 1080, new Color(0.025f, .07f, .065f, .28f));
            InformationSurface(_root, "Header", 0, 0, 1920, 188, new Color32(39, 58, 49, 230));
            W.Text(_root, "Brand", (_journey == null ? "MAGE KNIGHT  /  战斗演练" : "MAGE KNIGHT  /  连续旅程") + " · 阵营敌额外名望+1", 38, 18, 750, 28, 19, W.Gold);
            W.Text(_root, "Title", Encounter.displayName, 38, 55, 750, 60, 36, W.Cream);
            // Keep hero resources in their own column, clear of the two tool buttons.
            InformationSurface(_root, "HeroStatusPlate", 808, 14, 602, 108, new Color32(43, 64, 54, 248));
            _status = W.Text(_root, "Status", StatusText(), 825, 23, 570, 94, 21, W.Cream);
            DrawDemoControls();
            DrawPhaseIndicator();
            if (_journey == null) W.Text(_root, "Mode", (Encounter.dayPart == DayPart.Day ? "白昼" : "黑夜") + " · 演练快照 · 非标准牌库" + (Loadout.replayEffectRandom ? " · 种子" + Loadout.effectRandomSeed : ""), 1430, 138, 450, 30, 17, W.Muted);
            if(_journey?.Context.OfferedUnitUses>0)
                Button(_root,"Btn_OfferedUnits","战斗号召 · 选择供应能力",38,192,450,45,OpenOfferedUnits);
            if(_journey?.Context.SpellColors.Count>0)
                Button(_root,"Btn_OfferedSpells","魔法天赋 · 供应施法",500,192,450,45,()=>_offeredSpellsOpen=true);
            // Quiet teal for the hero and hand; warm, isolated stage for enemy targets.
            InformationSurface(_root, "EnemyStage", 454, 206, 902, 433, new Color32(61, 49, 38, 142));
            InformationSurface(_root, "HandSurface", 36, 713, 1320, 367, new Color32(44, 64, 58, 190), true);
            InformationSurface(_root, "HandToolbar", 37, 714, 1318, 43, new Color32(47, 66, 58, 245));
            DrawStage();
            InformationSurface(_root, "PowerPlate", 36, 647, 1320, 58, new Color32(42, 61, 54, 250), true);
            _pools = W.Text(_root, "Pools", PoolText(), 57, 661, 1285, 33, 22, W.Cream);
            if (_showSkills) DrawSkills(); else if (_showUnits) DrawUnits(); else DrawHand();
            if (_battle.Player.Skills.Count > 0)
                Button(_root, "Btn_Skills", _showSkills ? "返回手牌" : "英雄技能", 949, 713, 198, 43,
                    () => { _fluentDiscardMode=false; _showSkills = !_showSkills; _showUnits = false; _selectedSkill = null; _selected = null; _selectedUnit = null; _selectMaterials = false; });
            if (_battle.Player.Units.Count>0) Button(_root, "Btn_Roster", _showUnits ? "查看手牌" : "部队 " + _battle.Player.Units.Count, 1156, 713, 198, 43,
                () => { _fluentDiscardMode=false; _showUnits = !_showUnits; _showSkills = false; _selectedSkill = null; _selected = null; _selectedUnit = null; _choice = null; _selectMaterials = false; });
            if (_battle.Phase == Phase.End && (_recovery || _battle.Context.Healing.Total > 0 || _battle.Context.DrawPerHeal > 0))
                Button(_root, "Btn_Recovery", _recovery ? "返回战果" : "战后治疗 " + _battle.Context.Healing.Total, 738, 713, 198, 43,
                    () => { _recovery = !_recovery; _selected = null; _selectedUnit = null; _selectedSkill = null; });
            if (_battle.Context.StarBowActive && (_battle.Phase == Phase.Ranged || _battle.Phase == Phase.Melee) && BowContributions().Length > 0)
                Button(_root, "Btn_BowChoices", _bowPanel ? "返回行动" : "星辰之弓选择", 738, 713, 198, 43,
                    () => { _bowPanel = !_bowPanel; _selected = null; _selectedUnit = null; _selectedSkill = null; _targets.Clear(); });
            DrawInspector();
            if (embeddedBattleUi) ComposeEmbeddedBattle();
            DrawFluentSource(true);
            if (_journey != null && !_journey.Context.InBattle)
                Button(_root,"Btn_JourneyHand","手牌与恢复",490,713,236,43,OpenJourneyHand);
            if (_camp) DrawCamp();
            if (_journeyTools) DrawJourneyTools();
            if (_journeyHand) DrawJourneyHand();
            if (_journeyMap) DrawJourneyMap();
            if (_materialArtifact!=null) DrawMaterialArtifact();
            if(_journey?.Context?.MysticBox is {Used:false} box)Button(_root,"Btn_PendingBox","继续处理已揭示的神秘宝盒",38,192,450,45,()=>OpenSpecialArtifact(box.Box));
            if(_specialCard!=null)DrawSpecialArtifact();
            if(_copyHost!=null)DrawCopyPicker();
            if(_offeredSpellsOpen)DrawOfferedSpells();
            if(_siteRewardOpen)DrawSiteReward();
            if(_inputCard!=null)DrawDeedInputs();
            DrawPendingCopy();
            if(_learningOpen)DrawLearningSupply();
            if(_conversionsOpen)DrawConversions();
            if(_offeredUnitsOpen)DrawOfferedUnits();
            if(_ownedUnitsOpen)DrawOwnedUnits();
            if(_skillWoundPicker && _selectedSkill!=null)DrawSkillWoundPicker();
            if(_sidewaysSkill!=null)DrawSidewaysSkill();
            if(_shapeSkill!=null)DrawShapeshift();
            if(_contributionSkill!=null)DrawContributionSkill();
            DrawHeadbandTargetInput();
            DrawHeadbandSocialInput();
            DrawHeadbandFlipInput();
            DrawHeadbandDuelInput();
            if(_enemyTokenSkill!=null)DrawEnemyTokenSkill();
            if(_leadershipSkill!=null)
            {
                bool borrowed=IsHeadbandInput(_leadershipSkill);
                bool pending=borrowed && _journey.Context.PendingHeadbandBorrow!=null;
                string label=borrowed?"头环 · 领导第"+((_journey.Context.PendingHeadbandBorrow?.Completed??0)+1)+"次 · 选择部队原能力"+(pending?" · 无合法输入时点击结束":" · 点击取消")
                    :"领导已选 · 选择部队原攻击或格挡能力（点击取消）";
                Button(_root,"Btn_CancelLeadership",label,38,192,1310,45,()=>
                {
                    if(pending){_journey.FinishHeadbandBorrowIfImpossible();AfterHeadbandInput();}
                    else if(borrowed)ClearHeadbandInput();else _leadershipSkill=null;
                });
            }
            if(_manaPaymentOpen)DrawManaPayment();
            DrawHeadbandPaymentStatus();
            DrawSharedSkillsIfNeeded();
            if (_enemyDefendOpen) DrawEnemyDefend();
            if (_attackSelectionOpen) DrawAttackSelection();
            if (_blockSelectionOpen) DrawBlockSelection();
            if (_battleMenuOpen) DrawBattleMenu();
            DrawCombatKnowledgeLayer();
        }
        private string StatusText() => $"护甲 {_battle.Player.Armor}    名望 {_battle.Player.Fame}    创伤 {_battle.Player.Wounds}\n" +
            $"晶体   红 {Crystal(ManaColor.Red)}   蓝 {Crystal(ManaColor.Blue)}   白 {Crystal(ManaColor.White)}   绿 {Crystal(ManaColor.Green)}\n" +
            $"魔力   红 {ManaDisplay(ManaColor.Red)}   蓝 {ManaDisplay(ManaColor.Blue)}   白 {ManaDisplay(ManaColor.White)}   绿 {ManaDisplay(ManaColor.Green)}   金 {ManaDisplay(ManaColor.Gold)}" +
            (Token(ManaColor.Black) > 0 || _battle.Context.InfiniteMana.Contains(ManaColor.Black) ? $"   黑 {ManaDisplay(ManaColor.Black)}" : "");
        private string PoolText() => $"战斗资源    远程 {_battle.AttackTotal(AttackType.Ranged)}    攻城 {_battle.AttackTotal(AttackType.Siege)}    格挡 {_battle.Context.BlockPool}    近战 {_battle.AttackTotal(AttackType.Melee)}" +
            (_battle.Player.Units.Any(u => u.Card.DamageInfluenceCost > 0 || u.Card.AssaultActivationInfluenceCost > 0 || u.Card.Actions.Any(a => a.Kind == UnitAbilityKind.Influence)) ? $"    影响力 {_battle.Context.InfluencePool}    声誉 {_battle.Player.Reputation}" : "") +
            (_battle.Context.AllAttacksColdFire ? "  · 全部冰火" : "") + (_battle.Context.AllAttacksSiege ? "  · 全部攻城" : "");
        private int Token(ManaColor c) => _battle.Player.Mana.Tokens.GetValueOrDefault(c);
        private string SkillUsage(SkillCard skill) => _battle.Player.FlippedSkills.Contains(skill.SourceId)
            ? (skill.SourceId=="hero_skill_pool_003:02"?"已翻面 · 休息后恢复":"已翻面 · 使用回合行动恢复") : SkillActions.IsUsed(_battle.Player, skill)
            ? (skill.Frequency == SkillFrequency.OncePerRound ? "本轮已翻面使用" : "本回合已使用") : "已学技能 · 尚未使用";
        private int Crystal(ManaColor c) => _battle.Player.Mana.Crystals.GetValueOrDefault(c);
        private void DrawPhaseIndicator()
        {
            // Observe the actual session, including restores and formal skipped phases.
            // New sessions establish a baseline; redraws never restart the animation.
            if (!ReferenceEquals(_phasePresentationBattle, _battle))
            {
                _phasePresentationBattle = _battle; _presentedPhase = _battle.Phase;
                _phaseChangedAt = -10; _phaseTransitionText = null;
            }
            else if (_presentedPhase != _battle.Phase)
            {
                _phaseTransitionText = "阶段切换：" + PhaseIndicatorName(_presentedPhase) + "  →  " + PhaseIndicatorName(_battle.Phase);
                _presentedPhase = _battle.Phase; _phaseChangedAt = Time.unscaledTime;
            }
            var track = InformationSurface(_root, "PhaseTrack", 36, 130, 1320, 56, new Color32(27, 46, 40, 245));
            W.Panel(track, "CurrentPhaseAccent", 0, 0, 5, 56, W.Gold);
            _phaseLabel = W.Text(track, "Phase", "当前阶段 · " + PhaseIndicatorName(_battle.Phase), 20, 5, 570, 46, 27, W.Gold);
            W.Text(track, "PhaseDecision", _battle.Phase == Phase.Block ? "有效格挡 / 需求" : _battle.Phase == Phase.AssignDamage ? "护甲 · 承伤" : "目标 · 费用", 620, 10, 270, 38, 20, W.Cream);
            UpdatePhaseCue();
        }
        private static string PhaseIndicatorName(Phase phase) => phase switch
        {
            Phase.Ranged => "远程 / 攻城", Phase.Block => "格挡", Phase.AssignDamage => "分配伤害",
            Phase.Melee => "近战", Phase.End => "战斗结束", _ => "战外"
        };
        private void UpdatePhaseCue()
        {
            // No timescale dependency, input shield, coroutine, or delayed rule work.
            float age = Time.unscaledTime - _phaseChangedAt;
            if (_phaseTransitionCue != null) _phaseTransitionCue.alpha = Mathf.Clamp01((2.1f - age) / .5f);
            if (_phaseHighlight != null) _phaseHighlight.alpha = Mathf.Clamp01(1 - age / .8f);
        }
        private void DrawStage()
        {
            if(!optimizedInteractionUi || !_fluentSourceOpen) DrawHeroPortrait();
            DrawCombatTacticalRail();
            int count = _battle.Enemies.Count;
            float width = Math.Min(420, 875f / count);
            for (int i = 0; i < count; i++)
            {
                int target = i; var enemy = _battle.Enemies[i]; var binding = EnemyView(enemy.Id);
                bool killed = _battle.Context.DefeatedEnemyIndices.Contains(i);
                float x = 470 + (875 - width * count) / 2 + i * width;
                bool targeted = _targets.Contains(i);
                var panel = W.Panel(_root, "Enemy_" + i, x, 226, width - 18, 405,
                    targeted ? new Color32(102, 72, 34, 255) : new Color32(54, 32, 27, 255), true);
                // One card-shaped target, with the same formal target command in both UI modes.
                var button = Button(panel, "Btn_Target", "", 0, 0, width - 18, 405, () =>
                {
                    if (killed) throw new InvalidOperationException("目标已经被击败");
                    bool fearBurn = (_journey?.ArtifactEffectId(_selected) ?? _selected?.Id) == "items_001" && _artifactBurn;
                    bool cardTarget = _selected != null && _battle.Choices(_selected).Any(c=>c.Id==_choice && c.Target!=BattleCardTarget.None);
                    bool skillTarget=BattleSkillChoice.NeedsEnemy(_selectedSkill?.SourceId);
                    bool unitTarget = _selectedUnit != null && (_selectedUnit.CanActivate || DelphanaMastersActions.CanContinue(_battle.Context, _selectedUnit));
                    if (skillTarget || cardTarget || unitTarget && _selectedUnit.BannerEffectId != "items_001" || !fearBurn && (_battle.Phase == Phase.Block || _battle.Phase == Phase.AssignDamage)) { _targets.Clear(); _targets.Add(target); _attackSlot = 0; }
                    else if (!_targets.Add(target)) _targets.Remove(target);
                    if (!fearBurn && !cardTarget) _selected = null;
                    if (!cardTarget) _choice = null;
                    if (!skillTarget) _selectedSkill = null;
                    // Retain an actionable unit for targeting; exhausted ordinary units yield to combat resolution.
                    if (_battle.Phase != Phase.AssignDamage && _selectedUnit?.BannerEffectId != "items_001" && !unitTarget) _selectedUnit = null;
                });
                DrawEnemyCardArtwork(button.transform, binding.originalId, binding.originalToken, width - 18, targeted, killed);
                W.Text(panel, "EnemyIndex", (i + 1).ToString("00"), 14, 253, 32, 25, 17, W.Gold);
                W.Text(panel, "Name", binding.displayName, 48, 247,
                    width - 79, 36, 25, W.Cream, TextAlignmentOptions.Center);
                string attacks = string.Join(" / ", Enumerable.Range(0, enemy.AttackCount).Select(a =>
                {
                    var original = enemy.GetAttack(a);
                    var effective = CardCombatActions.DefenseProfile(enemy, i, _battle.Context, a);
                    return ElementLabel(effective.AttackElement) + original.Value + (effective.Attack == original.Value ? "" : "→" + effective.Attack);
                }));
                if (enemy.SummonAttack) attacks = $"召唤棕敌 ×{enemy.SummonCount}";
                int armor = CombatMath.EffectiveArmor(enemy, i, _battle.Context);
                int factionFame = EnemyRewardRules.GetFactionFame(enemy, _battle.Context);
                string fame = $"名望 {enemy.Fame}" + (factionFame > 0 ? $"+{factionFame}" : "");
                string numbers = armor == enemy.Armor ? $"攻击 {attacks} · 护甲 {enemy.Armor}\n{fame}  {Abilities(enemy, i)}" :
                    $"攻击 {attacks}\n护甲 {armor}（原 {enemy.Armor}）\n{fame}  {Abilities(enemy, i)}";
                if (enemy.AttackCount > 1 || enemy.SummonAttack) numbers = $"攻击 {attacks}\n护甲 {armor} · {fame}\n{Abilities(enemy, i)}";
                bool abilitiesRemoved = _battle.Context.FortificationsRemoved.Contains(i)
                    || _battle.Context.ResistancesRemoved.Contains(i) || _battle.Context.FireResistRemoved.Contains(i);
                if (abilitiesRemoved)
                    numbers = $"攻击 {attacks}\n护甲 {armor}" + (armor == enemy.Armor ? "" : $"（原 {enemy.Armor}）")
                        + $" · {fame}\n{Abilities(enemy, i)}";
                if (enemy.ElusiveArmor > enemy.Armor)
                    numbers = $"攻击 {attacks}\n护甲 {armor}（{enemy.Armor}/{enemy.ElusiveArmor}） · {fame}\n{Abilities(enemy, i)}";
                bool compact = armor != enemy.Armor || enemy.AttackCount > 1 || enemy.SummonAttack || abilitiesRemoved || enemy.ElusiveArmor > enemy.Armor;
                // Use the full, separate-line readout in both presentation modes.
                {
                    string armorText = enemy.ElusiveArmor > enemy.Armor
                        ? $"护甲 {armor}（{enemy.Armor}/{enemy.ElusiveArmor}）"
                        : $"护甲 {armor}" + (armor == enemy.Armor ? "" : $"（原 {enemy.Armor}）");
                    // Keep combat values and all ability labels on separate
                    // lines. Do not reduce the existing font or token size.
                    numbers = $"攻击 {attacks}\n{armorText} · {fame}\n{Abilities(enemy, i)}";
                }
                var readout = W.Text(panel, "Numbers", numbers, 15, 278, width - 48, 94,
                    compact ? 18 : 20, W.Cream, TextAlignmentOptions.Center);
                readout.overflowMode = TextOverflowModes.Overflow;
                int cancelled = Enumerable.Range(0, enemy.AttackCount).Count(a => _battle.Context.IsAttackCancelled(i, a) || _battle.Context.ColdStareSkips(enemy));
                W.Panel(panel, "TargetRibbon", 8, 373, width - 34, 30,
                    targeted ? new Color32(126, 83, 31, 255) : new Color32(53, 32, 26, 255));
                W.Text(panel, "State", killed ? "已击败" : cancelled > 0 ? $"已取消攻击 {cancelled}/{enemy.AttackCount}" : _battle.IsBlocked(i) ? "已格挡" : targeted ? "◆ 已选目标 ◆" : "点击卡面选择目标",
                    15, 375, width - 48, 26, 18, W.Gold, TextAlignmentOptions.Center);
                if (_impactTargets.Contains(i) && Time.unscaledTime - _resolutionAt < .7f)
                {
                    var cue = W.Rect(panel, "ResolutionImpact", 14, 14, width - 46, 228);
                    var group = cue.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false; group.interactable = false;
                    group.alpha = Mathf.Clamp01(1 - (Time.unscaledTime - _resolutionAt) / .7f);
                    _impactGraphics.Add(group);
                    W.Panel(cue, "WarmFlash", 0, 0, width - 46, 228, new Color(1, .72f, .3f, .22f));
                    var slash = W.Panel(cue, "Strike", (width - 46) / 2 - 56, 125, 112, 5, W.Cream);
                    slash.localRotation = Quaternion.Euler(0, 0, 34);
                    var spark = W.Panel(cue, "Spark", (width - 46) / 2 - 28, 104, 56, 3, W.Gold);
                    spark.localRotation = Quaternion.Euler(0, 0, -46);
                    W.Text(cue, "Result", killed ? "击败" : _resolutionAttack ? "未击败" : _battle.IsBlocked(i) ? "格挡成立" : "效果生效",
                        4, 164, width - 54, 45, 28, W.Cream, TextAlignmentOptions.Center);
                }
            }
            if (!string.IsNullOrEmpty(_resolutionText) && Time.unscaledTime - _resolutionAt < 3.5f)
            {
                // Above the targets: never covers the hand, resource totals or phase instructions.
                float receiptWidth = embeddedBattleUi ? 800 : 845;
                var receipt = W.Panel(_root, "ResolutionReceipt", embeddedBattleUi ? 700 : 500,
                    embeddedBattleUi ? 166 : 190, receiptWidth, 33, W.Ink, true);
                _resolutionReceipt = receipt.gameObject.AddComponent<CanvasGroup>();
                _resolutionReceipt.blocksRaycasts = false; _resolutionReceipt.interactable = false;
                _resolutionReceipt.alpha = Mathf.Clamp01((3.5f - (Time.unscaledTime - _resolutionAt)) / .5f);
                W.Text(receipt, "Result", _resolutionText.Replace("\n", " · "), 10, 4, receiptWidth - 20, 25, 18, W.Gold);
            }
        }
        private void DrawEnemyCardArtwork(Transform parent, string originalId, Texture2D token, float width, bool targeted, bool killed)
        {
            if (embeddedBattleUi) { DrawEmbeddedEnemy(parent, originalId, token, width, targeted, killed); return; }
            Color trim = targeted ? new Color32(244, 214, 156, 255) : new Color32(165, 117, 68, 255);
            Color tint = killed ? new Color(.42f, .42f, .42f, 1) : Color.white;
            W.Panel(parent, "EnemyCardFace", 3, 3, width - 6, 399, new Color32(27, 25, 21, 255));
            W.Panel(parent, "ArtMat", 11, 11, width - 22, 234, trim);
            W.Panel(parent, "ArtBacking", 14, 14, width - 28, 228, new Color32(35, 36, 31, 255));
            if (!_enemyIllustrations.TryGetValue(originalId, out var illustration))
            {
                // Versioned generated presentation art is keyed to the formal ID.
                // Missing art retains the complete original token, never a scan crop.
                illustration = Resources.Load<Texture2D>("UI/EnemyIllustrations/r13/" + originalId);
                _enemyIllustrations[originalId] = illustration;
            }
            var image = illustration != null ? illustration : token;
            // Fit the WHOLE composition in the same art region at every card width.
            // No UV crop, scan sharpening, printed seal, or number plate covers it.
            float scale = Mathf.Min((width - 28) / image.width, 228f / image.height);
            float artWidth = image.width * scale, artHeight = image.height * scale;
            var art = W.Rect(parent, "EnemyIllustration", 14 + (width - 28 - artWidth) / 2,
                14 + (228 - artHeight) / 2, artWidth, artHeight).gameObject.AddComponent<RawImage>();
            art.texture = image;
            art.uvRect = new Rect(0, 0, 1, 1);
            art.color = tint; art.raycastTarget = false;
            // A selected target has a strong warm outline, separate from the teal hero area.
            W.Panel(parent, "EnemyLeftTrim", 0, 0, targeted ? 4 : 2, 405, trim);
            W.Panel(parent, "EnemyRightTrim", width - (targeted ? 4 : 2), 0, targeted ? 4 : 2, 405, trim);
        }
        private static string ElementLabel(Element e) => e switch { Element.Physical => "物理", Element.Fire => "火", Element.Ice => "冰", _ => "冰火" };
        private string Abilities(Monster enemy, int index) => string.Join(" · ", enemy.Abilities.Select(ability =>
        {
            string label = ability switch
            {
                Ability.Swift => "迅捷", Ability.Poison => "毒素", Ability.Brutal => "残暴",
                Ability.Fortified => "城防", Ability.FireResist => "火抗", Ability.IceResist => "冰抗",
                Ability.PhysicalResist => "物抗", Ability.ColdFireResist => "冰火抗",
                Ability.Paralyze => "麻痹", Ability.Regenerate => "再生",
                Ability.Elusive => "闪避", Ability.Defend => "防护" + enemy.DefendValue,
                Ability.MagicResist => "魔法抗性",
                Ability.Assassination => "刺杀",
                Ability.Cumbersome => "笨重", Ability.Vampirism => "吸血", Ability.IgnoreSiteFortification => "不受城防", _ => ability.ToString()
            };
            var context = _battle.Context;
            bool resistance = ability == Ability.FireResist || ability == Ability.IceResist
                || ability == Ability.PhysicalResist || ability == Ability.ColdFireResist;
            bool removed = ability == Ability.Fortified && context.FortificationsRemoved.Contains(index)
                || resistance && context.ResistancesRemoved.Contains(index)
                || ability == Ability.FireResist && context.FireResistRemoved.Contains(index);
            return label + (removed ? "已移除" : context.InsightIgnores(enemy, ability) ? "已忽略" : "");
        }));
        private void DrawHand()
        {
            var hand = _battle.Player.Deck.Hand;
            W.Text(_root, "HandLabel", $"手牌 {hand.Count}  ·  牌库 {_battle.Player.Deck.DrawPileCount}  ·  弃牌 {_battle.Player.Deck.DiscardPile.Count}", 39, 718, 680, 36, 22, W.Gold);
            var row = W.Rect(_root, "Hand", 36, 761, 1320, 287);
            _handPage = Math.Min(_handPage, Math.Max(0, (hand.Count - 1) / HandPageSize));
            float width = Math.Min(190, 1300f / Math.Max(1, Math.Min(HandPageSize, hand.Count)));
            int visibleCards = Math.Min(HandPageSize, hand.Count - _handPage * HandPageSize);
            float handInset = (1320 - visibleCards * width + 10) / 2;
            if (hand.Count > HandPageSize)
                Button(_root, "Btn_HandPage", $"手牌 {_handPage + 1}/{(hand.Count + HandPageSize - 1) / HandPageSize} 页 · 点击翻页", 36, 1036, 1320, 44,
                    () => _handPage = (_handPage + 1) % ((hand.Count + HandPageSize - 1) / HandPageSize));
            for (int i = _handPage * HandPageSize; i < Math.Min(hand.Count, (_handPage + 1) * HandPageSize); i++)
            {
                var card = hand[i]; var binding = Binding(card); var artifact = ArtifactBinding(card); int occurrence = hand.Take(i).Count(c => c.Id == card.Id);
                bool selectedCard = ReferenceEquals(card, _selected) || _materials.Any(m => ReferenceEquals(m, card)) || _fluentDiscardMode && _endTurnDiscards.Any(c=>ReferenceEquals(c,card));
                var button = Button(row, "Btn_Card_" + card.Id + "_" + occurrence, "", handInset + (i % HandPageSize) * width, selectedCard ? -10 : 0,
                    width - 10, 267, () =>
                    {
                        if(optimizedInteractionUi && _fluentDiscardMode)
                        {
                            if(card.Type==CardType.Wound)return;
                            int di=_endTurnDiscards.FindIndex(c=>ReferenceEquals(c,card));
                            if(di>=0)_endTurnDiscards.RemoveAt(di);else _endTurnDiscards.Add(card);
                            return;
                        }
                        if (_selectMaterials && _selected?.Type == CardType.Artifact)
                        {
                            if (ReferenceEquals(card, _selected) || card.Type == CardType.Wound)
                                throw new InvalidOperationException("请选择另一张非创伤手牌作为材料");
                            if (!_materials.Remove(card)) _materials.Add(card);
                            return;
                        }
                        _deedChoicePage=0; _bowPanel = false; _recovery = false; _selected = card; _selectedUnit = null; _targets.Clear(); _choice = _battle.Choices(card).FirstOrDefault(c=>!_battle.Context.NextCardEnhanced || card.Type==CardType.Spell || c.Enhanced)?.Id ?? _battle.Choices(card).FirstOrDefault()?.Id;
                        ResetArtifactInputs(); _hornBonus = 0; _supportTarget = -1; _recovery = false; _grailFirst = true; _selectMaterials = false; _artifactUnit = 0; _materials.Clear(); _shieldSplit = false;
                    }, selectedCard);
                var artwork = CardArtwork(card);
                var hover = button.gameObject.AddComponent<BattleCardHover>();
                hover.artwork = artwork; hover.surface = _root; hover.previewX = 36 + handInset + i % HandPageSize * width;
                hover.cardTitle = CardName(card); hover.printedRules = CardReadingText(card);
                hover.dockInInspector = optimizedInteractionUi;
                if (artwork != null) W.Sprite(button.transform, "OriginalArt", artwork, 5, 5, width - 20, 227);
                else W.Text(button.transform, "Wound", "创伤\n\n"+(_battle.Context.SidewaysWoundValue>0?"可横置为"+_battle.Context.SidewaysWoundValue:"不能正常打出"), 14, 39, width - 38, 170, 24, new Color32(225, 140, 140, 255), TextAlignmentOptions.Center);
                W.Text(button.transform, "Name", (_materials.Contains(card) ? "材料 · " : "") + CardName(card), 5, 234, width - 20, 32, 20, W.Cream, TextAlignmentOptions.Center);
            }
        }
        private void DrawInspector()
        {
            bool blockDecision = _battle.Phase == Phase.Block && _selected == null && _selectedUnit == null
                && _selectedSkill == null && !_bowPanel && !_recovery && !_showResolutionRecall;
            var p = InformationSurface(_root, "Inspector", 1390, 206, 492, 833, new Color32(40, 59, 52, 234), true);
            InformationSurface(p, "ActionHeadingBand", 1, 1, 490, 73, new Color32(58, 76, 65, 245));
            // Keep the forest in the surrounding gutters, not behind live reading lines.
            // These non-interactive insets preserve every content/control rectangle.
            if (!blockDecision)
            {
                InformationSurface(p, "DetailsReadingPlate", 14, 79, 464, 192, new Color32(34, 53, 46, 255));
                InformationSurface(p, "SelectionReadingPlate", 14, 278, 464, 226, new Color32(36, 55, 48, 250));
            }
            W.Panel(p, "ActionDivider", 25, 272, 442, 1, new Color32(80, 96, 88, 255));
            InformationSurface(p, "FeedbackSurface", 14, blockDecision ? 342 : 516, 464, blockDecision ? 246 : 125, new Color32(25, 43, 37, 255), true);
            W.Panel(p, "FeedbackAccent", 14, blockDecision ? 346 : 520, 3, blockDecision ? 238 : 117,
                _inputRejected ? new Color32(244, 166, 142, 255) : W.Gold);
            InformationSurface(p, "PhaseActionBand", 14, 763, 464, 63, new Color32(68, 58, 39, 245));
            if(DrawFluentDiscardInspector(p))return;
            if (_showResolutionRecall && !string.IsNullOrEmpty(_lastResolutionText))
                DrawResolutionRecall(p);
            else
            {
            bool courageUnit = _selectedUnit?.BannerEffectId == "items_003";
            bool postcombatUnit = courageUnit || _selectedUnit != null && _selectedUnit.Card.Actions.Any(a => a.Kind == UnitAbilityKind.GainChosenTokens || a.Kind == UnitAbilityKind.AllUnitResistance || a.Kind == UnitAbilityKind.GainMana || a.Kind == UnitAbilityKind.GainToken || a.Kind == UnitAbilityKind.GainCrystal || a.Kind == UnitAbilityKind.Healing || a.Kind == UnitAbilityKind.ReadyUnit);
            string heading = _bowPanel ? "星辰之弓 · 攻击选择" : _recovery ? "战后治疗" : postcombatUnit ? _selectedUnit.Card.NameCn : _selected?.Id == "items_018" || _selected?.Id == "items_003" || _selected?.Id == "items_012" ? CardName(_selected) : _selectedSkill != null ? _selectedSkill.Id : _battle.Phase == Phase.End ? (_battle.Won ? "战斗胜利" : "仍有敌人生还") : _selectedSkill != null ? _selectedSkill.Id : _selectedUnit != null ? _selectedUnit.Card.NameCn : _selected != null ? CardName(_selected) : "行动与目标";
            W.Text(p, "Heading", blockDecision ? "格挡 · 战术决策" : heading, 25, 21, 442, 49, 31, W.Gold);
            string text = _bowPanel ? "每笔远程：翻倍或转同元素攻城。\n每笔攻城：可翻倍但变同元素远程。\n各笔独立选择，效果持续本回合。\n城防仍会阻挡远程攻击。" : _recovery ? $"剩余治疗点 {_battle.Context.Healing.Total}\n圣杯计名望点 {_battle.Context.Healing.Grail}\n英雄每张创伤消耗1点；部队按等级消费。\n只治疗手牌或自有部队；治疗不重整。" : postcombatUnit ? UnitText(_selectedUnit) : _selected?.Id == "items_018" || _selected?.Id == "items_003" || _selected?.Id == "items_012" ? CardText(_selected) : _selectedSkill != null ? _selectedSkill.PrintedRule + "\n\n" + SkillUsage(_selectedSkill) : _selectedUnit != null && _battle.Phase != Phase.End ? UnitText(_selectedUnit) : _selected != null ? CardText(_selected) : _battle.Phase == Phase.End
                ? $"已击败 {_battle.Context.DefeatedEnemyIndices.Count} / {_battle.Enemies.Count}\n名望 {_battle.Player.Fame}    手牌创伤 {_battle.Player.Wounds}\n" + (_journey == null ? "奖励已结算，可更换手牌和敌群再次挑战。" : "可先处理战后恢复，再结束本回合与补牌。")
                : InputSelectionState() + "\n" + (_targets.Count > 0 ? TargetPreview() : "尚未指定本次结算目标");
            if (blockDecision) text = "";
            // Entity details stay with their actions; generic teaching is in the Wiki.
            var details = W.Text(p, "Details", text, 25, blockDecision ? 322 : 82, 441, blockDecision ? 194 : 188, blockDecision ? 36 : _selected?.Type == CardType.Artifact || _selected?.Type == CardType.Spell ? 19 : 22, W.Cream);
            details.enableAutoSizing = true; details.fontSizeMin = _selectedSkill?.SourceId == "hero_skill_pool_003:07" ? 14 : 16;
            if (blockDecision)
            {
                details.enableAutoSizing = false;
                W.Text(p, "BlockThreat", BlockThreatText(), 25, 82, 442, 60, 22, W.Cream);
                InformationSurface(p, "BlockComparisonSurface", 14, 145, 464, 91, new Color32(22, 43, 38, 255), true);
                W.Text(p, "BlockComparison", TargetPreview(), 25, 148, 442, 62, 26, W.Cream);
                W.Text(p, "BlockSources", _targets.Count == 1 ? $"本次使用 {SelectedBlockParts().Length} 笔来源 · 分配后正式消费" : "先选择敌人，再核对本次来源。", 25, 204, 442, 30, 20, W.Cream);
            }
            if (_bowPanel) DrawBowChoices(p);
            else if (_recovery)
            {
                string targetName = _supportTarget < 0 ? "英雄手牌" : _battle.Player.Units[_supportTarget].Card.NameCn;
                Button(p, "Btn_UnitSupportTarget", "目标：" + targetName + " · 点击切换", 25, 280, 442, 48,
                    () => _supportTarget = (_supportTarget + 2) % (_battle.Player.Units.Count + 1) - 1);
                if (_battle.Context.Healing.Grail > 0 && _battle.Context.Healing.Total > _battle.Context.Healing.Grail)
                    Button(p, "Btn_HealingOrder", _grailFirst ? "优先消费圣杯治疗点" : "优先消费普通治疗点", 25, 347, 442, 48,
                        () => _grailFirst = !_grailFirst);
                else W.Text(p, "HealingRule", _battle.Context.DrawPerHeal > 0 ? "治疗每张手牌创伤后从真实牌库抽牌；治疗部队不抽牌。" : _battle.Context.Healing.Grail > 0 ? "只有消费圣杯提供的治疗点才增加名望。" : "普通治疗点不增加名望；选择实际受伤目标后消费。", 25, 347, 442, 77, 21, W.Cream);
                Button(p, "Btn_HealWound", "治疗目标一张真实创伤", 25, 447, 442, 52,
                    () => _battle.HealWound(_supportTarget, _grailFirst), true);
            }
            else if (_selectedSkill != null)
            {
                var options = _battle.SkillChoices(_selectedSkill,_targets.Count==1?_targets.Single():-1,_attackSlot);
                // Dedicated inputs may expose stored tokens or pending choices
                // after an activation. Keep those entry points; only gate the
                // direct UseBattleSkill path by its native used-skill marker.
                bool opensDedicatedInput = _selectedSkill.SourceId is "hero_skill_pool_001:05" or "hero_skill_pool_003:06"
                    or "hero_skill_pool_002:06" or "hero_skill_pool_004:04" or "hero_skill_pool_001:06"
                    || NeedsSidewaysSkill(_selectedSkill);
                bool skillEntryAvailable = opensDedicatedInput || !SkillActions.IsUsed(_battle.Player,_selectedSkill);
                if(options.Length>0 && !options.Any(o=>o.Option==_skillOption))_skillOption=options[0].Option;
                bool targeted = BattleSkillChoice.NeedsEnemy(_selectedSkill.SourceId) && (_selectedSkill.SourceId!="hero_skill_pool_001:01" || _skillOption==1);
                // Three single-column choices must leave room for the target and attack slot.
                bool compactChoices = targeted && options.Length == 3;
                for (int i = 0; i < options.Length; i++)
                {
                    var choice = options[i];
                    int columns=options.Length>4?3:options.Length>3?2:1;
                    var choiceButton=Button(p, "Btn_SkillChoice_" + choice.Option, (_skillOption == choice.Option ? "● " : "") + choice.Label,
                        25+i%columns*(454/columns),280+i/columns*(compactChoices?35:51),442/columns,compactChoices?31:45, () => _skillOption = choice.Option);
                    var choiceLabel=choiceButton.GetComponentInChildren<TextMeshProUGUI>();
                    choiceButton.interactable=skillEntryAvailable;
                    choiceLabel.enableAutoSizing=true;choiceLabel.fontSizeMin=14;choiceLabel.fontSizeMax=21;
                }
                if (options.Length == 0) W.Text(p, "SkillTiming", IsPaymentSkill(_selectedSkill)?"选择原牌或部队，再从本次费用入口选择此技能。":"当前阶段不能使用此技能。", 25, 282, 442, 90, 22, W.Muted);
                bool woundSkill=NeedsSkillWound(_selectedSkill);
                if(_selectedSkill.SourceId=="hero_skill_pool_003:00" && _battle.Context.SoulGuideBlockPending && _battle.Phase==Phase.Block)
                    Button(p,"Btn_SoulContribution","选择一次实际格挡 +1",25,386,442,48,()=>OpenContributionSkill(_selectedSkill));
                if(woundSkill)Button(p,"Btn_SkillWound",_skillWound!=null && _battle.Player.Deck.Hand.Any(c=>ReferenceEquals(c,_skillWound))?"已选实际伤牌 · 更换":"选择一张实际伤牌",25,386,442,48,()=>_skillWoundPicker=true);
                if (targeted) W.Text(p, "SkillTarget", _targets.Count == 1 ? "目标：" + EnemyView(_battle.Enemies[_targets.Single()].Id).displayName : "点击战场上的一名敌人作为目标。", 25, 387, 442, 28, 19, W.Cream);
                if(targeted && _targets.Count==1 && BattleSkillChoice.NeedsAttack(_selectedSkill.SourceId,_skillOption))
                {
                    var slotText=Button(p,"Btn_SkillAttackSlot","攻击 #"+(_attackSlot+1)+" · 点击切换",25,413,442,31,()=>_attackSlot=(_attackSlot+1)%_battle.Enemies[_targets.Single()].AttackCount).GetComponentInChildren<TextMeshProUGUI>();
                    slotText.enableAutoSizing=true;slotText.fontSizeMin=12;slotText.fontSizeMax=16;
                }
                Button(p, "Btn_UseSkill", "确认使用技能", 25, 447, 442, 52,
                    () => { if(_selectedSkill.SourceId is "hero_skill_pool_001:05" or "hero_skill_pool_003:06"){OpenEnemyTokenSkill(_selectedSkill);return;} if(_selectedSkill.SourceId=="hero_skill_pool_002:06"){_leadershipSkill=_selectedSkill;_showUnits=true;_showSkills=false;_selectedSkill=null;return;} if(_selectedSkill.SourceId=="hero_skill_pool_004:04"){OpenContributionSkill(_selectedSkill);return;} if(_selectedSkill.SourceId=="hero_skill_pool_001:06"){OpenShapeshift(_selectedSkill);return;} if(NeedsSidewaysSkill(_selectedSkill)){OpenSidewaysSkill(_selectedSkill);return;} UseBattleSkill(_selectedSkill, _skillOption, _targets.Count == 1 ? _targets.Single() : -1,woundSkill?_skillWound:null,_attackSlot); if (targeted) _selectedSkill = null; }, true).interactable = skillEntryAvailable && options.Length > 0 && (!targeted || _targets.Count==1) && (!woundSkill || _skillWound!=null && _battle.Player.Deck.Hand.Any(c=>ReferenceEquals(c,_skillWound)));
            }
            else if (_selected?.Type == CardType.Artifact && (_battle.Phase != Phase.End || _selected.Id == "items_018" || _selected.Id == "items_003" || _selected.Id == "items_012"))
                DrawArtifactActions(p);
            else if (_selectedUnit != null && (_battle.Phase != Phase.End || postcombatUnit))
                DrawUnitActions(p);
            else if (_battle.Phase == Phase.AssignDamage)
                W.Text(p, "DamagePlan", DamagePlanText(), 25, 281, 442, 213, 21, W.Cream);
            else if (_selected != null)
            {
                var allChoices = _battle.Choices(_selected);
                var choices = DrawCardMode(p, allChoices, _choice, id=>{_choice=id;_deedChoicePage=0;}, 25, 280, 442);
                _deedChoicePage = Math.Min(_deedChoicePage,Math.Max(0,(choices.Length-1)/4));
                for (int i = 0; i < Math.Min(4,choices.Length-_deedChoicePage*4); i++)
                {
                    var choice = choices[_deedChoicePage*4+i];
                    if(choice.Id=="base" || choice.Id=="strong")continue;
                    var button = Button(p, "Btn_Choice_" + choice.Id, (_choice == choice.Id ? "● " : "") + choice.Label,
                        25+i%2*227, 333+i/2*39, 215, 35, () => _choice = choice.Id);
                    var label=button.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=14;
                }
                if(choices.Length>4)
                    Button(p,"Btn_MoreChoices","下一页 · 当前模式选项",25,409,442,25,()=>_deedChoicePage=(_deedChoicePage+1)%((choices.Length+3)/4));
                var selectedChoice = choices.FirstOrDefault(c => c.Id == _choice);
                if (selectedChoice != null && selectedChoice.Target != BattleCardTarget.None)
                {
                    if (selectedChoice.Target == BattleCardTarget.EnemyAttack && _targets.Count == 1 && _battle.Enemies[_targets.Single()].AttackCount > 1)
                    {
                        var slotLabel = Button(p, "Btn_CardAttackSlot", "敌人 #" + (_targets.Single()+1) + " · 攻击 #" + (_attackSlot + 1) + " · 点击切换", 25, 419, 442, 26,
                            () => _attackSlot = (_attackSlot + 1) % _battle.Enemies[_targets.Single()].AttackCount).GetComponentInChildren<TextMeshProUGUI>();
                        slotLabel.fontSize=16;
                    }
                    else W.Text(p, "CardTarget", _targets.Count == 1 ? "目标：" + EnemyView(_battle.Enemies[_targets.Single()].Id).displayName : "点击一名存活敌人，再确认出牌。",
                        25, 419, 442, 26, 17, W.Gold);
                }
                Button(p, "Btn_Play", CardModeConfirm(allChoices,_choice), 25, 447, 214, 52, () =>
                {
                    if(PveDeedInputs.Supports(_selected.Id)){OpenDeedInputs(_selected,_choice,true);return;}
                    WithManaPayment(_selected,()=>_battle.Play(_selected, _choice, _targets.Count == 1 ? _targets.Single() : -1, _attackSlot),
                        (physical,payment)=>_battle.PlayWithBorrowedPayment(physical,payment,_selected,_choice,_targets.Count==1?_targets.Single():-1,_attackSlot));
                    _selected = null; _choice = null; _targets.Clear();
                }, true).interactable = choices.Length > 0;
                Button(p, "Btn_Sideways", "横置使用", 251, 447, 216, 52, () =>
                { _battle.PlaySideways(_selected); _selected = null; _choice = null; }).interactable = _battle.Phase == Phase.Block || _battle.Phase == Phase.Melee || _battle.Phase == Phase.Ranged && _battle.Context.AllAttacksSiege;
            }
            else if (_battle.Phase == Phase.Block || _battle.Phase == Phase.Ranged || _battle.Phase == Phase.Melee)
            {
                string preview = TargetPreview();
                if (!blockDecision) W.Text(p, "TargetPreview", preview, 25, 281, 442, _battle.Phase == Phase.Block ? 60 : 96, 23, W.Cream);
                if (_battle.Phase == Phase.Block && _targets.Count == 1 && _battle.Enemies[_targets.Single()].AttackCount > 1)
                    Button(p, "Btn_AttackSlot", blockDecision ? "攻击 #" + (_attackSlot + 1) + " · 切换" : "切换敌人攻击序号：" + (_attackSlot + 1), blockDecision ? 251 : 25, blockDecision ? 237 : 386, blockDecision ? 216 : 442, blockDecision ? 35 : 43,
                        () => _attackSlot = (_attackSlot + 1) % _battle.Enemies[_targets.Single()].AttackCount);
                if (_battle.Phase == Phase.Melee && _targets.Count == 1 && _battle.Context.CanChooseKillCrystal)
                {
                    int target = _targets.Single();
                    var color = _battle.Context.KillCrystalColor(target, _battle.Enemies[target]);
                    Button(p, "Btn_KillCrystal", "奖励：" + ColorName(color).Replace("魔力", "魔晶"), 299, 386, 168, 43, () =>
                    {
                        var colors = MK.Logic.Runtime.CardEffects.SoulHarvesterEffect.AvailableColors(_battle.Enemies[target]);
                        _battle.ChooseKillCrystal(target, colors[(Array.IndexOf(colors, color) + 1) % colors.Length]);
                    });
                }
                if (_targets.Count>0 && _battle.Context.CombatPower.AvailableAttacks(_battle.Phase,_battle.Context.AllAttacksSiege).Length>1 && (_battle.Phase == Phase.Melee || _battle.Phase == Phase.Ranged))
                    Button(p, "Btn_AttackParts", "分配攻击来源", 25, 386,
                        _battle.Phase == Phase.Melee && _targets.Count == 1 && _battle.Context.CanChooseKillCrystal ? 260 : 442,
                        43, () => { _attackSelectionPage = 0; _attackSelectionOpen = true; });
                if (_battle.Phase == Phase.Block && _targets.Count == 1)
                    Button(p, "Btn_BlockParts", "分配格挡来源", 25, blockDecision ? 237 : 343, blockDecision && _battle.Enemies[_targets.Single()].AttackCount > 1 ? 214 : 442, blockDecision ? 35 : 40,
                        () => { _blockSelectionPage = 0; _blockSelectionOpen = true; });
                if(_targets.Count>0) Button(p, "Btn_Resolve", _battle.Phase == Phase.Block ? "确认格挡" : "结算攻击", 25, blockDecision ? 276 : 447, 442, blockDecision ? 43 : 52, () =>
                {
                    if (_targets.Count == 0) throw new InvalidOperationException("请先选择目标");
                    if (_battle.Phase == Phase.Block)
                    {
                        _battle.BlockSelected(_targets.Single(), SelectedBlockParts(), _attackSlot);
                        _reservedBlocks.Clear(); _blockSelectionOpen = false;
                    }
                    else { ResolveAttackWithDefend(); return; }
                    _targets.Clear();
                }, true);
            }
            }
            bool paymentButton=!_showResolutionRecall && DrawPaymentButton(p,(object)_selected??_selectedUnit,25,502,442,37);
            bool hasRecall = _combatJournal.Count > 0;
            string feedback = _showResolutionRecall ? "完整结算记录已保留。\n选择手牌或敌人可继续行动。" : LastRule;
            var feedbackText=W.Text(p, "Feedback", feedback, 25, blockDecision ? 350 : paymentButton?548:521, 442, blockDecision ? 230 : hasRecall?(paymentButton?49:76):(paymentButton?88:115), 22, _inputRejected ? new Color32(244, 166, 142, 255) : W.Cream);
            // The operation status and input label are receipt headings, not two
            // mandatory rows. Keep the complete rule body at 22pt in the same
            // rectangle by joining only those headings when the original overflows.
            // Leave the stored feedback untouched for state capture and recall.
            if (feedback.StartsWith("操作 ", StringComparison.Ordinal)
                && feedbackText.GetPreferredValues(feedback, 442, 0).y > feedbackText.rectTransform.rect.height)
            {
                int headerBreak = feedback.IndexOf('\n');
                int bodyBreak = headerBreak < 0 ? -1 : feedback.IndexOf('\n', headerBreak + 1);
                if (bodyBreak > headerBreak)
                {
                    string compactReceipt = feedback.Substring(0, headerBreak) + " · " + feedback.Substring(headerBreak + 1);
                    if (feedbackText.GetPreferredValues(compactReceipt, 442, 0).y <= feedbackText.rectTransform.rect.height)
                        feedbackText.text = compactReceipt;
                }
            }
            if (hasRecall && !_showResolutionRecall && feedbackText.GetPreferredValues(feedbackText.text, 442, 0).y > feedbackText.rectTransform.rect.height)
            {
                // Keep the full operation explanation in the existing reading surface.
                // The status strip stays readable at its original type size.
                var lines = feedbackText.text.Split('\n');
                // Spend the compact strip on the result/rejection itself. The
                // chronological journal retains the receipt heading and all lines.
                feedbackText.text = lines.LastOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? feedback;
            }
            if (hasRecall) Button(p, "Btn_ResolutionRecall", "操作记录 · 查看完整反馈", 25, 599, 442, 43,
                () => { _combatLogOpen = true; _combatWikiOpen = false; });
            bool blockPhase = _battle.Phase == Phase.Block;
            bool blockArmorReward = blockPhase && _battle.Context.ExplosiveBoltArmorUses > 0 && HasCombatConversions;
            if(_rules && !blockPhase) Button(p, "Btn_Rules", "收起规则", 25, 644, HasCombatConversions?210:442, 42, () => _rules = false);
            if(HasCombatConversions)Button(p,"Btn_Conversions",blockArmorReward?"资源转换":"原卡资源转换",blockPhase?25:247,644,blockArmorReward?140:blockPhase?210:220,42,()=>_conversionsOpen=true);
            if(_battle.Context.ExplosiveBoltArmorUses>0)
            {
                Button(p,"Btn_ExplosiveArmor",blockArmorReward?"目标减甲1":"所选敌人减甲1",blockArmorReward?176:25,blockPhase?644:693,blockArmorReward?140:215,blockPhase?42:58,()=>{_battle.ResolveExplosiveArmor(_targets.Count==1?_targets.Single():throw new InvalidOperationException("请选择一个实际存活敌人"));_targets.Clear();});
                Button(p,"Btn_SkipExplosiveArmor",blockArmorReward?"放弃奖励":"放弃减甲奖励",blockArmorReward?327:252,blockPhase?644:693,blockArmorReward?140:215,blockPhase?42:58,()=>_battle.ResolveExplosiveArmor());
            }
            else if (_battle.Phase == Phase.Block && _battle.Context.AttackReductionTimes > 0 && !_rules)
                Button(p, "Btn_ReduceAttack", $"攻击减 {_battle.Context.AttackReduction} · 余 {_battle.Context.AttackReductionTimes} 次", HasCombatConversions?247:25, 644, HasCombatConversions?220:442, 42,
                    () => _battle.ReduceAttack(_targets.Count == 1 ? _targets.Single() : -1, _attackSlot));
            else if(!_rules && _journey!=null && _battle.Context.LearningModes!=0 && !_battle.Context.LearnUsed)
                Button(p,"Btn_LearningSupply","学习 · 供应购牌",blockPhase && HasCombatConversions?247:25,blockPhase?644:693,blockPhase && HasCombatConversions?220:442,blockPhase?42:58,OpenLearningSupply);
            // Full phase teaching lives in the global Wiki. Immediate costs remain in the payment controls.
            if (_battle.Phase != Phase.End)
                Button(p, "Btn_Next", _battle.Phase == Phase.AssignDamage ? "确认承伤分配" : _battle.Phase == Phase.Melee ? "结束战斗" : _battle.Phase == Phase.Block ? "结束格挡 · 分配伤害" : "结束远程 · 进入格挡",
                    25, 770, 442, 47, () => { _battle.Advance(); ClearAttackSelection(); _bowPanel = false; _selected = null; _selectedUnit = null; _selectedSkill = null; _choice = null; _targets.Clear(); });
            else if (_journey?.Context.TunnelAssaultActive==true) Button(p,"Btn_FinishSite","结算地道地点 / 撤退",25,770,442,47,FinishWorldSite,true);
            else if (_journey?.Context.Dungeon?.Reward==DungeonRewardKind.Artifact && _journey.Context.Dungeon.SelectedArtifact==null) Button(p,"Btn_SiteReward","地下城：翻二选一原神器奖励",25,770,442,47,()=>{_journey.RevealDungeonArtifactReward();_siteRewardOpen=true;},true);
            else if (_journey?.Context.PendingSpellReward==true && _journey.Context.SelectedSpellReward==null) Button(p,"Btn_SiteReward","选择地点原法术奖励",25,770,442,47,()=>_siteRewardOpen=true,true);
            else if (_journey != null) Button(p,"Btn_EndTurn","结束回合 / 补牌",25,770,442,47,EndJourneyTurn,true);
            else Button(p, "Btn_Restart", "更换配置 / 再战", 25, 770, 442, 47, OpenCamp, true);
        }
        private void EnsureResolutionPresentationSession()
        {
            if (ReferenceEquals(_resolutionHistoryBattle, _battle)) return;
            _resolutionHistoryBattle = _battle;
            _lastResolutionText = null; _lastResolutionHeading = null; _lastActionFeedback = null;
            _showResolutionRecall = false; _resolutionText = null; _resolutionAt = -10;
            _combatJournal.Clear(); _combatReadPosition.Remove("journal"); _combatLogOpen = false; _combatWikiOpen = false;
            _impactTargets.Clear();
        }
        private void DrawResolutionRecall(RectTransform p)
        {
            W.Text(p, "Heading", _lastResolutionHeading, 25, 21, 442, 49, 31, W.Gold);
            InformationSurface(p, "ResolutionReadingPlate", 14, 79, 464, 425, new Color32(34, 53, 46, 255));
            var viewport = W.Rect(p, "ResolutionViewport", 25, 89, 422, 380);
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear; hitArea.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            string recallText = _lastResolutionText + (string.IsNullOrEmpty(_lastActionFeedback) ? "" : "\n\n—— 最近操作说明 ——\n" + _lastActionFeedback);
            var body = W.Text(viewport, "ResolutionFullText", recallText, 0, 0, 414, 380, 22, W.Cream);
            body.enableAutoSizing = false;
            body.overflowMode = TextOverflowModes.Overflow;
            float height = Mathf.Max(380, body.GetPreferredValues(recallText, 414, 0).y + 12);
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            // This existing inspector region is the reading surface, not a modal.
            // Long results scroll at the same type size; ordinary card/target inputs remain live.
            if (height > 380) W.Text(p, "ResolutionScrollHint", "滚轮查看完整结算与较早记录 ↓", 25, 472, 442, 31, 18, W.Gold);
        }
        private void DrawSkills()
        {
            if (embeddedBattleUi) { DrawEmbeddedSkillChoices(); return; }
            W.Text(_root, "SkillLabel", $"{_battle.Player.Name} · 等级 {_battle.Player.Level} · 已学技能 {_battle.Player.Skills.Count}", 39, 718, 900, 36, 22, W.Gold);
            var row = W.Rect(_root, "Skills", 36, 768, 1320, 275);
            for (int i = _skillPage; i < Math.Min(_skillPage + 1, _battle.Player.Skills.Count); i++)
            {
                var skill = _battle.Player.Skills[i];
                var art = Loadout.hero?.skillArtwork.FirstOrDefault(a => skill.SourceId == Loadout.hero.skillPoolId + ":" + a.skillIndex.ToString("00"));
                var button = Button(row, "Btn_Skill_" + i, "", 0, 0, 1320, 245,
                    () => { if(skill.IsInteractive==true && _journey!=null){OpenSharedSkills(skill);return;} _recovery = false; _selectedSkill = skill; _skillOption = 0; _selected = null; _selectedUnit = null; });
                var original = W.Rect(button.transform, "OriginalSkillArt", 16, 13, 895, 213).gameObject.AddComponent<RawImage>();
                if(art!=null){original.texture=art.originalSheet;original.uvRect=art.uvRect;}
                else
                {
                    original.rectTransform.sizeDelta=new Vector2(152,213);original.texture=SkillReference(skill);
                    var printed=W.Text(button.transform,"OriginalSkillRule",skill.PrintedRule,192,15,712,207,22,W.Cream);
                    printed.enableAutoSizing=true;printed.fontSizeMin=16;printed.fontSizeMax=22;
                }
                original.raycastTarget = false;
                W.Text(button.transform, "SkillStatus", skill.Id + "\n" + SkillUsage(skill), 944, 73, 343, 112, 26, W.Gold);
            }
            if (_battle.Player.Skills.Count > 1)
                Button(row, "Btn_NextSkill", $"下一技能 ({_skillPage + 1}/{_battle.Player.Skills.Count})", 944, 192, 343, 43,
                    () => { _skillPage = (_skillPage + 1) % _battle.Player.Skills.Count; _selectedSkill = null; });
        }

        private void DrawUnits()
        {
            if (embeddedBattleUi) { DrawEmbeddedUnitChoices(); DrawUnitDamageShortcuts(); return; }
            W.Text(_root, "UnitLabel", $"部队 {_battle.Player.Units.Count} / 指挥槽 {_battle.Player.CommandSlots}  ·  激活后耗竭，承伤资格独立", 39, 718, 1080, 36, 22, W.Gold);
            var row = W.Rect(_root, "Units", 36, 761, 1320, 287);
            for (int i = 0; i < _battle.Player.Units.Count; i++)
            {
                var unit = _battle.Player.Units[i];
                var binding = unitCatalog.Concat(_activeUnits).First(u => u.originalId == unit.Card.Id);
                var button = Button(row, "Btn_Unit_" + i, "", i * 252, unit == _selectedUnit ? -10 : 0, 240, 267,
                    () => { _recovery = false; _selectedUnit = unit; _selected = null; _choice = null; _bannerMode = false; _unitUtilityPanel = false; });
                W.Sprite(button.transform, "OriginalUnitArt", binding.artwork, 12, 4, 216, 208);
                W.Text(button.transform, "UnitState", $"{unit.Card.NameCn}\n{(unit.IsReady ? "就绪" : "耗竭")} · 创伤 {unit.Wounds}", 6, 212, 228, 54, 18, W.Cream, TextAlignmentOptions.Center);
            }
            DrawUnitDamageShortcuts();
            if (_battle.Player.Units.Count == 0)
                W.Text(row, "NoUnits", "此出战配置没有部队。可在出战配置中选择带有已招募部队的快照。", 10, 50, 1270, 80, 25, W.Cream);
        }
        private void DrawUnitDamageShortcuts()
        {
            if(optimizedInteractionUi && _battle.Phase==Phase.AssignDamage && _battle.Player.Units.Count>0 && _battle.Player.Units.Count<=4)
            {
                bool targetChosen=_targets.Count==1;
                int attacks=targetChosen?_battle.Enemies[_targets.Single()].AttackCount:0;
                Button(_root,"Btn_QuickDamageAttack",targetChosen?"承伤攻击 #"+(_attackSlot+1)+" · 切换":"先选择敌人的攻击",1066,761,288,43,
                    ()=>_attackSlot=(_attackSlot+1)%attacks).interactable=targetChosen && attacks>1;
                for(int i=0;i<_battle.Player.Units.Count;i++)
                {
                    var unit=_battle.Player.Units[i];
                    Button(_root,"Btn_QuickAssignUnit_"+i,"加入 / 取消 "+unit.Card.NameCn,1066,812+i*56,288,48,
                        ()=>ToggleDamageUnitPlan(unit)).interactable=targetChosen && UnitParticipation.CanUse(_battle.Player,_battle.Context,unit);
                }
            }
        }
        private string UnitText(UnitState u) => $"等级 {u.Card.Level} · 护甲 {u.Card.Armor + _battle.Context.UnitArmorBonus + BannerActions.HonorBonus(_battle.Context,u) + BannerActions.ProtectionArmor(u)} · 创伤 {u.Wounds}\n" +
            $"{(u.IsReady ? "就绪" : "耗竭")} · {(DelphanaMastersActions.CanContinue(_battle.Context, u) ? "可用尚未使用的大师能力" : u.CanActivate ? "可以激活" : "不能激活")}\n" +
            (u.Card.DamageInfluenceCost > 0 ? $"承伤费用：影响力{u.Card.DamageInfluenceCost}；当前{_battle.Context.InfluencePool}。\n" : "") +
            (u.Card.AssaultActivationInfluenceCost > 0 ? $"本场激活另付：影响力{CardUnitActions.ActivationInfluenceCost(_battle.Context, u)}。\n" : "") +
            (_battle.Context.UnitsResistAll ? "本回合：全体部队拥有所有抗性。\n" : "") +
            (u.Banner == null ? "" : "旌旗：" + CardName(u.Banner) + "\n") +
            (u.Card.Abilities.Length == 0 ? "" : "抗性：" + string.Join("、", u.Card.Abilities.Select(a => a switch
                { Ability.PhysicalResist => "物理", Ability.FireResist => "火焰", Ability.IceResist => "寒冰", _ => a.ToString() })) + "；先吸收等于护甲的伤害。\n") +
            (_battle.Phase == Phase.End ? "战斗已结束，可用战外能力。\n重整不会治疗创伤。" : _battle.Phase == Phase.AssignDamage ? "先选敌人，再加入承伤顺序。\n耗竭部队仍能承伤，受伤部队不能。" : "激活消耗原印刷费用，随后耗竭。\n战斗中的能力按钮仅显示当前阶段适用项；全文另列全部原能力。");
        private string DamagePlanText() => ForcedDamageText() + (_battle.DamagePlan.Count == 0 ? "其余未分配伤害由英雄承担。\n可打开部队面板，点选敌人与部队。" :
            string.Join("\n", _battle.DamagePlan.Select(a => EnemyView(_battle.Enemies[a.TargetIndex].Id).displayName + " #" + (a.AttackIndex + 1) + "：" +
                string.Join(" → ", a.Units.Select(u => u.Card.NameCn)) + " → 英雄")));
        private string ForcedDamageText() => string.Concat(Enumerable.Range(0, _battle.Enemies.Count)
            .SelectMany(i => Enumerable.Range(0, _battle.Enemies[i].AttackCount).Select(a => (Target: i, Attack: a)))
            .Where(k => !_battle.IsBlocked(k.Target, k.Attack) && !_battle.Context.IsAttackCancelled(k.Target, k.Attack)
                && !_battle.Context.DefeatedEnemyIndices.Contains(k.Target)
                && _battle.Context.ForcedDamageUnit(_battle.Enemies[k.Target], k.Attack) != null
                && CardCombatActions.DefenseProfile(_battle.Enemies[k.Target], k.Target, _battle.Context, k.Attack).Attack > 0)
            .Select(k => "强制先承伤：" + EnemyView(_battle.Enemies[k.Target].Id).displayName + " #" + (k.Attack + 1) + " → "
                + _battle.Context.ForcedDamageUnit(_battle.Enemies[k.Target], k.Attack).Card.NameCn + "。\n"));
        private void DrawUnitActions(Transform p)
        {
            var unit = _selectedUnit;
            if(!UnitParticipation.CanUse(_battle.Player,_battle.Context,unit))
            {W.Text(p,"UnitSiteRestriction","地下城禁止使用此部队。\n只有忠诚契约实际指挥的部队例外。",25,280,442,150,24,W.Gold);return;}
            bool versatile = unit.Card.Actions.Any(a => a.Kind == UnitAbilityKind.GainChosenTokens);
            if (versatile && (_battle.Phase != Phase.AssignDamage || _unitUtilityPanel)) { DrawVersatileUnitActions(p, unit); return; }
            bool fortitude = unit.BannerEffectId == "items_018";
            var utilityAbilities = Enumerable.Range(0, unit.Card.Actions.Length).Where(i =>
            {
                var a = unit.Card.Actions[i];
                return a.Kind == UnitAbilityKind.GainMana || a.Kind == UnitAbilityKind.GainToken || a.Kind == UnitAbilityKind.GainCrystal || a.Kind == UnitAbilityKind.AllUnitResistance || a.Kind == UnitAbilityKind.Influence && _battle.Phase != Phase.End;
            }).ToArray();
            int utilityAbility = utilityAbilities.Length == 0 ? -1 : utilityAbilities[0];
            string UtilityLabel(UnitAbility a) => a.Kind == UnitAbilityKind.Influence ? a.PrintedRule + " [无魔力费用]" : a.Kind == UnitAbilityKind.AllUnitResistance ? "全体部队全抗性 · 本回合 [绿]" : a.Kind == UnitAbilityKind.GainCrystal ? a.PrintedRule + " [无魔力费用]" : $"获得{ColorName(a.GrantedMana.Value)}{a.Value}" + (a.Kind == UnitAbilityKind.GainMana ? $" + 同色魔晶{a.Value}" : "");
            bool support = unit.Card.Actions.Any(a => a.Kind == UnitAbilityKind.Healing || a.Kind == UnitAbilityKind.ReadyUnit);
            if (_battle.Phase == Phase.End && support)
            {
                string name = _supportTarget < 0 ? "英雄手牌" : _battle.Player.Units[_supportTarget].Card.NameCn;
                Button(p, "Btn_UnitSupportTarget", "目标：" + name + " · 点击切换", 25, 276, 442, 40,
                    () => _supportTarget = (_supportTarget + 2) % (_battle.Player.Units.Count + 1) - 1);
                int row = 0;
                for (int i = 0; i < unit.Card.Actions.Length; i++)
                {
                    int abilityIndex = i; var a = unit.Card.Actions[i];
                    if (a.Kind != UnitAbilityKind.Healing && a.Kind != UnitAbilityKind.ReadyUnit && a.Kind != UnitAbilityKind.GainToken
                        && a.Kind != UnitAbilityKind.GainMana && a.Kind != UnitAbilityKind.GainCrystal && a.Kind != UnitAbilityKind.AllUnitResistance) continue;
                    string label = a.PrintedRule + (a.Cost.Length == 0 ? " [免费]" : " [" + string.Join("、", a.Cost.Select(ColorName)) + "]");
                    var button = Button(p, "Btn_UnitAbility_" + i, label, 25, 320 + row++ * 44, 442, 40,
                        () => UseSelectedUnit(unit, abilityIndex, target: _supportTarget), true);
                    button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
                }
                Button(p, "Btn_HealWound", "治疗目标一张创伤 · 余 " + _battle.Context.Healing.Total + " 点", 25, 458, 442, 40,
                    () => _battle.HealWound(_supportTarget), true);
                return;
            }
            void FortitudeButton(float y) => Button(p, "Btn_Fortitude", unit.FortitudeBannerSpent ? "强韧旌旗：本轮已使用" : unit.FortitudeProtectionSelected ? "已选择免伤 · 点击取消" : "选择强韧旌旗免伤", 25, y, 442, 42,
                () => _battle.SelectFortitude(unit, !unit.FortitudeProtectionSelected), true);
            if (_battle.Phase == Phase.AssignDamage && utilityAbilities.Length > 1 && _unitUtilityPanel)
            {
                for (int u = 0; u < utilityAbilities.Length; u++)
                {
                    int abilityIndex = utilityAbilities[u];
                    Button(p, "Btn_UnitAbility_" + abilityIndex, UtilityLabel(unit.Card.Actions[abilityIndex]), 25, 300 + u * 60, 442, 52,
                        () => UseSelectedUnit(unit, abilityIndex), true);
                }
                Button(p, "Btn_BackToDamage", "返回承伤安排", 25, 447, 442, 52, () => _unitUtilityPanel = false);
                return;
            }
            if (_battle.Phase == Phase.AssignDamage)
            {
                W.Text(p, "DamagePlan", DamagePlanText(), 25, 276, 442, fortitude && utilityAbility >= 0 ? 46 : utilityAbility >= 0 ? 68 : fortitude ? 78 : 111, 19, W.Cream);
                if (fortitude) FortitudeButton(utilityAbility >= 0 ? 326 : 360);
                if (versatile) Button(p, "Btn_UnitUtility", "选择魔力或攻击转换", 25, 350, 442, 43, () => _unitUtilityPanel = true);
                if (utilityAbilities.Length > 1)
                    Button(p, "Btn_UnitUtility", "选择影响力或魔晶能力", 25, fortitude ? 372 : 350, 442, fortitude ? 31 : 43, () => _unitUtilityPanel = true);
                else if (utilityAbility >= 0)
                    Button(p, "Btn_UnitAbility_" + utilityAbility, UtilityLabel(unit.Card.Actions[utilityAbility]), 25, fortitude ? 372 : 350, 442, fortitude ? 31 : 43,
                        () => UseSelectedUnit(unit, utilityAbility), true);
                if (_targets.Count == 1 && _battle.Enemies[_targets.Single()].AttackCount > 1)
                    Button(p, "Btn_AttackSlot", "敌人攻击序号：" + (_attackSlot + 1), 25, utilityAbility >= 0 || fortitude ? 401 : 391, 442, utilityAbility >= 0 || fortitude ? 39 : 43,
                        () => _attackSlot = (_attackSlot + 1) % _battle.Enemies[_targets.Single()].AttackCount);
                Button(p, "Btn_AssignUnit", "加入 / 取消承伤", 25, 447, 442, 52, () =>
                {
                    ToggleDamageUnitPlan(unit);
                }, true);
                return;
            }
            float nextY = 280;
            if (unit.BannerEffectId == "items_003")
            {
                Button(p, "Btn_Courage", unit.CourageBannerSpent ? "勇气旌旗：本轮已使用" : "勇气旌旗：战外重整此部队", 25, nextY, 442, 48,
                    () => _battle.UseCourage(unit), true);
                nextY += 55;
            }
            if (_battle.Phase == Phase.End && utilityAbility < 0) return;
            if (fortitude) { FortitudeButton(nextY); nextY += 49; }
            bool fearBanner = unit.BannerEffectId == "items_001" && _battle.Phase == Phase.Block;
            if (fearBanner)
            {
                Button(p, "Btn_BannerMode", _bannerMode ? "返回部队原能力" : "使用恐惧旌旗", 25, 280, 442, 48, () => _bannerMode = !_bannerMode);
                if (_bannerMode)
                {
                    W.Text(p, "BannerRule", "耗竭此部队，取消所选敌人的一次攻击，名望+1。", 25, 338, 442, 51, 19, W.Cream);
                    if (_targets.Count == 1 && _battle.Enemies[_targets.Single()].AttackCount > 1)
                        Button(p, "Btn_AttackSlot", "取消敌人攻击序号：" + (_attackSlot + 1), 25, 390, 442, 43,
                            () => _attackSlot = (_attackSlot + 1) % _battle.Enemies[_targets.Single()].AttackCount);
                    Button(p, "Btn_UseFear", "确认取消这次攻击", 25, 447, 442, 52,
                        () => _battle.UseFearBanner(unit, _targets.Count == 1 ? _targets.Single() : -1, _attackSlot), true);
                    return;
                }
                nextY = 337;
            }
            if (_battle.Phase == Phase.Block && unit.Card.Actions.Any(ShockTroopsActions.IsReaction)
                && _targets.Count == 1 && _battle.Enemies[_targets.Single()].AttackCount > 1)
            {
                Button(p, "Btn_AttackSlot", "敌人攻击序号：" + (_attackSlot + 1), 25, nextY, 442, 36,
                    () => _attackSlot = (_attackSlot + 1) % _battle.Enemies[_targets.Single()].AttackCount);
                nextY += 43;
            }
            for (int i = 0; i < unit.Card.Actions.Length; i++)
            {
                int abilityIndex = i; var a = unit.Card.Actions[i];
                bool blocks = a.Kind == UnitAbilityKind.Block || a.Kind == UnitAbilityKind.AttackOrBlock;
                bool attacks = a.Kind == UnitAbilityKind.Attack || a.Kind == UnitAbilityKind.AttackOrBlock;
                bool freeze = a.Kind == UnitAbilityKind.FreezeArmor;
                bool stop = a.Kind == UnitAbilityKind.StopUnfortifiedEnemy;
                bool masters = DelphanaMastersActions.IsMasters(unit);
                bool reaction = ShockTroopsActions.IsReaction(a);
                bool targeted = reaction || masters || freeze || stop || a.RemoveTargetFortifications || a.RemoveTargetResistances;
                if (reaction && _battle.Phase != Phase.Block) continue;
                if (masters && !DelphanaMastersActions.PhaseAllows(a, _battle.Phase)) continue;
                if (stop && _battle.Phase != Phase.Block || a.Kind == UnitAbilityKind.Influence && _battle.Phase == Phase.End) continue;
                if (!reaction && !masters && !stop && a.Kind != UnitAbilityKind.GainCrystal && a.Kind != UnitAbilityKind.Influence && a.Kind != UnitAbilityKind.AllUnitResistance && a.Kind != UnitAbilityKind.GainToken && a.Kind != UnitAbilityKind.GainMana && (freeze ? _battle.Phase != Phase.Ranged && _battle.Phase != Phase.Block && _battle.Phase != Phase.Melee : _battle.Phase == Phase.Block ? !blocks : !attacks || (_battle.Phase != Phase.Melee &&
                    (_battle.Phase != Phase.Ranged || a.AttackType == AttackType.Melee && !_battle.Context.AllAttacksSiege)))) continue;
                int option = a.Kind == UnitAbilityKind.AttackOrBlock && _battle.Phase == Phase.Block ? 1 : 0;
                string cost = a.Cost.Length == 0 ? "无魔力费用" : string.Join("、", a.Cost.Select(ColorName));
                int influenceCost = CardUnitActions.ActivationInfluenceCost(_battle.Context, unit);
                if (influenceCost > 0) cost += "；影响力" + influenceCost;
                string label = (a.Kind == UnitAbilityKind.AllUnitResistance || a.Kind == UnitAbilityKind.GainMana || a.Kind == UnitAbilityKind.GainToken || a.Kind == UnitAbilityKind.GainCrystal) ? UtilityLabel(a) : a.PrintedRule + " [" + cost + "]";
                if (a.Kind == UnitAbilityKind.InterceptEnemyAttack) label = "所选攻击-3；本部队先承伤（含刺杀） [" + cost + "]";
                else if (a.Kind == UnitAbilityKind.WeakenEnemyAttack) label = "目标护甲-1、所选一次攻击-1 [" + cost + "]";
                else if (a.OtherUnitAttackBonus > 0) label = "远程1；本回合其他部队的所有攻击+1 [" + cost + "]";
                if (masters && DelphanaMastersActions.Used(_battle.Context, unit, i)) label += " · 已使用";
                bool longLabel = label.Length > 22;
                float height = fearBanner ? (longLabel ? 55 : 48) : longLabel ? 69 : 59;
                var button = Button(p, "Btn_UnitAbility_" + i, label, 25, nextY, 442, height,
                    () => UseSelectedUnit(unit, abilityIndex, option, targeted && _targets.Count == 1 ? _targets.Single() : -1, attackIndex: _attackSlot), true);
                if (masters) button.interactable = !DelphanaMastersActions.Used(_battle.Context, unit, i)
                    && DelphanaMastersActions.CanContinue(_battle.Context, unit);
                if (longLabel || fearBanner) button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
                nextY += height + 7;
            }
        }
        private void DrawVersatileUnitActions(Transform p, UnitState unit)
        {
            string[] modes = { "魔力标记", "冰火攻挡", "攻击转换" };
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                Button(p, "Btn_UnitMode_" + i, (_unitMode == i ? "● " : "") + modes[i], 25 + i * 150, 280, 142, 42, () => _unitMode = mode);
            }
            if (_unitMode == 0)
            {
                Button(p, "Btn_UnitManaFirst", "第一枚：" + ColorName(_unitManaFirst), 25, 340, 215, 43,
                    () => _unitManaFirst = (ManaColor)(((int)_unitManaFirst + 1) % 6));
                Button(p, "Btn_UnitManaSecond", "第二枚：" + ColorName(_unitManaSecond), 251, 340, 216, 43,
                    () => _unitManaSecond = (ManaColor)(((int)_unitManaSecond + 1) % 6));
                Button(p, "Btn_UnitAbility_0", "获得两枚所选魔力 · 免费", 25, 447, 442, 52,
                    () => UseSelectedUnit(unit, 0, manaColors: new[] { _unitManaFirst, _unitManaSecond }), true);
            }
            else if (_unitMode == 1)
            {
                string[] fees = { "不支付 · 数值5", "红1 · 数值7", "蓝1 · 数值7", "红1蓝1 · 数值9" };
                Button(p, "Btn_UnitBoost", fees[_unitBoost] + " · 切换", 25, 340, 442, 43, () => _unitBoost = (_unitBoost + 1) % 4);
                bool block = _battle.Phase == Phase.Block;
                Button(p, "Btn_UnitAbility_1", block ? "确认冰火格挡" : "确认冰火攻击", 25, 447, 442, 52,
                    () => UseSelectedUnit(unit, 1, block ? 1 : 0, boost: _unitBoost), true).interactable = block || _battle.Phase == Phase.Melee || _battle.Phase == Phase.Ranged && _battle.Context.AllAttacksSiege;
            }
            else
            {
                Button(p, "Btn_UnitConversion", _unitConversion == 0 ? "全部攻击 → 冰火 · 切换" : "全部攻击 → 攻城 · 切换", 25, 340, 442, 43,
                    () => _unitConversion = 1 - _unitConversion);
                Button(p, "Btn_UnitAbility_2", "确认转换 · 黑1 · 本次战斗", 25, 447, 442, 52,
                    () => UseSelectedUnit(unit, 2, _unitConversion), true).interactable = _battle.Phase != Phase.End;
            }
            if (_battle.Phase == Phase.AssignDamage)
                Button(p, "Btn_UnitUtility", "返回承伤分配", 25, 396, 442, 37, () => _unitUtilityPanel = false);
        }
        private string CardText(DeedCard card)
        {
            var effectId = _journey?.ArtifactEffectId(card) ?? card.Id;
            var artifact = artifactCatalog.FirstOrDefault(b => b.source.Id == effectId);
            if (artifact != null)
                return (card.Id == "items_024" ? "宝盒借用 · " + artifact.source.NameCn + "\n" : "") + (_artifactBurn ? "焚毁：" + artifact.source.OncePerRound : "普通：" + artifact.source.AssignEffect) +
                    "\n" + (_artifactBurn ? "此卡永久移除。" : PveArtifactInputs.IsAttachedBanner(effectId) ? "此卡附着所选部队。" : "此卡进入已打出区。") +
                    (_materials.Count == 0 ? "" : "\n材料：" + string.Join("、", _materials.Select(CardName)));
            var binding = Binding(card);
            if (binding == null) return "真实创伤卡。不能正常打出或横置；占据手牌空间。";
            if (binding.source is SpellCardSO spell)
            {
                bool enhanced = _battle.Choices(card).FirstOrDefault(c=>c.Id==_choice)?.Enhanced == true;
                var half = enhanced ? spell.Bottom : spell.Top;
                return half.Name+"："+half.Effect+"\n印刷费用："+string.Join("、",half.ManaCost.Select(ColorName))+(enhanced ? "（通常须黑夜）" : "");
            }
            var s = binding.Action();
            bool strong = _battle.Choices(card).FirstOrDefault(c=>c.Id==_choice)?.Enhanced == true;
            return (strong?"强化效果："+s.EnhancedEffect+"\n印刷费用："+string.Join("、",s.RequiredCrystals.Select(ColorName))+(_battle.Context.NextCardEnhanced?"\n免费强化：免印刷费用，额外费用照常。":""):"基础效果："+s.BaseEffect)+"\n" +
                (_battle.Choices(card).Length == 0 ? "此阶段没有已接入的主动效果；可在格挡或近战横置。" : "确认后支付费用并移入已打出区。");
        }
        private int[] BowContributions() => _battle.Context.CombatPower.Attacks.Select((a, i) => new { a, i })
            .Where(x => x.a.Type != AttackType.Melee).Select(x => x.i).ToArray();
        private void DrawBowChoices(Transform p)
        {
            var indices = BowContributions();
            if (indices.Length == 0) return;
            _bowContribution %= indices.Length;
            int index = indices[_bowContribution]; var part = _battle.Context.CombatPower.Attacks[index];
            string kind = part.Type == AttackType.Siege ? "攻城" : "远程";
            Button(p, "Btn_BowContribution", $"第{_bowContribution + 1}/{indices.Length}笔：{ElementLabel(part.Element)}{kind}{part.Value}", 25, 280, 442, 48,
                () => _bowContribution = (_bowContribution + 1) % indices.Length);
            bool siege = CombatMath.IsSiege(part, _battle.Context);
            W.Text(p, "BowPower", $"当前：{ElementLabel(part.Element)}{(siege ? "攻城" : "远程")}{CombatMath.AttackValue(part, _battle.Phase, _battle.Context)}", 25, 341, 442, 45, 22, W.Cream);
            Button(p, "Btn_BowDouble", part.Type == AttackType.Siege ? "翻倍 · 转为远程" : "翻倍 · 保持远程", 25, 397, 442, 45,
                () => _battle.ChooseBowAttack(index, 1), true);
            Button(p, "Btn_BowSiege", part.Type == AttackType.Siege ? "保留原攻城 · 不翻倍" : "转为攻城 · 不翻倍", 25, 453, 442, 45,
                () => _battle.ChooseBowAttack(index, part.Type == AttackType.Siege ? 0 : 2), true);
        }
        private void DrawArtifactActions(Transform p)
        {
            string id = _journey?.ArtifactEffectId(_selected) ?? _selected.Id;
            if(PveArtifactInputs.IsSpecial(id))
            {
                Button(p,"Btn_OpenSpecialArtifact","选择实际供应 / 技能 / 宝盒",25,347,442,65,()=>OpenSpecialArtifact(_selected),true).interactable=_journey!=null;return;
            }
            if(PveArtifactInputs.IsMaterial(id))
            {
                Button(p,"Btn_OpenMaterialArtifact","选择材料 / 原效果 / 目标",25,347,442,65,()=>OpenMaterialArtifact(_selected),true).interactable=_journey!=null;
                return;
            }
            Button(p, "Btn_ArtifactNormal", (_artifactBurn ? "" : "● ") + "普通效果", 25, 280, 214, 48,
                () => { _artifactBurn = false; _selectMaterials = false; _materials.Clear(); });
            Button(p, "Btn_ArtifactBurn", (_artifactBurn ? "● " : "") + "焚毁效果", 251, 280, 216, 48,
                () => { _artifactBurn = true; _selectMaterials = false; _materials.Clear(); });
            if (!_artifactBurn && PveArtifactInputs.IsAttachedBanner(id))
                Button(p, "Btn_ArtifactUnit", "附着：" + (_battle.Player.Units.Count == 0 ? "没有部队" : _battle.Player.Units[_artifactUnit % _battle.Player.Units.Count].Card.NameCn),
                    25, 347, 442, 65, () => { if (_battle.Player.Units.Count > 0) _artifactUnit = (_artifactUnit + 1) % _battle.Player.Units.Count; });
            if (!_artifactBurn && (id == "items_008" || id == "items_016"))
                Button(p, "Btn_ArtifactMaterials", _selectMaterials ? "点手牌选择材料 · 完成选择" : "选择弃牌材料（已选 " + _materials.Count + "）",
                    25, 347, 442, 65, () => _selectMaterials = !_selectMaterials);
            if (_artifactBurn && id == "items_001")
                W.Text(p, "ArtifactTargets", "点选至多3名敌人；已选 " + _targets.Count + " 名。", 25, 347, 442, 65, 21, W.Cream);
            if (id == "items_009" && _artifactBurn)
                Button(p, "Btn_HornBonus", "额外攻城 +" + _hornBonus + " · 投骰 " + _hornBonus + " 次（点击选择）",
                    25, 347, 442, 65, () => _hornBonus = (_hornBonus + 1) % 6);
            if (id == "items_020")
                Button(p, "Btn_ShieldMode", _shieldSplit ? "分次格挡 · 点击切换为单次" : "单次格挡 · 点击切换为分次",
                    25, 347, 442, 65, () => _shieldSplit = !_shieldSplit);
            if(PveArtifactInputs.IsResource(id))DrawArtifactColors(p,_selected,_artifactBurn,25,342,442,34,id);
            Button(p, "Btn_ArtifactPlay", _artifactBurn ? "确认焚毁此神器" : "确认使用神器", 25, 447, 442, 52, () =>
            {
                var unit = !_artifactBurn && PveArtifactInputs.IsAttachedBanner(id) && _battle.Player.Units.Count > 0 ? _battle.Player.Units[_artifactUnit % _battle.Player.Units.Count] : null;
                if (_artifactBurn && id == "items_001") _battle.BurnFearBanner(_selected, _targets.OrderBy(i => i).ToArray());
                else if (id == "items_009") _battle.PlayHorn(_selected, _artifactBurn, _artifactBurn ? _hornBonus : 0);
                else if (id == "items_020") _battle.PlayShield(_selected, _artifactBurn, _shieldSplit);
                else if (PveArtifactInputs.IsResource(id)) _battle.PlayResourceArtifact(_selected,_artifactBurn,_artifactColors.Take(PveArtifactInputs.ColorCount(id,_artifactBurn)).ToArray());
                else _battle.PlayArtifact(_selected, _artifactBurn, unit, _materials.ToArray());
                _targets.Clear();
                _selected = null; _materials.Clear(); _selectMaterials = false;
            }, true).interactable = _battle.CanUseArtifact(_selected);
        }
        private static string ColorName(ManaColor c) => c switch { ManaColor.Red => "红色魔力", ManaColor.Blue => "蓝色魔力", ManaColor.White => "白色魔力", ManaColor.Green => "绿色魔力", ManaColor.Gold => "金色魔力", ManaColor.Black => "黑色魔力", _ => c.ToString() };
        private string BlockThreatText()
        {
            if (_targets.Count != 1) return "选择一名敌人的一次攻击。\n尚未指定格挡目标";
            int target = _targets.Single();
            var enemy = _battle.Enemies[target];
            var attack = CardCombatActions.DefenseProfile(enemy, target, _battle.Context, _attackSlot);
            return $"#{target + 1} {EnemyView(enemy.Id).displayName}\n攻击 #{_attackSlot + 1} · {ElementLabel(attack.AttackElement)} {attack.Attack}";
        }
        private string TargetPreview()
        {
            if (_targets.Count == 0) return "选择战场上的存活敌人。";
            if (_battle.Phase == Phase.Block)
            {
                if (_battle.IsBlocked(_targets.Single(), _attackSlot))
                    return $"攻击 #{_attackSlot + 1} 已格挡，请另选攻击。";
                var blockParts = SelectedBlockParts();
                var value = blockParts.Length == 0 ? (Effective: 0, Required: _battle.BlockPreview(_targets.Single(), _attackSlot).Required)
                    : _battle.BlockSelectedPreview(_targets.Single(), blockParts, _attackSlot);
                return $"攻击 #{_attackSlot + 1} · 有效格挡 {value.Effective} / 需求 {value.Required}";
            }
            var targets = _targets.OrderBy(i => i).ToArray();
            var parts = SelectedAttackParts(targets);
            bool legal = CombatMath.CanAttack(_battle.Enemies, targets, parts, _battle.Phase, _battle.Context);
            int effective = CombatMath.EffectiveAttack(_battle.Enemies, targets, parts, _battle.Phase, _battle.Context);
            if (_battle.DefendChoices(targets).Count > 0)
                return $"所选 {_targets.Count} 名敌人 · 有效攻击 {effective}\n敌人防护尚待分配；结算时选择受保护目标。";
            int need = _battle.AttackArmorPreview(targets);
            int kept = _battle.Context.CombatPower.AvailableAttacks(_battle.Phase, _battle.Context.AllAttacksSiege).Length - parts.Length;
            return $"{_targets.Count} 名目标 · 护甲 {need} / 有效攻击 {effective}\n" +
                (!legal ? "城防禁止此次攻击" : effective >= need ? "可击败所选目标" : $"还差 {need - effective} 攻击，不会累积伤害") +
                $" · 保留 {kept} 笔";
        }
        private string RulesText() => _battle.Phase switch
        {
            Phase.Ranged => "敌人反击前进行远程／攻城攻击。城防敌人只接受攻城；每次攻击须达到目标护甲。",
            Phase.Block => "逐次完整格挡；迅捷需要双倍。对元素攻击的无效格挡按正式规则减半。",
            Phase.AssignDamage => "部队按顺序承伤，同场最多一次；抗性先吸收护甲值，剩余伤害造成部队创伤，再由英雄承受余量。",
            Phase.Melee => "可合并多张卡的攻击。每次须达到护甲；不足不累积削甲，不能再次领取击杀奖励。",
            _ => "战斗结束不等同回合结束。已打出牌保留实际牌区，奖励不再重复发放。"
        };
        private void OpenCamp()
        {
            _camp = true; _campPicker = 0;
            _draftLoadout = _loadout; _draftEncounter = _encounter;
            _draftUnits = _activeUnits.ToList();
        }
        private void OpenPicker(int kind)
        {
            _campPicker = kind;
            _pickerPage = (kind == 1 ? _draftLoadout : _draftEncounter) / PickerPageSize;
        }
        private void DrawPicker(Transform shade)
        {
            bool choosingLoadout = _campPicker == 1;
            int count = choosingLoadout ? loadouts.Length : encounters.Length;
            int selected = choosingLoadout ? _draftLoadout : _draftEncounter;
            int pages = (count + PickerPageSize - 1) / PickerPageSize;
            var panel = W.Panel(shade, "Picker", 240, 100, 1440, 880, W.Ink, true);
            W.Text(panel, "PickerTitle", choosingLoadout ? "选择出战配置" : "选择敌群", 40, 30, 1360, 55, 36, W.Gold);
            W.Text(panel, "PickerNote", "点选一项后返回预览；开始战斗前不会改变当前玩家。", 40, 99, 1360, 45, 23, W.Cream);
            for (int offset = 0; offset < PickerPageSize && _pickerPage * PickerPageSize + offset < count; offset++)
            {
                int index = _pickerPage * PickerPageSize + offset;
                string name = choosingLoadout ? loadouts[index].displayName : encounters[index].displayName;
                var button = Button(panel, "Btn_Select_" + index, (index == selected ? "已选 · " : "") + name,
                    40 + offset % 3 * 460, 168 + offset / 3 * 88, 440, 72, () =>
                    {
                        if (choosingLoadout)
                        {
                            _draftLoadout = index; _draftDeeds = null;
                            _draftUnits = (loadouts[index].units ?? Array.Empty<BattleUnitDefinition>()).ToList();
                        }
                        else _draftEncounter = index;
                        _campPicker = 0;
                    }, index == selected);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
            }
            Button(panel, "Btn_PreviousPage", "上一页", 40, 720, 260, 57, () => _pickerPage--).interactable = _pickerPage > 0;
            W.Text(panel, "PickerPage", $"{_pickerPage + 1} / {pages} 页 · 共 {count} 项", 335, 727, 760, 43, 24, W.Cream);
            Button(panel, "Btn_NextPage", "下一页", 1140, 720, 260, 57, () => _pickerPage++).interactable = _pickerPage + 1 < pages;
            Button(panel, "Btn_Back", "返回配置", 40, 801, 1360, 49, () => _campPicker = 0);
        }
        private void DrawCamp()
        {
            var shade = W.Panel(_root, "CampShade", 0, 0, 1920, 1080, new Color(0, 0, 0, .8f));
            shade.GetComponent<Image>().raycastTarget = true;
            if (_campPicker == 3) { DrawUnitPicker(shade); return; }
            if (_campPicker == 4) { DrawDeedPicker(shade); return; }
            if (_campPicker == 5) { DrawHeroStart(shade); return; }
            if (_campPicker == 6) { DrawHeroBuild(shade); return; }
            if (_campPicker != 0) { DrawPicker(shade); return; }
            var panel = W.Panel(shade, "Camp", 465, 130, 990, 820, W.Ink, true);
            W.Text(panel, "Title", "出战与演练配置", 45, 37, 900, 57, 37, W.Gold);
            W.Text(panel, "Note", "原英雄开局和技能构筑均用完整16牌；小牌库入口仅供演练。", 45, 96, 900, 47, 21, W.Cream);
            Button(panel,"Btn_MapMode",_draftOriginalMap?"连续旅程地图：原起始 A / 1 / 2":"连续旅程地图：自定验收布局",45,145,900,40,()=>_draftOriginalMap=!_draftOriginalMap);
            Button(panel, "Btn_Loadout", "更换手牌：" + loadouts[_draftLoadout].displayName, 45, 197, 900, 67,
                () => OpenPicker(1));
            Button(panel, "Btn_Encounter", "更换敌群：" + encounters[_draftEncounter].displayName, 45, 285, 900, 67,
                () => OpenPicker(2));
            Button(panel, "Btn_ChooseUnits", "选择原版部队（" + _draftUnits.Count + "/" + DraftCommandSlots + "）", 45, 373, 900, 67,
                () => { _unitPickerFromHeroBuild = false; _campPicker = 3; _pickerPage = 0; });
            W.Text(panel, "Preview", "手牌：" + (_draftDeeds == null ? string.Join(" / ", loadouts[_draftLoadout].openingHand.Select(c => c.source.NameCn)) +
                string.Concat((loadouts[_draftLoadout].artifacts ?? Array.Empty<BattleArtifactDefinition>()).Select(a => " / " + a.source.NameCn)) : string.Join(" / ",_draftDeeds.Select(d=>d.source.NameCn))) +
                "\n敌群：" + string.Join(" / ", encounters[_draftEncounter].enemies.Select(e => e.displayName)) +
                "\n部队：" + string.Join(" / ", _draftUnits.Select(u => u.Snapshot().NameCn)), 45, 462, 900, 130, 23, W.Muted);
            Button(panel,"Btn_ChooseCards","选择原卡手牌",45,613,360,60,OpenDeedPicker);
            Button(panel,"Btn_Journey","小牌库演练",425,613,250,60,BeginJourney);
            Button(panel,"Btn_SmallDeckHeroBuild","小牌库＋技能",695,613,250,60,()=>OpenHeroBuild(false));
            Button(panel,"Btn_OriginalHero","原英雄 · 标准开局",45,683,440,54,()=>_campPicker=5,true);
            Button(panel,"Btn_HeroBuild","原16牌 · 技能构筑",505,683,440,54,()=>OpenHeroBuild(true));
            Button(panel, "Btn_Cancel", "返回", 45, 751, 250, 52, () => _camp = false);
            Button(panel, "Btn_Start", "开始演练战斗", 320, 751, 625, 52, Begin, true);
        }

        private bool _unitPickerFromHeroBuild;
        private int DraftCommandSlots => new PlayerState { Fame = _unitPickerFromHeroBuild ? _buildFame : loadouts[_draftLoadout].startingFame }.CommandSlots;
        private void DrawUnitPicker(Transform shade)
        {
            var panel = W.Panel(shade, "UnitPicker", 240, 100, 1440, 880, W.Ink, true);
            W.Text(panel, "Title", "选择已招募的原版部队", 40, 30, 1360, 55, 36, W.Gold);
            W.Text(panel, "Note", $"已选 {_draftUnits.Count}/{DraftCommandSlots} 指挥槽 · 点选加入或移除；仅配置演练起点，战斗开始前不改变玩家。", 40, 99, 1360, 45, 22, W.Cream);
            for (int offset = 0; offset < PickerPageSize && _pickerPage * PickerPageSize + offset < unitCatalog.Length; offset++)
            {
                var binding = unitCatalog[_pickerPage * PickerPageSize + offset]; var card = binding.Snapshot();
                bool selected = _draftUnits.Any(u => u.originalId == binding.originalId);
                string name = card.NameCn + (card.Id switch { "army_008" => "（蓝）", "army_009" => "（红）", "army_010" => "（绿）", "army_011" => "（白）", _ => "" });
                var button = Button(panel, "Btn_Unit_" + binding.originalId, (selected ? "已选 · " : "") + name + " · " + card.Level + "级",
                    40 + offset % 3 * 460, 168 + offset / 3 * 88, 440, 72, () =>
                    {
                        if (_draftUnits.Any(u => u.originalId == binding.originalId)) _draftUnits.RemoveAll(u => u.originalId == binding.originalId);
                        else
                        {
                            if (_draftUnits.Count >= DraftCommandSlots) throw new InvalidOperationException("当前指挥槽已满，请先移除一支部队");
                            _draftUnits.Add(binding);
                        }
                    }, selected);
                button.interactable = selected || _draftUnits.Count < DraftCommandSlots;
                var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
            }
            int pages = Math.Max(1, (unitCatalog.Length + PickerPageSize - 1) / PickerPageSize);
            Button(panel, "Btn_PreviousPage", "上一页", 40, 720, 260, 57, () => _pickerPage--).interactable = _pickerPage > 0;
            W.Text(panel, "Page", $"{_pickerPage + 1}/{pages} 页 · {unitCatalog.Length}种原部队", 335, 727, 760, 43, 24, W.Cream);
            Button(panel, "Btn_NextPage", "下一页", 1140, 720, 260, 57, () => _pickerPage++).interactable = _pickerPage + 1 < pages;
            Button(panel, "Btn_Back", "返回配置预览", 40, 801, 1360, 49, () => { _campPicker = _unitPickerFromHeroBuild ? 6 : 0; _unitPickerFromHeroBuild = false; });
        }

        public Dictionary<string, string> ReadAutomationState()
        {
            Canvas.ForceUpdateCanvases();
            var texts = _root.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts) text.ForceMeshUpdate();
            if (_journey != null && (_journey.Game.IsEndingTurn || !_journey.Game.HasActiveTurn || _battle == null)) return CaptureJourneyState(texts);
            var p = _battle.Player;
            var state = new Dictionary<string, string>
            {
                ["phase"] = _battle.Phase.ToString(), ["won"] = _battle.Won.ToString(), ["fame"] = p.Fame.ToString(),
                ["wounds"] = p.Wounds.ToString(), ["realWounds"] = p.Deck.Hand.Count(c => c.Type == CardType.Wound).ToString(),
                ["influence"] = _battle.Context.InfluencePool.ToString(), ["reputation"] = p.Reputation.ToString(),
                ["discardWounds"] = p.DiscardWounds.ToString(), ["hand"] = p.Deck.Hand.Count.ToString(), ["played"] = p.Deck.PlayedCards.Count.ToString(),
                ["red"] = Crystal(ManaColor.Red).ToString(), ["blue"] = Crystal(ManaColor.Blue).ToString(), ["white"] = Crystal(ManaColor.White).ToString(),
                ["green"] = Crystal(ManaColor.Green).ToString(), ["siege"] = _battle.AttackTotal(AttackType.Siege).ToString(),
                ["killCrystal.choices"] = string.Join(",", _battle.Context.KillCrystalChoices.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value)),
                ["ranged"] = _battle.AttackTotal(AttackType.Ranged).ToString(), ["block"] = _battle.Context.BlockPool.ToString(), ["melee"] = _battle.AttackTotal(AttackType.Melee).ToString(),
                ["kills"] = _battle.Context.DefeatedEnemyIndices.Count.ToString(), ["loadout"] = _loadout.ToString(), ["encounter"] = _encounter.ToString(),
                ["draftLoadout"] = _draftLoadout.ToString(), ["draftEncounter"] = _draftEncounter.ToString(), ["rejections"] = _rejections.ToString(),
                ["camp.open"] = _camp.ToString(), ["picker.kind"] = _campPicker.ToString(), ["picker.page"] = _pickerPage.ToString(),
                ["ui.status"] = _status.text, ["ui.pools"] = _pools.text, ["ui.phase"] = _phaseLabel.text,
                ["source.cards"] = string.Join(",", Loadout.openingHand.Select(c => c.source.Id)),
                ["source.enemies"] = string.Join(",", _battle.Enemies.Select(e => e.Id)),
                ["art.cards"] = Loadout.openingHand.All(c => c.artwork != null && c.artwork.name == c.source.Id).ToString(),
                ["art.enemies"] = _battle.Enemies.All(e => EnemyView(e.Id).originalToken != null).ToString()
            };
            state["ui.cardMode"] = CurrentCardMode();
            state["rl.sharedRuleState"] = CaptureRlSharedAudit();
            state["summon.active"] = _battle.Context.Summoning.IsActive.ToString();
            state["summon.count"] = _battle.Context.Summoning.Bindings.Count.ToString();
            state["summon.actualDefenders"] = _battle.Context.Summoning.Bindings.All(binding =>
                ReferenceEquals(_battle.Enemies[binding.Index], binding.Token)).ToString();
            state["enemySupply.discard"] = (_battle.Context.EnemySupply?.DiscardPile.Count ?? 0).ToString();
            state["ui.truncated"] = string.Join(",", texts.Where(t => t.isTextOverflowing).Select(t => t.name));
            state["picker.visible"] = string.Join(",", _root.GetComponentsInChildren<Button>()
                .Where(b => b.name.StartsWith("Btn_Select_", StringComparison.Ordinal)).Select(b => b.name.Substring(11)));
            state["picker.hasPrevious"] = (_campPicker != 0 && _pickerPage > 0).ToString();
            state["picker.hasNext"] = (_campPicker != 0 && (_pickerPage + 1) * PickerPageSize < (_campPicker == 1 ? loadouts.Length : encounters.Length)).ToString();
            state["catalog.deeds"] = deedCatalog.Length.ToString();
            state["catalog.spells"] = deedCatalog.Count(d=>d.source is SpellCardSO).ToString();
            state["source.actualOpening"] = _openingSourceIds;
            state["draft.deeds"] = string.Join(",",(_draftDeeds??new List<BattleDeedDefinition>()).Select(d=>d.id));
            state["dice.faces"] = string.Join(",", _battle.Context.LastManaRolls);
            state["dice.count"] = _battle.Context.LastManaRolls.Count.ToString();
            state["mana.infinite"] = string.Join(",",_battle.Context.InfiniteMana.OrderBy(c=>(int)c));
            state["mana.allowBlackAtDay"] = _battle.Context.AllowBlackAtDay.ToString();
            state["mana.allowGoldAtNight"] = _battle.Context.AllowGoldAtNight.ToString();
            state["deck.count"] = p.Deck.DrawPileCount.ToString();
            state["attacks.selected"] = SelectedAttackParts(_targets.OrderBy(i => i).ToArray()).Length.ToString();
            state["attacks.reserved"] = _battle.Context.CombatPower.Attacks.Count(a => _reservedAttacks.Any(r => ReferenceEquals(r, a))).ToString();
            state["ui.attackSources"] = string.Join(",", _battle.Context.CombatPower.Attacks.Select(AttackSource));
            state["attacks.sources"] = string.Join(",", _battle.Context.CombatPower.Attacks.Select(a => a.SourceCard is DeedCard c ? c.Id : a.SourceSkill?.SourceId ?? a.SourceUnit?.Card.Id ?? "effect"));
            // Selecting a previously blocked attack is a legal intermediate
            // UI state while cycling to another attack of the same enemy.
            state["blocks.selected"] = (_battle.Phase == Phase.Block && _targets.Count == 1
                && !_battle.IsBlocked(_targets.Single(), _attackSlot) ? SelectedBlockParts().Length : 0).ToString();
            state["ui.blockSources"] = string.Join(",", _battle.Context.CombatPower.Blocks.Select(BlockSource));
            state["blocks.sources"] = string.Join(",", _battle.Context.CombatPower.Blocks.Select(b => b.SourceCard is DeedCard c ? c.Id : b.SourceSkill?.SourceId ?? b.SourceUnit?.Card.Id ?? "effect"));
            state["hand.ids"] = string.Join(",", p.Deck.Hand.Select(c => c.Id));
            state["healing.grail"] = _battle.Context.Healing.Grail.ToString();
            state["healing.grailFirst"] = _grailFirst.ToString();
            state["drawPerHeal"] = _battle.Context.DrawPerHeal.ToString();
            state["bow.active"] = _battle.Context.StarBowActive.ToString();
            state["bow.fame"] = _battle.Context.BowFamePerKill.ToString();
            state["bow.choices"] = string.Join(",", _battle.Context.CombatPower.Attacks.Select(a => a.StarBowChoice));
            state["ui.bowPower"] = texts.FirstOrDefault(t => t.name == "BowPower")?.text ?? "";
            state["units.resistAll"] = _battle.Context.UnitsResistAll.ToString();
            state["attacks.coldFire"] = _battle.Context.AllAttacksColdFire.ToString();
            state["attacks.siege"] = _battle.Context.AllAttacksSiege.ToString();
            state["unit.boost"] = _unitBoost.ToString();
            state["unit.conversion"] = _unitConversion.ToString();
            state["unit.manaFirst"] = _unitManaFirst.ToString();
            state["ui.unitManaChoices"] = string.Join(",", _root.GetComponentsInChildren<Button>().Where(b => b.name == "Btn_UnitManaFirst" || b.name == "Btn_UnitManaSecond").Select(b => b.GetComponentInChildren<TextMeshProUGUI>().text));
            state["unit.manaSecond"] = _unitManaSecond.ToString();
            state["healing"] = _battle.Context.Healing.Total.ToString();
            state["support.target"] = _supportTarget.ToString();
            state["horn.bonus"] = _hornBonus.ToString();
            state["replay.seed"] = Loadout.replayEffectRandom ? Loadout.effectRandomSeed.ToString() : "random";
            state["headband.remaining"] = (_journey?.Context?.PendingHeadbandBorrow?.Remaining??0).ToString();
            state["headband.skill"] = _journey?.Context?.PendingHeadbandBorrow?.Skill.SourceId??"";
            state["hero.id"] = _battle.Player.HeroId;
            state["art.hero.source"] = _root.Find("Hero")?.GetComponent<RawImage>()?.texture?.name ?? "";
            state["art.hero.visible"] = (_root.Find("Hero")?.gameObject.activeInHierarchy==true).ToString();
            state["ui.heroActor.texture"] = _root.Find("Btn_HeroActor/PartyFigure")?.GetComponent<RawImage>()?.texture?.name ?? "";
            for (int i = 0; i < _battle.Player.Units.Count; i++)
            {
                var displayed = _root.Find("Btn_UnitActor_" + i + "/PartyFigure")?.GetComponent<RawImage>()?.texture;
                state["ui.unitActor." + i + ".texture"] = displayed?.name ?? "";
                state["ui.unitActor." + i + ".resourceVersion"] = CharacterAssetVersion(displayed, "UnitCharacters", _battle.Player.Units[i].Card.Id);
            }
            state["hero.preview"]=_campPicker==5?_draftHeroId:"";
            state["hero.previewCount"]=_campPicker==5?OriginalHeroes.Get(_draftHeroId).StartingCards.Count.ToString():"";
            state["hero.previewUnique"]=_campPicker==5?string.Join(",",OriginalHeroes.Get(_draftHeroId).UniqueCards):"";
            state["dayPart"] = _battle.Context.DayPart.ToString();
            state["faction.mode"] = _battle.Context.EnemyRewardMode.ToString();
            state["faction.fame"] = _battle.Context.EnemyRewards.TotalFame.ToString();
            state["faction.count"] = _battle.Context.EnemyRewards.Count.ToString();
            state["enemies.armor"] = string.Join(",", _battle.Enemies.Select((e,i) => CombatMath.EffectiveArmor(e,i,_battle.Context)));
            state["enemies.defendApplied"] = string.Join(";", _battle.Context.EnemyDefense.Applied.Select(a =>
                Enumerable.Range(0,_battle.Enemies.Count).Single(i=>ReferenceEquals(_battle.Enemies[i],a.Source)) + ">" +
                Enumerable.Range(0,_battle.Enemies.Count).Single(i=>ReferenceEquals(_battle.Enemies[i],a.Target)) + ":" + a.Value));
            state["enemies.defendSelected"] = string.Join(";", _battle.Context.EnemyDefense.Selected.Select(a =>
                Enumerable.Range(0,_battle.Enemies.Count).Single(i=>ReferenceEquals(_battle.Enemies[i],a.Source)) + ">" +
                Enumerable.Range(0,_battle.Enemies.Count).Single(i=>ReferenceEquals(_battle.Enemies[i],a.Target)) + ":" + a.Value));
            state["enemies.attacks"] = string.Join(";", _battle.Enemies.Select((e, i) => string.Join(",",
                Enumerable.Range(0, e.AttackCount).Select(a => CardCombatActions.DefenseProfile(e, i, _battle.Context, a).Attack))));
            state["attacks.cancelled"] = string.Join(",", _battle.Context.CancelledAttacks.OrderBy(a => a.Target).ThenBy(a => a.Attack).Select(a => a.Target + ":" + a.Attack));
            state["enemies.skipped"] = string.Join(",", _battle.Context.SkipAttackIndices.OrderBy(i => i));
            state["attacks.successfullyBlocked"] = string.Join(",", _battle.Context.SuccessfullyBlockedAttacks.OrderBy(a => a.Target).ThenBy(a => a.Attack).Select(a => a.Target + ":" + a.Attack));
            state["enemies.coldStareStopped"] = string.Join(",", Enumerable.Range(0,_battle.Enemies.Count).Where(i=>_battle.Context.ColdStareSkips(_battle.Enemies[i])));
            state["enemies.fortified"] = string.Join(",", _battle.Enemies.Select((e,i) => CombatMath.IsFortified(e,i,_battle.Context)));
            state["enemies.resistancesRemoved"] = string.Join(",", _battle.Context.ResistancesRemoved.OrderBy(i => i));
            state["reduction.remaining"] = _battle.Context.AttackReductionTimes.ToString();
            state["reduction.pending"] = string.Join(",", _battle.Context.PendingAttackReductions.OrderBy(e=>e.Key.Item1).ThenBy(e=>e.Key.Item2)
                .Select(e=>$"{e.Key.Item1}:{e.Key.Item2}={e.Value}"));
            state["attackSlot"] = _attackSlot.ToString();
            state["explosive.pending"]=_battle.Context.ExplosiveBoltArmorUses.ToString();
            state["units.physicalResist"]=string.Join(",",_battle.Player.Units.Select(u=>u.PhysicalResistTemp));
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White, ManaColor.Green, ManaColor.Gold, ManaColor.Black })
                state["tokens." + color.ToString().ToLowerInvariant()] = Token(color).ToString();
            state["skills.ids"] = string.Join(",", p.Skills.Select(s => s.SourceId));
            state["duel.target"] = _battle.Context.Duel.EnemyTarget?.Id??"";
            state["duel.unitAffected"] = _battle.Context.Duel.TargetAffectedByUnit.ToString();
            state["duel.attackGranted"] = _battle.Context.Duel.AttackIsGranted.ToString();
            state["power.boundAttacks"] = string.Join(",",_battle.Context.CombatPower.Attacks.Where(a=>a.RequiredEnemy!=null).Select(a=>a.RequiredEnemy.Id+":"+a.Element+":"+a.Value));
            state["skills.used"] = string.Join(",", p.Skills.Select(s => SkillActions.IsUsed(p, s)));
            state["skills.flipped"]=string.Join(",",p.FlippedSkills.OrderBy(s=>s));
            state["ui.skillArtCount"] = _root.GetComponentsInChildren<RawImage>().Count(i => i.name == "OriginalSkillArt" && i.texture != null).ToString();
            state["ui.skillRedrawnIconCount"] = _root.GetComponentsInChildren<RawImage>().Count(i => i.name == "RedrawnSkillIcon" && i.texture != null).ToString();
            state["ui.skillOriginalPictogramCount"] = _root.GetComponentsInChildren<RawImage>().Count(i => i.name == "OriginalSkillPictogram").ToString();
            state["ui.embeddedBattle"] = embeddedBattleUi.ToString();
            state["ui.embeddedTooltipVisible"] = _root.Find("BattleHoverInfo") != null ? "True" : "False";
            for (int i = 0; i < _battle.Enemies.Count; i++)
            {
                state["ui.enemyArt." + i + ".texture"] = _root.Find("Enemy_" + i + "/Btn_Target/EnemyIllustration")?.GetComponent<RawImage>()?.texture?.name ?? "";
                state["ui.enemyArt." + i + ".characterBound"] = (Resources.Load<Texture2D>("UI/EnemyCharacters/embedded_v1/" + _battle.Enemies[i].Id) != null).ToString();
                state["ui.enemyArt." + i + ".resourceVersion"] = CharacterAssetVersion(_root.Find("Enemy_" + i + "/Btn_Target/EnemyIllustration")?.GetComponent<RawImage>()?.texture, "EnemyCharacters", _battle.Enemies[i].Id);
            }
            state["ui.cardArtCount"] = _root.GetComponentsInChildren<Image>().Count(i => i.name == "OriginalArt" && i.sprite != null).ToString();
            state["ui.enemyArtCount"] = _root.GetComponentsInChildren<RawImage>().Count(i => i.name == "OriginalToken" && i.texture != null).ToString();
            state["source.units"] = string.Join(",", p.Units.Select(u => u.Card.Id));
            state["units.count"] = p.Units.Count.ToString();
            state["draft.units"] = string.Join(",", (_draftUnits ?? new List<BattleUnitDefinition>()).Select(u => u.originalId));
            state["catalog.units"] = string.Join(",", unitCatalog.Select(u => u.originalId));
            state["catalog.visibleUnits"] = _campPicker == 3 ? string.Join(",", unitCatalog.Skip(_pickerPage * PickerPageSize).Take(PickerPageSize).Select(u => u.originalId)) : "";
            state["draft.commandSlots"] = DraftCommandSlots.ToString();
            state["units.otherAttackBonus"] = string.Join(",", p.Units.Select(u => _battle.Context.OtherUnitAttackBonus(u)));
            state["units.forcedDamage"] = ForcedDamageText();
            state["units.ready"] = string.Join(",", p.Units.Select(u => u.IsReady));
            state["units.wounds"] = string.Join(",", p.Units.Select(u => u.Wounds));
            state["units.banners"] = string.Join(",", p.Units.Select(u => u.BannerEffectId ?? "none"));
            state["units.courageSpent"] = string.Join(",", p.Units.Select(u => u.CourageBannerSpent));
            state["source.forgone"] = p.ForgoSourceDiceThisTurn.ToString();
            state["source.usedThisTurn"] = (_battle.Context.ManaSource?.WasUsedThisTurn(p) ?? false).ToString();
            state["soul.pending"] = _battle.Context.SoulGuideBlockPending.ToString();
            state["power.attacks"] = string.Join(",",_battle.Context.CombatPower.Attacks.Select(a=>a.Type+":"+a.Element+":"+a.Value));
            state["power.blocks"] = string.Join(",",_battle.Context.CombatPower.Blocks.Select(b=>b.Element+":"+b.Value));
            state["units.canActivate"] = string.Join(",", p.Units.Select(u => u.CanActivate));
            state["units.fortitudeSpent"] = string.Join(",", p.Units.Select(u => u.FortitudeBannerSpent));
            state["units.fortitudeSelected"] = string.Join(",", p.Units.Select(u => u.FortitudeProtectionSelected));
            state["units.armor"] = string.Join(",", p.Units.Select(u => u.Card.Armor + _battle.Context.UnitArmorBonus + BannerActions.HonorBonus(_battle.Context,u) + BannerActions.ProtectionArmor(u)));
            state["source.artifacts"] = string.Join(",", (Loadout.artifacts ?? Array.Empty<BattleArtifactDefinition>()).Select(a => a.source.Id));
            state["discard.ids"] = string.Join(",", p.Deck.DiscardPile.Select(c => c.Id));
            state["removed.ids"] = string.Join(",", p.Deck.RemovedCards.Select(c => c.Id));
            state["material.ids"] = string.Join(",", _materials.Select(c => c.Id));
            state["played.ids"] = string.Join(",", p.Deck.PlayedCards.Select(c => c.Id));
            state["units.plan"] = string.Join(";", _battle.DamagePlan.Select(a => a.TargetIndex + ":" + a.AttackIndex + ":" + string.Join(",", a.Units.Select(u => u.Card.Id))));
            state["ui.unitArtCount"] = _root.GetComponentsInChildren<Image>().Count(i => i.name == "OriginalUnitArt" && i.sprite != null).ToString();
            foreach (string name in new[] { "Heading", "Details", "TargetPreview", "Feedback", "Rules" })
                state["ui." + name] = texts.FirstOrDefault(t => t.name == name)?.text ?? "";
            if (_journey != null) foreach (var pair in CaptureJourneyState(texts)) state[pair.Key] = pair.Value;
            return state;
        }
    }
}

