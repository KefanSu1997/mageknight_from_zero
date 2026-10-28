using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MageKnight.Adventure.Presentation;
using MageKnight.SceneAutomation;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using Monster = MK.Logic.Data.Monster;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

/// <summary>
/// A scene-based audit of the existing ActionSystem. It does not repair results,
/// simulate missing consumers, or derive expectations from implementation output.
/// </summary>
public sealed class OriginalCardVerificationController : MonoBehaviour, ISceneAutomationStateSource
{
    public string batch = "basic_card";
    private CardVerificationCatalog _catalog;
    private CardVerificationCase[] _cases;
    private CardVerificationCase _case;
    private CardVerificationCatalog.Entry _entry;
    private readonly List<CardVerificationResult> _results = new();
    private PlayerState _player;
    private ActionContext _context;
    private GameState _battleGame;
    private GameEngine _roundEngine;
    private TurnMachine _battleMachine;
    private List<Monster> _battleEnemies;
    private ActionSystem _system;
    private UnitState[] _damageUnits;
    private readonly List<UnitCard> _recruitOffer = new();
    private Image _recruitArt;
    private Image _materialArt;
    private bool IsSupplyExchange => _case.cardId == "items_013" || (_case.cardId == "advanced_card_027" && _case.playFromHand);
    private bool IsRitualLifecycle => _case.cardId == "advanced_card_029" && _case.playFromHand;
    private bool IsLearning => _case.cardId == "advanced_card_026" && _case.playFromHand;
    private bool IsBloodline => _case.cardId == "advanced_card_030" && _case.playFromHand;
    private bool IsDecompose => _case.playFromHand && (_case.cardId == "advanced_card_020"
        || (IsBloodline && _case.enhanced && _case.advancedOffer!=null && _case.option>=0
            && _case.option<_case.advancedOffer.Length && _case.advancedOffer[_case.option]=="advanced_card_020"));
    private string _wisdomOfferId;
    private int _wisdomOfferIndex;
    private Image _guardArt;
    private Image _secondGuardArt;
    private TextMeshProUGUI _guardInfo;
    private int _index;
    private int _operationIndex;
    private readonly Dictionary<string, string> _operationResults = new();
    private sealed class ScriptedRollRandom : System.Random
    {
        private readonly Queue<int> _faces;
        public ScriptedRollRandom(IEnumerable<int> faces) => _faces = new Queue<int>(faces);
        public override int Next(int maxValue)
        {
            if (_faces.Count == 0) throw new InvalidOperationException("验收骰面序列已耗尽");
            int face = _faces.Dequeue();
            if (face < 0 || face >= maxValue) throw new InvalidOperationException("验收骰面超出实际骰子范围");
            return face;
        }
    }
    private bool _selected;
    private bool _executed;
    private bool _played;
    private Dictionary<string, string> _playedState;
    private CardCombatActions.Result _combatResult;
    private string _started;
    private string _suiteSha256;
    private string _outputRoot;
    private DeedCard[] _artifactFixtureInstances = Array.Empty<DeedCard>();
    private DeedCard[] _mindInitialInstances = Array.Empty<DeedCard>();
    private DeedCard[] _mindChosenInstances = Array.Empty<DeedCard>();
    private SkillCard _headbandSelected;
    private SkillCard[] _headbandOfferInstances = Array.Empty<SkillCard>();
    private Image _headbandSkillArt;
    private Sprite _headbandSkillCrop;
    private readonly List<int> _warpTurnOrder = new();
    private readonly List<MapTile> _warpExplored = new();
    private MapTile[] _warpOriginalTiles = Array.Empty<MapTile>();
    private ActionContext _engineTurnContext;
    private Monster _warpGuard;
    private ArtifactCard[] _boxSupplyOriginal = Array.Empty<ArtifactCard>();
    private readonly List<ArtifactCard> _boxSupplyProbe = new();
    private UnitCard _boxRecruitOriginal;
    private sealed class QuietExplorationRandom : System.Random
    { public override int Next(int maxValue) => maxValue - 1; }
    private SpellCard[] _originalSpellOffer = Array.Empty<SpellCard>();
    private ManaDie[] _originalManaDice = Array.Empty<ManaDie>();
    private readonly List<ActionContext> _otherContexts = new();
    private int _checkpoint;
    private Dictionary<string, string> _before;
    private TextMeshProUGUI _title, _printed, _fixture, _comparison, _status, _limits;
    private Image _art;
    private Button _play;
    public string LastRule => _printed != null ? _printed.text : "";
    private static readonly string[] ItemEffects =
    {
        "HonorBanner", "FearBanner", "FortitudeBanner", "CourageBanner", "RubyRing", "SapphireRing",
        "DiamondRing", "EmeraldRing", "JusticeSword", "WrathHorn", "GoldPouch", "GemBag", "HolyGrail",
        "WisdomBook", "SunAmulet", "DarkAmulet", "StarBow", "CommandBanner", "ToughBanner",
        "SoulHarvester", "NecroShield", "DruidStaff", "Headband", "SpellTome", "MysticBox"
    };
    private readonly Dictionary<string, string> _labels = new()
    {
        ["MovementPool"] = "移动力", ["InfluencePool"] = "影响力", ["MeleePool"] = "近战攻击",
        ["RangedPool"] = "远程攻击", ["BlockPool"] = "格挡", ["Wounds"] = "创伤计数",
        ["handWounds"] = "手牌伤牌", ["hand"] = "手牌张数", ["discard"] = "弃牌张数",
        ["Fame"] = "名望", ["Reputation"] = "声誉", ["unitReady"] = "目标部队就绪",
        ["unitWounds"] = "目标部队创伤", ["unitLevel"] = "目标部队等级",
        ["originalArtwork"] = "原卡面绑定", ["printedEffectPresent"] = "印刷效果文本",
        ["attackElement"] = "攻击元素", ["blockElement"] = "格挡元素", ["SiegePool"] = "攻城攻击",
        ["executionException"] = "操作合法性",
        ["move:selectedCost"] = "选中格费用", ["move:otherCost"] = "同类另一格费用",
        ["position:q"] = "位置Q", ["position:r"] = "位置R", ["readyUnits"] = "就绪部队数",
        ["unitCanActivate"] = "目标可发动",
        ["combat:printed"] = "出牌总值", ["combat:effective"] = "折算后有效值", ["combat:required"] = "所需数值",
        ["combat:kills"] = "击杀数", ["combat:fame"] = "结算名望", ["combat:wounds"] = "敌人造成创伤"
    };

    private void Awake()
    {
        CardJsonLoader.SetBaseDirectory(Path.Combine(Application.dataPath, "../resources/text_json"));
        _system = new ActionSystem();
        _catalog = Resources.Load<CardVerificationCatalog>("CardVerification/Catalog");
        using (var sha = System.Security.Cryptography.SHA256.Create())
            _suiteSha256 = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(_catalog.cases.text.Replace("\r\n", "\n")))).Replace("-", "").ToLowerInvariant();
        _cases = JsonUtility.FromJson<CardVerificationSuite>(_catalog.cases.text).cases.Where(c => c.batch == batch).ToArray();
        var selectedCases = SceneAutomationRuntimeState.PendingRequest?.caseIds;
        if (selectedCases?.Length > 0)
        {
            var requested = new HashSet<string>(selectedCases);
            _cases = _cases.Where(c => requested.Contains(c.id)).ToArray();
            if (requested.Count != selectedCases.Length || _cases.Length != requested.Count)
                throw new InvalidOperationException("指定用例重复或不属于当前场景，拒绝不完整验收");
        }
        _started = DateTime.UtcNow.ToString("o");
        string reportPath = SceneAutomationRuntimeState.PendingRequest?.reportPath;
        _outputRoot = string.IsNullOrEmpty(reportPath)
            ? Path.Combine(Application.dataPath, "../AutomationOutputs/AllOriginalCards/manual_" + Guid.NewGuid().ToString("N"), batch)
            : Path.GetDirectoryName(Path.GetFullPath(reportPath));
        Directory.CreateDirectory(Path.Combine(_outputRoot, "checkpoints"));
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var root = W.Panel(canvasObject.transform, "UIRoot", 0, 0, 1920, 1080, new Color32(12, 24, 27, 255));
        W.Text(root, "Heading", "魔法骑士 · 原版卡效果验收", 48, 28, 1600, 52, 34, W.Gold);
        _title = W.Text(root, "Case", "", 48, 84, 1816, 62, 26, W.Cream);
        W.Panel(root, "CardFrame", 48, 159, 500, 790, W.Ink, true);
        _art = W.Sprite(root, "OriginalCard", null, 66, 172, 464, 749);
        W.Text(root, "Source", "原卡面 · Assets/GameData/cards", 64, 945, 500, 44, 20, W.Muted);
        _printed = W.Text(root, "PrintedRule", "", 590, 158, 1266, 188, 25, W.Cream);
        _printed.enableAutoSizing = true; _printed.fontSizeMin = 18; _printed.fontSizeMax = 25;
        _fixture = W.Text(root, "Fixture", "", 590, 360, 1266, 68, 21, W.Muted);
        _comparison = W.Text(root, "Comparison", "", 590, 442, 1266, 336, 23, W.Cream);
        _recruitArt = W.Sprite(root, "RecruitOriginalCard", null, 1536, 444, 280, 392);
        _recruitArt.gameObject.SetActive(false);
        _materialArt = W.Sprite(root, "MaterialOriginalCard", null, 1240, 444, 250, 350);
        _materialArt.gameObject.SetActive(false);
        _headbandSkillArt = W.Sprite(root, "HeadbandOriginalSkill", null, 1230, 170, 620, 160);
        _headbandSkillArt.gameObject.SetActive(false);
        _guardArt = W.Sprite(root,"OriginalGuardToken",null,68,782,154,154);
        _secondGuardArt = W.Sprite(root,"SecondOriginalGuardToken",null,148,782,72,72);
        _guardInfo = W.Text(root,"GuardSource","",240,786,290,154,18,W.Cream);
        _status = W.Text(root, "Result", "", 590, 786, 1266, 50, 27, W.Gold);
        _limits = W.Text(root, "Coverage", "", 590, 845, 1266, 104, 20, W.Muted);
        W.Button(root, "Btn_Select", "选择卡牌与目标", 590, 967, 260, 65).onClick.AddListener(Select);
        _play = W.Button(root, "Btn_Play", "执行所选效果", 870, 967, 350, 65, true);
        _play.onClick.AddListener(Execute);
        W.Button(root, "Btn_Next", "下一验收案例", 1240, 967, 285, 65).onClick.AddListener(Next);
        W.Button(root, "Btn_Reset", "重置本例", 1545, 967, 285, 65).onClick.AddListener(LoadCase);
        LoadCase();
    }

    private void LoadCase()
    {
        _case = _cases[_index];
        _materialArt.gameObject.SetActive(false);
        _wisdomOfferId = null;
        _wisdomOfferIndex = _case.option;
        _entry = _catalog.cards.Single(e => e.source.Id == _case.cardId);
        _selected = _executed = _played = false;
        _playedState = null; _combatResult = null;
        _operationIndex = 0; _operationResults.Clear();
        _recruitOffer.Clear();
        _otherContexts.Clear();
        _originalManaDice = Array.Empty<ManaDie>();
        foreach (string id in _case.recruitOffer ?? Array.Empty<string>()) _recruitOffer.Add(CardJsonLoader.LoadUnit(id));
        _recruitArt.gameObject.SetActive(_recruitOffer.Count > 0);
        if (_recruitOffer.Count > 0) _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == _recruitOffer[Math.Clamp(_case.option,0,_recruitOffer.Count-1)].Id);
        _comparison.rectTransform.sizeDelta = new Vector2(_case.recruitOffer != null || _case.cardId == "advanced_card_025" ? 900 : 1266, 336);
        _play.GetComponentInChildren<TextMeshProUGUI>().text = "执行所选效果";
        _player = new PlayerState(1, "验收玩家");
        if (_case.ongoingBlackMana > 0) _player.TokensPerTurn[ManaColor.Black] = _case.ongoingBlackMana;
        _art.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,_case.enemySupplyIds != null ? 590 : 749);
        _guardArt.gameObject.SetActive(false); _secondGuardArt.gameObject.SetActive(false); _guardInfo.text="";
        _context = new ActionContext { CurrentTerrain = TerrainType.Forest, DayPart = _case.enhanced && _entry.source is SpellCardSO ? DayPart.Night : DayPart.Day };
        if (_case.recruitOffer != null) _context.UnitOffer = _recruitOffer;
        foreach (ManaColor color in Enum.GetValues(typeof(ManaColor)))
        {
            _player.Mana.AddToken(color, 2);
            if (color <= ManaColor.White) _player.Mana.AddCrystal(color, 1);
        }
        var source = new DeedCard(_case.cardId, _entry.source is SpellCardSO ? CardType.Spell : _entry.source is ItemCardSO ? CardType.Artifact : CardType.Action);
        _player.Deck.Hand.Add(source);
        _player.Deck.Hand.Add(new DeedCard("basic_card_021", CardType.Action));
        _player.Deck.Hand.Add(new DeedCard("basic_card_014", CardType.Action));
        for (int i = 0; i < 3; i++) _player.Deck.Hand.Add(new DeedCard("wound_" + i, CardType.Wound));
        // Limit the default fixture hand before adding explicitly requested original cards.
        if (_case.initialHandCount >= 0 && _case.initialHandCount < _player.Deck.Hand.Count)
            _player.Deck.Hand.RemoveRange(_case.initialHandCount, _player.Deck.Hand.Count - _case.initialHandCount);
        foreach (string id in _case.extraHandCards ?? Array.Empty<string>())
        {
            if (id == "w")
            {
                _player.Deck.Hand.Add(new DeedCard("w", CardType.Wound));
                continue;
            }
            var original = _catalog.cards.Single(e => e.source.Id == id).source;
            _player.Deck.Hand.Add(new DeedCard(id, original is SpellCardSO ? CardType.Spell
                : original is ItemCardSO ? CardType.Artifact : CardType.Action));
        }
        _artifactFixtureInstances = _player.Deck.Hand.ToArray();
        for (int i = 0; i < _case.initialDrawCount; i++) _player.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
        foreach (string id in _case.initialDrawCards ?? Array.Empty<string>())
            _player.Deck.PutUnder(new DeedCard(id, id == "w" ? CardType.Wound
                : id.StartsWith("magic_") ? CardType.Spell : id.StartsWith("items_") ? CardType.Artifact : CardType.Action));
        foreach (string id in _case.initialDiscardCards ?? new[] { "basic_card_008", "basic_card_016" })
            _player.Deck.GainToDiscard(new DeedCard(id, id == "w" ? CardType.Wound : CardType.Action));
        // A declared fixture, not a new gameplay card: two exhausted level-2 units.
        var unit = new UnitCard("verification_unit", "验收部队", "Fixture", 2, 4, 6, RecruitLocation.Village,
            Array.Empty<AttackProfile>(), Array.Empty<Ability>());
        if (_case.unitIds?.Length > 0)
            foreach (string id in _case.unitIds) _player.Units.Add(new UnitState(CardJsonLoader.LoadUnit(id)));
        else
            foreach (int level in _case.unitLevels ?? new[] { 2, 2 })
                _player.Units.Add(new UnitState(new UnitCard(unit.Id, unit.NameCn, "Fixture", level, 4, 6,
                    RecruitLocation.Village, Array.Empty<AttackProfile>(), Array.Empty<Ability>())));
        _damageUnits = _player.Units.ToArray();
        foreach (var u in _player.Units) u.Exhaust();
        _player.Units[0].AddWounds(_case.unitWounds);
        if (_case.initialUnitWounds != null)
            for (int i = 0; i < _case.initialUnitWounds.Length; i++) _player.Units[i].AddWounds(_case.initialUnitWounds[i]);
        if (_case.unitInitiallyReady) _player.Units[0].Ready();
        foreach (int index in _case.initialReadyUnitIndices ?? Array.Empty<int>()) _player.Units[index].Ready();
        if (_case.unitDestroyed) _player.Units[0].Destroy();
        if (_case.disbandUnitIndex >= 0) _context.UnitToDisband = _player.Units[_case.disbandUnitIndex];
        if (_case.foreignDisband) _context.UnitToDisband = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
        foreach (var otherFixture in _case.otherPlayers ?? Array.Empty<CardVerificationOtherPlayer>())
        {
            var other = _case.useTurnEngine ? new PlayerState(_context.OtherPlayers.Count + 2, "后续玩家") : new PlayerState();
            other.Position = new AxialCoord(otherFixture.q, otherFixture.r);
            foreach (var id in otherFixture.unitIds ?? Array.Empty<string>()) other.Units.Add(new UnitState(CardJsonLoader.LoadUnit(id)));
            foreach (var crystal in otherFixture.crystals ?? Array.Empty<CardVerificationValue>())
                other.Mana.AddCrystal((ManaColor)Enum.Parse(typeof(ManaColor), crystal.key), int.Parse(crystal.value));
            foreach (var token in otherFixture.tokens ?? Array.Empty<CardVerificationValue>())
                other.Mana.AddToken((ManaColor)Enum.Parse(typeof(ManaColor), token.key), int.Parse(token.value));
            foreach (string id in otherFixture.extraHandCards ?? Array.Empty<string>())
                other.Deck.Hand.Add(new DeedCard(id, id == "w" ? CardType.Wound
                    : id.StartsWith("magic_") ? CardType.Spell : CardType.Action));
            foreach (int i in otherFixture.exhaustedIndices ?? Array.Empty<int>()) other.Units[i].Exhaust();
            foreach (int i in otherFixture.destroyedIndices ?? Array.Empty<int>()) other.Units[i].Destroy();
            _context.OtherPlayers.Add(other);
        }
        _context.MovementMap = new MapState();
        var destinationTerrain = string.IsNullOrEmpty(_case.destinationTerrain) ? TerrainType.Forest
            : (TerrainType)Enum.Parse(typeof(TerrainType), _case.destinationTerrain);
        foreach (var position in Enumerable.Range(0, _case.revealedDistance + 1).Select(q => new AxialCoord(q, 0)).Append(new AxialCoord(0, 1)))
            _context.MovementMap.Placed[position] = new MapTile(TileSet.Countryside, position.Q, new[] { destinationTerrain });
        _context.TravelPath.AddRange((_case.travelPath ?? Array.Empty<CardVerificationHex>()).Select(h => new AxialCoord(h.q, h.r)));
        foreach (var tile in _case.mapTerrains ?? Array.Empty<CardVerificationTerrain>())
            _context.MovementMap.Placed[new AxialCoord(tile.q,tile.r)] = new MapTile(TileSet.Countryside,tile.q,new[] { (TerrainType)Enum.Parse(typeof(TerrainType),tile.terrain) });
        _context.SpellTargetIndices.AddRange(_case.spellTargets ?? Array.Empty<int>());
        if (_case.enemySupplyIds != null)
        {
            var originals = CardJsonLoader.LoadMonsters();
            _context.EnemySupply = new EnemyTokenSupply(_case.enemySupplyIds.Select(id=>originals.Single(m=>m.Id==id) with {}));
        }
        foreach (var site in _case.mapPlaces ?? Array.Empty<CardVerificationPlace>())
        {
            var place = new PlaceState((PlaceType)Enum.Parse(typeof(PlaceType), site.type))
            {
                CityColor = string.IsNullOrEmpty(site.cityColor) ? CityColor.None : (CityColor)Enum.Parse(typeof(CityColor),site.cityColor),
                OwnerId = site.ownerId < 0 ? null : (int?)site.ownerId,
                GuardNeedsReplacement = site.guardNeedsReplacement,
                RuinToken = string.IsNullOrEmpty(site.ruinToken) ? null : (RuinTokenType?)Enum.Parse(typeof(RuinTokenType),site.ruinToken),
                Enemy = site.guardFromSupply ? _context.EnemySupply.Draw(EnemyTokenSupply.GuardColor((PlaceType)Enum.Parse(typeof(PlaceType),site.type)))
                    : site.enemyDefinition != null && !string.IsNullOrEmpty(site.enemyDefinition.id) ? site.enemyDefinition.Create()
                    : site.enemy ? new Monster("验收地点驻军", 4, 4, Element.Physical, 2, Array.Empty<Ability>()) : null
            };
            place.CityShields.AddRange(site.cityShields ?? Array.Empty<int>());
            foreach (string color in site.guardColors ?? Array.Empty<string>())
            {
                var guard = _context.EnemySupply.Draw((EnemyTokenColor)Enum.Parse(typeof(EnemyTokenColor), color));
                if (place.Enemy == null) place.Enemy = guard;
                else place.AdditionalGuards.Add(guard);
            }
            _context.MovementMap.Places[new AxialCoord(site.q, site.r)] = place;
        }
        var discard = _case.discardIndex >= 0 && _case.discardIndex < _player.Deck.Hand.Count ? _player.Deck.Hand[_case.discardIndex] : null;
        _context.CardToDiscard = _case.foreignDiscard && discard != null ? new DeedCard(discard.Id, discard.Type) : discard;
        var removal = _case.removeIndex >= 0 && _case.removeIndex < _player.Deck.Hand.Count
            ? _player.Deck.Hand[_case.removeIndex] : null;
        _context.CardToRemove = _case.foreignRemove && removal != null ? new DeedCard(removal.Id, removal.Type) : removal;
        _context.OverloadStepwise = _case.overloadStepwise;
        if (_case.overloadSelections != null)
        {
            DeedCard HandAt(int index) => index >= 0 && index < _player.Deck.Hand.Count ? _player.Deck.Hand[index] : null;
            _context.OverloadSelections = _case.overloadSelections.Select(selection =>
            {
                var target = HandAt(selection.removeIndex);
                return new RepeatedCardSelection {
                    Option = selection.option,
                    CardToRemove = selection.foreignRemove && target != null ? new DeedCard(target.Id, target.Type) : target,
                    CardToDiscard = HandAt(selection.discardIndex)
                };
            }).ToList();
        }
        _context.CardToRecycle = source;
        _context.CardsToDiscard.AddRange(_player.Deck.Hand.Skip(1).Take(2));
        if (_case.discardIndices != null)
        {
            _context.CardsToDiscard.Clear();
            foreach (int index in _case.discardIndices)
            {
                var target = _player.Deck.Hand[index];
                _context.CardsToDiscard.Add(_case.foreignDiscard ? new DeedCard(target.Id, target.Type) : target);
            }
        }
        _context.CardsFromDiscard.AddRange(_player.Deck.DiscardPile);
        if (_case.recoverDiscardIndices != null)
        {
            _context.CardsFromDiscard.Clear();
            foreach (int index in _case.recoverDiscardIndices)
            {
                var card = index < 0 ? source : _player.Deck.DiscardPile[index];
                _context.CardsFromDiscard.Add(_case.foreignRecovery ? new DeedCard(card.Id, card.Type) : card);
            }
        }
        _roundEngine=(_case.useRoundEngine || _case.useTurnEngine) ? new GameEngine(new[]{_player}.Concat(_context.OtherPlayers).ToArray(),5,
            map:_context.MovementMap, manaRandom:_case.manaSourceRolls == null ? null : new ScriptedRollRandom(_case.manaSourceRolls)) : null;
        _context.AdvActionSupply = _roundEngine?.Globals.AdvActions ?? new AdvActionSupply();
        _context.AdvActionSupply.SetDeck(new[] { new AdvActionCard("advanced_card_000"), new AdvActionCard("advanced_card_001"), new AdvActionCard("advanced_card_003") });
        _context.AdvActionSupply.Refill(false);
        if (_case.advancedOffer != null)
        {
            _context.AdvActionSupply.Offer.Clear();
            _context.AdvActionSupply.Offer.AddRange(_case.advancedOffer.Select(id=>new AdvActionCard(id)));
        }
        _context.SpellSupply = _roundEngine?.Globals.Spells ?? new SpellSupply();
        _context.SpellSupply.SetDeck((_case.spellOffer ?? new[] { "magic_009", "magic_015", "magic_005" }).Select(id => new SpellCard(id)));
        _context.SpellSupply.Refill();
        _originalSpellOffer = _context.SpellSupply.Offer.ToArray();
        if (_case.advancedReserve!=null)
            _context.AdvActionSupply.SetDeck(_case.advancedReserve.Select(id=>new AdvActionCard(id)));
        if (_case.spellReserve!=null)
            _context.SpellSupply.SetDeck(_case.spellReserve.Select(id=>new SpellCard(id)));
        if (IsSupplyExchange)
        {
            var offer=(_case.cardId == "items_013" && _case.enhanced ? _context.SpellSupply.Offer.Select(c=>c.Id)
                : _context.AdvActionSupply.Offer.Select(c=>c.Id)).ToArray();
            if (_case.option>=0 && _case.option<offer.Length) _wisdomOfferId=offer[_case.option];
        }
        _context.SkillSupply = new SkillSupply();
        _context.SkillSupply.SetDeck(CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")); _context.SkillSupply.Refill();
        _headbandSkillArt.gameObject.SetActive(false);
        _printed.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, IsHeadband ? 610 : 1266);
        if (IsHeadband)
        {
            var skills = CardJsonLoader.LoadHeroSkills(_case.skillPoolId);
            _context.SkillSupply.Offer.Clear();
            _context.SkillSupply.Offer.AddRange(_case.skillOfferIndices.Select(i => skills[i]));
            _headbandOfferInstances = _context.SkillSupply.Offer.ToArray();
            _headbandSelected = _headbandOfferInstances[_case.option];
            _context.HeadbandSkillOption = _case.headbandSkillOption;
            // Display a region of the existing original sheet; no new or redrawn skill asset.
            int sourceIndex = _case.skillOfferIndices[_case.option];
            if (_case.skillPoolId != "hero_skill_pool_000" || (sourceIndex != 0 && sourceIndex != 4))
                throw new InvalidOperationException("所选原技能的牌面区域尚未接入");
            var sheet = _catalog.originalSkillSheet000;
            var rect = sheet.rect;
            float top = sourceIndex == 0 ? 0 : 0.65f;
            float height = sourceIndex == 0 ? 0.18f : 0.17f;
            if (_headbandSkillCrop != null) Destroy(_headbandSkillCrop);
            _headbandSkillCrop = Sprite.Create(sheet.texture,
                new Rect(rect.x, rect.y + rect.height * (1 - top - height), rect.width, rect.height * height), new Vector2(0.5f, 0.5f));
            _headbandSkillCrop.name = _headbandSelected.SourceId;
            _headbandSkillArt.sprite = _headbandSkillCrop;
            _headbandSkillArt.gameObject.SetActive(true);
        }
        if (_case.skillIndices != null)
        {
            var skills = CardJsonLoader.LoadHeroSkills(_case.skillPoolId);
            foreach (int index in _case.skillIndices) _player.Skills.Add(skills[index]);
        }
        _context.ArtifactSupply = new ArtifactSupply();
        _boxSupplyOriginal = (_case.artifactSupplyIds ?? new[] { "items_004", "items_010" }).Select(id => new ArtifactCard(id)).ToArray();
        _context.ArtifactSupply.SetDeck(_boxSupplyOriginal);
        _boxSupplyProbe.Clear();
        _boxRecruitOriginal = IsMysticBox ? _recruitOffer.FirstOrDefault() : null;
        _context.ManaSource = _case.manaSourceRolls == null
            ? new ManaSource(1, () => _context.DayPart, new System.Random(20260910))
            : _roundEngine?.Globals.Mana ?? throw new InvalidOperationException("验收公共骰必须绑定同一GameEngine");
        if (_case.manaSourceRolls != null) _originalManaDice = _context.ManaSource.Dice.ToArray();
        foreach (var other in _context.OtherPlayers)
            _otherContexts.Add(new ActionContext { ManaSource = _context.ManaSource, DayPart = _context.DayPart,
                MovementMap = _context.MovementMap, CurrentTerrain = _context.CurrentTerrain });
        if (_case.manaRolls != null) _context.EffectRandom = new ScriptedRollRandom(_case.manaRolls);
        _context.Enemies = new[] { new Monster("fixture_enemy", 5, 4, Element.Physical, 3, Array.Empty<Ability>()) };
        if (_case.enemies != null && _case.enemies.Length > 0)
            _context.Enemies = _case.enemies.Select(e => e.Create()).ToList();
        _context.EngagedEnemies = _context.Enemies.Count;
        foreach (var value in _case.setup ?? Array.Empty<CardVerificationValue>()) Set(value.key, value.value);
        foreach (string color in _case.bloodRitualColors ?? Array.Empty<string>())
            _context.BloodRitualColors.Add((ManaColor)Enum.Parse(typeof(ManaColor), color));
        _context.BloodRitualCrystalColor = string.IsNullOrEmpty(_case.bloodRitualCrystalColor)
            ? (ManaColor?)null : (ManaColor)Enum.Parse(typeof(ManaColor), _case.bloodRitualCrystalColor);
        _context.SelectedActionManaColor = string.IsNullOrEmpty(_case.selectedActionManaColor)
            ? (ManaColor?)null : (ManaColor)Enum.Parse(typeof(ManaColor), _case.selectedActionManaColor);
        _context.ManaPowerBonusChoices.AddRange(_case.manaPowerBonusChoices ?? Array.Empty<int>());
        if (_case.manaSourceFaces != null)
        {
            _originalManaDice = _context.ManaSource.Dice.ToArray();
            if (_case.manaSourceFaces.Length != _originalManaDice.Length)
                throw new InvalidOperationException("初始骰面必须逐一对应实际公共骰");
            for (int i = 0; i < _originalManaDice.Length; i++)
                _originalManaDice[i].TurnTo((ManaColor)Enum.Parse(typeof(ManaColor), _case.manaSourceFaces[i]));
            if (_case.manaPaymentDieIndex >= 0)
            {
                var feeDie = _originalManaDice[_case.manaPaymentDieIndex];
                CardManaActions.UseSourceDie(_player, _context, feeDie, feeDie.Face);
            }
            foreach (int index in _case.manaTransformIndices ?? Array.Empty<int>())
                _context.ManaDiceToTransform.Add(_originalManaDice[index]);
        }
        if (_case.manaSourceRolls != null && _roundEngine.Clock.DayPart != _context.DayPart)
            _roundEngine.Clock.NextRound();
        if (_case.cardId == "items_002") new PlayerTurnEngine().StartTurn(_player,_context.DayPart);
        _battleGame = null; _battleMachine = null; _battleEnemies = null;
        if (!string.IsNullOrEmpty(_case.turnMachineStartPhase))
        {
            _battleGame = new GameState(); _battleGame.Players.Add(_player);
            _battleMachine = new TurnMachine(_battleGame);
            _battleEnemies = _context.Enemies.ToList(); _context.Enemies = _battleEnemies;
            _battleMachine.BeginBattle(_context);
            if (_case.turnMachineStartPhase == "Block" || _case.turnMachineStartPhase == "Melee")
                _battleMachine.ApplyRanged(_battleEnemies, Array.Empty<AttackAllocation>(), _context);
            if (_case.turnMachineStartPhase == "Melee")
            {
                _battleMachine.SetBlock(0, Element.Physical);
                _battleMachine.ResolveDefense(_battleEnemies, Array.Empty<BlockAllocation>(), _context);
            }
        }
        _warpTurnOrder.Clear(); _warpExplored.Clear(); _warpGuard = null;
        _warpOriginalTiles = Array.Empty<MapTile>();
        if (IsWarp)
        {
            _warpGuard = _context.MovementMap.Places.Values.FirstOrDefault(s => s.Type == PlaceType.OrcRampaging)?.Enemy;
            _warpOriginalTiles = (_case.exploreTileIds ?? Array.Empty<int>()).Select(id =>
                new MapTile(TileSet.Countryside, id, Enumerable.Repeat(TerrainType.Plains, 6).ToArray())).ToArray();
            _context.MovementMap.Countryside.SetCards(_warpOriginalTiles);
            if (_case.useTurnEngine)
            {
                if (_roundEngine.Clock.DayPart != _context.DayPart) _roundEngine.Clock.NextRound();
                var players = new[] { _player }.Concat(_context.OtherPlayers).ToArray();
                _roundEngine.StartNewRound(players.Select((p, i) => new TacticCard("验收顺序" + p.Id, _context.DayPart, i + 1, 0)).ToArray());
                _engineTurnContext = _context;
                _warpTurnOrder.Add(_roundEngine.BeginNextTurn(_engineTurnContext).Id);
            }
        }
        if (IsMindRead)
        {
            var indices = _case.mindReadDiscardIndices ?? Array.Empty<int>();
            if (indices.Length != _context.OtherPlayers.Count)
                throw new InvalidOperationException("思维读取必须逐位选择实际手牌或展示手牌");
            _mindChosenInstances = _context.OtherPlayers.Select((p, i) => indices[i] < 0 ? null : p.Deck.Hand[indices[i]]).ToArray();
            for (int i = 0; i < _mindChosenInstances.Length; i++)
                _context.MindReadChoices[_context.OtherPlayers[i]] = _mindChosenInstances[i];
            _context.MindStealTarget = _case.mindStealPlayerIndex < 0 ? null : _mindChosenInstances[_case.mindStealPlayerIndex];
            _mindInitialInstances = new[] { _player }.Concat(_context.OtherPlayers).SelectMany(MindPlayerCards).ToArray();
        }
        if (IsColdStare && _case.option == 1 && _case.selectedEnemyIndex >= 0)
        {
            _context.SelectedEnemy = _context.Enemies[_case.selectedEnemyIndex];
            _context.SelectedEnemyAttackIndex = _case.selectedEnemyAttackIndex;
        }
        _before = Snapshot();
        _title.text = $"{_index + 1}/{_cases.Length}   {_entry.source.NameCn}   {_case.cardId}   ·   {_case.title}";
        _art.sprite = _entry.artwork;
        _printed.text = PrintedEffect();
        _fixture.text = $"初始：手牌{_before["hand"]}（伤牌{_before["handWounds"]}）· 弃牌{_before["discard"]} · 魔力 红{_before["token:Red"]}/蓝{_before["token:Blue"]}/绿{_before["token:Green"]}/白{_before["token:White"]}/金{_before["token:Gold"]}/黑{_before["token:Black"]}\n目标：2级耗竭部队 / 敌人护甲5、攻击4；{_context.CurrentTerrain}，{_context.DayPart}；选项 {_case.option}";
        _comparison.text = "独立规则预期（执行后绝对值）\n" + string.Join("\n", _case.expected.Take(9).Select(e => $"{Label(e.key)}：{e.value}"));
        if (_case.cardId == "magic_023") _fixture.text = WindFixture();
        if (_case.cardId == "items_008" || _case.cardId == "items_016") _fixture.text = WeaponFixture();
        _status.text = "待操作：先选择卡牌和目标，再执行";
        _limits.text = "范围：" + ((_case.limitations?.Length ?? 0) == 0 ? "本例的即时效果分支；整卡结论需汇总全部案例。" : string.Join("；", _case.limitations));
        if (!string.IsNullOrEmpty(_case.combatPhase))
            _fixture.text = "目标：" + string.Join(" / ", _context.Enemies.Select(e => $"{e.Id} 护甲{e.Armor} 攻击{e.Attack}({e.AttackElement}) [{string.Join(",", e.Abilities)}]"))
                + $"\n{_case.combatPhase}阶段 · 城防地点={_context.FortifiedSite} · {_context.DayPart} · 地形{_context.CurrentTerrain} · 移动费修正{(_context.TerrainCostOverride.TryGetValue(_context.CurrentTerrain, out int overrideCost) ? overrideCost.ToString() : "无")}";
        if (_case.unitIds?.Length > 0) _fixture.text = DamageFixture();
        if (_case.cardId == "advanced_card_029")
            _fixture.text = $"去除目标：{_context.CardToRemove?.Id ?? "未选择"} · 颜色{(_context.CardToRemove == null ? "无" : CardColorHelper.Get(_context.CardToRemove.Id)?.ToString() ?? "无")} · 实际手牌实例={_player.Deck.Hand.Any(c => ReferenceEquals(c, _context.CardToRemove))}"
                + $"\n手牌{_before["hand"]} / 弃牌{_before["discard"]} · 红色魔力{_before["token:Red"]} · {_case.combatPhase}阶段 · 城防地点={_context.FortifiedSite}";
        if (_case.cardId == "advanced_card_025")
        {
            _fixture.text = SpellFixture();
            ShowOfferedSpell(_case.enhanced ? _case.option >> 8 : 0);
        }
        if (_case.discardIndices != null)
            _fixture.text = "待弃手牌：" + string.Join(" / ", _context.CardsToDiscard.Select(c => c.Id))
                + $"\n手牌{_player.Deck.Hand.Count}，弃牌{_player.Deck.DiscardPile.Count}；神器依次选色编码{_case.option}；外来实例{_case.foreignDiscard}";
        if (_case.recruitOffer != null) _fixture.text = RecruitmentFixture();
        if (_case.cardId == "magic_002") _fixture.text = MeditationFixture();
        if (_case.cardId == "magic_004") _fixture.text = TunnelFixture();
        ShowGuardToken();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _case.cardId == "items_023" || IsManaArtifact ? 920 : 1266);
        if (IsManaArtifact) _fixture.text = GemRingFixture();
        if (_case.cardId == "items_023")
        {
            _fixture.text = TomeFixture();
            ShowOfferedSpell(_case.option);
        }
        if (IsHealingCard)
        {
            _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 920);
            _fixture.text = GrailFixture();
        }
        if (_case.cardId == "magic_000")
        {
            _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 920);
            _fixture.text = EnergyFixture();
        }
        _play.interactable = false;
        if (IsDiseaseScene)
        {
            _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 920);
            _fixture.text = DiseaseFixture();
        }
        if (IsSupplyExchange) ShowWisdomState("待选择材料与供给");
        if (IsRitualLifecycle) ShowRitualState("待选择原行动牌材料");
        if (IsLearning) ShowLearningState("出牌只获得影响力和本回合一次购牌能力；可稍后决定是否购买。");
        if (IsBloodline) ShowBloodlineState("选择真实高级行动供给，确认伤牌去向和所需费用。");
        if (IsDecompose) ShowDecomposeState("按实际原行动材料的颜色获得魔晶，材料永久移除。");
        if (IsOverload) ShowOverloadState("选择实际原行动材料，重复其原效果；红色宿主费用与材料印刷强化费用分开。");
        if (_case.basicAcceptance) ShowBasicAcceptanceState("选择原卡与目标，按原文执行基本效果");
    }

    private bool IsGemRing => _case.cardId == "items_004" || _case.cardId == "items_005"
        || _case.cardId == "items_006" || _case.cardId == "items_007";
    private bool IsBannerScenario => _case.cardId == "items_000" || _case.cardId == "items_001" || _case.cardId == "items_002" || _case.cardId == "items_003" || _case.cardId == "items_018" || (_case.cardId == "items_017" && !_case.enhanced);
    private bool IsCommandBannerScenario => _case.cardId == "items_017" && !_case.enhanced;
    private bool IsAmulet => _case.cardId == "items_014" || _case.cardId == "items_015";
    private bool IsManaArtifact => IsGemRing || _case.cardId == "items_011";
    private bool IsDiseaseScene => (_case.cardId == "magic_001" && _case.enhanced)
        || (_case.cardId == "basic_card_021" && _case.operations?.Any(o => o.cardId == "magic_001") == true);
    private bool IsHealingCard => _case.cardId == "items_012" || _case.cardId == "basic_card_001" || _case.cardId == "basic_card_005"
        || (_case.cardId == "basic_card_004" && _case.option == 0) || _case.cardId == "advanced_card_007" || _case.cardId == "advanced_card_015"
        || (_case.cardId == "magic_001" && !_case.enhanced)
        || _case.cardId == "magic_005"
        || (_case.cardId == "items_023" && _case.spellOffer?.Contains("magic_001") == true);

    private string GemRingFixture() => $"{_context.DayPart}；名望{_player.Fame}；"
        + (IsGemRing ? $"无尽魔力[{string.Join(",", _context.InfiniteMana)}]" : $"实际骰面[{string.Join(",", _context.LastManaRolls)}]；金色选色{_case.option & 255}/{(_case.option >> 8) & 255}")
        + $"\n手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 弃牌{_player.Deck.DiscardPile.Count} / 移除{_player.Deck.RemovedCards.Count}";

    private string PrintedEffect()
    {
        if (_entry.source is ActionCardSO action) return (_case.enhanced ? action.EnhancedEffect : action.BaseEffect) ?? "";
        if (_entry.source is SpellCardSO spell) return (_case.enhanced ? spell.Bottom?.Effect : spell.Top?.Effect) ?? "";
        if (_entry.source is ItemCardSO item) return IsMysticBox
            ? item.OncePerRound + "\n" + item.AssignEffect : (_case.enhanced ? item.OncePerRound : item.AssignEffect) ?? "";
        return "";
    }

    private ActionCardData Data(CardSO source, bool enhanced)
    {
        if (source is ActionCardSO action) return new ActionCardData(action.Id, action.NameCn, action.Set, action.ImagePath,
            action.EnImagePath, action.BaseEffect, action.EnhancedEffect, action.RequiredCrystals ?? Array.Empty<ManaColor>());
        if (source is SpellCardSO spell) return new ActionCardData(spell.Id, spell.NameCn, spell.Set, spell.ImagePath,
            spell.EnImagePath, spell.Top?.Effect, spell.Bottom?.Effect,
            (enhanced ? spell.Bottom?.ManaCost : spell.Top?.ManaCost) ?? Array.Empty<ManaColor>());
        var item = (ItemCardSO)source;
        return new ActionCardData(item.Id, item.NameCn, item.Set, item.ImagePath, item.EnImagePath,
            item.AssignEffect, item.OncePerRound, Array.Empty<ManaColor>());
    }

    private string DiseaseFixture() => $"{_context.DayPart}；战斗{_context.InBattle}；感染{_context.BlockedArmorOne}；"
        + $"绿色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)} / 黑色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}；格挡{_context.BlockPool} / 攻击{_context.MeleePool}\n"
        + string.Join("；",_context.Enemies.Select((enemy,i)=>$"敌人{i}：护甲{enemy.Armor}→{CombatMath.EffectiveArmor(enemy,i,_context)}，攻击{enemy.Attack}，[{string.Join(",",enemy.Abilities)}]"));

    private string EnergyFixture() => $"{_context.DayPart}；战斗{_context.InBattle}；绿色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)} / 黑色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}；实际单位治疗记录{_context.HealedUnitsThisTurn.Count}"
        + "\n其他玩家就绪：" + string.Join(" / ", _context.OtherPlayers.Select((p,i) => i+"：["+string.Join(",",p.Units.Select(u=>u.IsReady))+ "]"))
        + "；选中耗尽数" + _context.EnergyExhaustTargets.Count;

    private void Select()
    {
        if (_executed) return;
        _selected = true; _context.TargetUnit = _player.Units[0];
        if (_case.omitUnitTarget) _context.TargetUnit = null;
        else if (_case.foreignUnitTarget) _context.TargetUnit = new UnitState(_player.Units[0].Card);
        foreach (var choice in _case.exhaustTargets ?? Array.Empty<CardVerificationUnitTarget>())
        {
            var owner = choice.playerIndex < 0 ? _player : _context.OtherPlayers[choice.playerIndex];
            var target = owner.Units[choice.unitIndex];
            _context.EnergyExhaustTargets.Add(choice.foreign ? new UnitState(target.Card) : target);
        }
        if (_case.cardId == "magic_000" && _case.unitIds?.Length > 0)
        {
            _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == _player.Units[0].Card.Id);
            _recruitArt.gameObject.SetActive(true);
            _comparison.text = "自有重整目标：" + _player.Units[0].Card.NameCn
                + "\n可选耗尽目标：" + string.Join(" / ", _context.EnergyExhaustTargets.Select(u => u.Card.NameCn))
                + "\n未选的其他玩家部队保持原状；每位其他玩家至多选择一支。";
        }
        if (_case.readyUnitTargets != null)
        {
            _context.ReadyUnitTargets = _case.readyUnitTargets.Select(i => _player.Units[i]).ToList();
            if (_case.foreignUnitTarget && _context.ReadyUnitTargets.Count > 0)
                _context.ReadyUnitTargets[_context.ReadyUnitTargets.Count - 1] = new UnitState(_context.ReadyUnitTargets.Last().Card);
        }
        if (_case.cardId == "magic_005" && _case.unitIds?.Length > 0)
        {
            _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == _player.Units[0].Card.Id);
            _recruitArt.gameObject.SetActive(true);
            _comparison.text = "所选重整目标：" + string.Join(" / ", (_context.ReadyUnitTargets ?? new List<UnitState>()).Select(u => u.Card.NameCn + " 等级" + u.Card.Level))
                + "\n强化才重整，总等级不得超过地形额度；治疗点独立消费。";
        }
        if (((_case.cardId == "basic_card_005" && _case.option == 3) || _case.cardId == "advanced_card_015") && _case.unitIds?.Length > 0)
        {
            _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == _player.Units[0].Card.Id);
            _recruitArt.gameObject.SetActive(true);
            _comparison.text = $"重整目标：{_context.TargetUnit?.Card.NameCn ?? "未选择"}；等级{_player.Units[0].Card.Level}。\n"
                + $"本分支等级上限{(_case.enhanced ? 3 : 2)}，重整不移除伤牌。";
        }
        if (!_case.omitMovementTarget)
        {
            _context.TargetHex = new AxialCoord(1, 0);
            _context.TargetTerrain = _context.MovementMap.Placed[_context.TargetHex.Value].Edges[0];
        }
        _status.text = "已选择原卡和夹具目标，可以执行"; _play.interactable = true;
    }

    private void Execute()
    {
        if (!_selected || _executed) return;
        if (_played) { if ((_case.operations?.Length ?? 0) > 0) ResolveOperation(); else ResolveCombat(); return; }
        _before = Snapshot();
        string exception = "";
        string executionPath = "ActionSystem.Play";
        try
        {
            int option = string.IsNullOrEmpty(_case.effectChoice) ? _case.option : (int)Enum.Parse(typeof(ActionEffectId), _case.effectChoice);
            if (_entry.source is ItemCardSO itemSource && _case.playFromHand)
            {
                executionPath = "CardPlayActions.PlayArtifact / actual hand -> played or permanently removed";
                CardPlayActions.PlayArtifact(_player, _context, _player.Deck.Hand.First(c => c.Id == _case.cardId),
                    new ItemCardData(itemSource.Id, itemSource.NameCn, itemSource.Set, itemSource.ImagePath,
                        itemSource.EnImagePath, "Artifact", itemSource.AssignEffect, itemSource.OncePerRound), _case.enhanced, option);
            }
            else if (_entry.source is ItemCardSO)
            {
                // Items have existing effects but no ItemCardData play service. Audit
                // the real module explicitly; do not report this as game integration.
                int index = int.Parse(_entry.source.Id.Substring("items_".Length));
                var id = (ActionEffectId)Enum.Parse(typeof(ActionEffectId), ItemEffects[index] + (_case.enhanced ? "Once" : "Assign"));
                var effect = CardEffectFactory.Get(id);
                executionPath = "CardEffectFactory / " + effect.GetType().Name + " (module only)";
                effect.Execute(_player, _context, option);
            }
            else if (_case.playFromHand)
            {
                executionPath = "CardPlayActions.Play / actual hand -> played -> discard";
                CardPlayActions.Play(_player, _context, _player.Deck.Hand.First(c => c.Id == _case.cardId),
                    Data(_entry.source, _case.enhanced), _case.enhanced, option);
            }
            else _system.Play(Data(_entry.source, _case.enhanced), _player, _context, _case.enhanced, option);
            if (!string.IsNullOrEmpty(_case.followup))
            {
                var next = _catalog.cards.Single(e => e.source.Id == _case.followup);
                _system.Play(Data(next.source, _case.followupEnhanced), _player, _context, _case.followupEnhanced);
            }
        }
        catch (Exception e) { exception = e.GetType().Name + ": " + e.Message; }
        if (exception.Length == 0 && (_case.operations?.Length ?? 0) > 0)
        {
            _played = true; _playedState = Snapshot();
            ShowNextOperation();
            return;
        }
        if (exception.Length == 0 && !string.IsNullOrEmpty(_case.combatPhase))
        {
            _played = true;
            _playedState = Snapshot();
            _comparison.text = "已打出原卡：" + _entry.source.NameCn + "\n"
                + $"近战 {_context.MeleePool} / 远程 {_context.RangedPool} / 攻城 {_context.SiegePool} / 格挡 {_context.BlockPool}\n"
                + "攻击元素：" + _playedState["attackElement"] + "；格挡元素：" + _playedState["blockElement"]
                + "\n费用已支付，下一步对所选目标结算抗性、护甲、名望或创伤。";
            _status.text = "原卡效果已执行 · 等待战斗结算";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认目标并结算";
            return;
        }
        CompleteCase(exception, executionPath);
    }

    private void ShowNextOperation()
    {
        var operation = _case.operations[_operationIndex];
        if (_battleGame != null)
        {
            _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
            _fixture.text = $"正式阶段{_battleGame.CurrentPhase}，战斗{_context.InBattle}；敌人护甲[{string.Join(",",_battleEnemies.Select(e=>e.Armor))}]"
                + $"\n手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 移除{_player.Deck.RemovedCards.Count}";
            string action = operation.kind switch
            {
                "TurnRanged" => operation.option == 1 ? "放弃远程攻击并结束该阶段" : "确认远程攻击并结束该阶段",
                "TurnResolve" => "确认伤害与近战结算，结束战斗", "TurnBegin" => "开始下一场战斗",
                "TurnDefense" => "确认格挡与伤害，进入攻击阶段", "TurnAttack" => $"选择敌人{string.Join(",", (operation.attackTargets ?? new[] { operation.targetIndex }).Select(i => i + 1))}并消费攻击",
                "TurnFinish" => "不再攻击，结束战斗", "PlayCard" => "打出所选原卡：" + operation.cardId,
                "RangedAttack" => "尝试使用剩余远程攻击", "EndTurn" => "结束回合并检查卡牌去向", "StartTurn" => "开始玩家下一回合", _ => operation.kind
            };
            _comparison.text = $"下一步：{action}\n近战{_context.MeleePool} / 远程{_context.RangedPool} / 攻城{_context.SiegePool} / 格挡{_context.BlockPool}"
                + $"\n红{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)} 蓝{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)} 白{_player.Mana.Tokens.GetValueOrDefault(ManaColor.White)} 黑{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}魔力；伤牌{_player.Wounds} / 名望{_player.Fame} / 声誉{_player.Reputation}";
            if (operation.kind == "UnitUse")
            {
                var unit = _player.Units[operation.targetIndex];
                _art.sprite = _catalog.unitArtwork.Single(s=>s.name==unit.Card.Id);
                var ability = unit.Card.Actions[operation.option];
                _printed.text = unit.Card.NameCn + "：" + ability.PrintedRule;
                _comparison.text += $"\n确认部队能力{operation.option+1}；就绪{unit.IsReady}、伤牌{unit.Wounds}；魔力费用：{string.Join(",",ability.Cost)}";
            }
            else if (operation.kind == "SkillUse")
            {
                if (_case.skillPoolId != "hero_skill_pool_000") throw new InvalidOperationException("此技能池原图尚未接入");
                var skill = _player.Skills[operation.targetIndex];
                _art.sprite = _catalog.originalSkillSheet000;
                _printed.text = skill.Id + "：" + skill.PrintedRule;
                _comparison.text += $"\n确认自有技能{skill.SourceId}，选项{operation.option}；本次已用{SkillActions.IsUsed(_player,skill)}；昏迷{_context.IsKnockedOut}";
            }
            _status.text = $"等待确认操作 {_operationIndex+1}/{_case.operations.Length}";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认所示阶段操作";
            if (_case.basicAcceptance && (_case.cardId == "basic_card_003" || _case.cardId == "advanced_card_012" || _case.cardId == "advanced_card_013" || _case.crystalMasteryScenario || IsStormOrAmbush || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox))
                ShowBasicAcceptanceState("下一步：" + action);
            return;
        }
        if (_case.cardId == "magic_000")
        {
            _fixture.text = EnergyFixture();
            _comparison.text = "后续操作：" + operation.kind + "\n自有部队先实际重整，强化才治疗；其他玩家的部队只处理明确选择的目标。";
            if (operation.kind == "OtherUnitUse")
            {
                var unit = _context.OtherPlayers[operation.q].Units[operation.targetIndex];
                _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == unit.Card.Id);
                _comparison.text += $"\n玩家{operation.q}，部队{operation.targetIndex}：{unit.Card.NameCn}，就绪{unit.IsReady}。尝试发动原能力。";
            }
            else if (operation.kind == "PlayCard")
                _recruitArt.sprite = _catalog.cards.Single(c => c.source.Id == operation.cardId).artwork;
            _recruitArt.gameObject.SetActive(true);
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认并执行所选操作";
            return;
        }
        if (IsDiseaseScene)
        {
            _fixture.text = DiseaseFixture();
            _comparison.text = $"下一步：{operation.kind}，目标索引{operation.targetIndex}。\n"
                + "感染只影响实际成功格挡的敌人；奥术免疫保留护甲。失败格挡不触发减甲。";
            if (operation.kind == "PlayCard")
            {
                var next = _catalog.cards.Single(c => c.source.Id == operation.cardId);
                _recruitArt.sprite = next.artwork; _recruitArt.gameObject.SetActive(true);
                _comparison.text += $"\n实际打出{next.source.NameCn}，{(operation.enhanced ? "强化" : "基础")}选项{operation.option}。";
            }
            _status.text = $"等待确认操作 {_operationIndex + 1}/{_case.operations.Length}";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认并执行所选操作";
            return;
        }
        _fixture.text = $"本次操作前：位置({_player.Position.Q},{_player.Position.R}) · {_context.DayPart} · 目标地形{_context.MovementMap.Placed[new AxialCoord(1, 0)].Edges[0]}"
            + $"\n部队等级{_damageUnits[0].Card.Level} · 伤牌{_damageUnits[0].Wounds} · 就绪{_damageUnits[0].IsReady}";
        _comparison.text = $"原卡已执行：移动力{_context.MovementPool}，影响力{_context.InfluencePool}\n"
            + (operation.kind == "Move" ? $"下一步移动至({operation.q},{operation.r})，实际费用{MovementService.GetCost(new AxialCoord(operation.q, operation.r), _context.MovementMap, _context.DayPart, _context)}"
            : operation.kind == "Ready" ? $"下一步重整目标部队{operation.targetIndex}；每级{_context.ReadyInfluencePerLevel}影响力，等级上限{_context.ReadyUnitMaxLevel}"
            : operation.kind == "EndBattle" ? "下一步结束本场战斗，检查附身的攻击与不攻击标记过期。"
            : "下一步结束回合，检查临时减费与重整许可过期。")
            + "\n" + string.Join("；", _operationResults.Select(v => v.Key + "=" + v.Value));
        if (operation.kind == "Block" || operation.kind == "Attack")
        {
            _fixture.text = "所选敌人索引" + operation.targetIndex + "：" + string.Join(" / ", _context.Enemies.Select(e => $"{e.Id} 护甲{e.Armor} 攻击{e.Attack}({e.AttackElement}) [{string.Join(",", e.Abilities)}]"));
            _comparison.text = $"已出牌：格挡{_context.BlockPool}，攻击{_context.MeleePool}，元素{Snapshot()["attackElement"]}\n"
                + $"待成功格挡：追加攻击{_context.AttackAfterBlock}，减甲{_context.BlockArmorReduction}，消灭{_context.KillBlockedEnemy}\n"
                + "下一步：" + (operation.kind == "Block" ? "格挡所选敌人" : "攻击所选敌人")
                + "\n" + string.Join("；", _operationResults.Where(v => v.Key.EndsWith(":effective") || v.Key.EndsWith(":kills") || v.Key.EndsWith(":wounds")).Select(v => Label(v.Key) + "=" + v.Value));
        }
        _status.text = $"等待确认操作 {_operationIndex + 1}/{_case.operations.Length}";
        _play.GetComponentInChildren<TextMeshProUGUI>().text = operation.kind == "Block" ? "确认敌人并格挡" : operation.kind == "Attack" ? "确认敌人并攻击" : operation.kind == "Move" ? "确认目标并移动" : operation.kind == "Ready" ? "确认部队并重整" : operation.kind == "EndBattle" ? "确认结束战斗" : "确认结束回合";
        if (IsHealingCard)
        {
            _fixture.text = GrailFixture();
            _comparison.text = "下一步：" + operation.kind + "\n手牌每张伤牌花费治疗1；部队每张伤牌花费等于其等级。\n圣杯基础来源每花费1点名望+1；焚毁仅治疗手牌伤牌时抽牌。";
            if (_case.cardId == "advanced_card_015" || _case.cardId == "magic_005")
                _comparison.text = "治疗与重整后续：" + operation.kind + "\n已按等级范围重整所选部队；重整本身不移除伤牌。\n治疗点另行分配：手牌每张1点，部队每张等于其等级。";
            if (operation.kind == "HealUnit" && operation.targetIndex >= 0 && operation.targetIndex < _player.Units.Count)
            {
                var unit = _player.Units[operation.targetIndex];
                _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == unit.Card.Id);
                _recruitArt.gameObject.SetActive(true);
                _comparison.text += $"\n所选原部队：{unit.Card.NameCn}，等级{unit.Card.Level}，伤牌{unit.Wounds}。\n拟治疗{operation.option}张，需{operation.option * unit.Card.Level}点；治疗不会自动重整。";
            }
            else if (operation.kind == "PlayCard")
            {
                var next = _catalog.cards.Single(c => c.source.Id == operation.cardId);
                _recruitArt.sprite = next.artwork; _recruitArt.gameObject.SetActive(true);
                _comparison.text += "\n继续打出原卡：" + next.source.NameCn + "，检查治疗抽牌奖励是否仍有效。";
            }
            else if (operation.kind == "HealHand") _comparison.text += "\n确认治疗手牌伤牌：" + operation.option + "张。";
            else if (operation.kind == "UnitUse")
                _comparison.text += "\n确认发动所选原部队能力；必须自有、就绪、无伤且支付原费用。";
            else if (operation.kind == "Move") _comparison.text += $"\n当前位置({_player.Position.Q},{_player.Position.R})，下一格({operation.q},{operation.r})；"
                + $"平原费用{MovementService.GetCost(new AxialCoord(operation.q,operation.r),_context.MovementMap,_context.DayPart,_context)}，移动点{_context.MovementPool}。\n战斗中或行动已提交后不能进行地图移动。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认并执行所选操作";
            return;
        }
        if (_case.cardId == "advanced_card_025")
        {
            _fixture.text = SpellFixture();
            if (operation.kind == "CastOffer")
            {
                ShowOfferedSpell(operation.targetIndex);
                _comparison.text = $"下一步：从当前供给施放索引{operation.targetIndex}的原法术\n强化={operation.enhanced}；正常支付法术费用，成功才消耗一次同色许可。\n法术应留在供给，不加入手牌或弃牌。";
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认供给法术并施放";
            }
            else if (operation.kind == "EndTurn")
            {
                _comparison.text = "结束回合：未用的供给施法许可失效；供给法术保持原位。";
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认结束回合";
            }
        }
        if (_case.cardId == "advanced_card_032")
        {
            _fixture.text = PortalFixture();
            _comparison.text = operation.kind == "Teleport"
                ? $"选择落点({operation.q},{operation.r})：已揭示且安全、距离不超过{_context.TeleportRange}；不花移动力。\n成功只能传送一次；拒绝后可重新选择合法落点。"
                : $"结束回合：传送失效；{(operation.refill ? "实际抽到上限并消费一次奖励" : "跳过补牌，保留下一次抽牌奖励")}。\n手牌{_player.Deck.Hand.Count}，牌库{_player.Deck.DrawPileCount}，通常上限{_player.HandLimit()}，下次加成{_context.NextDrawBonus}。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = operation.kind == "Teleport" ? "确认落点并传送" : "结束回合并检查补牌";
        }
        if (_case.cardId == "magic_002")
        {
            _fixture.text = MeditationFixture();
            _comparison.text = operation.kind == "DiscardNonWoundHand" ? "将可弃的非伤手牌放入弃牌堆，再检查下一次正常补牌。"
                : operation.refill ? "结束回合并按实际手牌上限补牌；仅消费一次+2，牌序与伤牌不得变化。"
                : "结束回合但跳过补牌：法术进入弃牌堆，+2保留到下一次补牌。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认卡区与补牌操作";
        }
        if (_case.cardId == "magic_004")
        {
            _fixture.text = TunnelFixture();
            _comparison.text = "下一步：" + operation.kind + "；逐步核对移动、守军城防、伤牌和返回起点。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认地道后续操作";
            if (operation.kind == "SelectSpellReward") ShowOfferedSpell(operation.option);
        }
        if (_case.cardId == "magic_023" && operation.kind == "EndBattle")
        {
            _fixture.text = WindFixture();
            _comparison.text = $"结束本场战斗：黑夜之翼禁止攻击的目标标记应失效。\n当前跳过敌人[{string.Join(",", _context.SkipAttackIndices)}]；手牌伤牌{_player.Wounds}。";
        }
        if (_case.cardId == "items_008" || _case.cardId == "items_016")
        {
            _fixture.text = WeaponFixture();
            _comparison.text = $"下一步：{operation.kind}；目标{operation.targetIndex}\n近战{_context.MeleePool}，远程{_context.RangedPool}，名望{_player.Fame}。"
                + "\n剑的额外名望持续本回合；弓仅持续使用它的战斗阶段。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认并执行下一步";
        }
        if (IsManaArtifact)
        {
            _fixture.text = GemRingFixture();
            if (operation.kind == "PlayCard")
            {
                var next = _catalog.cards.Single(c => c.source.Id == operation.cardId);
                _recruitArt.sprite = next.artwork;
                _recruitArt.gameObject.SetActive(true);
                _comparison.text = $"下一步打出原卡：{next.source.NameCn}（{operation.cardId}）\n"
                    + $"{(operation.enhanced ? "强化" : "基础")}效果；实际手牌实例、正常费用与昼夜限制均检查。\n"
                    + (IsGemRing ? "戒指仅奖励同色法术；行动牌和异色法术不增加此项名望。" : "使用宝石袋所得的真实魔力支付费用，观察剩余资源与原卡结果。");
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认原卡并打出";
            }
            else
            {
                _comparison.text = (IsGemRing ? "结束回合：无尽魔力和同色施法奖励失效。\n" : "结束回合：核对神器与所获资源的去向。\n")
                    + "临时魔力清零，魔晶和名望保留；已打出牌进入弃牌区，焚毁神器保持移除。";
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认结束回合";
            }
        }
        if (_case.unitIds?.Length > 0)
        {
            _fixture.text = DamageFixture();
            string action = operation.kind == "Damage" ? $"敌人{operation.targetIndex}承伤；部队顺序[{string.Join(",", operation.unitTargets ?? Array.Empty<int>())}]"
                : operation.kind == "EndBattle" ? "结束本场战斗" : operation.kind == "SelfWound" ? "测试非敌人来源伤牌" : "结束回合";
            _comparison.text = $"下一步：{action}\n全抗性{_context.UnitsResistAll}；英雄首伤忽略待用{_context.IgnoreNextWound}\n" + DamageHistory();
            _play.GetComponentInChildren<TextMeshProUGUI>().text = operation.kind == "Damage" ? "确认分配伤害" : "确认后续操作";
        }
        if (_case.recruitOffer != null)
        {
            _fixture.text = RecruitmentFixture();
            if (operation.kind == "Recruit")
            {
                var candidate = operation.targetIndex >= 0 && operation.targetIndex < _recruitOffer.Count ? _recruitOffer[operation.targetIndex] : null;
                if (candidate != null) _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == candidate.Id);
                var location = (RecruitLocation)Enum.Parse(typeof(RecruitLocation), operation.recruitLocation);
                _comparison.text = candidate == null ? "目标不在当前供给区，操作应拒绝。"
                    : $"下一步招募：{candidate.NameCn}（{candidate.Id}）\n地点 {location}；牌面费用{candidate.InfluenceCost}；实际费用{RecruitmentService.GetCost(_player, candidate, location, _context)}\n"
                    + $"可用影响力{_context.InfluencePool}；一次折扣{_context.CharmRecruitDiscount}；剩余指挥槽{_player.FreeSlots}";
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认部队并招募";
            }
            else
            {
                _comparison.text = "结束回合，检查未用的魅惑折扣过期。";
                _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认结束回合";
            }
        }
        if (_case.cardId == "magic_020" && !_case.enhanced)
        {
            _fixture.text = $"本回合供应能力许可{_context.OfferedUnitUses}；只禁向临时部队分配伤害，自有部队照常。\n供应：{string.Join(",",_recruitOffer.Select(u=>u.Id))}";
            _comparison.text = $"下一步：{operation.kind}；剩余许可{_context.OfferedUnitUses}\n白魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.White)}；蓝魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)}\n移动{_context.MovementPool} / 影响力{_context.InfluencePool} / 近战{_context.MeleePool} / 远程{_context.RangedPool} / 格挡{_context.BlockPool}";
            if (operation.kind == "OfferedUnitUse" || operation.kind == "OfferedUnitDamage")
            {
                var candidate = operation.targetIndex >= 0 && operation.targetIndex < _recruitOffer.Count ? _recruitOffer[operation.targetIndex] : null;
                if (candidate != null)
                {
                    _recruitArt.sprite = _catalog.unitArtwork.Single(s=>s.name==candidate.Id);
                    var ability = operation.option >= 0 && operation.option < candidate.Actions.Length ? candidate.Actions[operation.option] : null;
                    _comparison.text += $"\n选择：{candidate.NameCn}；能力{operation.option+1}，分支{operation.abilityChoice}\n" + (ability?.PrintedRule ?? "不存在的能力，操作必须拒绝。");
                }
                else _comparison.text += "\n目标不在供应中，操作必须拒绝。";
            }
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认选择并执行";
        }
        if (_case.cardId == "advanced_card_016")
        {
            _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
            _fixture.text = $"当前阶段{_context.CombatPhase}；本场加成{_context.ChargeUnitBonus}；整回合禁伤{_context.NoUnitDamage}";
            _comparison.text = $"下一步：{operation.kind}；原部队索引{operation.targetIndex}，能力{operation.option+1}，选择{operation.abilityChoice}\n"
                + $"近战{_context.MeleePool} / 远程{_context.RangedPool} / 攻城{_context.SiegePool} / 格挡{_context.BlockPool}\n红魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)}，蓝魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)}；部队伤牌{_player.Units[0].Wounds}";
            if (operation.kind == "UnitUse" || operation.kind == "OfferedUnitUse")
            {
                var card = operation.kind == "UnitUse" ? _player.Units[operation.targetIndex].Card : _recruitOffer[operation.targetIndex];
                _recruitArt.sprite = _catalog.unitArtwork.Single(s=>s.name==card.Id); _recruitArt.gameObject.SetActive(true);
                _comparison.text += "\n" + card.NameCn + "：" + card.Actions[operation.option].PrintedRule;
            }
            if (operation.kind == "Attack" || operation.kind == "RangedAttack" || operation.kind == "Block")
                _comparison.text += "\n目标：" + string.Join("；",_context.Enemies.Select(e=>$"{e.Id} 护甲{e.Armor} 攻击{e.Attack} 名望{e.Fame}"));
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认部队与后续结算";
        }
        if (IsBannerScenario)
        {
            _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
            var state = BannerSnapshot();
            _fixture.text = (IsCommandBannerScenario ? "指挥旌旗：影响力4；只指挥本回合招募部队，失旗移除部队。" : "荣誉旌旗：基础附着单支部队；焚毁加成持续本回合。") + "\n" + _case.title;
            _comparison.text = $"下一步：{operation.kind}；所选部队{operation.targetIndex}，能力{operation.option + 1}，分支{operation.abilityChoice}\n"
                + $"附着[{state["attached"]}]；实际护甲[{state["armor"]}]；焚毁加成{state["bonus"]}\n"
                + $"攻击{state["attack"]} / 格挡{state["block"]}；名望{state["fame"]}\n"
                + $"手牌{state["hand"]} / 弃牌{state["discard"]} / 牌库{state["draw"]} / 移除{state["removed"]}\n"
                + "基础每次攻防发动奖励；焚毁每支部队限一次。替换或解散弃牌，轮末可选回收。";
            if (_case.cardId == "items_018")
            {
                _fixture.text = "强韧旌旗：附着部队每轮忽略一次受伤及附加效果；同场仍限承伤一次。\n" + _case.title;
                _comparison.text = $"下一步：{operation.kind}；目标{operation.targetIndex}\n"
                    + $"本轮已用[{state["spent"]}]；预选防护[{state["protection"]}]；部队伤牌[{state["wounds"]}]\n"
                    + $"就绪[{state["ready"]}]；英雄伤牌{_player.Wounds}；实际治疗部队数{state["healed"]}\n"
                    + "焚毁只在战斗外治疗自有部队，不治疗英雄或直接重整。";
                if (operation.kind == "Damage") _comparison.text += "\n敌人：" + string.Join("；",_context.Enemies.Select(e=>$"{e.Id} 攻击{e.Attack}({e.AttackElement}) [{string.Join(",",e.Abilities)}]"));
                if (operation.kind == "PlayCard") _comparison.text += "\n打出原卡：" + _catalog.cards.Single(c=>c.source.Id==operation.cardId).source.NameCn;
            }
            if (_case.cardId == "items_002")
            {
                _fixture.text = "防护旌旗：附着单支部队护甲+1、火冰抗性；焚毁延后到回合末。\n" + _case.title;
                _comparison.text = $"下一步：{operation.kind}；目标{operation.targetIndex}\n"
                    + $"附着[{state["attached"]}]；护甲[{state["armor"]}]；旗帜火冰抗性[{state["elementalResist"]}]\n"
                    + $"英雄手中伤牌{_player.Wounds} / 弃牌伤牌{_player.DiscardWounds}；本回合新伤{state["newWounds"]}\n"
                    + $"部队伤牌[{state["wounds"]}]；回合末移除新伤{state["cleanup"]}；已移除{state["removed"]}\n"
                    + "旧伤、抽回的旧伤和部队伤牌保留；焚毁不立即治疗或防止昏迷。";
                if (operation.kind == "Damage") _comparison.text += "\n敌人：" + string.Join("；",_context.Enemies.Select(e=>$"{e.Id} 攻击{e.Attack}({e.AttackElement}) [{string.Join(",",e.Abilities)}]"));
                if (operation.kind == "PlayCard") _comparison.text += "\n打出原卡：" + _catalog.cards.Single(c=>c.source.Id==operation.cardId).source.NameCn;
            }
            if (_case.cardId == "items_003")
            {
                _fixture.text = "勇气旌旗：附着不立即重整；每轮一次，战斗外主动重整。\n" + _case.title;
                _comparison.text = $"下一步：{operation.kind}；目标{operation.targetIndex}；战斗中{_context.InBattle}\n"
                    + $"附着[{state["attached"]}]；本轮已用[{state["spent"]}]\n"
                    + $"就绪[{state["ready"]}]；伤牌[{state["wounds"]}]；移动力{_context.MovementPool}\n"
                    + $"手牌{state["hand"]} / 弃牌{state["discard"]} / 牌库{state["draw"]} / 移除{state["removed"]}\n"
                    + "焚毁在战斗外重整全部自有部队；重整保留伤牌，受伤部队仍不能发动。";
            }
            if (IsCommandBannerScenario)
                _comparison.text = $"下一步：{operation.kind}；目标索引{operation.targetIndex}\n"
                    + $"普通指挥槽{state["slots"]} / 空闲{state["freeSlots"]}；旗帜指挥{state["commanded"]}；影响力{state["influence"]}\n"
                    + $"自有部队[{state["owned"]}]；旗帜背面[{state["faceDown"]}]\n"
                    + $"附着[{state["attached"]}]；预选旗帜{state["selected"]}\n"
                    + $"已打出{state["played"]} / 弃牌{state["discard"]} / 牌库{state["draw"]}\n"
                    + "招募前可预选旗帜；本回合已招募的部队也可事后绑定。轮末收回必移除部队。";
            if (operation.targetIndex >= 0 && operation.targetIndex < _player.Units.Count)
            {
                var card = _player.Units[operation.targetIndex].Card;
                _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == card.Id);
                _recruitArt.gameObject.SetActive(true);
                if (operation.kind == "UnitUse" && operation.option >= 0 && operation.option < card.Actions.Length)
                    _comparison.text += "\n" + card.NameCn + "：" + card.Actions[operation.option].PrintedRule;
            }
            if (IsCommandBannerScenario && operation.kind == "Recruit" && operation.targetIndex >= 0 && operation.targetIndex < _recruitOffer.Count)
            {
                var candidate = _recruitOffer[operation.targetIndex];
                _recruitArt.sprite = _catalog.unitArtwork.Single(s => s.name == candidate.Id);
                _recruitArt.gameObject.SetActive(true);
                _comparison.text += $"\n招募原{candidate.NameCn}，牌面费用{candidate.InfluenceCost}，地点{operation.recruitLocation}。";
            }
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认旗帜与后续操作";
        }
        if (_case.cardId == "items_010")
        {
            _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
            var s = ArtifactFixtureSnapshot();
            string action = operation.kind switch
            {
                "PlayArtifactInstance" => "使用指定原神器实例" + operation.targetIndex + (operation.enhanced ? "（焚毁）" : "（基础）"),
                "UnownedArtifact" => "尝试未持有的同ID神器（非法目标）",
                "Recruit" => "招募所选原部队", "UnitUse" => "发动所选部队能力",
                "EndTurn" => "结束回合", "EndRound" => "结束轮次", "NewRound" => "开始新轮",
                "PlayCard" => "打出原金币包", _ => operation.kind
            };
            _fixture.text = "无尽金币包：基础影响力4/名望2；焚毁影响力9/名望3\n" + _case.title;
            _comparison.text = $"下一步：{action}\n影响力{s["influence"]}；名望{s["fame"]}\n"
                + $"手牌{s["hand"]} / 已打出{s["played"]} / 弃牌{s["discard"]} / 牌库{s["draw"]} / 移除{s["removed"]}\n"
                + $"原卡0：{ArtifactZoneLabel(s.GetValueOrDefault("zone0"))}；原卡1：{ArtifactZoneLabel(s.GetValueOrDefault("zone1"))}";
            UnitCard card = operation.kind == "Recruit" ? _recruitOffer.ElementAtOrDefault(operation.targetIndex)
                : operation.kind == "UnitUse" ? _player.Units.ElementAtOrDefault(operation.targetIndex)?.Card : null;
            if (card != null)
            {
                _recruitArt.sprite = _catalog.unitArtwork.Single(a=>a.name==card.Id); _recruitArt.gameObject.SetActive(true);
                _comparison.text += operation.kind == "Recruit" ? $"\n原{card.NameCn}：费用{card.InfluenceCost}，地点{operation.recruitLocation}"
                    : "\n" + card.NameCn + "：" + card.Actions[operation.option].PrintedRule;
            }
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认原卡与后续结算";
        }
        if (_case.cardId == "items_009" && _case.playFromHand)
        {
            string action = operation.kind switch
            {
                "PlayArtifactInstance" => "再次使用指定原号角实例",
                "UnownedArtifact" => "尝试未持有的号角实例",
                "RangedAttack" => "用攻城攻击所选原敌人",
                "EndBattle" => "结束战斗", "EndTurn" => "结束回合",
                "EndRound" => "结束轮次", "NewRound" => "开始新轮", _ => operation.kind
            };
            ShowWrathState("下一步：" + action);
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认号角与后续操作";
        }
        if (_case.cardId == "items_001")
        {
            string action=operation.kind == "UseFear" ? $"耗尽部队{operation.targetIndex}，取消敌人{operation.option}的攻击" : operation.kind;
            ShowFearState("下一步："+action);
            _comparison.text += "\n取消攻击不是格挡；焚毁只影响显式所选敌人。";
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认恐惧旌旗操作";
        }
        if (IsAmulet)
        {
            string action=operation.kind == "ConvertGold" ? "选择金色用作"+(ManaColor)operation.option
                : operation.kind == "Move" ? $"移动到({operation.q},{operation.r})" : operation.kind;
            ShowAmuletState("下一步："+action);
            if (_context.MovementMap.Places.Count>0) _comparison.text += "\n"+AmuletSites();
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认护符与后续操作";
        }
        if (IsSupplyExchange)
        {
            string action=operation.kind=="PlayCard" ? "使用取得的原卡"
                : operation.kind=="SelectRemoval" ? "选择下一张实际材料"
                : operation.kind=="PlayArtifactInstance" ? "使用第二张智慧之书领取新补入的牌"
                : operation.kind=="EndMarketRound" ? "结束本轮并按原规则轮换供给" : "结束回合";
            ShowWisdomState("下一步："+action);
            _play.GetComponentInChildren<TextMeshProUGUI>().text=_case.cardId=="items_013" ? "确认智慧之书后续操作" : "确认修行与后续操作";
        }
        if (IsRitualLifecycle)
        {
            string action=operation.kind=="Attack" || operation.kind=="RangedAttack" ? "选择原敌人并结算攻击"
                : operation.kind=="EndBattle" ? "结束战斗" : operation.kind=="EndMarketRound" ? "换轮检查永久移除材料" : "结束回合";
            ShowRitualState("下一步："+action);
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认仪式攻击后续操作";
        }
        if (IsLearning)
        {
            ShowLearningState("下一步："+LearningOperationLabel(operation));
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认学习与后续操作";
        }
        if (IsBloodline)
        {
            ShowBloodlineState("下一步："+operation.kind+"；原供给效果选项"+_context.BloodlineEffectOption);
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认血脉与后续操作";
        }
        if (IsDecompose)
        {
            ShowDecomposeState(operation.kind=="PlayCard"?"用所得魔晶强化实际下一张原卡":operation.kind=="EndMarketRound"?"换轮核对永久移除材料":"结束回合，魔力标记清空、晶体保留");
            _play.GetComponentInChildren<TextMeshProUGUI>().text="确认分解与后续操作";
        }
        if (IsOverload)
        {
            ShowOverloadState(operation.kind == "EndMarketRound" ? "下一步：换轮检查原材料仍永久移除。" : "下一步：结束回合检查数值清空和实际弃牌。");
            if (_case.overloadStepwise)
                ShowOverloadState($"下一步：{operation.kind}；当前选择{operation.option}，剩余{_context.PendingRepeatedEffect?.Remaining ?? 0}次。\n" + RepeatOfferText());
            if (IsOverloadSpellPermission)
            {
                ShowOverloadState($"下一步：{operation.kind}；许可[{string.Join(",",_context.SpellColors)}]\n远程{_context.RangedPool} / 攻城{_context.SiegePool}；红/蓝/黑魔力 {_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)}/{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)}/{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}");
                if(operation.kind=="CastOffer")ShowOfferedSpell(operation.targetIndex);
                if((operation.kind=="Block" || operation.kind=="RepeatAttackPhase") && _case.enemies?.Length>0)
                {
                    _recruitArt.sprite=_catalog.enemyArtwork.FirstOrDefault(e=>e.id==_case.enemies[0].id)?.artwork;
                    _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
                }
                _comparison.text += $"\n本战新伤{_context.BattleWoundsReceived}；昏迷{_context.IsKnockedOut}；手牌{_player.Deck.Hand.Count}";
            }
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认过载后续操作";
        }
        if (_case.basicAcceptance)
        {
            string action = operation.kind == "ChooseMeltdownCrystal" ? (operation.option < 0 ? "放弃取得魔晶" : "选择实际丢失的"+(ManaColor)operation.option+"魔晶")
                : operation.kind == "SelectJoyfulReturn" ? "选择弃牌并在回合末收回欢悦魔晶"
                : operation.kind == "SelectSteadyTempoReturn" ? (operation.option == 1 ? "选择回合末回收本张稳步前进" : "选择正常弃牌，不回收稳步前进")
                : operation.kind == "EndTurn" ? "结束回合，核对返手与弃牌"
                : operation.kind == "PlayCard" ? "打出原卡"+operation.cardId
                : operation.kind == "PlaySidewaysMove" ? "横置原卡获得移动力"
                : operation.kind == "UseMysticBox" ? "将宝盒作为揭示神器"+(operation.enhanced ? "焚毁使用" : "普通使用")
                : operation.kind == "ProbeArtifactBottom" ? "核对回合末供应底原实例"
                : operation.kind == "MoveHarvest" ? $"移动至({operation.q},{operation.r})并弃所选原卡收获{(ManaColor)operation.option}魔晶"
                : operation.kind == "ConvertMoveToAttack" ? $"移动兑换近战{operation.option}"
                : operation.kind == "ConvertMoveToRanged" ? $"移动兑换远程{operation.option}"
                : operation.kind == "ConvertInfluenceToBlock" ? $"影响力兑换{_context.InfluenceBlockElement}格挡{operation.option}"
                : operation.kind == "PeacefulHealing" ? $"宁静时光行动：付{operation.option * 2}影响换{operation.option}治疗"
                : operation.kind == "PeacefulReady" ? $"宁静时光行动：重整自有部队{operation.targetIndex + 1}"
                : operation.kind == "ReduceEnemyAttack" ? $"减低敌人{operation.targetIndex + 1}的第{operation.attackIndex + 1}次攻击"
                : operation.kind == "UseSourceDie" || operation.kind == "UseExtraSourceDie"
                    ? $"用{(operation.kind == "UseExtraSourceDie" ? "额外" : "普通")}额度取骰{operation.targetIndex + 1}，产生{(ManaColor)operation.option}魔力"
                : operation.kind == "Block" ? "确认目标并实际格挡"
                : operation.kind == "Attack" ? "确认目标并结算攻击" : operation.kind;
            ShowBasicAcceptanceState("下一步："+action);
            _play.GetComponentInChildren<TextMeshProUGUI>().text = "确认后续操作";
        }
    }

    private void ShowBasicAcceptanceState(string heading)
    {
        var state = Snapshot();
        _comparison.enableAutoSizing = true; _comparison.fontSizeMin = 18; _comparison.fontSizeMax = 23;
        _fixture.text = $"{_context.DayPart}；手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 弃牌{_player.Deck.DiscardPile.Count} / 永久移除{_player.Deck.RemovedCards.Count}"
            + $"\n伤牌{_player.Wounds}；名望{_player.Fame}；主效果选择{_case.option}";
        var rows = new List<string> { heading };
        rows.Add("魔力 红/蓝/绿/白/黑：" + string.Join(" / ", new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black}.Select(c => _player.Mana.Tokens.GetValueOrDefault(c))));
        rows.Add("魔晶 红/蓝/绿/白：" + string.Join(" / ", new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c => _player.Mana.Crystals.GetValueOrDefault(c))));
        rows.Add($"移动{_context.MovementPool} / 影响{_context.InfluencePool} / 治疗{_context.Healing.Total}；位置({_player.Position.Q},{_player.Position.R})");
        rows.Add($"近战{_context.MeleePool} / 远程{_context.RangedPool} / 格挡{_context.BlockPool}；声誉{_player.Reputation}");
        if (IsMindRead)
        {
            rows.Add("思维选择：" + (ManaColor)_case.option + "；偷取" + (!_case.enhanced ? "基础不适用" : _case.mindStealPlayerIndex < 0 ? "放弃" : "玩家" + (_case.mindStealPlayerIndex + 1)));
            string Name(string id) => _catalog.cards.FirstOrDefault(c => c.source.Id == id)?.source.NameCn ?? id;
            for (int i = 0; i < _mindChosenInstances.Length; i++)
            {
                var chosen = _mindChosenInstances[i];
                if (chosen != null)
                    rows.Add($"玩家{i + 1}选{ Name(chosen.Id) }：" + MindZoneLabel(state[$"mind:choice:{i}:zone"]));
                else
                {
                    string reveal = _playedState?.GetValueOrDefault($"mind:revealed:{i}") ?? "";
                    rows.Add($"玩家{i + 1}展示：" + (reveal.Length == 0 ? "待确认" : string.Join("、", reveal.Split(',').Select(Name))));
                }
            }
            rows.Add($"全体原牌实体：{state["mind:unique"]}/{state["mind:total"]}；守恒{state["mind:identity"]}");
        }
        if (IsWarp)
        {
            rows.Add($"扭曲许可：跨两格{_context.SpaceBendingActive}；不激怒{_context.IgnoreRampaging}");
            if (_case.useTurnEngine)
            {
                rows.Add("实际回合顺序：" + string.Join("→", _warpTurnOrder));
                rows.Add($"暂存至轮末：{_player.Deck.RoundAsideCards.Count}张；手牌{_player.Deck.Hand.Count} / 牌库{_player.Deck.DrawPileCount}");
            }
            if (_case.exploreTileIds?.Length > 0)
                rows.Add($"实际探索：[{state["warp:exploredIds"]}]；原板块{state["warp:tilesSame"]}；供给余{_context.MovementMap.Countryside.Count}");
            if (_warpGuard != null)
            {
                rows.Add($"原肆虐敌：{_warpGuard.Id}；在场{state["warp:guardAlive"]}；强制战斗{state["warp:rampaging"]}");
                _recruitArt.sprite = _catalog.enemyArtwork.First(e => e.id == _warpGuard.Id).artwork;
                _recruitArt.gameObject.SetActive(true);
            }
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "RampagingDefense"))
                rows.Add("肆虐实际承伤：" + _operationResults.GetValueOrDefault($"operation:{i}:wounds", "-"));
        }
        if (IsHeadband)
        {
            rows.Add("头环选择：" + _headbandSelected.Id + "；选项" + _case.headbandSkillOption);
            rows.Add("原技能归属：" + state["headband:selectedZone"] + "；实体守恒" + state["headband:identity"]);
            rows.Add("使用方式：" + (_case.enhanced ? "焚毁取得；后续正式发动" : _headbandSelected.Frequency == SkillFrequency.OncePerTurn ? "每回合技能，普通借用两次" : "每轮技能，普通借用一次"));
        }
        if (IsColdStare)
        {
            rows.Add("冷峻选择：" + (_case.option == 0 ? "影响力" + (_playedState?.GetValueOrDefault("InfluencePool") ?? "待出牌")
                : (_playedState?.GetValueOrDefault("cold:target") ?? "待选敌") + " / 攻击" + (_case.selectedEnemyAttackIndex + 1)));
            if (_case.option == 1 && _playedState != null)
                rows.Add($"出牌后攻击：{_playedState["cold:element"]}{_playedState["cold:power"]}；去能力{_playedState["cold:suppress"]}；停攻{_playedState["cold:skip"]}");
        }
        if (IsBloodSurge)
        {
            int drawn = (_case.initialDrawCards?.Length ?? 0) - _player.Deck.DrawPileCount;
            int removedWounds = _player.Deck.RemovedCards.Count(c => c.Type == CardType.Wound);
            rows.Add($"新伤抽牌：已抽{drawn} / 余库{_player.Deck.DrawPileCount}；移除新伤{removedWounds}");
            rows.Add($"手伤{_player.Wounds}；本战新增{_context.BattleWoundsReceived}；抽牌余次{_context.WoundDrawRemaining}");
        }
        if (IsExplosiveBolt)
        {
            rows.Add("爆裂箭矢：" + (_case.enhanced ? "费用" + _case.selectedActionManaColor + "；本次击杀待选减甲" + _context.ExplosiveBoltArmorUses : "自伤1；白/红晶各1"));
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "ChooseExplosiveBoltArmor"))
                rows.Add("实际减甲：" + _operationResults.GetValueOrDefault($"operation:{i}:targetId", "-")
                    + " " + _operationResults.GetValueOrDefault($"operation:{i}:armorBefore", "-") + "→"
                    + _operationResults.GetValueOrDefault($"operation:{i}:armorAfter", "-") + "；剩余次数"
                    + _operationResults.GetValueOrDefault($"operation:{i}:uses", "-"));
        }
        if (IsManaPower)
        {
            string[] choices = { "移动", "治疗", "抽牌" };
            string bonuses = string.Join("/", (_case.manaPowerBonusChoices ?? Array.Empty<int>()).Select(i => choices[i]));
            rows.Add("魔晶力量选择：" + (_case.enhanced ? choices[_case.option] + "；奖励" + (bonuses.Length == 0 ? "无" : bonuses)
                + "；费用" + _case.selectedActionManaColor : "取得" + (ManaColor)_case.option + "晶"));
            rows.Add("实际原牌：" + string.Join("/", _player.Deck.Hand.Where(c => c.Type != CardType.Wound)
                .Select(c => _catalog.cards.FirstOrDefault(e => e.source.Id == c.Id)?.source.NameCn ?? c.Id))
                + "；牌库" + _player.Deck.DrawPileCount);
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "HealHand" || _case.operations[i].kind == "HealUnit"))
                rows.Add("实际治疗：" + _case.operations[i].kind + " " + _case.operations[i].option
                    + "；手伤" + _operationResults.GetValueOrDefault($"operation:{i}:state:Wounds", "-")
                    + "；部队伤" + _operationResults.GetValueOrDefault($"operation:{i}:state:unitWounds", "-"));
        }
        if (_case.cardId == "advanced_card_039")
        {
            var terrain = _context.MovementMap.Placed[_player.Position].Edges[0];
            rows.Add($"实际驻留：{terrain}；待结算模式{_context.MountainLoreMode}；下次补牌奖励{_context.NextDrawBonus}");
            rows.Add($"本回合山脉安全许可{_context.MountainSafe}；当前手牌{_player.Deck.Hand.Count} / 牌库{_player.Deck.DrawPileCount}");
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "EndTurn"))
                rows.Add("回合末：" + _operationResults.GetValueOrDefault($"operation:{i}:endingTerrain", "-")
                    + "；实际安全检查" + _operationResults.GetValueOrDefault($"operation:{i}:safeBeforeEnd", "-")
                    + "；补牌后手牌" + _operationResults.GetValueOrDefault($"operation:{i}:state:hand", "-"));
            rows.Add("实际手牌顺序：" + string.Join(" / ", _player.Deck.Hand.Select(c => _catalog.cards.FirstOrDefault(e => e.source.Id == c.Id)?.source.NameCn ?? c.Id)));
        }
        if (_case.cardId == "advanced_card_038")
        {
            rows.Add("原卡物抗：出牌后" + (_playedState?.GetValueOrDefault("unitPhysicalResist") ?? "待出牌")
                + " / 当前" + _damageUnits[0].PhysicalResistTemp);
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "TurnDefense"))
                rows.Add("原部队承伤：单位伤" + _operationResults.GetValueOrDefault($"operation:{i}:state:unitWounds", "-")
                    + " / 英雄伤" + _operationResults.GetValueOrDefault($"operation:{i}:wounds", "-")
                    + "；承伤单位数" + _operationResults.GetValueOrDefault($"operation:{i}:assignedUnits", "-"));
        }
        if (_case.cardId == "advanced_card_037")
        {
            string[] choices = { "移动", "影响", "近战", "格挡" };
            string[] pools = { "MovementPool", "InfluencePool", "MeleePool", "BlockPool" };
            rows.Add("不屈选择：" + choices[_case.option] + "；原卡产出" + (_playedState?.GetValueOrDefault(pools[_case.option]) ?? "待出牌"));
            var materials = _case.enhanced ? (_case.discardIndices ?? Array.Empty<int>())
                : _case.discardIndex < 0 ? Array.Empty<int>() : new[] { _case.discardIndex };
            rows.Add("弃牌材料：" + (materials.Length == 0 ? "不弃牌" : string.Join(" / ", materials.Select(i =>
                (_artifactFixtureInstances[i].Type == CardType.Wound ? "旧伤牌" : _catalog.cards.First(c => c.source.Id == _artifactFixtureInstances[i].Id).source.NameCn)
                + "→" + ArtifactZoneLabel(state[$"basic:zone{i}"])))));
            rows.Add($"弃伤不算治疗；手牌伤{_player.Wounds}；本战新增{_context.BattleWoundsReceived}");
        }
        if (_case.cardId == "advanced_card_035")
        {
            rows.Add("宁静时光：2影响→1治疗" + (_case.enhanced ? "；2×等级影响→重整" : ""));
            rows.Add($"治疗行动{_context.PeacefulMomentAction}；许可{_context.PeacefulMomentAvailable}；本回合已治{_context.HealedHandWoundsThisTurn}");
            var peacefulCosts = Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "PeacefulHealing" || _case.operations[i].kind == "PeacefulReady")
                .Select(i => (_case.operations[i].kind == "PeacefulReady" ? "重整" : "换治疗")
                    + $"：花影响{_operationResults.GetValueOrDefault($"operation:{i}:cost", "-")}；成功{_operationResults.GetValueOrDefault($"operation:{i}:success", "-")}");
            if (_case.unitIds?.Contains("army_003") == true) rows.Add(string.Join("；", peacefulCosts));
            else rows.AddRange(peacefulCosts);
        }
        if (_case.cardId == "advanced_card_023")
        {
            int first = Enumerable.Range(0, _operationIndex).Where(i =>
                _case.operations[i].kind == "PlayCard" && _operationResults.GetValueOrDefault($"operation:{i}:state:AmbushTriggered") == "True").DefaultIfEmpty(-1).First();
            rows.Add("伏击首效：" + (first < 0 ? "未触发" :
                $"远程{_operationResults.GetValueOrDefault($"operation:{first}:state:RangedPool", "0")} / 格挡{_operationResults.GetValueOrDefault($"operation:{first}:state:BlockPool", "0")}"));
        }
        if (_case.cardId == "advanced_card_012")
        {
            rows.Add("鲜血选色：" + (_case.enhanced ? string.Join("/", _case.bloodRitualColors ?? new[] { ((ManaColor)_case.option).ToString() }) : ((ManaColor)_case.option).ToString())
                + "；转晶：" + (_case.enhanced ? (string.IsNullOrEmpty(_case.bloodRitualCrystalColor) ? "放弃" : _case.bloodRitualCrystalColor) : "基础红晶1"));
            rows.Add($"金魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Gold)}；攻城{_context.SiegePool}；本战新增伤{_context.BattleWoundsReceived}");
        }
        if (_case.cardId == "advanced_card_013")
            rows.Add($"纯净魔法选{(ManaColor)_case.option}：正文费1" + (_case.enhanced ? "＋印刷蓝费1 → 7" : " → 4"));
        if (_case.cardId == "advanced_card_017")
        {
            int choice = Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "SelectSteadyTempoReturn").DefaultIfEmpty(-1).Last();
            rows.Add("回收选择：" + (choice < 0 ? "未选" : _operationResults.GetValueOrDefault($"operation:{choice}:choice", "未选")));
            rows.Add((_case.enhanced ? "强化可放牌库顶" : "基础可放牌库底，但回合末须非空") + "；本卡区：" + ArtifactZoneLabel(state["basic:zone0"]));
            string Names(IEnumerable<DeedCard> cards) => string.Join(" → ", cards.Select(c => _catalog.cards.FirstOrDefault(e => e.source.Id == c.Id)?.source.NameCn ?? c.Id));
            rows.Add("当前牌库（顶到底）：" + Names(_player.Deck.DrawPile));
            rows.Add("实际手牌顺序：" + Names(_player.Deck.Hand));
        }
        if (_case.cardId == "advanced_card_021" || _case.crystalMasteryScenario)
        {
            rows.Add("已花魔晶 红/蓝/绿：" + string.Join("/", new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green }.Select(c => _context.SpentCrystals.GetValueOrDefault(c)))
                + "；本回合返还：" + _context.ReturnSpentCrystals);
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "EndTurn"))
                rows.Add("回合结算后魔晶 红/蓝/绿：" + string.Join("/", new[] { "Red", "Blue", "Green" }.Select(c => _operationResults.GetValueOrDefault($"operation:{i}:state:crystal:{c}", "-"))));
        }
        if (_case.cardId == "basic_card_017")
            rows.Add($"移动加值：下一张{_context.MoveBonusNext}；本回合每张{_context.MoveBonusAll}");
        if (_case.cardId == "basic_card_003")
        {
            rows.Add("野蛮收获：" + (_context.HarvestTriggers < 0 ? "每个成功移动格可选一次" : $"本回合剩{_context.HarvestTriggers}次"));
            var harvests = Enumerable.Range(0, _operationIndex)
                .Where(i => _case.operations[i].kind == "MoveHarvest").ToArray();
            foreach (int i in harvests)
                rows.Add($"收获{ i + 1 }：{_operationResults.GetValueOrDefault($"operation:{i}:material", "-")} → {(_operationResults.GetValueOrDefault($"operation:{i}:success") == "True" ? (ManaColor)_case.operations[i].option + "晶1" : "拒绝，资源不变")}");
        }
        if (_case.cardId == "basic_card_010" || _case.cardId == "advanced_card_036")
        {
            var reductions = Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "ReduceEnemyAttack").ToArray();
            if (_case.cardId == "advanced_card_036" && reductions.Length > 1)
            {
                rows.Add($"减攻余{_context.AttackReductionTimes}次；本战新伤{_context.BattleWoundsReceived}；待发奖励{_context.AttackIfNoWound}");
                rows.Add("实际减攻：" + string.Join("；", reductions.Select(i =>
                    $"敌{_case.operations[i].targetIndex + 1}/攻{_case.operations[i].attackIndex + 1} {_operationResults.GetValueOrDefault($"operation:{i}:before", "-")}→{_operationResults.GetValueOrDefault($"operation:{i}:after", "-")}")));
            }
            else
            {
                rows.Add($"可用减攻{_context.AttackReduction}；剩余次数{_context.AttackReductionTimes}");
                foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "ReduceEnemyAttack"))
                    rows.Add($"敌人{_case.operations[i].targetIndex + 1}第{_case.operations[i].attackIndex + 1}次攻击：{_operationResults.GetValueOrDefault($"operation:{i}:before", "-")} → {_operationResults.GetValueOrDefault($"operation:{i}:after", "-")}");
                if (_case.cardId == "advanced_card_036")
                    rows.Add($"本战新伤{_context.BattleWoundsReceived}；待发无新伤奖励{_context.AttackIfNoWound}");
            }
        }
        if (_case.cardId == "basic_card_025" || _case.extraHandCards?.Contains("basic_card_025") == true)
            rows.Add($"本场实际收到伤牌{_context.BattleWoundsReceived}；战斗之怒整场加值上限{_context.AttackBonusLimit}");
        if (_case.cardId == "advanced_card_006" || _case.cardId == "advanced_card_018")
        {
            rows.Add(_case.cardId == "advanced_card_006"
                ? $"战斗兑换：{_context.MoveCostAttack}移动→1近战；" + (_context.MoveCostRanged > 0 ? $"{_context.MoveCostRanged}移动→1远程" : "不可转远程")
                : $"外交选择：{_context.InfluenceBlockElement}格挡；1影响→1格挡");
            int conversion = Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind.StartsWith("Convert")).DefaultIfEmpty(-1).Last();
            if (conversion >= 0)
                rows.Add($"兑换结果{_operationResults.GetValueOrDefault($"operation:{conversion}:success", "-")}；花费{_operationResults.GetValueOrDefault($"operation:{conversion}:cost", "-")}；产出{_operationResults.GetValueOrDefault($"operation:{conversion}:produced", "-")}");
        }
        foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "Block"))
        {
            string prefix = $"operation:{i}:";
            rows.Add($"实际格挡：印刷{_operationResults.GetValueOrDefault(prefix+"printed", "-")} / 有效{_operationResults.GetValueOrDefault(prefix+"effective", "-")} / 所需{_operationResults.GetValueOrDefault(prefix+"required", "-")}");
        }
        if (_case.cardId == "advanced_card_036" || _case.cardId == "advanced_card_013" || _case.cardId == "advanced_card_023" || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox)
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "TurnDefense"))
            {
                string prefix = $"operation:{i}:";
                rows.Add($"正式防御：格挡{_operationResults.GetValueOrDefault(prefix+"blockEffective", "-")} / 所需{_operationResults.GetValueOrDefault(prefix+"blockRequired", "-")}；新增伤{_operationResults.GetValueOrDefault(prefix+"wounds", "-")}");
                if (_case.cardId == "advanced_card_036" || _case.cardId == "advanced_card_013") rows.Add($"进入近战获得攻击{_operationResults.GetValueOrDefault(prefix+"meleeRemaining", "-")}");
            }
        if (_case.operations?.Any(o => o.kind == "Block") == true)
            rows.Add("当前目标护甲：" + string.Join(" / ", _context.Enemies.Select((e,i) => CombatMath.EffectiveArmor(e,i,_context))));
        var completedMoves = Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "Move" || _case.operations[i].kind == "MoveHarvest").ToArray();
        if (_case.manaSourceFaces != null || _case.cardId == "basic_card_003" || _case.cardId == "advanced_card_012" || _case.cardId == "advanced_card_021" || _case.crystalMasteryScenario || IsStormOrAmbush || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox)
        {
            int movesPerRow = _case.crystalMasteryScenario || IsStormOrAmbush ? 4 : 2;
            for (int start = 0; start < completedMoves.Length; start += movesPerRow)
                rows.Add((_case.crystalMasteryScenario || IsStormOrAmbush ? "移动（坐标/费）：" : "逐格移动（坐标/费/结果）：") + string.Join("；", completedMoves.Skip(start).Take(movesPerRow).Select(i =>
                    $"({_case.operations[i].q},{_case.operations[i].r})/{_operationResults.GetValueOrDefault($"operation:{i}:cost", "-")}/{(_operationResults.GetValueOrDefault($"operation:{i}:success") == "True" ? "成" : "拒")}")));
        }
        else foreach (int i in completedMoves)
        {
            var step = _case.operations[i];
            var terrain = _context.MovementMap.Placed[new AxialCoord(step.q, step.r)].Edges[0];
            string cost = _operationResults.GetValueOrDefault($"operation:{i}:cost", "-");
            if (cost == int.MaxValue.ToString()) cost = "不可通行";
            rows.Add($"移动至({step.q},{step.r}) {terrain}：费用{cost}；成功{_operationResults.GetValueOrDefault($"operation:{i}:success", "-")}");
        }
        var shownOperation = _case.operations?.Take(Math.Min(_operationIndex+1, _case.operations.Length))
            .LastOrDefault(o => o.kind == "PlayCard" || o.kind == "OtherPlayCard" || o.kind == "PlaySidewaysMove" || o.kind == "MoveHarvest");
        string shownCard = shownOperation?.cardId ?? (_case.discardIndex >= 0 && _case.discardIndex < _artifactFixtureInstances.Length
            ? _artifactFixtureInstances[_case.discardIndex].Id : null);
        if (_case.crystalMasteryScenario && _case.cardId != "advanced_card_021") shownCard = "advanced_card_021";
        var shownEntry = _catalog.cards.FirstOrDefault(c => c.source.Id == shownCard);
        _materialArt.sprite = shownEntry?.artwork;
        if ((_case.cardId == "advanced_card_035" && _case.unitIds?.Contains("army_003") == true) || _case.cardId == "advanced_card_038" || (IsManaPower && _case.enhanced && _case.option == 1 && _case.unitIds?.Length > 0))
        {
            var unit = _player.Units[0];
            _materialArt.sprite = _catalog.unitArtwork.FirstOrDefault(s => s.name == unit.Card.Id);
            rows.Add($"{unit.Card.NameCn}：等级{unit.Card.Level} / 就绪{unit.IsReady} / 伤{unit.Wounds}");
        }
        _materialArt.gameObject.SetActive(_materialArt.sprite != null);
        if (shownEntry != null && !(_case.crystalMasteryScenario && _case.cardId != "advanced_card_021")
            && !(_case.cardId == "advanced_card_035" && _case.unitIds?.Contains("army_003") == true))
            rows.Add("关联原卡："+shownEntry.source.NameCn+(shownOperation?.enhanced == true ? "（强化）" : ""));
        if (_case.crystalMasteryScenario && _case.cardId != "advanced_card_021")
            rows.Add("魔晶精通（强化）蓝费1；本回合花晶回合末返还");
        if (IsMysticBox)
        {
            var original = _catalog.cards.First(c => c.source.Id == _boxSupplyOriginal[0].Id);
            _materialArt.sprite = original.artwork;
            _materialArt.gameObject.SetActive(true);
            _recruitArt.sprite = _catalog.unitArtwork.First(s => s.name == "army_2_006");
            _recruitArt.gameObject.SetActive(_case.recruitOffer != null || _boxSupplyOriginal[0].Id == "items_003");
            rows.Add($"揭示原神器：{original.source.NameCn}；供应余{_context.ArtifactSupply.Count}");
            rows.Add($"宝盒本体：{ArtifactZoneLabel(state["box:boxZone"])}；同一实体{state["box:identity"]}");
            var use = _case.operations.Take(_operationIndex).FirstOrDefault(o => o.kind == "UseMysticBox");
            rows.Add("借用方式：" + (use == null ? "未使用" : use.enhanced ? "焚毁" : "普通") + $"；实际名望{_player.Fame}");
            if (_boxSupplyOriginal[0].Id == "items_003")
                rows.Add($"借旗：原勇气旗；盒仍附着{state["box:bannerIdentity"]}；部队就绪{_player.Units[0].IsReady}");
            foreach (int i in Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "Recruit"))
                rows.Add($"实际招募：农民；费用{_operationResults[$"operation:{i}:cost"]}；余额{_operationResults[$"operation:{i}:influence"]}；原供给{state["box:recruitedIdentity"]}");
            if (_boxSupplyProbe.Count == 2)
                rows.Add($"供应顺序：{_boxSupplyProbe[0].Id}→{_boxSupplyProbe[1].Id}；原实例{ReferenceEquals(_boxSupplyProbe[0], _boxSupplyOriginal[1]) && ReferenceEquals(_boxSupplyProbe[1], _boxSupplyOriginal[0])}");
        }
        if (_case.cardId == "advanced_card_033")
            rows.Add("真实法术供给："+string.Join(" / ", _context.SpellSupply.Offer.Select((c,i) =>
                i+":"+(_catalog.cards.FirstOrDefault(e => e.source.Id == c.Id)?.source.NameCn ?? c.Id)
                +"（"+CardColorHelper.Get(c.Id)+"）")));
        if (_case.enemies?.Any(e => e.originalDefinition) == true)
        {
            var targetedOperation = _case.operations?.Take(Math.Min(_operationIndex + 1, _case.operations.Length))
                .LastOrDefault(o => o.kind == "Block" || o.kind == "Attack" || o.kind == "RangedAttack" || o.kind == "ReduceEnemyAttack" || o.kind == "TurnAttack" || o.kind == "ChooseExplosiveBoltArmor");
            var enemy = targetedOperation != null ? _context.Enemies.ElementAtOrDefault(targetedOperation.targetIndex)
                : _context.Enemies.FirstOrDefault(e => e.Id == _case.enemies.First(c => c.originalDefinition).id);
            if (enemy == null && (_case.cardId == "advanced_card_012" || _case.cardId == "advanced_card_013" || _case.crystalMasteryScenario || IsStormOrAmbush || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox))
                enemy = _case.enemies.First(c => c.originalDefinition).Create(); // Keep the original target artwork after removal.
            _recruitArt.sprite = _catalog.enemyArtwork.FirstOrDefault(e => e.id == enemy?.Id)?.artwork;
            _recruitArt.gameObject.SetActive(_recruitArt.sprite != null);
            rows.Add($"原敌人{enemy?.Id}；护甲{enemy?.Armor}；名望{enemy?.Fame}");
            if (enemy != null && enemy.AttackCount > 1)
                rows.Add("原攻击：" + string.Join(" / ", Enumerable.Range(0, enemy.AttackCount).Select(i => enemy.GetAttack(i).Value)));
        }
        else if (_case.cardId == "items_021" && _case.unitIds?.Length > 0)
        {
            var unit = _player.Units[0];
            _recruitArt.sprite = _catalog.unitArtwork.FirstOrDefault(s => s.name == unit.Card.Id);
            _recruitArt.gameObject.SetActive(_recruitArt.sprite != null);
            rows.Add($"{unit.Card.NameCn}：等级{unit.Card.Level} / 就绪{unit.IsReady} / 伤{unit.Wounds}");
        }
        if (_originalManaDice.Length > 0)
        {
            if (_case.manaSourceFaces != null || _case.cardId == "advanced_card_022")
            {
                rows.Add($"源骰{state["mana:sourceCount"]} / 本回合持骰{state["mana:turnHeld"]}；额外额度{_context.ExtraManaDice}；原骰守恒{state["mana:identity"]}");
                rows.Add("原骰：" + string.Join("；", _originalManaDice.Select((d,i) =>
                    $"{i+1}:{d.Face}/{(state[$"mana:die{i}:zone"] == "source" ? "源" : "持有")}")));
            }
            else rows.Add($"公共骰{state["mana:sourceCount"]}；轮末归还骰[{state["mana:heldFaces"]}]；普通持骰{state["mana:ordinaryHeld"]}");
            if (_case.cardId != "advanced_card_022")
                rows.AddRange(_context.OtherPlayers.Select((p,i) => $"玩家{i+1}：红标记{p.Mana.Tokens.GetValueOrDefault(ManaColor.Red)} / 红晶{p.Mana.Crystals.GetValueOrDefault(ManaColor.Red)} / 伤{p.Wounds}；诅咒{p.ManaCurseColor.HasValue}"));
        }
        if (_case.cardId == "magic_006")
        {
            rows.Add("待选魔晶：" + string.Join(",", _context.ManaMeltdownChoices));
            rows.AddRange(_context.OtherPlayers.Select((p,i) => $"玩家{i+1}：伤牌{p.Wounds}；魔晶 " + string.Join("/",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>p.Mana.Crystals.GetValueOrDefault(c)))));
        }
        else if (_case.cardId == "items_019")
        {
            var enemyId = _case.enemies?.FirstOrDefault()?.id;
            _recruitArt.sprite = _catalog.enemyArtwork.FirstOrDefault(e=>e.id==enemyId)?.artwork;
            _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
            rows.Add("原敌人："+enemyId+ $"；攻击余量{_context.MeleePool}；名望{_player.Fame}");
            var attack = _operationResults.Keys.LastOrDefault(k=>k.EndsWith(":kills"));
            if (attack != null)
            {
                var prefix=attack.Substring(0,attack.Length-5);
                rows.Add($"实际攻击：印刷{_operationResults.GetValueOrDefault(prefix+"printed","-")} / 有效{_operationResults.GetValueOrDefault(prefix+"effective","-")} / 击杀{_operationResults[attack]}");
            }
        }
        else if (_case.cardId == "basic_card_019")
            rows.Add("原卡位置：" + ArtifactZoneLabel(state["basic:zone0"]) + "；材料位置：" + ArtifactZoneLabel(state["basic:zone1"]));
        if (_case.cardId != "items_019")
        {
            var attack = _operationResults.Keys.LastOrDefault(k => k.EndsWith(":kills"));
            if (_case.cardId == "advanced_card_012" || IsStormOrAmbush || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox)
                attack = _operationResults.Keys.LastOrDefault(k => k.EndsWith(":kills")
                    && _operationResults.ContainsKey(k.Substring(0, k.Length - 5) + "printed"));
            if (attack != null)
            {
                var prefix = attack.Substring(0,attack.Length-5);
                rows.Add($"结算：印刷{_operationResults.GetValueOrDefault(prefix+"printed","-")} / 有效{_operationResults.GetValueOrDefault(prefix+"effective","-")} / 击杀{_operationResults[attack]}");
            }
            if (_case.recruitOffer != null && _operationIndex > 0)
            {
                int recruit = _case.cardId == "advanced_card_013" || IsPeacefulOrResolve || IsColdStare
                    ? Enumerable.Range(0, _operationIndex).Where(i => _case.operations[i].kind == "Recruit").DefaultIfEmpty(-1).Last()
                    : _operationIndex - 1;
                rows.Add("招募费用"+_operationResults.GetValueOrDefault("operation:"+recruit+":cost","待确认")+"；现有部队"+_player.Units.Count);
            }
        }
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            _materialArt.gameObject.activeSelf ? 610 : _recruitArt.gameObject.activeSelf ? 920 : 1266);
        if (_case.crystalMasteryScenario || IsStormOrAmbush || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox)
        {
            int successes = Enumerable.Range(0, _operationIndex).Count(i =>
                _operationResults.GetValueOrDefault("operation:" + i + ":success") == "True");
            rows.Add($"操作：已完成{_operationIndex}/{_case.operations.Length}；成功{successes}；拒绝{_operationIndex - successes}");
        }
        else rows.Add("操作：" + string.Join("；", Enumerable.Range(0,_operationIndex).Select(i =>
            _case.manaSourceFaces == null && _case.cardId != "basic_card_003" && _case.cardId != "advanced_card_012" && !_case.crystalMasteryScenario && _case.cardId != "advanced_card_021" ? $"{i+1}={_operationResults.GetValueOrDefault("operation:"+i+":success", "完成")}"
                : $"{i+1}{(_operationResults.GetValueOrDefault("operation:"+i+":success") == "True" ? "成" : "拒")}")));
        _comparison.text = string.Join("\n", rows);
        if ((IsExplosiveBolt && _case.enhanced) || IsBloodSurge || IsColdStare || IsMindRead || IsHeadband || IsWarp || IsMysticBox) _comparison.ForceMeshUpdate();
    }

    private bool IsStormOrAmbush => _case.basicAcceptance && (_case.cardId == "advanced_card_022" || _case.cardId == "advanced_card_023");
    private bool IsPeacefulOrResolve => _case.basicAcceptance && (_case.cardId == "advanced_card_035" || _case.cardId == "advanced_card_037");
    private bool IsColdStare => _case.basicAcceptance && _case.cardId == "advanced_card_043";
    private bool IsMindRead => _case.basicAcceptance && _case.cardId == "magic_018";
    private bool IsHeadband => _case.basicAcceptance && _case.cardId == "items_022";
    private bool IsWarp => _case.basicAcceptance && _case.cardId == "magic_016";
    private bool IsMysticBox => _case.basicAcceptance && _case.cardId == "items_024";

    private Dictionary<string, string> MysticBoxSnapshot()
    {
        var box = _artifactFixtureInstances[0];
        var pending = _context.MysticBox;
        var unit = _player.Units.FirstOrDefault(u => ReferenceEquals(u.Banner, box));
        var zones = new[] { _player.Deck.Hand.ToArray(), _player.Deck.PlayedCards.ToArray(),
            _player.Deck.DiscardPile.ToArray(), _player.Deck.RemovedCards.ToArray(),
            _player.Units.Where(u => u.Banner != null).Select(u => u.Banner).ToArray() };
        var names = new[] { "hand", "played", "discard", "removed", "attached" };
        return new Dictionary<string, string>
        {
            ["box:revealedId"] = pending?.RevealedArtifact.Id ?? "",
            ["box:used"] = (pending?.Used ?? false).ToString(),
            ["box:supplyCount"] = _context.ArtifactSupply.Count.ToString(),
            ["box:boxZone"] = string.Join("+", names.Where((name, i) => zones[i].Any(c => ReferenceEquals(c, box)))),
            ["box:identity"] = (zones.SelectMany(c => c).Count(c => ReferenceEquals(c, box)) == 1
                && (pending == null || (ReferenceEquals(pending.Box, box) && ReferenceEquals(pending.RevealedArtifact, _boxSupplyOriginal[0])))).ToString(),
            ["box:bannerEffectId"] = unit?.BannerEffectId ?? "",
            ["box:bannerIdentity"] = (unit != null && ReferenceEquals(unit.Banner, box)).ToString(),
            ["box:recruitedIdentity"] = (_boxRecruitOriginal != null && _player.Units.Any(u => ReferenceEquals(u.Card, _boxRecruitOriginal))).ToString()
        };
    }

    private static IEnumerable<DeedCard> MindPlayerCards(PlayerState player) => player.Deck.Hand
        .Concat(player.Deck.PlayedCards).Concat(player.Deck.DiscardPile).Concat(player.Deck.DrawPile).Concat(player.Deck.RemovedCards);

    private static string MindZoneLabel(string zone)
    {
        var parts = zone.Split(':');
        return (parts[0] == "self" ? "本人" : "玩家" + (int.Parse(parts[1]) + 1)) + ArtifactZoneLabel(parts.Last());
    }

    private Dictionary<string, string> MindSnapshot()
    {
        var players = new[] { _player }.Concat(_context.OtherPlayers).ToArray();
        var all = players.SelectMany(MindPlayerCards).ToArray();
        var result = new Dictionary<string, string>
        {
            ["mind:total"] = all.Length.ToString(),
            ["mind:unique"] = all.Where((c, i) => !all.Take(i).Any(other => ReferenceEquals(c, other))).Count().ToString(),
            ["mind:identity"] = (all.Length == _mindInitialInstances.Length
                && _mindInitialInstances.All(c => all.Count(other => ReferenceEquals(c, other)) == 1)).ToString()
        };
        for (int i = 0; i < _mindChosenInstances.Length; i++)
        {
            var chosen = _mindChosenInstances[i];
            if (chosen != null)
                for (int p = 0; p < players.Length; p++)
                {
                    var deck = players[p].Deck;
                    var zones = new[] { deck.Hand.ToArray(), deck.PlayedCards.ToArray(), deck.DiscardPile.ToArray(), deck.DrawPile.ToArray(), deck.RemovedCards.ToArray() };
                    var names = new[] { "hand", "played", "discard", "draw", "removed" };
                    for (int z = 0; z < zones.Length; z++)
                        if (zones[z].Any(c => ReferenceEquals(c, chosen)))
                            result[$"mind:choice:{i}:zone"] = (p == 0 ? "self" : "other:" + (p - 1)) + ":" + names[z];
                }
            result[$"mind:revealed:{i}"] = _context.MindReadRevealedHands.TryGetValue(_context.OtherPlayers[i], out var reveal)
                ? string.Join(",", reveal.Select(c => c.Id)) : "";
        }
        return result;
    }
    private bool IsBloodSurge => _case.basicAcceptance && _case.cardId == "advanced_card_042";
    private bool IsExplosiveBolt => _case.basicAcceptance && _case.cardId == "advanced_card_041";
    private bool IsManaPower => _case.basicAcceptance && _case.cardId == "advanced_card_040";
    private bool IsNatureOrLore => _case.basicAcceptance && (_case.cardId == "advanced_card_038" || _case.cardId == "advanced_card_039");
    private bool IsOverload => _case.cardId == "advanced_card_024" && _case.playFromHand;
    private bool IsOverloadSpellPermission => IsOverload && _case.extraHandCards?.FirstOrDefault()=="advanced_card_025"
        && (_case.operations?.Any(o=>o.kind=="CastOffer") ?? false);

    private string RepeatOfferText() => "当前法术供给：" + string.Join(" / ", _context.SpellSupply.Offer.Select((c,i)=>
        $"{i}:{_catalog.cards.FirstOrDefault(e=>e.source.Id==c.Id)?.source.NameCn ?? c.Id}"));

    private void ShowOverloadState(string heading)
    {
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 610);
        var material = _case.removeIndex >= 0 && _case.removeIndex < _artifactFixtureInstances.Length
            ? _artifactFixtureInstances[_case.removeIndex] : null;
        var entry = material == null ? null : _catalog.cards.FirstOrDefault(c => c.source.Id == material.Id);
        _materialArt.sprite = entry?.artwork;
        _materialArt.gameObject.SetActive(_materialArt.sprite != null);
        _fixture.text = $"原材料：{entry?.source.NameCn ?? material?.Id ?? "无"}；{(_case.enhanced ? "强效×2" : "基础×3")}；原效果选项{_case.option}"
            + $"\n手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 弃牌{_player.Deck.DiscardPile.Count} / 永久移除{_player.Deck.RemovedCards.Count}；第{_roundEngine?.Clock.RoundIndex ?? 1}轮";
        if (_case.combatPhase == "Block")
        {
            var enemy = _context.Enemies.FirstOrDefault();
            _recruitArt.sprite = enemy == null ? null : _catalog.enemyArtwork.FirstOrDefault(e=>e.id==enemy.Id)?.artwork;
            _recruitArt.gameObject.SetActive(_recruitArt.sprite != null);
            _fixture.text += $"；原敌人{enemy?.Id} 攻击{enemy?.Attack}（{enemy?.AttackElement}），伤牌{_player.Wounds}";
        }
        if (_case.overloadStepwise)
        {
            var spell = _context.SpellSupply.Offer.FirstOrDefault();
            _recruitArt.sprite = spell == null ? null : _catalog.cards.FirstOrDefault(c=>c.source.Id==spell.Id)?.artwork;
            if (_case.enemies?.Any(e=>e.originalDefinition) == true)
                _recruitArt.sprite = _catalog.enemyArtwork.FirstOrDefault(e=>e.id==_case.enemies[0].id)?.artwork;
            _recruitArt.gameObject.SetActive(_recruitArt.sprite != null);
        }
        _comparison.text = heading;
        if (_case.overloadSelections != null)
        {
            string MaterialName(int index) => index >= 0 && index < _artifactFixtureInstances.Length
                ? _catalog.cards.FirstOrDefault(c=>c.source.Id==_artifactFixtureInstances[index].Id)?.source.NameCn ?? _artifactFixtureInstances[index].Id : "无";
            _comparison.text = string.Join("\n", _case.overloadSelections.Select((s,i) =>
                $"第{i+1}次：选项{s.option}；移除{MaterialName(s.removeIndex)}；弃牌{MaterialName(s.discardIndex)}")) + "\n" + heading;
        }
    }

    private Dictionary<string,string> DecomposeSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        result["selectionCleared"]=(_context.CardToRemove==null).ToString();
        result["removedIds"]=string.Join(",",_player.Deck.RemovedCards.Select(c=>c.Id));
        result["drawIds"]=string.Join(",",_player.Deck.DrawPile.Select(c=>c.Id));
        result["handIds"]=string.Join(",",_player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Select(c=>c.Id));
        result["round"]=(_roundEngine?.Clock.RoundIndex ?? 1).ToString();
        result["move"]=_context.MovementPool.ToString();result["block"]=_context.BlockPool.ToString();result["attack"]=_context.MeleePool.ToString();
        foreach(ManaColor color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
        {
            result["crystal:"+color]=_player.Mana.Crystals.GetValueOrDefault(color).ToString();
            result["token:"+color]=_player.Mana.Tokens.GetValueOrDefault(color).ToString();
        }
        return result;
    }

    private void ShowDecomposeState(string heading)
    {
        _art.sprite=_entry.artwork;_printed.text=PrintedEffect();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
        var material=_case.removeIndex>=0 && _case.removeIndex<_artifactFixtureInstances.Length ? _artifactFixtureInstances[_case.removeIndex] : null;
        var entry=material==null?null:_catalog.cards.FirstOrDefault(c=>c.source.Id==material.Id);
        _materialArt.sprite=entry?.artwork;_materialArt.gameObject.SetActive(_materialArt.sprite!=null);
        _recruitArt.sprite=IsBloodline?_catalog.cards.FirstOrDefault(c=>c.source.Id=="advanced_card_020")?.artwork:null;
        _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        _fixture.text=$"实际材料：{entry?.source.NameCn ?? material?.Id ?? "无"}；原卡颜色{(material==null?"无":CardColorHelper.Get(material.Id)?.ToString() ?? "无")}；永久移除{_player.Deck.RemovedCards.Count}张"
            +$"\n手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 弃牌{_player.Deck.DiscardPile.Count} / 牌库{_player.Deck.DrawPileCount}；伤牌手中{_player.Wounds}/弃牌{_player.DiscardWounds}；第{_roundEngine?.Clock.RoundIndex ?? 1}轮";
        _comparison.text=heading;
    }

    private Dictionary<string,string> BloodlineSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        result["wounds"]=_player.Wounds.ToString();result["discardWounds"]=_player.DiscardWounds.ToString();
        result["handActions"]=string.Join(",",_player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Select(c=>c.Id));
        result["discardActions"]=string.Join(",",_player.Deck.DiscardPile.Where(c=>c.Type!=CardType.Wound).Select(c=>c.Id));
        result["advanced"]=string.Join(",",_context.AdvActionSupply.Offer.Select(c=>c.Id));
        result["spells"]=string.Join(",",_context.SpellSupply.Offer.Select(c=>c.Id));
        result["ranged"]=_context.RangedPool.ToString();result["siege"]=_context.SiegePool.ToString();
        foreach(ManaColor color in Enum.GetValues(typeof(ManaColor)))
            result["token:"+color]=_player.Mana.Tokens.GetValueOrDefault(color).ToString();
        return result;
    }

    private void ShowBloodlineState(string heading)
    {
        _art.sprite=_entry.artwork;_printed.text=PrintedEffect();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
        _materialArt.gameObject.SetActive(false);
        int index=_case.enhanced?_case.option:_case.option&255;
        var offer=_case.advancedOffer ?? new[]{"advanced_card_000","advanced_card_001","advanced_card_003"};
        var entry=index>=0 && index<offer.Length ? _catalog.cards.FirstOrDefault(c=>c.source.Id==offer[index]) : null;
        _recruitArt.sprite=entry?.artwork;_recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        _fixture.text=$"所选原供给[{index}]：{entry?.source.NameCn ?? "无效"}；伤牌手中{_player.Wounds}/弃牌{_player.DiscardWounds}"
            +$"\n红{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)} / 蓝{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)} / 绿{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)} / 白{_player.Mana.Tokens.GetValueOrDefault(ManaColor.White)}魔力；影响力{_context.InfluencePool}；远程{_context.RangedPool} / 攻城{_context.SiegePool}";
        _comparison.text=heading;
    }

    private string LearningOperationLabel(CardVerificationOperation operation) => operation.kind == "Learn"
        ? $"选择供给[{operation.option}]，支付{(operation.enhanced?9:6)}影响力"
        : operation.kind == "PlayCard" ? "打出"+(_catalog.cards.FirstOrDefault(e=>e.source.Id==operation.cardId)?.source.NameCn ?? operation.cardId)
        : operation.kind == "EndTurn" ? "结束回合" : operation.kind;

    private Dictionary<string,string> LearningSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        result["influence"]=_context.InfluencePool.ToString();
        result["used"]=_context.LearnUsed.ToString();
        result["modes"]=_context.LearningModes.ToString();
        result["white"]=_player.Mana.Tokens.GetValueOrDefault(ManaColor.White).ToString();
        result["handIds"]=string.Join(",",_player.Deck.Hand.Select(c=>c.Id));
        result["discardIds"]=string.Join(",",_player.Deck.DiscardPile.Select(c=>c.Id));
        result["advanced"]=string.Join(",",_context.AdvActionSupply.Offer.Select(c=>c.Id));
        return result;
    }

    private void ShowLearningState(string heading)
    {
        _art.sprite=_entry.artwork;_printed.text=PrintedEffect();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
        _materialArt.gameObject.SetActive(false);
        string offered=_case.advancedOffer!=null && _case.option>=0 && _case.option<_case.advancedOffer.Length ? _case.advancedOffer[_case.option] : "";
        _recruitArt.sprite=_catalog.cards.FirstOrDefault(e=>e.source.Id==offered)?.artwork;
        _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        _fixture.text=$"影响力{_context.InfluencePool}；白魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.White)}；本回合购牌{(_context.LearnUsed?"已用":"未用")}；能力{(_context.LearningModes==0?"无":_context.LearningModes==1?"基础":"强化")}"
            +$"\n基础：影响力+2，付6进弃牌；强化：影响力+4，付9进手牌。供给即时补齐。";
        _comparison.text=heading;
    }

    private Dictionary<string,string> RitualSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        result["handIds"]=string.Join(",",_player.Deck.Hand.Select(c=>c.Id));
        result["drawIds"]=string.Join(",",_player.Deck.DrawPile.Select(c=>c.Id));
        result["discardIds"]=string.Join(",",_player.Deck.DiscardPile.Select(c=>c.Id));
        result["removedIds"]=string.Join(",",_player.Deck.RemovedCards.Select(c=>c.Id));
        result["red"]=_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString();
        result["selectionCleared"]=(_context.CardToRemove==null).ToString();
        if (_roundEngine!=null) result["round"]=_roundEngine.Clock.RoundIndex.ToString();
        return result;
    }

    private void ShowRitualState(string heading)
    {
        _art.sprite=_entry.artwork;_printed.text=PrintedEffect();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
        var material=_case.removeIndex>=0 && _case.removeIndex<_artifactFixtureInstances.Length ? _artifactFixtureInstances[_case.removeIndex] : null;
        var entry=material==null?null:_catalog.cards.FirstOrDefault(e=>e.source.Id==material.Id);
        _materialArt.sprite=entry?.artwork;_materialArt.gameObject.SetActive(_materialArt.sprite!=null);
        var enemy=_context.Enemies.FirstOrDefault();
        _recruitArt.sprite=enemy==null?null:_catalog.enemyArtwork.FirstOrDefault(e=>e.id==enemy.Id)?.artwork;
        _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        _fixture.text=$"材料：{entry?.source.NameCn ?? material?.Id ?? "未选择"}；红魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)}；敌人{enemy?.Id} 护甲{enemy?.Armor}"
            +$"\n近战{_context.MeleePool} / 远程{_context.RangedPool} / 攻城{_context.SiegePool}；名望{_player.Fame}；第{_roundEngine?.Clock.RoundIndex ?? 1}轮";
        _comparison.text=heading;
    }

    private Dictionary<string,string> WisdomSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        result["handIds"]=string.Join(",",_player.Deck.Hand.Select(c=>c.Id));
        result["discardIds"]=string.Join(",",_player.Deck.DiscardPile.Select(c=>c.Id));
        result["removedIds"]=string.Join(",",_player.Deck.RemovedCards.Select(c=>c.Id));
        result["advanced"]=string.Join(",",_context.AdvActionSupply.Offer.Select(c=>c.Id));
        result["spells"]=string.Join(",",_context.SpellSupply.Offer.Select(c=>c.Id));
        result["advancedDiscard"]=_context.AdvActionSupply.DiscardPile.Count.ToString();
        result["spellDiscard"]=_context.SpellSupply.DiscardPile.Count.ToString();
        if (_roundEngine!=null) result["round"]=_roundEngine.Clock.RoundIndex.ToString();
        result["selectionCleared"]=(_context.CardToRemove==null).ToString();
        result["ranged"]=_context.RangedPool.ToString();
        foreach (ManaColor color in Enum.GetValues(typeof(ManaColor)))
        {
            result["crystal:"+color]=_player.Mana.Crystals.GetValueOrDefault(color).ToString();
            result["token:"+color]=_player.Mana.Tokens.GetValueOrDefault(color).ToString();
        }
        return result;
    }

    private void ShowWisdomState(string heading)
    {
        _art.sprite=_entry.artwork; _printed.text=PrintedEffect();
        _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
        var material=_case.removeIndex>=0 && _case.removeIndex<_artifactFixtureInstances.Length
            ? _artifactFixtureInstances[_case.removeIndex] : null;
        var materialEntry=material==null?null:_catalog.cards.FirstOrDefault(e=>e.source.Id==material.Id);
        var offered=_catalog.cards.FirstOrDefault(e=>e.source.Id==_wisdomOfferId);
        _materialArt.sprite=materialEntry?.artwork;
        _materialArt.gameObject.SetActive(_materialArt.sprite!=null);
        _recruitArt.sprite=offered?.artwork;
        _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        _fixture.text=$"材料：{materialEntry?.source.NameCn ?? material?.Id ?? "未选择"} ({(material==null?"无":CardColorHelper.Get(material.Id)?.ToString() ?? "无色")})；所选供给[{_wisdomOfferIndex}]：{offered?.source.NameCn ?? "无效选择"}"
            +$"\n晶体 红{_player.Mana.Crystals.GetValueOrDefault(ManaColor.Red)}/蓝{_player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue)}/绿{_player.Mana.Crystals.GetValueOrDefault(ManaColor.Green)}/白{_player.Mana.Crystals.GetValueOrDefault(ManaColor.White)}；{_context.DayPart}；远程{_context.RangedPool}";
        if (_roundEngine!=null) _fixture.text+=$"；第{_roundEngine.Clock.RoundIndex}轮";
        if (_case.cardId=="advanced_card_027") _fixture.text+=$"；绿魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)}";
        _comparison.text=heading;
    }

    private Dictionary<string,string> AmuletSnapshot()
    {
        var result=ArtifactFixtureSnapshot();
        foreach (ManaColor color in Enum.GetValues(typeof(ManaColor)))
            result[color.ToString()]=_player.Mana.Tokens.GetValueOrDefault(color).ToString();
        result["goldAllowed"]=_context.AllowGoldAtNight.ToString();
        result["blackAllowed"]=_context.AllowBlackAtDay.ToString();
        result["movement"]=_context.MovementPool.ToString();
        result["time"]=_context.DayPart.ToString();
        result["revealed"]=_context.MovementMap.RevealedSites.Count.ToString();
        foreach (var pair in _context.MovementMap.Places)
            result[$"site:{pair.Key.Q},{pair.Key.R}"]=_context.MovementMap.RevealedSites.Contains(pair.Value).ToString();
        return result;
    }

    private string AmuletSites()
    {
        var map=_context.MovementMap;
        return string.Join("\n",map.Places.Select(pair=>
        {
            var site=pair.Value; bool visible=map.RevealedSites.Contains(site);
            string content=visible ? (site.Enemy?.Id ?? site.RuinToken?.ToString() ?? "无驻军") : "背面，内容未公开";
            return $"({pair.Key.Q},{pair.Key.R}) {site.Type}：{content}";
        }));
    }

    private void ShowAmuletState(string heading)
    {
        var s=AmuletSnapshot(); _art.sprite=_entry.artwork; _printed.text=PrintedEffect();
        _fixture.text=$"实际{_context.DayPart}；魔力 红{s["Red"]}/蓝{s["Blue"]}/绿{s["Green"]}/白{s["White"]}/金{s["Gold"]}/黑{s["Black"]}"
            +$"\n夜间金许可{s["goldAllowed"]}；白昼黑许可{s["blackAllowed"]}；移动力{s["movement"]}；公开地点{s["revealed"]}";
        _comparison.text=heading;
        var guard=_context.MovementMap.Places.Values.FirstOrDefault(p=>_context.MovementMap.RevealedSites.Contains(p)&&p.Enemy!=null)?.Enemy;
        var art=guard==null?null:_catalog.enemyArtwork.FirstOrDefault(e=>e.id==guard.Id)?.artwork;
        var followup=_case.operations?.LastOrDefault(o=>o.kind=="PlayCard");
        if (art==null && followup!=null)
            art=_catalog.cards.Single(e=>e.source.Id==followup.cardId).artwork;
        _recruitArt.sprite=art; _recruitArt.gameObject.SetActive(art!=null);
    }

    private void ShowFearState(string heading)
    {
        var s = BannerSnapshot();
        _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
        _fixture.text = $"恐惧旌旗：阶段{_context.CombatPhase}；停攻[{s["skip"]}]；成功格挡{s["blocked"]}；名望{_player.Fame}"
            + $"\n附着[{s["attached"]}]；就绪[{s["ready"]}]；英雄创伤{_player.Wounds}；永久移除{s["removed"]}";
        _comparison.text = heading;
        var unit = _player.Units.FirstOrDefault();
        if (unit != null)
        {
            _recruitArt.sprite = _catalog.unitArtwork.Single(a=>a.name==unit.Card.Id);
            _recruitArt.gameObject.SetActive(true);
        }
    }

    private Dictionary<string,string> WrathSnapshot()
    {
        var result = ArtifactFixtureSnapshot();
        result["siege"] = _context.SiegePool.ToString();
        result["wounds"] = _player.Wounds.ToString();
        result["knockedOut"] = _context.IsKnockedOut.ToString();
        result["battleWounds"] = _context.BattleWoundsReceived.ToString();
        return result;
    }

    private void ShowWrathState(string heading)
    {
        _art.sprite = _entry.artwork; _printed.text = PrintedEffect();
        var s = WrathSnapshot();
        _fixture.text = $"愤怒号角：攻城{s["siege"]}；手中创伤{s["wounds"]}；本战创伤{s["battleWounds"]}；昏迷{s["knockedOut"]}"
            + $"\n骰色[{string.Join(",",_context.LastManaRolls)}] 共{_context.LastManaRolls.Count}颗；名望{s["fame"]}";
        _comparison.text = (string.IsNullOrEmpty(heading) ? "" : heading+"\n") + $"手牌{s["hand"]} / 已打出{s["played"]} / 弃牌{s["discard"]} / 牌库{s["draw"]} / 移除{s["removed"]}"
            + $"\n原卡0：{ArtifactZoneLabel(s.GetValueOrDefault("zone0"))}；原卡1：{ArtifactZoneLabel(s.GetValueOrDefault("zone1"))}";
        var enemy = _case.enemies?.FirstOrDefault(e=>e.originalDefinition)?.Create();
        var artwork = enemy == null ? null : _catalog.enemyArtwork.FirstOrDefault(e=>e.id==enemy.Id)?.artwork;
        if (artwork != null)
        {
            _recruitArt.sprite = artwork; _recruitArt.gameObject.SetActive(true);
            _comparison.text += $"\n原敌人{enemy.Id}：护甲{enemy.Armor}，能力[{string.Join(",",enemy.Abilities)}]";
        }
    }

    private Dictionary<string, string> BannerSnapshot()
    {
        var attached = _player.Units.Where(u => u.Banner != null).Select(u => u.Banner).ToArray();
        var all = _player.Deck.Hand.Concat(_player.Deck.DrawPile).Concat(_player.Deck.PlayedCards)
            .Concat(_player.Deck.DiscardPile).Concat(_player.Deck.RemovedCards).Concat(attached).ToArray();
        return new Dictionary<string, string>
        {
            ["attached"] = string.Join(",", _player.Units.Select(u => u.Banner?.Id ?? "none")),
            ["count"] = attached.Length.ToString(),
            ["total"] = all.Length.ToString(),
            ["unique"] = all.Where((c,i) => !all.Take(i).Any(previous => ReferenceEquals(previous,c))).Count().ToString(),
            ["armor"] = string.Join(",", _player.Units.Select(u => u.Card.Armor + BannerActions.HonorBonus(_context,u) + BannerActions.ProtectionArmor(u))),
            ["elementalResist"] = string.Join(",",_player.Units.Select(u => BannerActions.ProtectionResists(u,Element.Fire))),
            ["newWounds"] = _player.Deck.NewWoundsThisTurn.ToString(),
            ["cleanup"] = _context.RemoveNewWoundsAtTurnEnd.ToString(),
            ["skip"] = string.Join(",",_context.SkipAttackIndices.OrderBy(i=>i)),
            ["blocked"] = _context.Enemies.Count(e=>_context.WasBlockedThisBattle(e)).ToString(),
            ["phase"] = _context.CombatPhase.ToString(),
            ["heroWounds"] = _player.Wounds.ToString(),
            ["bonus"] = _context.HonorBannerTurnBonus.ToString(),
            ["fame"] = _player.Fame.ToString(),
            ["healing"] = _context.Healing.Total.ToString(),
            ["attack"] = _context.MeleePool.ToString(), ["block"] = _context.BlockPool.ToString(),
            ["hand"] = _player.Deck.Hand.Count.ToString(), ["discard"] = _player.Deck.DiscardPile.Count.ToString(),
            ["draw"] = _player.Deck.DrawPileCount.ToString(), ["removed"] = _player.Deck.RemovedCards.Count.ToString(),
            ["unitArt"] = _recruitArt.sprite?.name ?? "",
            ["slots"] = _player.CommandSlots.ToString(), ["freeSlots"] = _player.FreeSlots.ToString(),
            ["commanded"] = _player.BannerCommandCount.ToString(),
            ["selected"] = _context.CommandBannerForRecruit?.Id ?? "none",
            ["owned"] = string.Join(",",_player.Units.Select(u => u.Card.Id)),
            ["faceDown"] = string.Join(",",_player.Units.Select(u => u.CommandBannerFaceDown)),
            ["wounds"] = string.Join(",",_player.Units.Select(u => u.Wounds)),
            ["spent"] = string.Join(",",_damageUnits.Select(u => _case.cardId == "items_003" ? u.CourageBannerSpent : u.FortitudeBannerSpent)),
            ["protection"] = string.Join(",",_damageUnits.Select(u => u.FortitudeProtectionSelected)),
            ["ready"] = string.Join(",",_damageUnits.Select(u => u.IsReady)),
            ["healed"] = _context.HealedUnitsThisTurn.Count.ToString(),
            ["played"] = _player.Deck.PlayedCards.Count.ToString(), ["influence"] = _context.InfluencePool.ToString()
        };
    }

    private static string ArtifactZoneLabel(string zone) => zone switch
    {
        "hand" => "手牌", "played" => "已打出", "discard" => "弃牌", "draw" => "牌库", "removed" => "永久移除",
        null => "无", "" => "无", _ => zone
    };

    private Dictionary<string,string> ArtifactFixtureSnapshot()
    {
        var zones = new[] { _player.Deck.Hand.ToArray(), _player.Deck.PlayedCards.ToArray(),
            _player.Deck.DiscardPile.ToArray(), _player.Deck.DrawPile.ToArray(), _player.Deck.RemovedCards.ToArray() };
        var names = new[] { "hand", "played", "discard", "draw", "removed" };
        if (IsWarp)
        {
            zones = zones.Append(_player.Deck.RoundAsideCards.ToArray()).ToArray();
            names = names.Append("aside").ToArray();
        }
        var all = zones.SelectMany(cards => cards).ToArray();
        var result = new Dictionary<string,string>
        {
            ["influence"] = _context.InfluencePool.ToString(), ["fame"] = _player.Fame.ToString(),
            ["total"] = all.Length.ToString(),
            ["unique"] = all.Where((c,i) => !all.Take(i).Any(other => ReferenceEquals(c,other))).Count().ToString()
        };
        for (int i=0;i<zones.Length;i++) result[names[i]] = zones[i].Length.ToString();
        for (int i=0;i<_artifactFixtureInstances.Length;i++)
            result["zone"+i] = string.Join("+",names.Where((name,j) => zones[j].Any(c => ReferenceEquals(c,_artifactFixtureInstances[i]))));
        return result;
    }

    private string GrailFixture() => _case.cardId == "magic_005"
        ? $"{_context.CurrentTerrain}；治疗点{_context.Healing.Total}；绿色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)} / 黑色{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}；{_context.DayPart}；战斗{_context.InBattle}"
            + $"\n部队等级[{string.Join(",", _player.Units.Select(u => u.Card.Level))}]；所选[{string.Join(",", _case.readyUnitTargets ?? Array.Empty<int>())}]，等级合计{_context.ReadyUnitTargets?.Sum(u => u.Card.Level) ?? _context.TargetUnit?.Card.Level ?? 0} / 上限{(_context.CurrentTerrain == TerrainType.Forest ? 5 : 3)}"
        : $"治疗点{_context.Healing.Total}（圣杯来源{_context.Healing.Grail}）；伤牌{_player.Wounds}；名望{_player.Fame}；治疗抽牌{_context.DrawPerHeal}；绿魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Green)}"
        + $"\n手牌{_player.Deck.Hand.Count} / 牌库{_player.Deck.DrawPileCount} / 已打出{_player.Deck.PlayedCards.Count} / 移除{_player.Deck.RemovedCards.Count}；战斗中{_context.InBattle}";

    private string RecruitmentFixture() => "原版供给：" + string.Join(" / ", _recruitOffer.Select(c => $"{c.NameCn}({c.Id}) 费用{c.InfluenceCost}"))
        + $"\n交涉={_context.InNegotiation}；影响力{_context.InfluencePool}；一次折扣{_context.CharmRecruitDiscount}；队伍{_player.Units.Count} / 指挥槽{_player.CommandSlots}";

    private string DamageFixture() => "原版部队：" + string.Join(" / ", _damageUnits.Select((u, i) => $"{i}:{u.Card.NameCn} 护甲{u.Card.Armor} 伤牌{u.Wounds} 销毁{u.IsDestroyed}"))
        + "\n敌人：" + string.Join(" / ", _context.Enemies.Select((e, i) => $"{i}:攻击{e.Attack}({e.AttackElement}) [{string.Join(",", e.Abilities)}]"));

    private string DamageHistory() => string.Join("\n", Enumerable.Range(0, _operationIndex).Select(i =>
    {
        string p = "operation:" + i + ":";
        return $"步骤{i + 1}：英雄伤牌{_operationResults.GetValueOrDefault(p + "heroWounds")} / 弃牌伤牌{_operationResults.GetValueOrDefault(p + "discardWounds")}；部队伤牌{_operationResults.GetValueOrDefault(p + "unitWounds")}；首伤待用{_operationResults.GetValueOrDefault(p + "ignoreWound")}";
    }));

    private string SpellFixture() => "原法术供给：" + string.Join(" / ", _context.SpellSupply.Offer.Select(s => s.Id))
        + $"\n许可[{string.Join(",", _context.SpellColors)}] · 红/蓝/黑魔力 {_player.Mana.Tokens.GetValueOrDefault(ManaColor.Red)}/{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)}/{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)} · {_context.DayPart} · 手牌{_player.Deck.Hand.Count}/弃牌{_player.Deck.DiscardPile.Count}";

    private void ShowOfferedSpell(int index)
    {
        bool valid = index >= 0 && index < _context.SpellSupply.Offer.Count;
        _recruitArt.gameObject.SetActive(valid);
        _recruitArt.sprite = valid ? _catalog.cards.Single(c => c.source.Id == _context.SpellSupply.Offer[index].Id).artwork : null;
    }

    private AttackAllocation[] BattleAllocations(Phase phase, int target, int[] targets = null)
    {
        var parts = _context.CombatPower.AvailableAttacks(phase);
        return parts.Length == 0 || _battleEnemies.Count == 0 ? Array.Empty<AttackAllocation>()
            : new[] { new AttackAllocation(targets ?? new[] { target }, parts.Sum(p => p.Value), Element.Physical, Components: parts) };
    }

    private void ResolveOperation()
    {
        var operation = _case.operations[_operationIndex];
        string key = "operation:" + _operationIndex;
        _operationResults[key + ":art"] = _art.sprite?.name ?? "";
        _operationResults[key + ":printedRule"] = _printed.text;
        if (IsMysticBox && (operation.kind == "UseMysticBox" || operation.kind == "ProbeArtifactBottom"))
        {
            try
            {
                if (operation.kind == "UseMysticBox")
                {
                    _context.TargetUnit = _player.Units.ElementAtOrDefault(operation.targetIndex);
                    CardPlayActions.UseMysticBox(_player, _context, _artifactFixtureInstances[0], operation.enhanced, operation.option);
                }
                else
                {
                    if (_context.MysticBox != null || _operationIndex == 0 || _case.operations[_operationIndex - 1].kind != "EndTurn"
                        || _operationResults.GetValueOrDefault($"operation:{_operationIndex - 1}:success") != "True")
                        throw new InvalidOperationException("供应底验证必须紧随真实回合结束");
                    _operationResults[key + ":beforeCount"] = _context.ArtifactSupply.Count.ToString();
                    _boxSupplyProbe.Add(_context.ArtifactSupply.Draw());
                    _boxSupplyProbe.Add(_context.ArtifactSupply.Draw());
                    _operationResults[key + ":afterCount"] = _context.ArtifactSupply.Count.ToString();
                    _operationResults[key + ":firstId"] = _boxSupplyProbe[0].Id;
                    _operationResults[key + ":secondId"] = _boxSupplyProbe[1].Id;
                    _operationResults[key + ":firstIdentity"] = ReferenceEquals(_boxSupplyProbe[0], _boxSupplyOriginal[1]).ToString();
                    _operationResults[key + ":secondIdentity"] = ReferenceEquals(_boxSupplyProbe[1], _boxSupplyOriginal[0]).ToString();
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (IsWarp && new[] { "EngineNextTurn", "EngineEndTurn", "DiscardHandExact", "RemoveHandExact",
            "ExploreActual", "RampagingDefense", "RampagingAttack", "RampagingFinish" }.Contains(operation.kind))
        {
            try
            {
                if (operation.kind == "EngineNextTurn")
                {
                    _engineTurnContext = operation.playerIndex == 0 ? _context : _otherContexts[operation.playerIndex - 1];
                    var actual = _roundEngine.BeginNextTurn(_engineTurnContext);
                    _warpTurnOrder.Add(actual.Id);
                    _operationResults[key + ":id"] = actual.Id.ToString();
                }
                else if (operation.kind == "EngineEndTurn")
                {
                    _engineTurnContext.SkipDraw = false;
                    _roundEngine.EndCurrentTurn(_engineTurnContext);
                }
                else if (operation.kind == "DiscardHandExact" || operation.kind == "RemoveHandExact")
                {
                    var original = _artifactFixtureInstances[operation.targetIndex];
                    bool changed = operation.kind == "DiscardHandExact"
                        ? _player.Deck.TryDiscardExact(original) : _player.Deck.TryRemovePermanentlyExact(original);
                    if (!changed) throw new InvalidOperationException("原手牌实例不在手中");
                }
                else if (operation.kind == "ExploreActual")
                {
                    int before = _context.MovementPool;
                    var explored = new ExplorationService(_context.MovementMap, new QuietExplorationRandom())
                        .ExploreUsingPool(_player, new AxialCoord(operation.q, operation.r), operation.option, _context);
                    _operationResults[key + ":cost"] = (before - _context.MovementPool).ToString();
                    if (!explored.Success) throw new InvalidOperationException(explored.ErrorMessage);
                    _warpExplored.Add(explored.Tile);
                    _operationResults[key + ":tileId"] = explored.Tile.Id.ToString();
                    _operationResults[key + ":tileSame"] = _warpOriginalTiles.Any(t => ReferenceEquals(t, explored.Tile)).ToString();
                    _operationResults[key + ":countryCount"] = _context.MovementMap.Countryside.Count.ToString();
                }
                else
                {
                    var machine = _context.RampagingBattle?.Machine ?? throw new InvalidOperationException("没有移动触发的肆虐战斗");
                    if (operation.kind == "RampagingDefense")
                    {
                        machine.ApplyRanged(_context.Enemies, Array.Empty<AttackAllocation>(), _context);
                        machine.SetBlock(0, Element.Physical);
                        var defense = machine.ResolveDefense(_context.Enemies, Array.Empty<BlockAllocation>(), _context);
                        _operationResults[key + ":wounds"] = defense.TotalWounds.ToString();
                    }
                    else if (operation.kind == "RampagingAttack")
                    {
                        _combatResult = machine.Attack(new[] { operation.targetIndex }, _context);
                        _operationResults[key + ":printed"] = _combatResult.PrintedPower.ToString();
                        _operationResults[key + ":effective"] = _combatResult.EffectivePower.ToString();
                        _operationResults[key + ":required"] = _combatResult.RequiredPower.ToString();
                        _operationResults[key + ":kills"] = _combatResult.Kills.ToString();
                        _operationResults[key + ":fame"] = _combatResult.Fame.ToString();
                    }
                    else machine.FinishBattle(_context);
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind.StartsWith("Repeat"))
        {
            try
            {
                if (operation.kind == "RepeatNext" || operation.kind == "RepeatWrongPlayer")
                {
                    var choice=operation.repeatedSelection;
                    DeedCard Selected(int index,bool foreign)
                    {
                        var card=index>=0 && index<_player.Deck.Hand.Count ? _player.Deck.Hand[index] : null;
                        return foreign && card!=null ? new DeedCard(card.Id,card.Type) : card;
                    }
                    CardPlayActions.ContinueRepeatedEffect(operation.kind == "RepeatWrongPlayer" ? new PlayerState() : _player,
                        _context, choice==null ? new RepeatedCardSelection { Option=operation.option } : new RepeatedCardSelection {
                            Option=choice.option,CardToRemove=Selected(choice.removeIndex,choice.foreignRemove),
                            CardToDiscard=Selected(choice.discardIndex,choice.foreignDiscard) });
                }
                else if (operation.kind == "RepeatAttackPhase")
                    CardCombatActions.EnterPhase(_context,Phase.Melee);
                else if (operation.kind == "RepeatBlock")
                    _combatResult = CardCombatActions.Block(_player,_context,_context.Enemies[0]);
                else if (operation.kind == "RepeatEndTurn")
                    new PlayerTurnEngine().EndTurn(_player,_context.DayPart,_context.ManaSource,_context);
                else if (operation.kind == "RepeatMove")
                {
                    if (!new MovementService().TryMoveUsingPool(_player,new AxialCoord(operation.q,operation.r),_context.MovementMap,_context))
                        throw new InvalidOperationException("本次移动未执行");
                }
                else if (operation.kind == "RepeatOtherCard")
                {
                    var card=_player.Deck.Hand.First(c=>c.Id==operation.cardId);
                    CardPlayActions.Play(_player,_context,card,CardJsonLoader.LoadBasicActions().Single(c=>c.Id==card.Id));
                }
                else throw new InvalidOperationException("未知重复效果操作");
                _operationResults[key+":success"]="True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key+":success"]="False";
                _operationResults[key+":exception"]=e.Message;
            }
            _operationResults[key+":remaining"]=(_context.PendingRepeatedEffect?.Remaining ?? 0).ToString();
            _operationResults[key+":spells"]=string.Join(",",_context.SpellSupply.Offer.Select(c=>c.Id));
            _operationResults[key+":discardIds"]=string.Join(",",_player.Deck.DiscardPile.Select(c=>c.Id));
            _operationResults[key+":ranged"]=_context.RangedPool.ToString();
            _operationResults[key+":q"]=_player.Position.Q.ToString();
        }
        else if (operation.kind == "ChooseExplosiveBoltArmor")
        {
            try
            {
                Monster target = operation.targetIndex < 0 ? null : _context.Enemies[operation.targetIndex];
                _operationResults[key + ":targetId"] = target?.Id ?? "放弃";
                _operationResults[key + ":armorBefore"] = (target == null ? 0 : CombatMath.EffectiveArmor(target, operation.targetIndex, _context)).ToString();
                CardCombatActions.ResolveExplosiveBoltArmor(_context, target);
                _operationResults[key + ":armorAfter"] = (target == null ? 0 : CombatMath.EffectiveArmor(target, operation.targetIndex, _context)).ToString();
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e) { _operationResults[key + ":success"] = "False"; _operationResults[key + ":exception"] = e.Message; }
            _operationResults[key + ":uses"] = _context.ExplosiveBoltArmorUses.ToString();
        }
        else if (operation.kind.StartsWith("Turn"))
        {
            int beforeFame = _player.Fame;
            _operationResults[key + ":blockBefore"] = _context.BlockPool.ToString();
            try
            {
                if (_battleMachine == null && operation.kind == "TurnBegin")
                {
                    _battleGame = new GameState(); _battleGame.Players.Add(_player);
                    _battleMachine = new TurnMachine(_battleGame);
                }
                if (_battleMachine == null) throw new InvalidOperationException("未配置正式战斗调度器");
                if (operation.kind == "TurnBegin")
                {
                    _battleMachine.BeginBattle(_context);
                    _battleEnemies = _case.cardId == "magic_004" ? _context.Enemies.ToList() : _case.enemies.Select(e => e.Create()).ToList();
                    _context.Enemies = _battleEnemies;
                }
                else if (operation.kind == "TurnCloseBlock") _battleMachine.SetBlock(0,Element.Physical);
                else if (operation.kind == "TurnRanged")
                {
                    int count = _battleEnemies.Count;
                    var allocations = operation.option == 1 ? Array.Empty<AttackAllocation>() : BattleAllocations(Phase.Ranged, operation.targetIndex, operation.attackTargets);
                    if ((_case.cardId == "advanced_card_012" || IsExplosiveBolt || IsBloodSurge || IsColdStare) && allocations.Length == 1)
                    {
                        var targets = operation.attackTargets ?? new[] { operation.targetIndex };
                        var parts = _context.CombatPower.AvailableAttacks(Phase.Ranged, targets, _battleEnemies);
                        _operationResults[key + ":printed"] = parts.Sum(p => p.Value).ToString();
                        _operationResults[key + ":effective"] = CombatMath.EffectiveAttack(_battleEnemies, targets, parts, Phase.Ranged, _context).ToString();
                        _operationResults[key + ":required"] = targets.Sum(i => CombatMath.EffectiveArmor(_battleEnemies[i], i, _context)).ToString();
                    }
                    _battleMachine.ApplyRanged(_battleEnemies, allocations, _context);
                    _operationResults[key + ":kills"] = (count - _battleEnemies.Count).ToString();
                }
                else if (operation.kind == "TurnResolve" || operation.kind == "TurnDefense")
                {
                    var blocks = _battleEnemies.Count == 0 ? Array.Empty<BlockAllocation>()
                        : _context.CombatPower.AvailableBlocks(operation.targetIndex).Select(b => b with { TargetIndex = operation.targetIndex, AttackIndex = operation.attackIndex }).ToArray();
                    if ((_case.cardId == "magic_004" || _case.cardId == "advanced_card_036" || _case.cardId == "advanced_card_013" || _case.cardId == "advanced_card_023" || IsPeacefulOrResolve || IsNatureOrLore || IsManaPower || IsExplosiveBolt || IsBloodSurge || IsColdStare) && _battleEnemies.Count > operation.targetIndex)
                    {
                        var defender = CardCombatActions.DefenseProfile(_battleEnemies[operation.targetIndex], operation.targetIndex, _context, operation.attackIndex);
                        var element = _context.AttackElementChange.TryGetValue(operation.targetIndex,out var changedElement) ? changedElement : defender.AttackElement;
                        var ignored = _context.IgnoredAbilities.TryGetValue(operation.targetIndex,out var ignoredAbility) ? new HashSet<Ability>{ignoredAbility} : null;
                        _operationResults[key + ":blockPrinted"] = blocks.Sum(b=>b.Value).ToString();
                        _operationResults[key + ":blockEffective"] = CombatMath.EffectiveBlock(element,CombatMath.ResolveBlocks(defender,blocks)).ToString();
                        _operationResults[key + ":blockRequired"] = AbilityRules.RequiredBlock(defender,ignored).ToString();
                    }
                    if (_battleGame.CurrentPhase == Phase.Block)
                        _battleMachine.SetBlock(blocks.Sum(b => b.Value), blocks.Length == 0 ? Element.Physical : blocks[0].Element);
                    UnitDamageAllocation[] unitAssignments = operation.unitTargets == null ? null
                        : new[] { new UnitDamageAllocation(operation.targetIndex, operation.unitTargets.Select(i => _player.Units[i]).ToArray()) };
                    var result = operation.kind == "TurnDefense"
                        ? _battleMachine.ResolveDefense(_battleEnemies, blocks, _context, unitAssignments)
                        : _battleMachine.ResolveBattle(_battleEnemies, blocks, BattleAllocations(Phase.Melee, operation.targetIndex), ctx: _context);
                    _operationResults[key + ":wounds"] = result.TotalWounds.ToString();
                    if (operation.unitTargets != null) _operationResults[key + ":assignedUnits"] = _context.DamageAssignedUnits.Count.ToString();
                    _operationResults[key + ":kills"] = result.KilledIndices.Count.ToString();
                }
                else if (operation.kind == "TurnAttack")
                {
                    var result = _battleMachine.Attack(operation.attackTargets ?? new[] { operation.targetIndex }, _context);
                    _operationResults[key + ":printed"] = result.PrintedPower.ToString();
                    _operationResults[key + ":effective"] = result.EffectivePower.ToString();
                    _operationResults[key + ":required"] = result.RequiredPower.ToString();
                    _operationResults[key + ":kills"] = result.Kills.ToString();
                }
                else if (operation.kind == "TurnFinish") _battleMachine.FinishBattle(_context);
                else throw new InvalidOperationException("未知调度器操作");
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":phase"] = _battleGame?.CurrentPhase.ToString() ?? "None";
            _operationResults[key + ":inBattle"] = _context.InBattle.ToString();
            _operationResults[key + ":rangedRemaining"] = (_context.RangedPool + _context.SiegePool).ToString();
            _operationResults[key + ":meleeRemaining"] = _context.MeleePool.ToString();
            _operationResults[key + ":red"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString();
            _operationResults[key + ":black"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Black).ToString();
            _operationResults[key + ":white"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.White).ToString();
            _operationResults[key + ":blue"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString();
            _operationResults[key + ":reputation"] = _player.Reputation.ToString();
            _operationResults[key + ":fame"] = (_player.Fame - beforeFame).ToString();
            _operationResults[key + ":enemyArmor"] = string.Join(",", _battleEnemies.Select((e, i) => CombatMath.EffectiveArmor(e, i, _context)));
            _operationResults[key + ":skip"] = string.Join(",", _context.SkipAttackIndices.OrderBy(i => i));
        }
        else if (operation.kind == "Damage")
        {
            var targets = (operation.unitTargets ?? Array.Empty<int>()).Select(i => _damageUnits[i]).ToArray();
            try
            {
                BattleResolver.Resolve(_player, new[] { _context.Enemies[operation.targetIndex] },
                    Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: _context,
                    damageAssignments: new[] { new UnitDamageAllocation(0, targets) });
                _operationResults[key + ":success"] = "True";
                _operationResults[key + ":exception"] = "";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "PeacefulHealing" || operation.kind == "PeacefulReady")
        {
            int influenceBefore = _context.InfluencePool;
            try
            {
                if (operation.kind == "PeacefulHealing")
                    PeacefulMomentActions.ConvertHealing(_player, _context, operation.option);
                else if (operation.targetIndex < 0 || operation.targetIndex >= _player.Units.Count
                    || !CardUnitActions.TryReady(_player, _context, _player.Units[operation.targetIndex], peacefulMoment: true))
                    throw new InvalidOperationException("宁静时光无法重整该部队");
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":cost"] = (influenceBefore - _context.InfluencePool).ToString();
        }
        else if (operation.kind == "HealHand" || operation.kind == "HealUnit")
        {
            try
            {
                UnitState target = null;
                if (operation.kind == "HealUnit")
                {
                    if (operation.targetIndex < 0 || operation.targetIndex >= _player.Units.Count)
                        throw new InvalidOperationException("只能治疗未销毁的自有部队");
                    target = _player.Units[operation.targetIndex];
                }
                CardHealing.Spend(_player, _context, operation.option, target);
                _operationResults[key + ":success"] = "True";
                _operationResults[key + ":exception"] = "";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "OtherUnitUse")
        {
            var other = _context.OtherPlayers[operation.q];
            var otherContext = new ActionContext { InBattle = true };
            try
            {
                CardUnitActions.Use(other, otherContext, other.Units[operation.targetIndex], operation.option);
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":attack"] = otherContext.MeleePool.ToString();
        }
        else if (operation.kind == "OfferedUnitUse" || operation.kind == "OfferedUnitDamage")
        {
            try
            {
                if (operation.kind == "OfferedUnitUse")
                    CardUnitActions.UseFromOffer(_player, _context, operation.targetIndex, operation.option, operation.abilityChoice);
                else
                {
                    if (operation.targetIndex < 0 || operation.targetIndex >= _recruitOffer.Count)
                        throw new InvalidOperationException("必须选择真实部队供应中的部队");
                    UnitDamageResolver.Assign(_player, _context, new UnitState(_recruitOffer[operation.targetIndex]),
                        _context.Enemies[0], 3, Element.Physical);
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "UnitUse")
        {
            try
            {
                if (operation.targetIndex < 0 || operation.targetIndex >= _player.Units.Count)
                    throw new InvalidOperationException("只能发动自有部队");
                CardUnitActions.Use(_player, _context, _player.Units[operation.targetIndex], operation.option, operation.abilityChoice);
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "SkillUse")
        {
            try
            {
                SkillActions.Use(_player,_context,_player.Skills[operation.targetIndex],operation.option);
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "ChooseProtectionCleanup")
        {
            try
            {
                BannerActions.ChooseProtectionCleanup(_context,operation.option == 1);
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "UseCourage" || operation.kind == "UseFear" || operation.kind == "SelectFortitudeProtection")
        {
            try
            {
                var target = operation.targetIndex >= 0 && operation.targetIndex < _player.Units.Count ? _player.Units[operation.targetIndex] : null;
                if (operation.kind == "UseCourage") BannerActions.UseCourage(_player,_context,target);
                else if (operation.kind == "UseFear") BannerActions.UseFear(_player,_context,target,operation.option);
                else BannerActions.SelectFortitudeProtection(_player,target,operation.option == 1);
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "SelectCommandBanner" || operation.kind == "AssignCommandBanner" || operation.kind == "SelectUnit" || operation.kind == "NewRound")
        {
            try
            {
                if (operation.kind == "NewRound") new GameEngine(new[] { _player },3).StartNewRound(Array.Empty<TacticCard>());
                else if (operation.kind == "SelectUnit")
                {
                    if (operation.targetIndex < 0 || operation.targetIndex >= _player.Units.Count) throw new InvalidOperationException("必须选择自有部队");
                    _context.TargetUnit = _player.Units[operation.targetIndex];
                }
                else if (operation.kind == "SelectCommandBanner" && operation.option == 1)
                    BannerActions.SelectCommandForRecruit(_player,_context,null);
                else
                {
                    var banner = _player.Deck.PlayedCards.Where(c => c.Id == "items_017").ElementAtOrDefault(operation.option);
                    if (banner == null) throw new InvalidOperationException("没有可选择的已打出指挥旌旗");
                    if (operation.kind == "SelectCommandBanner") BannerActions.SelectCommandForRecruit(_player,_context,banner);
                    else
                    {
                        var target = operation.targetIndex >= 0 && operation.targetIndex < _player.Units.Count ? _player.Units[operation.targetIndex] : null;
                        BannerActions.AssignCommand(_player,_context,target,banner);
                    }
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "ChooseBannerReturn" || operation.kind == "EndRound" || operation.kind == "Disband")
        {
            try
            {
                if (operation.kind == "EndRound")
                {
                    (_roundEngine ?? new GameEngine(new[] { _player }, 3)).EndRound();
                    if (_roundEngine != null) _context.DayPart = _roundEngine.Clock.DayPart;
                }
                else
                {
                    if (operation.targetIndex < 0 || operation.targetIndex >= _player.Units.Count)
                        throw new InvalidOperationException("必须选择自有部队");
                    var unit = _player.Units[operation.targetIndex];
                    if (operation.kind == "Disband") _player.Disband(unit);
                    else BannerActions.ChooseRoundReturn(_player,unit,operation.option == 1);
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "BeginBattle")
        {
            _context.InBattle = true;
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "EndBattle")
        {
            UnitDamageResolver.EndBattle(_player, _context);
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "SelfWound")
        {
            _player.Wounds += 1;
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "EnterMelee")
        {
            CardCombatActions.EnterPhase(_context, Phase.Melee);
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "ConvertMoveToAttack" || operation.kind == "ConvertMoveToRanged"
            || operation.kind == "ConvertInfluenceToBlock")
        {
            var actions = new ActionSystem(false);
            int before = operation.kind == "ConvertInfluenceToBlock" ? _context.InfluencePool : _context.MovementPool;
            bool success = operation.kind == "ConvertMoveToAttack" ? actions.ConvertMoveToAttack(_context, operation.option)
                : operation.kind == "ConvertMoveToRanged" ? actions.ConvertMoveToRanged(_context, operation.option)
                : actions.ConvertInfluenceToBlock(_context, operation.option);
            int after = operation.kind == "ConvertInfluenceToBlock" ? _context.InfluencePool : _context.MovementPool;
            _operationResults[key + ":success"] = success.ToString();
            _operationResults[key + ":cost"] = (before - after).ToString();
            _operationResults[key + ":produced"] = (success ? operation.option : 0).ToString();
        }
        else if (operation.kind == "ReduceEnemyAttack")
        {
            try
            {
                CardCombatActions.ReduceEnemyAttack(_context, operation.targetIndex, operation.attackIndex);
                var originalAttack = _context.Enemies[operation.targetIndex].GetAttack(operation.attackIndex);
                int attack = CityCombatRules.DefenseProfile(_context.Enemies[operation.targetIndex], _context, originalAttack).Attack;
                _operationResults[key + ":before"] = attack.ToString();
                _operationResults[key + ":after"] = CardCombatActions.DefenseProfile(_context.Enemies[operation.targetIndex], operation.targetIndex, _context, operation.attackIndex).Attack.ToString();
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "Block" || operation.kind == "Attack" || operation.kind == "RangedAttack")
        {
            try
            {
            var enemies = _context.Enemies.ToList();
            var result = operation.kind == "Block"
                ? CardCombatActions.Block(_player, _context, enemies, operation.targetIndex, operation.attackIndex)
                : CardCombatActions.Attack(_player, _context, enemies, new[] { operation.targetIndex }, operation.kind == "RangedAttack" ? Phase.Ranged : Phase.Melee);
            _operationResults[key + ":printed"] = result.PrintedPower.ToString();
            _operationResults[key + ":effective"] = result.EffectivePower.ToString();
            _operationResults[key + ":required"] = result.RequiredPower.ToString();
            _operationResults[key + ":kills"] = result.Kills.ToString();
            _operationResults[key + ":fame"] = result.Fame.ToString();
            _operationResults[key + ":wounds"] = result.Wounds.ToString();
            _operationResults[key + ":MeleePool"] = _context.MeleePool.ToString();
            _operationResults[key + ":armor0"] = _context.ArmorReduction.GetValueOrDefault(0).ToString();
            _operationResults[key + ":armor1"] = _context.ArmorReduction.GetValueOrDefault(1).ToString();
            _operationResults[key + ":success"] = "True";
            _operationResults[key + ":exception"] = "";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":BlockPool"] = _context.BlockPool.ToString();
            _operationResults[key + ":MeleePool"] = _context.MeleePool.ToString();
        }
        else if (operation.kind == "CastOffer")
        {
            try
            {
                CardSpellActions.PlayFromOffer(_player, _context, operation.targetIndex, operation.enhanced, operation.option);
                _operationResults[key + ":success"] = "True";
                _operationResults[key + ":exception"] = "";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":permissions"] = _context.SpellColors.Count.ToString();
            _operationResults[key + ":RangedPool"] = _context.RangedPool.ToString();
            _operationResults[key + ":SiegePool"] = _context.SiegePool.ToString();
            _operationResults[key + ":offer"] = string.Join(",", _context.SpellSupply.Offer.Select(s => s.Id));
        }
        else if (operation.kind == "Recruit")
        {
            var location = (RecruitLocation)Enum.Parse(typeof(RecruitLocation), operation.recruitLocation);
            var candidate = operation.targetIndex >= 0 && operation.targetIndex < _recruitOffer.Count ? _recruitOffer[operation.targetIndex] : null;
            _operationResults[key + ":unitId"] = candidate?.Id ?? "";
            _operationResults[key + ":cost"] = candidate == null ? "-1" : RecruitmentService.GetCost(_player, candidate, location, _context).ToString();
            _operationResults[key + ":success"] = CardUnitActions.TryRecruit(_player, _context, _recruitOffer, operation.targetIndex, location).ToString();
            _operationResults[key + ":influence"] = _context.InfluencePool.ToString();
            _operationResults[key + ":discount"] = _context.CharmRecruitDiscount.ToString();
            _operationResults[key + ":offer"] = string.Join(",", _recruitOffer.Select(c => c.Id));
            _operationResults[key + ":units"] = _player.Units.Count.ToString();
        }
        else if (operation.kind == "PlayHand")
        {
            try
            {
                CardPlayActions.Play(_player, _context, _player.Deck.Hand.FirstOrDefault(c => c.Id == _case.cardId),
                    Data(_entry.source, false));
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "Learn")
        {
            _operationResults[key+":beforeInfluence"]=_context.InfluencePool.ToString();
            _operationResults[key+":requestedCost"]=(operation.enhanced?9:6).ToString();
            _operationResults[key+":selectedId"]=operation.option>=0 && operation.option<_context.AdvActionSupply.Offer.Count
                ? _context.AdvActionSupply.Offer[operation.option].Id : "";
            try
            {
                var gained=CardLearningActions.Purchase(_player,_context,operation.option,operation.enhanced);
                _operationResults[key+":success"]="True";
                _operationResults[key+":gained"]=gained.Id;
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key+":success"]="False";
                _operationResults[key+":exception"]=e.Message;
            }
        }
        else if (operation.kind == "BeginInteraction")
        {
            RecruitmentService.BeginInteraction(_player, _context,
                (RecruitLocation)Enum.Parse(typeof(RecruitLocation), operation.recruitLocation));
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "EndMarketRound")
        {
            if (_roundEngine==null) throw new InvalidOperationException("轮次验收未绑定真实GameEngine");
            _roundEngine.EndRound();
            _context.DayPart=_roundEngine.Clock.DayPart;
            _operationResults[key+":success"]="True";
        }
        else if (operation.kind == "SelectRemoval")
        {
            var material=operation.targetIndex>=0 && operation.targetIndex<_artifactFixtureInstances.Length
                ? _artifactFixtureInstances[operation.targetIndex] : null;
            bool held=material!=null && _player.Deck.Hand.Any(c=>ReferenceEquals(c,material));
            if (held) _context.CardToRemove=material;
            _operationResults[key+":success"]=held.ToString();
        }
        else if (operation.kind == "PlayArtifactInstance" || operation.kind == "UnownedArtifact")
        {
            try
            {
                var artifact = (ItemCardSO)_catalog.cards.Single(c => c.source.Id == operation.cardId).source;
                if (_case.cardId=="items_013" && operation.cardId=="items_013")
                {
                    var offer=(operation.enhanced?_context.SpellSupply.Offer.Select(c=>c.Id):_context.AdvActionSupply.Offer.Select(c=>c.Id)).ToArray();
                    if (operation.option>=0 && operation.option<offer.Length)
                    {
                        _wisdomOfferId=offer[operation.option];
                        _wisdomOfferIndex=operation.option;
                    }
                }
                DeedCard instance = operation.kind == "UnownedArtifact"
                    ? new DeedCard(artifact.Id,CardType.Artifact)
                    : operation.targetIndex >= 0 && operation.targetIndex < _artifactFixtureInstances.Length
                        ? _artifactFixtureInstances[operation.targetIndex] : null;
                CardPlayActions.PlayArtifact(_player,_context,instance,
                    new ItemCardData(artifact.Id,artifact.NameCn,artifact.Set,artifact.ImagePath,
                        artifact.EnImagePath,"Artifact",artifact.AssignEffect,artifact.OncePerRound),operation.enhanced,operation.option);
                _operationResults[key+":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key+":success"] = "False";
                _operationResults[key+":exception"] = e.Message;
            }
        }
        else if (operation.kind == "PlaySidewaysMove")
        {
            var card = _player.Deck.Hand.FirstOrDefault(c => c.Id == operation.cardId);
            _operationResults[key + ":success"] = (card != null
                && new ActionSystem(false).PlaySideways(card, _player, _context, SidewaysType.Move)).ToString();
        }
        else if (operation.kind == "PlayCard" || operation.kind == "ChooseStarBow")
        {
            try
            {
                if (operation.kind == "ChooseStarBow") CardCombatActions.ChooseStarBow(_context, operation.targetIndex, operation.option);
                else
                {
                    var original = _catalog.cards.Single(c => c.source.Id == operation.cardId);
                    _context.SelectedActionManaColor = string.IsNullOrEmpty(operation.selectedActionManaColor)
                        ? (ManaColor?)null : (ManaColor)Enum.Parse(typeof(ManaColor), operation.selectedActionManaColor);
                    _context.ManaPowerBonusChoices.Clear();
                    _context.ManaPowerBonusChoices.AddRange(operation.manaPowerBonusChoices ?? Array.Empty<int>());
                    if (operation.cardId == "magic_004" && _case.travelPath != null)
                    {
                        _context.TravelPath.Clear();
                        _context.TravelPath.AddRange(_case.travelPath.Select(h=>new AxialCoord(h.q,h.r)));
                    }
                    var instance = _player.Deck.Hand.FirstOrDefault(c => c.Id == operation.cardId);
                    if (original.source is ItemCardSO artifact)
                        CardPlayActions.PlayArtifact(_player, _context, instance,
                            new ItemCardData(artifact.Id, artifact.NameCn, artifact.Set, artifact.ImagePath,
                                artifact.EnImagePath, "Artifact", artifact.AssignEffect, artifact.OncePerRound), operation.enhanced, operation.option);
                    else CardPlayActions.Play(_player, _context, instance,
                        Data(original.source, operation.enhanced), operation.enhanced, operation.option);
                }
                _operationResults[key + ":success"] = "True";
                _operationResults[key + ":exception"] = "";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "ChooseMeltdownCrystal" || operation.kind == "SelectJoyfulReturn")
        {
            try
            {
                if (operation.kind == "ChooseMeltdownCrystal")
                    ManaMeltdownEffect.ChooseCrystal(_player, _context, operation.option < 0 ? (ManaColor?)null : (ManaColor)operation.option);
                else
                    CardPlayActions.SelectJoyfulManaReturn(_player, _context,
                        _player.Deck.PlayedCards.Single(c=>c.Id==_case.cardId), _player.Deck.Hand[operation.targetIndex]);
                _operationResults[key+":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key+":success"] = "False";
                _operationResults[key+":exception"] = e.Message;
            }
        }
        else if (operation.kind == "SelectSteadyTempoReturn")
        {
            try
            {
                var card = _artifactFixtureInstances[operation.targetIndex];
                CardPlayActions.SelectSteadyTempoReturn(_player, _context, card, operation.option == 1);
                _operationResults[key + ":success"] = "True";
                _operationResults[key + ":choice"] = _context.SteadyTempoReturns.Contains(card) ? "回收" : "普通弃牌";
            }
            catch (InvalidOperationException e) { _operationResults[key + ":success"] = "False"; _operationResults[key + ":exception"] = e.Message; }
        }
        else if (operation.kind == "ConvertGold")
        {
            try { ManaTokenActions.ConvertGold(_player,_context,(ManaColor)operation.option); _operationResults[key+":success"]="True"; }
            catch(InvalidOperationException e) { _operationResults[key+":success"]="False"; _operationResults[key+":exception"]=e.Message; }
        }
        else if (operation.kind == "Teleport")
        {
            _operationResults[key + ":success"] = CardTravelActions.TryTemporalPortal(_player, _context, new AxialCoord(operation.q, operation.r)).ToString();
        }
        else if (operation.kind == "Move" || operation.kind == "MoveHarvest")
        {
            var to = new AxialCoord(operation.q, operation.r);
            _operationResults[key + ":cost"] = MovementService.GetCost(to, _context.MovementMap, _context.DayPart, _context).ToString();
            var material = operation.kind == "MoveHarvest" ? _artifactFixtureInstances[operation.targetIndex] : null;
            _operationResults[key + ":material"] = material == null ? ""
                : _catalog.cards.Single(c => c.source.Id == material.Id).source.NameCn;
            int before = _context.MovementPool;
            try
            {
                _operationResults[key + ":success"] = new MovementService().TryMoveUsingPool(_player, to,
                    _context.MovementMap, _context, harvestCard: material,
                    harvestColor: material == null ? (ManaColor?)null : (ManaColor)operation.option).ToString();
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
            _operationResults[key + ":spent"] = (before - _context.MovementPool).ToString();
        }
        else if (operation.kind == "Ready")
        {
            var target = operation.targetIndex < 0 ? null : operation.targetIndex < _player.Units.Count ? _player.Units[operation.targetIndex]
                : new UnitState(_player.Units[0].Card);
            if (target != null && !_player.Units.Contains(target)) target.Exhaust();
            _operationResults[key + ":success"] = CardUnitActions.TryReady(_player, _context, target).ToString();
        }
        else if (operation.kind == "StartTurn")
        {
            new PlayerTurnEngine().StartTurn(_player, _context.DayPart);
            _operationResults[key + ":success"] = "True";
        }
        else if (operation.kind == "TakeManaDie")
        {
            _operationResults[key + ":success"] = (_context.ManaSource.Take(_player, (ManaColor)operation.option) != null).ToString();
        }
        else if (operation.kind == "UseSourceDie" || operation.kind == "UseExtraSourceDie")
        {
            try
            {
                CardManaActions.UseSourceDie(_player, _context, _originalManaDice[operation.targetIndex],
                    (ManaColor)operation.option, operation.kind == "UseExtraSourceDie");
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "OtherPlayCard" || operation.kind == "OtherStartTurn" || operation.kind == "OtherEndTurn")
        {
            var other = _context.OtherPlayers[operation.playerIndex];
            var context = _otherContexts[operation.playerIndex];
            context.DayPart = _context.DayPart;
            try
            {
                if (operation.kind == "OtherStartTurn") new PlayerTurnEngine().StartTurn(other, context.DayPart);
                else if (operation.kind == "OtherEndTurn")
                {
                    context.SkipDraw = !operation.refill;
                    new PlayerTurnEngine().EndTurn(other, context.DayPart, context.ManaSource, context);
                }
                else
                {
                    var original = _catalog.cards.Single(c => c.source.Id == operation.cardId);
                    CardPlayActions.Play(other, context, other.Deck.Hand.FirstOrDefault(c => c.Id == operation.cardId),
                        Data(original.source, operation.enhanced), operation.enhanced, operation.option);
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "FinishTunnel" || operation.kind == "SelectSpellReward" || operation.kind == "TryEndTurn")
        {
            try
            {
                if (operation.kind == "FinishTunnel") CardTravelActions.FinishTunnelAssault(_player,_context,operation.option == 1);
                else if (operation.kind == "SelectSpellReward") FortifiedSiteActions.SelectSpellReward(_context,operation.option);
                else
                {
                    _context.CardToRecycle = null; _context.SkipDraw = !operation.refill;
                    new PlayerTurnEngine().EndTurn(_player,_context.DayPart,_context.ManaSource,_context);
                }
                _operationResults[key + ":success"] = "True";
            }
            catch (InvalidOperationException e)
            {
                _operationResults[key + ":success"] = "False";
                _operationResults[key + ":exception"] = e.Message;
            }
        }
        else if (operation.kind == "DiscardNonWoundHand")
        {
            foreach (var card in _player.Deck.Hand.Where(c => c.Type != CardType.Wound).ToArray())
                _player.Deck.TryDiscardExact(card);
        }
        else if (operation.kind == "EndTurn")
        {
            if (_case.cardId == "advanced_card_039")
            {
                _operationResults[key + ":endingTerrain"] = _context.MovementMap.Placed[_player.Position].Edges[0].ToString();
                _operationResults[key + ":safeBeforeEnd"] = CardTravelActions.IsSafeDestination(_player, _context, _player.Position, _context.OtherPlayers).ToString();
            }
            _context.CardToRecycle = null; _context.SkipDraw = !operation.refill;
            new PlayerTurnEngine().EndTurn(_player, _context.DayPart, _context.ManaSource, _context);
            _operationResults[key + ":success"] = "True";
        }
        else throw new InvalidOperationException("Unknown verification operation " + operation.kind);
        if (_battleGame != null)
        {
            _operationResults[key + ":enemyArmor"] = string.Join(",", _battleEnemies.Select((e, i) => CombatMath.EffectiveArmor(e, i, _context)));
            _operationResults[key + ":defeated"] = string.Join(",", _context.DefeatedEnemyIndices.OrderBy(i => i));
            _operationResults[key + ":meleeRemaining"] = _context.MeleePool.ToString();
            _operationResults[key + ":totalFame"] = _player.Fame.ToString();
            _operationResults[key + ":white"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.White).ToString();
            _operationResults[key + ":fireRemoved"] = _context.FireResistRemoved.Count.ToString();
            _operationResults[key + ":red"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString();
            _operationResults[key + ":siteIgnored"] = _context.IgnoreSiteFortifications.ToString();
            _operationResults[key + ":green"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Green).ToString();
            _operationResults[key + ":fortRemoved"] = string.Join(",", _context.FortificationsRemoved.OrderBy(i => i));
            _operationResults[key + ":resRemoved"] = string.Join(",", _context.ResistancesRemoved.OrderBy(i => i));
            _operationResults[key + ":rangedRemaining"] = (_context.RangedPool + _context.SiegePool).ToString();
            _operationResults[key + ":skip"] = string.Join(",", _context.SkipAttackIndices.OrderBy(i => i));
            _operationResults[key + ":blue"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString();
            _operationResults[key + ":black"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Black).ToString();
            _operationResults[key + ":played"] = _player.Deck.PlayedCards.Count.ToString();
            _operationResults[key + ":discard"] = _player.Deck.DiscardPile.Count.ToString();
        }
        if (_case.cardId == "advanced_card_032")
        {
            _operationResults[key + ":q"] = _player.Position.Q.ToString();
            _operationResults[key + ":hand"] = _player.Deck.Hand.Count.ToString();
            _operationResults[key + ":draw"] = _player.Deck.DrawPileCount.ToString();
            _operationResults[key + ":bonus"] = _context.NextDrawBonus.ToString();
            _operationResults[key + ":range"] = _context.TeleportRange.ToString();
            _operationResults[key + ":blue"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString();
            _operationResults[key + ":played"] = _player.Deck.PlayedCards.Count.ToString();
            _operationResults[key + ":discard"] = _player.Deck.DiscardPile.Count.ToString();
        }
        if (_case.cardId == "magic_002")
        {
            _operationResults[key + ":hand"] = _player.Deck.Hand.Count.ToString();
            _operationResults[key + ":draw"] = _player.Deck.DrawPileCount.ToString();
            _operationResults[key + ":bonus"] = _context.NextDrawBonus.ToString();
            _operationResults[key + ":discard"] = _player.Deck.DiscardPile.Count.ToString();
        }
        if (_case.unitIds?.Length > 0)
        {
            _operationResults[key + ":heroWounds"] = _player.Wounds.ToString();
            _operationResults[key + ":discardWounds"] = _player.DiscardWounds.ToString();
            _operationResults[key + ":unitWounds"] = string.Join(",", _damageUnits.Select(u => u.Wounds));
            _operationResults[key + ":destroyed"] = string.Join(",", _damageUnits.Select(u => u.IsDestroyed));
            _operationResults[key + ":ignoreWound"] = _context.IgnoreNextWound.ToString();
            _operationResults[key + ":allResist"] = _context.UnitsResistAll.ToString();
        }
        if (_case.recruitOffer != null)
        {
            _operationResults[key + ":influence"] = _context.InfluencePool.ToString();
            _operationResults[key + ":owned"] = string.Join(",",_player.Units.Select(u=>u.Card.Id));
            _operationResults[key + ":ready"] = string.Join(",",_player.Units.Select(u=>u.IsReady));
            _operationResults[key + ":offer"] = string.Join(",",_recruitOffer.Select(u=>u.Id));
            if (_case.cardId == "magic_020" && !_case.enhanced)
            {
                _operationResults[key + ":permissions"] = _context.OfferedUnitUses.ToString();
                _operationResults[key + ":move"] = _context.MovementPool.ToString();
                _operationResults[key + ":attack"] = _context.MeleePool.ToString();
                _operationResults[key + ":block"] = _context.BlockPool.ToString();
                _operationResults[key + ":ranged"] = _context.RangedPool.ToString();
                _operationResults[key + ":art"] = _recruitArt.sprite?.name ?? "";
                _operationResults[key + ":blue"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString();
            }
        }
        if (_case.cardId == "magic_004")
        {
            ShowGuardToken();
            _operationResults[key + ":q"] = _player.Position.Q.ToString();
            _operationResults[key + ":active"] = _context.TunnelAssaultActive.ToString();
            _operationResults[key + ":siteIgnored"] = _context.IgnoreSiteFortifications.ToString();
            _operationResults[key + ":handWounds"] = _player.Wounds.ToString();
            foreach (var entry in TunnelWorldSnapshot()) _operationResults[key + ":" + entry.Key] = entry.Value;
        }
        if (IsGemRing)
        {
            _operationResults[key + ":fame"] = _player.Fame.ToString();
            _operationResults[key + ":infinite"] = _context.InfiniteMana.Count.ToString();
            _operationResults[key + ":rewards"] = _context.FamePerSpell.Count.ToString();
        }
        if (_case.cardId == "advanced_card_016")
        {
            _operationResults[key + ":bonus"] = _context.ChargeUnitBonus.ToString();
            _operationResults[key + ":ban"] = _context.NoUnitDamage.ToString();
            _operationResults[key + ":attack"] = _context.MeleePool.ToString();
            _operationResults[key + ":block"] = _context.BlockPool.ToString();
            _operationResults[key + ":ranged"] = _context.RangedPool.ToString();
            _operationResults[key + ":siege"] = _context.SiegePool.ToString();
            _operationResults[key + ":unitArt"] = _recruitArt.sprite?.name ?? "";
            _operationResults[key + ":red"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString();
        }
        if (IsBannerScenario)
            foreach (var entry in BannerSnapshot()) _operationResults[key + ":" + entry.Key] = entry.Value;
        if (_case.cardId == "items_010")
            foreach (var entry in ArtifactFixtureSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (_case.cardId == "items_009")
            foreach (var entry in WrathSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsAmulet)
            foreach (var entry in AmuletSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsSupplyExchange)
            foreach (var entry in WisdomSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsRitualLifecycle)
            foreach (var entry in RitualSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsLearning)
            foreach (var entry in LearningSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsBloodline)
            foreach (var entry in BloodlineSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsDecompose || IsOverload)
            foreach (var entry in DecomposeSnapshot()) _operationResults[key+":"+entry.Key] = entry.Value;
        if (IsOverloadSpellPermission)
        {
            _operationResults[key+":permissions"]=_context.SpellColors.Count.ToString();
            _operationResults[key+":remaining"]=(_context.PendingRepeatedEffect?.Remaining ?? 0).ToString();
            _operationResults[key+":battleWounds"]=_context.BattleWoundsReceived.ToString();
            _operationResults[key+":knockedOut"]=_context.IsKnockedOut.ToString();
        }
        if (_case.basicAcceptance)
        {
            // Capture only independently requested observations, never expected values.
            string prefix = key + ":state:";
            var state = Snapshot();
            foreach (var expected in _case.expected.Where(e => e.key.StartsWith(prefix)))
                if (state.TryGetValue(expected.key.Substring(prefix.Length), out var observed))
                    _operationResults[expected.key] = observed;
        }
        _operationIndex++;
        if (IsHealingCard)
        {
            _operationResults[key + ":healing"] = _context.Healing.Total.ToString();
            _operationResults[key + ":fame"] = _player.Fame.ToString();
            _operationResults[key + ":handWounds"] = _player.Wounds.ToString();
        }
        if (_operationIndex < _case.operations.Length) ShowNextOperation();
        else CompleteCase("", "ActionSystem.Play -> real per-step combat / movement / unit / turn consumer");
    }

    private void ResolveCombat()
    {
        string exception = "";
        try
        {
            var enemies = _context.Enemies.ToList();
            _combatResult = _case.combatPhase == "Block"
                ? CardCombatActions.Block(_player, _context, enemies[0])
                : CardCombatActions.Attack(_player, _context, enemies, Enumerable.Range(0, enemies.Count).ToArray(),
                    (Phase)Enum.Parse(typeof(Phase), _case.combatPhase));
        }
        catch (Exception e) { exception = e.GetType().Name + ": " + e.Message; }
        CompleteCase(exception, "ActionSystem.Play -> CardCombatActions -> production combat resolver");
    }

    private void CompleteCase(string exception, string executionPath)
    {
        var after = Snapshot();
        var checks = _case.expected.Select(e => new CardVerificationCheck
        {
            key = e.key, expected = e.value, actual = after.TryGetValue(e.key, out var v) ? v : "<未实现/无对应状态>",
            passed = after.TryGetValue(e.key, out var actual) && actual == e.value
        }).ToList();
        checks.Add(new CardVerificationCheck { key = "originalArtwork", expected = _case.cardId, actual = _art.sprite?.name ?? "", passed = _art.sprite != null && _art.sprite.name == _case.cardId });
        checks.Add(new CardVerificationCheck { key = "printedEffectPresent", expected = "True", actual = (!string.IsNullOrWhiteSpace(_printed.text)).ToString(), passed = !string.IsNullOrWhiteSpace(_printed.text) });
        checks.Add(new CardVerificationCheck { key = "executionException", expected = _case.expectedException ?? "", actual = exception, passed = exception == (_case.expectedException ?? "") });
        string status = checks.Any(c => !c.passed) ? "failed" : (_case.limitations?.Length ?? 0) > 0 ? "partial" : "passed";
        var result = new CardVerificationResult { id = _case.id, cardId = _case.cardId, title = _case.title, status = status,
            exception = exception, expectedException = _case.expectedException, executionPath = executionPath, printedEffect = _printed.text, artwork = _art.sprite?.name, limitations = _case.limitations,
            before = Values(_before), after = Values(after), checks = checks.ToArray() };
        _results.RemoveAll(r => r.id == result.id); _results.Add(result);
        _executed = true; _play.interactable = false;
        var display = checks.Where(c => c.key != "executionException" || c.expected.Length > 0 || c.actual.Length > 0)
            .OrderBy(c => c.passed).ThenBy(c => !string.IsNullOrEmpty(_case.combatPhase) && c.key.StartsWith("combat:") ? 0 : 1).Take(9);
        _comparison.text = "检查项                       执行前 → 实际结果       规则预期\n" + string.Join("\n", display.Select(c =>
            $"{(c.passed ? "通过" : "不符")}  {Label(c.key)}   {(_before.TryGetValue(c.key, out var b) ? b : "—")} → {c.actual}   [预期 {c.expected}]"));
        _status.text = status == "passed" ? "本例断言通过" : status == "partial" ? "即时断言通过 · 整卡仍有未验证范围" : "发现不符：" + (exception.Length > 0 ? exception : checks.First(c => !c.passed).key);
        if (status == "passed" && !string.IsNullOrEmpty(_case.expectedException))
            _status.text = _case.cardId == "basic_card_005" || _case.cardId == "advanced_card_015" || _case.cardId == "magic_005" ? "按规则拒绝操作 · 资源守恒断言通过（原因见上方）"
                : "按规则拒绝操作 · 资源守恒断言通过：" + exception.Substring(exception.IndexOf(": ", StringComparison.Ordinal) + 2);
        if ((_case.cardId == "magic_021" && _case.enhanced || _case.cardId == "magic_022" || _case.cardId == "magic_007" || _case.cardId == "magic_013" || _case.cardId == "magic_019" || _case.cardId == "magic_003") && !string.IsNullOrEmpty(_case.expectedException))
        {
            string costColor = _case.cardId == "magic_003" ? "Green" : _case.cardId == "magic_007" ? "Red" : _case.cardId == "magic_013" ? "Blue" : "White";
            _fixture.text = $"战斗中：{_context.InBattle}；阶段：{_context.CombatPhase}；时间：{_context.DayPart}；所选敌人索引：{_case.option}\n"
                + "敌人能力：" + string.Join("；", _context.Enemies.Select((enemy, index) => $"{index} [{string.Join(",", enemy.Abilities)}]"));
            _comparison.text = "拒绝施法：实际结果 / 独立规则预期\n"
                + "实际原因：" + exception.Replace("InvalidOperationException: ", "")
                + "\n预期原因：" + _case.expectedException.Replace("InvalidOperationException: ", "")
                + "\n费用：" + string.Join("；", checks.Where(c => c.key == "token:" + costColor || c.key == "token:Black" || costColor != "White" && c.key == "crystal:" + costColor)
                    .Select(c => Label(c.key) + " " + _before[c.key] + "→" + c.actual + " [预期" + c.expected + "]"))
                + "\n卡区：" + string.Join("；", checks.Where(c => c.key == "hand" || c.key == "discard" || c.key == "cards:played")
                    .Select(c => Label(c.key) + " " + _before.GetValueOrDefault(c.key, "—") + "→" + c.actual + " [预期" + c.expected + "]"))
                + $"\n近战{_context.MeleePool}；停攻目标[{string.Join(",", _context.SkipAttackIndices)}]；附身目标[{string.Join(",", _context.PossessedEnemyIndices)}]"
                + $"\n全部断言 {checks.Count(c => c.passed)}/{checks.Count}";
            if (status != "failed") _status.text = "按规则拒绝操作 · 资源守恒断言通过" + (status == "partial" ? " · 整卡仍有待验证范围" : "");
        }
        if (_combatResult != null)
        {
            var costs = checks.Where(c => c.key.StartsWith("token:") || c.key.StartsWith("crystal:"));
            _comparison.text = "战斗结算：实际结果 / 独立规则预期\n" + string.Join("\n", checks
                .Where(c => c.key.StartsWith("combat:")).Select(c => $"{(c.passed ? "通过" : "不符")}  {Label(c.key)}：{c.actual} / {c.expected}"))
                + "\n费用：" + string.Join("；", costs.Select(c => $"{Label(c.key)} {_before[c.key]}→{c.actual} [预期{c.expected}]"))
                + $"\n剩余资源：近战{_context.MeleePool} 远程{_context.RangedPool} 攻城{_context.SiegePool} 格挡{_context.BlockPool}；手牌伤牌 {_before["handWounds"]}→{_player.Wounds}";
            if (status == "partial") _status.text = "本例操作与数值通过 · 整卡仍有待验证范围";
        }
        if (_operationResults.Count > 0)
        {
            var visible = checks.Where(c => c.key.StartsWith("operation:") || c.key.StartsWith("move:")
                || c.key.StartsWith("position:") || c.key.StartsWith("unit") || c.key == "readyUnits"
                || c.key == "MovementPool" || c.key == "InfluencePool" || c.key == "Reputation").Take(9);
            _comparison.text = "操作结算：实际结果 / 独立规则预期\n"
                + string.Join("\n", visible.Select(c => $"{(c.passed ? "通过" : "不符")} {Label(c.key)}：{c.actual} / {c.expected}"));
        }
        if (_case.operations?.Any(o => o.kind == "Block" || o.kind == "Attack") == true)
        {
            var lines = new List<string> { "逐步战斗：实际结果 / 独立规则预期" };
            for (int i = 0; i < _case.operations.Length; i++)
            {
                string prefix = "operation:" + i + ":";
                if (_operationResults.TryGetValue(prefix + "exception", out string operationError) && operationError.Length > 0)
                {
                    lines.Add($"操作{i + 1}已拒绝：{operationError}；剩余格挡{_operationResults.GetValueOrDefault(prefix + "BlockPool", "未记录")}，近战{_operationResults.GetValueOrDefault(prefix + "MeleePool", "未记录")}");
                    continue;
                }
                var fields = checks.Where(c => c.key.StartsWith(prefix) && new[] { "printed", "effective", "required", "kills", "wounds" }.Contains(c.key.Substring(prefix.Length))).ToArray();
                var labels = new Dictionary<string, string> { ["printed"] = "原值", ["effective"] = "有效值", ["required"] = "所需", ["kills"] = "击杀", ["wounds"] = "新增伤牌" };
                string action = _case.operations[i].kind switch
                {
                    "Block" => "格挡", "Attack" => "攻击", "RangedAttack" => "远程攻击",
                    "EndBattle" => "结束战斗", "BeginBattle" => "开始下一场战斗", "EndTurn" => "结束回合",
                    _ => _case.operations[i].kind
                };
                lines.Add($"操作{i + 1} {action}：" + string.Join("  ", fields.Select(c => labels[c.key.Substring(prefix.Length)] + " " + c.actual + "/" + c.expected)));
                if (_case.operations[i].kind == "Block" && _case.cardId != "items_020")
                {
                    int target = _case.operations[i].targetIndex;
                    if (_operationResults.TryGetValue(prefix + "armor" + target, out string reductionText) && int.TryParse(reductionText, out int reduction))
                        lines.Add($"目标{target + 1}护甲：{_context.Enemies.ElementAt(target).Armor}→{Math.Max(1, _context.Enemies.ElementAt(target).Armor - reduction)}，已生效减甲{reduction}");
                }
            }
            lines.Add("费用：" + string.Join("；", checks.Where(c => c.key.StartsWith("token:")).Select(c => Label(c.key) + " " + _before[c.key] + "→" + c.actual + " [预期" + c.expected + "]")));
            lines.Add($"剩余格挡{_context.BlockPool}，近战{_context.MeleePool}；实际名望{_player.Fame}，手牌伤牌{_player.Wounds}");
            if (_case.cardId == "items_020")
                lines.Add("真实卡区：" + string.Join("  ", checks.Where(c => c.key == "cards:played" || c.key == "cards:removed" || c.key == "discard")
                    .Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            _comparison.text = string.Join("\n", lines);
        }
        if (_context.LastManaRolls.Count > 0)
            _comparison.text += "\n本次实际骰面：" + string.Join("、", _context.LastManaRolls.Select(c => c switch
                { ManaColor.Red => "红", ManaColor.Blue => "蓝", ManaColor.Green => "绿", ManaColor.White => "白", ManaColor.Gold => "金", _ => "黑" }));
        if (_case.unitIds?.Length > 0 && _operationIndex > 0)
        {
            _fixture.text = DamageFixture();
            _comparison.text = "逐步承伤记录（实际牌区）\n" + DamageHistory()
                + "\n费用：" + string.Join("；", checks.Where(c => c.key.StartsWith("token:")).Select(c => Label(c.key) + " " + _before[c.key] + "→" + c.actual + " [预期" + c.expected + "]"))
                + $"\n独立效果断言：{checks.Count(c => c.passed)}/{checks.Count}；全抗性{_context.UnitsResistAll}；首伤待用{_context.IgnoreNextWound}";
        }
        if (_case.discardIndices != null)
        {
            var resources = checks.Where(c => c.key.StartsWith("crystal:") || c.key.StartsWith("token:")).ToArray();
            _comparison.text = "弃牌与魔晶：实际 / 独立预期\n"
                + $"手牌 {_before["hand"]}→{after["hand"]}；弃牌 {_before["discard"]}→{after["discard"]}；手牌伤牌{after["handWounds"]}\n"
                + string.Join("\n", resources.GroupBy(c => c.key.Split(':')[0]).Select(g =>
                    string.Join("；", g.Select(c => $"{Label(c.key)} {c.actual}/{c.expected}"))))
                + $"\n弃牌及牌区身份断言：{checks.Count(c => c.passed)}/{checks.Count}；实际弃牌："
                + string.Join("、", _player.Deck.DiscardPile.Select(c => _catalog.cards.FirstOrDefault(e => e.source.Id == c.Id)?.source.NameCn ?? c.Id));
        }
        if (_case.cardId == "magic_011" && _combatResult != null)
        {
            _comparison.text = "牺牲配对后真实攻击：实际 / 独立预期\n"
                + string.Join("\n", checks.Where(c => c.key.StartsWith("combat:")).Select(c => $"{Label(c.key)} {c.actual}/{c.expected}"))
                + "\n配对剩余魔晶：" + string.Join("；", checks.Where(c => c.key.StartsWith("crystal:")).Select(c => $"{Label(c.key)} {c.actual}/{c.expected}"))
                + "\n费用及转化后魔力：" + string.Join("；", checks.Where(c => c.key.StartsWith("token:")).Select(c => $"{Label(c.key)} {c.actual}/{c.expected}"));
        }
        if (_case.recruitOffer != null && _operationIndex > 0)
        {
            _fixture.text = RecruitmentFixture();
            var lines = new List<string> { "原版部队招募：实际 / 独立预期" };
            for (int i = 0; i < _operationIndex; i++)
            {
                string prefix = "operation:" + i + ":";
                lines.Add("操作" + (i + 1) + "：" + string.Join("；", checks.Where(c => c.key.StartsWith(prefix)
                    && new[] { "success", "cost", "influence", "discount", "units" }.Contains(c.key.Substring(prefix.Length)))
                    .Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            }
            lines.Add("实际供给：" + string.Join(",", _recruitOffer.Select(c => c.Id)));
            lines.Add("实际队伍：" + string.Join(",", _player.Units.Select(u => u.Card.Id)));
            lines.Add($"施法白色魔力 {_before["token:White"]}→{after["token:White"]}；一次折扣{_context.CharmRecruitDiscount}；全部断言{checks.Count(c => c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n", lines);
        }
        if (_case.recruitOffer != null && _case.enhanced && (_case.cardId == "magic_020" || _case.cardId == "items_017"))
        {
            _fixture.text = RecruitmentFixture();
            _comparison.text = "免费取得：真实供应 / 自有部队 / 卡区结算\n"
                + "剩余供应：" + after["recruit:offer"] + "\n队伍：" + after["recruit:owned"]
                + $"；空闲槽{_player.FreeSlots}\n影响力{after["InfluencePool"]}；名望{after["Fame"]}；声誉{after["Reputation"]}"
                + $"；白/黑魔力{after["token:White"]}/{after["token:Black"]}\n手牌{after["hand"]}；已打出{after["cards:played"]}；永久移除{after["cards:removed"]}；弃牌{after["discard"]}"
                + $"\n原部队已解散：{_damageUnits[0].IsDestroyed}；现有部队就绪：{after["recruit:ready"]}"
                + $"\n独立断言：{checks.Count(c=>c.passed)}/{checks.Count}；非交涉获取，不使用声誉折扣。";
        }
        if (_case.cardId == "magic_020" && !_case.enhanced)
        {
            _fixture.text = "本回合使用供应部队的一个能力；不取得部队、不占指挥槽。\n" + _case.title;
            _comparison.text = $"战斗号召许可剩余{_context.OfferedUnitUses}；白/蓝魔力{after["token:White"]}/{after["token:Blue"]}\n"
                + $"移动{_context.MovementPool} / 影响力{_context.InfluencePool} / 近战{_context.MeleePool} / 远程{_context.RangedPool} / 格挡{_context.BlockPool}\n"
                + $"供应：{after["recruit:offer"]}\n队伍：{after["recruit:owned"]}；空闲槽{_player.FreeSlots}；全队禁伤{_context.NoUnitDamage}\n"
                + $"手牌{after["hand"]}；已打出{after["cards:played"]}；弃牌{after["discard"]}；名望{_player.Fame}；声誉{_player.Reputation}\n"
                + string.Join("；",checks.Where(c=>c.key.EndsWith(":success")).Select(c=>$"操作{c.key.Split(':')[1]} {c.actual}/{c.expected}"))
                + $"\n独立断言：{checks.Count(c=>c.passed)}/{checks.Count}；失败不消费许可，回合结束许可失效。";
        }
        if (_case.cardId == "advanced_card_025")
        {
            _fixture.text = SpellFixture();
            var lines = new List<string> { "原卡获取 / 供给施法：实际 / 独立预期" };
            for (int i = 0; i < _operationIndex; i++)
                lines.Add("操作" + (i + 1) + "：" + string.Join("；", checks.Where(c => c.key.StartsWith("operation:" + i + ":")
                    && (c.key.EndsWith(":success") || c.key.EndsWith(":permissions"))).Select(c => c.key.Split(':').Last() + " " + c.actual + "/" + c.expected)));
            lines.Add(string.Join("；", checks.Where(c => new[] { "hand", "discard", "spellOffer", "SpellColors:count" }.Contains(c.key)).Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            lines.Add(string.Join("；", checks.Where(c => c.key == "RangedPool" || c.key == "SiegePool").Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            lines.Add(string.Join("；", checks.Where(c => c.key == "token:Red" || c.key == "token:Blue" || c.key == "token:Black").Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            lines.Add($"当前供给：{string.Join(",", _context.SpellSupply.Offer.Select(s => s.Id))}\n全部断言 {checks.Count(c => c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n", lines);
        }
        if (_case.cardId == "advanced_card_032")
        {
            _fixture.text = PortalFixture();
            _comparison.text = "传送及补牌：实际 / 独立规则预期\n" + string.Join("\n", checks.Where(c =>
                c.key.StartsWith("operation:") && !c.key.EndsWith(":exception") || c.key == "hand" || c.key == "draw" || c.key == "NextDrawBonus" || c.key == "cards:played" || c.key == "discard")
                .GroupBy(c => c.key.StartsWith("operation:") ? c.key.Split(':')[1] : "final")
                .Select(g => (g.Key == "final" ? "最终" : "操作" + (int.Parse(g.Key) + 1)) + "：" + string.Join("；", g.Select(c => c.key.Split(':').Last() + " " + c.actual + "/" + c.expected))))
                + $"\n蓝色魔力 {_before["token:Blue"]}→{after["token:Blue"]}；移动力{_context.MovementPool}；全部断言{checks.Count(c => c.passed)}/{checks.Count}";
        }
        if (_case.cardId == "magic_023")
        {
            _fixture.text = WindFixture();
            _comparison.text = "清风 / 黑夜之翼：实际 / 独立预期\n" + string.Join("\n", checks.Where(c =>
                c.key == "MovementPool" || c.key.StartsWith("position:") || c.key == "token:White" || c.key == "token:Black"
                || c.key == "Wounds" || c.key == "cards:played" || c.key == "discard" || c.key == "SkipAttackIndices:count")
                .Select(c => Label(c.key) + " " + c.actual + "/" + c.expected))
                + (_operationResults.Count == 0 ? "" : "\n逐次新增伤牌：" + string.Join("；", checks.Where(c => c.key.StartsWith("operation:") && c.key.EndsWith(":wounds")).Select(c => c.key.Split(':')[1] + " " + c.actual + "/" + c.expected)))
                + $"\n实际跳过敌人[{string.Join(",", _context.SkipAttackIndices)}]；全部断言{checks.Count(c => c.passed)}/{checks.Count}";
        }
        if (_case.cardId == "magic_002")
        {
            _fixture.text = MeditationFixture();
            string Compact(string value) => value.Replace("basic_card_", "B").Replace("magic_", "S");
            var lines = new List<string> { "回收与补牌：实际 / 独立原规则预期" };
            if (exception.Length > 0) lines.Add("拒绝：" + exception);
            foreach (var check in checks.Where(c => c.key == "played:draw:ids" || c.key == "draw:ids" || c.key == "discard:ids"))
                lines.Add((check.key.StartsWith("played:") ? "出牌后牌序" : check.key == "draw:ids" ? "最终牌序" : "弃牌") + " " + Compact(check.actual) + " / " + Compact(check.expected));
            foreach (var group in checks.Where(c => c.key.StartsWith("operation:")).GroupBy(c => c.key.Split(':')[1]))
                lines.Add("操作" + (int.Parse(group.Key)+1) + " " + string.Join("；", group.Select(c => c.key.Split(':').Last() + " " + c.actual + "/" + c.expected)));
            lines.Add("费用/卡区 " + string.Join("；", checks.Where(c => new[] { "token:Green", "token:Black", "crystal:Green", "hand", "cards:uniqueInstances" }.Contains(c.key))
                .Select(c => Label(c.key) + " " + c.actual + "/" + c.expected)));
            lines.Add($"下一次补牌加成{_context.NextDrawBonus}；已打出{_player.Deck.PlayedCards.Count}；全部断言{checks.Count(c => c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n", lines);
        }
        _status.color = status == "failed" ? new Color32(244, 142, 118, 255) : W.Gold;
        if (_case.cardId == "items_008" || _case.cardId == "items_016")
        {
            _fixture.text = WeaponFixture();
            var relevant = checks.Where(c => c.key == "hand" || c.key == "discard" || c.key == "cards:played"
                || c.key == "cards:removed" || c.key == "StarBowActive" || c.key == "DoublePhysicalAttack" || c.key == "IgnorePhysicalResist"
                || c.key == "cards:total" || c.key == "MeleePool" || c.key == "RangedPool" || c.key == "Fame"
                || c.key == "combat:effective" || c.key == "combat:kills" || c.key == "combat:fame"
                || c.key == "SiegePool");
            _comparison.text = "原武器：实际 / 独立规则预期\n" + string.Join("\n", relevant.Select((c,i) => new { c,i }).GroupBy(x => x.i/2)
                .Select(group => string.Join("；", group.Select(x => x.c.key + " " + x.c.actual + "/" + x.c.expected))))
                + string.Concat(checks.Where(c => c.key.StartsWith("operation:")
                    && new[] { "success", "effective", "kills", "fame" }.Contains(c.key.Split(':').Last()))
                    .GroupBy(c => c.key.Split(':')[1]).Select(g => "\n操作" + (int.Parse(g.Key)+1) + "："
                        + string.Join("；", g.Select(c => c.key.Split(':').Last()+" "+c.actual+"/"+c.expected))))
                + string.Concat(checks.Where(c => c.key.StartsWith("operation:") && c.key.EndsWith(":exception") && c.actual.Length > 0)
                    .Select(c => "\n拒绝原因：" + c.actual))
                + $"\n全部断言 {checks.Count(c => c.passed)}/{checks.Count}";
        }
        if (_case.cardId == "items_023")
        {
            _fixture.text = TomeFixture();
            var wanted = checks.Where(c => c.key == "hand" || c.key == "discard" || c.key.StartsWith("cards:")
                || c.key == "RangedPool" || c.key == "SiegePool" || c.key == "MeleePool" || c.key == "InfluencePool"
                || c.key == "Wounds" || c.key.StartsWith("combat:") && (c.key.EndsWith("effective") || c.key.EndsWith("kills")));
            _comparison.text = "宝典使用：实际 / 原规则预期\n" + string.Join("\n", wanted.Select((c,i)=>new{c,i}).GroupBy(x=>x.i/2)
                .Select(g=>string.Join("；",g.Select(x=>x.c.key+" "+x.c.actual+"/"+x.c.expected))))
                + "\n红/蓝/黑魔力：" + string.Join(" / ",new[]{"Red","Blue","Black"}.Select(c=>_before["token:"+c]+"→"+after["token:"+c]))
                + (exception.Length>0?"\n拒绝："+exception.Substring(exception.IndexOf(": ",StringComparison.Ordinal)+2):"")
                + $"\n全部断言 {checks.Count(c=>c.passed)}/{checks.Count}";
        }
        if (IsManaArtifact)
        {
            _fixture.text = GemRingFixture();
            var wanted = checks.Where(c => c.key == "Fame" || c.key == "hand" || c.key == "discard"
                || c.key == "cards:played" || c.key == "cards:removed" || c.key.StartsWith("token:")
                || c.key == "MeleePool" || c.key == "RangedPool" || c.key == "SiegePool" || c.key == "Wounds"
                || c.key.StartsWith("crystal:") && c.expected != "0" || c.key == "InfiniteMana:count" || c.key == "dice:count");
            _comparison.text = (IsGemRing ? "原戒指" : "原宝石袋") + "：实际 / 独立规则预期\n" + string.Join("\n", wanted.Select((c, i) => new {c, i}).GroupBy(x => x.i / 3)
                .Select(g => string.Join("；", g.Select(x => Label(x.c.key) + " " + x.c.actual + "/" + x.c.expected))))
                + string.Concat(checks.Where(c => c.key.StartsWith("operation:") && (c.key.EndsWith(":success") || c.key.EndsWith(":fame")))
                    .GroupBy(c => c.key.Split(':')[1]).Select(g => "\n操作" + (int.Parse(g.Key) + 1) + "："
                        + string.Join("；", g.Select(c => c.key.Split(':').Last() + " " + c.actual + "/" + c.expected))))
                + string.Concat(checks.Where(c => c.key.EndsWith(":exception") && c.actual.Length > 0).Select(c => "\n拒绝：" + c.actual))
                + (!IsGemRing && exception.Length > 0 ? "\n拒绝：" + exception.Substring(exception.IndexOf(": ", StringComparison.Ordinal) + 2) : "")
                + $"\n全部断言 {checks.Count(c => c.passed)}/{checks.Count}";
        }
        if (IsHealingCard)
        {
            _fixture.text = GrailFixture();
            var visible = checks.Where(c => c.key == "HealingPool" || c.key == "GrailHealing" || c.key == "DrawPerHeal"
                || c.key == "HealedHandWoundsThisTurn" || c.key == "ReadyUnitOnHeal"
                || (_case.cardId == "magic_005" && (c.key.StartsWith("units:") || c.key == "MeleePool" || c.key == "token:Black"))
                || (_case.cardId == "advanced_card_015" && c.key.StartsWith("units:"))
                || c.key == "token:Green" || c.key == "crystal:Green"
                || c.key == "Wounds" || c.key == "Fame" || c.key == "hand" || c.key == "draw" || c.key == "discard"
                || c.key == "unitWounds" || c.key == "unitReady" || c.key == "unitCanActivate" || c.key == "unitLevel" || c.key.StartsWith("cards:")
                || (_case.cardId == "advanced_card_007" && (c.key == "MovementPool" || c.key.StartsWith("position:")))
                || (_case.cardId == "advanced_card_015" && c.key == "MeleePool"));
            _comparison.text = (_case.cardId == "basic_card_005" ? "所选效果结算" : "治疗结算") + "：实际 / 独立规则预期\n" + string.Join("\n", visible.Select((c,i)=>new{c,i}).GroupBy(x=>x.i/3)
                .Select(g=>string.Join("；",g.Select(x=>Label(x.c.key)+" "+x.c.actual+"/"+x.c.expected))))
                + string.Concat(checks.Where(c=>c.key.StartsWith("operation:") && c.key.EndsWith(":success"))
                    .Select(c=>"\n"+Label(c.key)+" "+c.actual+"/"+c.expected))
                + string.Concat(checks.Where(c=>c.key.EndsWith(":exception") && c.actual.Length>0).Select(c=>"\n拒绝："+c.actual))
                + (exception.Length>0?"\n拒绝："+exception.Substring(exception.IndexOf(": ",StringComparison.Ordinal)+2):"")
                + $"\n全部断言 {checks.Count(c=>c.passed)}/{checks.Count}";
        }
        if (IsDiseaseScene)
        {
            _fixture.text = DiseaseFixture();
            var visible = checks.Where(c => c.key.StartsWith("enemyArmor:") || c.key.StartsWith("token:")
                || c.key == "BlockedArmorOne" || c.key == "Fame" || c.key == "Wounds" || c.key == "hand"
                || c.key == "discard" || c.key.StartsWith("cards:"));
            _comparison.text = "感染结算：实际 / 独立预期\n" + string.Join("\n",visible.Select((c,i)=>new{c,i}).GroupBy(x=>x.i/3)
                .Select(g=>string.Join("；",g.Select(x=>Label(x.c.key)+" "+x.c.actual+"/"+x.c.expected))))
                + string.Concat(checks.Where(c=>c.key.StartsWith("operation:")).GroupBy(c=>c.key.Split(':')[1])
                    .Select(g=>"\n操作"+(int.Parse(g.Key)+1)+"："+string.Join("；",g.Select(c=>c.key.Split(':').Last()+" "+c.actual+"/"+c.expected))))
                + (exception.Length>0?"\n拒绝："+exception.Substring(exception.IndexOf(": ",StringComparison.Ordinal)+2):"")
                + $"\n全部断言 {checks.Count(c=>c.passed)}/{checks.Count}";
        }
        if (_case.cardId == "magic_000")
        {
            _fixture.text = EnergyFixture();
            var visible = checks.Where(c => c.key.StartsWith("token:") || c.key.StartsWith("unit") || c.key.StartsWith("otherUnits:")
                || c.key.StartsWith("cards:") || c.key == "hand" || c.key == "draw" || c.key == "discard"
                || c.key == "Wounds" || c.key == "Fame" || c.key == "MeleePool" || c.key == "HealedUnitsThisTurn:count" || c.key == "HealingPool");
            string ShortValue(string value) => value.Replace("True","T").Replace("False","F");
            string ShortLabel(string key) => key switch
            {
                "otherUnits:ready" => "他方就绪", "HealedUnitsThisTurn:count" => "已治疗部队数",
                "HealingPool" => "治疗点", "cards:played" => "已打出", "draw" => "牌库张数", _ => Label(key)
            };
            _comparison.text = "能量结算：实际 / 规则预期（T是，F否）\n" + string.Join("\n", visible.Select((c,i)=>new{c,i}).GroupBy(x=>x.i/3)
                .Select(g=>string.Join("；",g.Select(x=>ShortLabel(x.c.key)+" "+ShortValue(x.c.actual)+"/"+ShortValue(x.c.expected)))))
                + string.Concat(checks.Where(c=>c.key.StartsWith("operation:") && c.key.EndsWith(":success"))
                    .Select((c,i)=>new{c,i}).GroupBy(x=>x.i/2)
                    .Select(g=>"\n"+string.Join("；",g.Select(x=>Label(x.c.key)+" "+x.c.actual+"/"+x.c.expected))))
                + (checks.Any(c=>c.key.EndsWith(":required")) ? "\n"+string.Join("；",checks.Where(c=>c.key.StartsWith("operation:") && (c.key.EndsWith(":required") || c.key.EndsWith(":kills")))
                    .Select(c=>(c.key.EndsWith(":required")?"所需攻击":"实际击杀")+" "+c.actual+"/"+c.expected)) : "")
                + string.Concat(checks.Where(c=>c.key.EndsWith(":exception") && c.actual.Length>0).Select(c=>"\n拒绝："+c.actual))
                + (exception.Length>0?"\n拒绝："+exception.Substring(exception.IndexOf(": ",StringComparison.Ordinal)+2):"")
                + $"\n全部断言 {checks.Count(c=>c.passed)}/{checks.Count}";
            if (!string.IsNullOrEmpty(_case.expectedException)) _status.text = "按规则拒绝操作 · 资源守恒断言通过（原因见上方）";
        }
        if (_battleGame != null)
        {
            var lines = new List<string> { "正式调度流程：实际 / 原规则预期" };
            _fixture.text = $"最终阶段{_battleGame.CurrentPhase}，战斗{_context.InBattle}；敌人护甲[{string.Join(",",_battleEnemies.Select(e=>e.Armor))}]"
                + $"\n手牌{_player.Deck.Hand.Count} / 已打出{_player.Deck.PlayedCards.Count} / 移除{_player.Deck.RemovedCards.Count}";
            for (int i = 0; i < _case.operations.Length; i++)
            {
                string prefix = "operation:" + i + ":";
                string name = _case.operations[i].kind switch
                {
                    "TurnBegin" => "开始下场", "TurnRanged" => "结束远程", "TurnResolve" => "结算战斗",
                    "TurnDefense" => "格挡与承伤", "TurnAttack" => "选择攻击", "TurnFinish" => "结束战斗", "PlayCard" => "实际出牌",
                    "EndTurn" => "结束回合", "StartTurn" => "开始回合", "RangedAttack" => "再次远攻", _ => _case.operations[i].kind
                };
                var visible = checks.Where(c => c.key.StartsWith(prefix) && new[] { "phase", "kills", "wounds", "success", "effective", "meleeRemaining", "enemyArmor", "skip", "fireRemoved" }.Contains(c.key.Substring(prefix.Length))).ToArray();
                if (_case.operations.Length > 6 && visible.Length == 0) continue;
                lines.Add($"{i+1}. {name}：" + string.Join("  ", visible.Select(c => (c.key.EndsWith(":phase") ? "阶段" : c.key.EndsWith(":kills") ? "击杀" : c.key.EndsWith(":wounds") ? "伤牌" : c.key.EndsWith(":effective") ? "有效" : c.key.EndsWith(":meleeRemaining") ? "余攻" : c.key.EndsWith(":enemyArmor") ? "护甲" : c.key.EndsWith(":skip") ? "停攻索引" : c.key.EndsWith(":fireRemoved") ? "去火抗数" : "成功") + c.actual + "/" + c.expected)));
            }
            lines.Add("卡区/费用：" + string.Join("  ", checks.Where(c => new[] { "hand", "discard", "cards:played", "cards:removed", "token:Red", "token:Blue", "token:White", "token:Black" }.Contains(c.key))
                .Select(c => (c.key == "cards:played" ? "已打出" : c.key == "cards:removed" ? "移除" : Label(c.key)) + c.actual + "/" + c.expected)));
            if (_case.operations.Length <= 6)
                lines.Add($"当前{_battleGame.CurrentPhase} / 战斗{_context.InBattle}；远程{_context.RangedPool} 攻城{_context.SiegePool} 格挡{_context.BlockPool}；伤牌{_player.Wounds}");
            if (checks.Any(c => c.key == "Fame" || c.key == "Reputation"))
                lines.Add("击杀奖励：" + string.Join("  ", checks.Where(c => c.key == "Fame" || c.key == "Reputation").Select(c => Label(c.key) + c.actual + "/" + c.expected)));
            lines.Add($"全部断言 {checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n", lines);
        }
        if ((_case.cardId == "magic_022" || _case.cardId == "magic_007" || _case.cardId == "magic_013" || _case.cardId == "magic_019" || _case.cardId == "magic_003") && _case.operations?.Length > 0)
        {
            string costColor = _case.cardId == "magic_003" ? "Green" : _case.cardId == "magic_007" ? "Red" : _case.cardId == "magic_013" ? "Blue" : "White";
            string Short(string key) => key switch
            {
                "defeated" => "已击败索引", "totalFame" => "名望", "wounds" => "新增伤牌", "meleeRemaining" => "余攻",
                "fortRemoved" => "去城防索引", "resRemoved" => "去抗性索引", "green" => "绿魔力",
                "enemyArmor" => "敌人护甲", "siteIgnored" => "忽略地点城防", "red" => "红魔力", "fireRemoved" => "去火抗数", "required" => "所需攻击", "effective" => "有效攻击", "rangedRemaining" => "剩余远程/攻城",
                "skip" => "停攻索引", "white" => "白魔力", "blue" => "蓝魔力", "black" => "黑魔力", "played" => "已打出", "kills" => "击杀", _ => key
            };
            var lines = new List<string> { "实际操作与原规则预期（实际/预期）" };
            foreach (var group in checks.Where(c => c.key.StartsWith("operation:") && !c.key.EndsWith(":success"))
                .GroupBy(c => c.key.Split(':')[1]))
                lines.Add("步骤" + (int.Parse(group.Key) + 1) + "：" + string.Join("；", group.Select(c =>
                    c.key.EndsWith(":exception") ? "拒绝 " + c.actual : Short(c.key.Split(':').Last()) + " " + c.actual + "/" + c.expected)));
            lines.Add("卡区/费用：" + string.Join("；", checks.Where(c => new[] { "hand", "discard", "cards:played", "token:" + costColor, "token:Black" }.Contains(c.key) || costColor != "White" && c.key == "crystal:" + costColor)
                .Select(c => (c.key == "cards:played" ? "已打出" : Label(c.key)) + " " + c.actual + "/" + c.expected)));
            lines.Add($"实际已击败[{string.Join(",", _context.DefeatedEnemyIndices.OrderBy(i => i))}]；名望{_player.Fame}；伤牌{_player.Wounds}；全部断言{checks.Count(c => c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n", lines);
        }
        if (_case.cardId == "magic_004")
        {
            _fixture.text = TunnelFixture();
            string Short(string key) => key switch { "blockPrinted" => "原格挡", "blockEffective" => "有效格挡", "blockRequired" => "所需格挡", "battleWounds" => "本战新伤", "knockedOut" => "昏迷", "nonWoundHand" => "非伤手牌", "q" => "横坐标", "success" => "成功", "kills" => "击杀", "handWounds" => "伤牌", "active" => "待结束突袭", "siteIgnored" => "忽略地点城防", "pendingReward" => "待领奖", "drawTop" => "牌库顶", "hand" => "手牌", "draw" => "牌库", "reputation" => "声誉", "conquered" => "征服", "owner" => "所有者", "enemy" => "驻军", "replaceGuard" => "待抽守军", _ => key };
            var lines = new List<string> { "地道行动：实际 / 原规则预期" };
            if (exception.Length > 0) lines.Add("拒绝：" + exception);
            if (_playedState != null) lines.Add($"出牌后位置({_playedState["position:q"]},{_playedState["position:r"]})；绿色魔力{_before["token:Green"]}→{_playedState["token:Green"]}；黑色魔力{_before["token:Black"]}→{_playedState["token:Black"]}");
            string Value(string value) => value.Replace("True","是").Replace("False","否");
            string CompactLabel(string key) => key switch { "red"=>"红", "rangedRemaining"=>"远攻", "meleeRemaining"=>"近攻", "skillsUsed"=>"技能已用", "unitsReady"=>"部队就绪", "effective"=>"有效攻击", _=>Short(key) };
            var operationLines = new List<string>();
            foreach (var group in checks.Where(c=>c.key.StartsWith("operation:") && !c.key.EndsWith(":art") && !c.key.EndsWith(":printedRule")).GroupBy(c=>c.key.Split(':')[1]).OrderBy(g=>int.Parse(g.Key)))
            {
                string line = "操作"+(int.Parse(group.Key)+1)+"："+string.Join("；",group.Select(c=>CompactLabel(c.key.Split(':').Last())+" "+Value(c.actual)+"/"+Value(c.expected)));
                if (operationLines.Count > 0 && operationLines.Last().Length + line.Length < 64)
                    operationLines[operationLines.Count-1] += "；" + line;
                else operationLines.Add(line);
            }
            lines.AddRange(operationLines);
            lines.Add(string.Join("；",checks.Where(c=>new[]{"position:q","MovementPool","token:Green","token:Black","hand","discard","cards:played"}.Contains(c.key)).Select(c=>Label(c.key)+" "+c.actual+"/"+c.expected)));
            lines.Add($"名望{_player.Fame}；伤牌{_player.Wounds}；仅地点城防豁免{_context.IgnoreSiteFortifications}；全部断言{checks.Count(c=>c.passed)}/{checks.Count}");
            var sourceChecks = checks.Where(c=>c.key.StartsWith("operation:") && (c.key.EndsWith(":art") || c.key.EndsWith(":printedRule"))).ToArray();
            if (sourceChecks.Length > 0) lines[lines.Count-1] += $"；操作原图与原文{sourceChecks.Count(c=>c.passed)}/{sourceChecks.Length}";
            if (_context.TunnelTargetSite != null)
                lines.Add($"地图：{_context.TunnelTargetSite.Type} 征服{_context.TunnelTargetSite.Conquered}，所有者{_context.TunnelTargetSite.OwnerId?.ToString() ?? "无"}；驻军{(_context.TunnelTargetSite.Enemy != null)}；待抽守军{_context.TunnelTargetSite.GuardNeedsReplacement}；声誉{_player.Reputation}；待领奖{_context.PendingSpellReward}");
            if (_context.TunnelDefenders.Count > 1)
                lines.Add($"城市幸存守军{_context.TunnelTargetSite.Defenders.Count}；盾徽顺序[{string.Join(",",_context.TunnelTargetSite.CityShields)}]；城市补牌加成{FortifiedSiteActions.HandBonus(_player,_context.MovementMap)}");
            if (_context.TunnelTargetSite?.CityColor != null && _context.TunnelTargetSite.CityColor != CityColor.None)
                lines.Add($"城市{_context.TunnelTargetSite.CityColor}；本场加成已清理{(_context.CombatSite == null)}；原标记印刷护甲{_context.TunnelDefender.Armor} 攻击{_context.TunnelDefender.Attack}");
            _comparison.text=string.Join("\n",lines);
        }
        if (_case.cardId == "advanced_card_016")
        {
            _fixture.text = "冲锋陷阵：必须在战斗开始打出；本场攻防加成，整回合禁止部队承伤。\n" + _case.title;
            var lines = new List<string> { $"本场部队加成{_context.ChargeUnitBonus}；整回合禁伤{_context.NoUnitDamage}；红魔力{after["token:Red"]}",
                $"近战{_context.MeleePool} / 远程{_context.RangedPool} / 攻城{_context.SiegePool} / 格挡{_context.BlockPool}；名望{_player.Fame}" };
            string Short(string key) => key switch { "success"=>"成功", "bonus"=>"加值", "ban"=>"禁伤", "printed"=>"原攻", "required"=>"所需", "kills"=>"击杀", "attack"=>"近攻", "block"=>"格挡", "ranged"=>"远攻", "siege"=>"攻城", "unitWounds"=>"部队伤牌", _=>key };
            string Value(string value) => value.Replace("True","T").Replace("False","F");
            foreach (var group in checks.Where(c=>c.key.StartsWith("operation:") && new[]{"success","bonus","ban","printed","required","kills","attack","block","ranged","siege","unitWounds"}.Contains(c.key.Split(':').Last())).GroupBy(c=>c.key.Split(':')[1]))
                lines.Add("操作" + group.Key + "：" + string.Join("；",group.Select(c=>Short(c.key.Split(':').Last())+" "+Value(c.actual)+"/"+Value(c.expected))));
            lines.Add($"手牌{after["hand"]}；已打出{after["cards:played"]}；弃牌{after["discard"]}；自有部队伤牌{after["unitWounds"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n",lines);
        }
        if (IsBannerScenario)
        {
            _fixture.text = (IsCommandBannerScenario ? "指挥旌旗：真实招募、指挥标记与卡区守恒\n" : "荣誉旌旗：原卡附着、部队能力与卡区守恒\n") + _case.title;
            var state = BannerSnapshot();
            var lines = new List<string> { $"附着[{state["attached"]}]；护甲[{state["armor"]}]；焚毁加成{state["bonus"]}",
                $"攻击{state["attack"]} / 格挡{state["block"]}；名望{state["fame"]}" };
            if (IsCommandBannerScenario)
            {
                lines.Clear();
                lines.Add($"普通指挥槽{state["slots"]} / 空闲{state["freeSlots"]}；旗帜指挥{state["commanded"]}；影响力{state["influence"]}");
                lines.Add($"附着[{state["attached"]}]；旗帜背面[{state["faceDown"]}]；部队[{state["owned"]}]");
            }
            if (_case.cardId == "items_018")
            {
                _fixture.text = "强韧旌旗：原卡受伤防护、每轮次数与真实治疗\n" + _case.title;
                lines.Clear();
                lines.Add($"本轮已用[{state["spent"]}]；预选防护[{state["protection"]}]；部队伤牌[{state["wounds"]}]");
                lines.Add($"就绪[{state["ready"]}]；治疗部队{state["healed"]}；英雄伤牌{_player.Wounds}；销毁[{string.Join(",",_damageUnits.Select(u=>u.IsDestroyed))}]");
            }
            if (_case.cardId == "items_003")
            {
                _fixture.text = "勇气旌旗：真实附着、每轮重整次数与伤牌独立\n" + _case.title;
                lines.Clear();
                lines.Add($"附着[{state["attached"]}]；本轮已用[{state["spent"]}]；战斗中{_context.InBattle}");
                lines.Add($"就绪[{state["ready"]}]；伤牌[{state["wounds"]}]；移动力{_context.MovementPool}；英雄伤牌{_player.Wounds}");
            }
            if (_case.cardId == "items_002")
            {
                _fixture.text = "防护旌旗：单支护甲与抗性、回合末实际新伤移除\n" + _case.title;
                lines.Clear();
                lines.Add($"护甲[{state["armor"]}]；旗帜火冰抗性[{state["elementalResist"]}]；部队伤牌[{state["wounds"]}]");
                lines.Add($"英雄手中伤牌{_player.Wounds} / 弃牌伤牌{_player.DiscardWounds}；新伤{state["newWounds"]}；回合末移除{state["cleanup"]}");
            }
            string Name(string key) => key switch { "success"=>"成功", "attack"=>"攻击", "block"=>"格挡", "fame"=>"名望", "count"=>"附着数", "bonus"=>"加成", "discard"=>"弃牌", "draw"=>"牌库", "kills"=>"击杀", "required"=>"所需", "freeSlots"=>"空闲槽", "commanded"=>"旗帜指挥", "influence"=>"影响力", "faceDown"=>"背面", "spent"=>"已用", "wounds"=>"伤牌", "ready"=>"就绪", "healed"=>"已治疗", "destroyed"=>"销毁", "heroWounds"=>"英雄伤牌", "discardWounds"=>"弃牌伤牌", "newWounds"=>"新伤", "removed"=>"移除", _=>key };
            foreach (var group in checks.Where(c => c.key.StartsWith("operation:") && new[] { "success", "attack", "block", "fame", "count", "bonus", "discard", "draw", "kills", "required", "freeSlots", "commanded", "influence", "faceDown", "spent", "wounds", "ready", "healed", "destroyed", "heroWounds", "discardWounds", "newWounds", "removed" }.Contains(c.key.Split(':').Last())).GroupBy(c => c.key.Split(':')[1]))
                lines.Add("操作" + group.Key + "：" + string.Join("；",group.Select(c => Name(c.key.Split(':').Last()) + " " + c.actual.Replace("True","T").Replace("False","F") + "/" + c.expected.Replace("True","T").Replace("False","F"))));
            lines.Add($"手牌{state["hand"]} / 弃牌{state["discard"]} / 牌库{state["draw"]} / 移除{state["removed"]}；总实例{state["total"]}，唯一{state["unique"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text = string.Join("\n",lines);
        }
        if (_case.cardId == "items_010")
        {
            var s = ArtifactFixtureSnapshot();
            _fixture.text = $"无尽金币包：影响力{s["influence"]}；名望{s["fame"]}；实际实例{s["unique"]}/{s["total"]}"
                + $"\n手牌{s["hand"]} 已打出{s["played"]} 弃牌{s["discard"]} 牌库{s["draw"]} 移除{s["removed"]}";
            var rows = new List<string>();
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                string success = _operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝");
                rows.Add($"操作{i+1} {success}；影响力{_operationResults.GetValueOrDefault(k+":influence")}；名望{_operationResults.GetValueOrDefault(k+":fame")}");
            }
            rows.Add($"原卡0：{ArtifactZoneLabel(s.GetValueOrDefault("zone0"))}；原卡1：{ArtifactZoneLabel(s.GetValueOrDefault("zone1"))}");
            rows.Add($"已打出/移除{after["cards:played"]}/{after["cards:removed"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
        }
        if (_case.cardId == "items_009" && _case.playFromHand)
        {
            ShowWrathState("");
            var rows = new List<string>();
            for (int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                string success=_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝");
                rows.Add($"步骤{i+1} {success}；攻城{_operationResults.GetValueOrDefault(k+":siege")}；名望{_operationResults.GetValueOrDefault(k+":fame")}"
                    + (_operationResults.ContainsKey(k+":kills") ? $"；击杀{_operationResults[k+":kills"]}" : ""));
            }
            _comparison.text += "\n" + string.Join("\n",rows) + $"\n断言{checks.Count(c=>c.passed)}/{checks.Count}；总实体{after["wrath:total"]}/{after["wrath:unique"]}";
        }
        if (_case.cardId == "items_001")
        {
            ShowFearState("");
            var rows = new List<string>();
            for (int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                rows.Add($"步骤{i+1} {_operationResults.GetValueOrDefault(k+":success", "完成").Replace("True","成功").Replace("False","拒绝")}"+
                    $"；停攻[{_operationResults.GetValueOrDefault(k+":skip")}]；名望{_operationResults.GetValueOrDefault(k+":fame")}；英雄伤{_operationResults.GetValueOrDefault(k+":heroWounds")}");
            }
            rows.Add($"手牌{after["hand"]} / 弃牌{after["discard"]} / 移除{after["cards:removed"]}；实际实体{after["banner:unique"]}/{after["banner:total"]}");
            rows.Add($"取消攻击不算格挡；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
        }
        if (IsAmulet)
        {
            ShowAmuletState("");
            var rows=new List<string>();
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                rows.Add($"步骤{i+1} {_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝")}；金{_operationResults.GetValueOrDefault(k+":Gold")}/黑{_operationResults.GetValueOrDefault(k+":Black")}；移动{_operationResults.GetValueOrDefault(k+":movement")}；公开{_operationResults.GetValueOrDefault(k+":revealed")}");
            }
            if (_context.MovementMap.Places.Count>0) rows.Add(AmuletSites());
            if (_case.operations?.Any(o=>o.kind=="PlayCard")==true)
                rows.Add($"后续实值：远程{_context.RangedPool} / 攻城{_context.SiegePool} / 移动力{_context.MovementPool} / 伤牌{_player.Wounds}");
            rows.Add($"手牌{after["hand"]} / 已打出{after["cards:played"]} / 弃牌{after["discard"]} / 移除{after["cards:removed"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
        }
        if (IsSupplyExchange)
        {
            ShowWisdomState("");
            if (!string.IsNullOrEmpty(exception))
                _status.text="按原规则拒绝："+exception.Replace("InvalidOperationException: ","");
            var s=WisdomSnapshot();
            string DisplayIds(string ids) => _case.cardId=="advanced_card_027" || _case.operations?.Any(o=>o.kind=="SelectRemoval" || o.kind=="EndMarketRound")==true
                ? string.Join("、",ids.Split(',').Where(id=>id.Length>0).Select(id=>_catalog.cards.FirstOrDefault(e=>e.source.Id==id)?.source.NameCn ?? id)) : ids;
            var rows=new List<string>();
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
                rows.Add($"步骤{i+1}：{_operationResults.GetValueOrDefault("operation:"+i+":success","完成").Replace("True","成功").Replace("False","拒绝")}");
            rows.Add("实际手牌："+DisplayIds(s["handIds"]));
            if (_case.cardId=="advanced_card_027") rows.Add("实际弃牌："+DisplayIds(s["discardIds"]));
            rows.Add("永久移除："+DisplayIds(s["removedIds"]));
            rows.Add("高级供给："+DisplayIds(s["advanced"]));
            if (_case.cardId=="items_013") rows.Add("法术供给："+DisplayIds(s["spells"]));
            rows.Add($"已打出{s["played"]} / 弃牌{s["discard"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
        }
        if (IsRitualLifecycle)
        {
            ShowRitualState("");
            string Names(string ids)=>string.Join("、",ids.Split(',').Where(id=>id.Length>0).Select(id=>_catalog.cards.FirstOrDefault(e=>e.source.Id==id)?.source.NameCn ?? id));
            var s=RitualSnapshot();var rows=new List<string>();
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                var action=_case.operations[i].kind;
                rows.Add(action=="Attack" || action=="RangedAttack"
                    ? $"攻击 {_operationResults.GetValueOrDefault(k+":printed")}/{_operationResults.GetValueOrDefault(k+":effective")} 对护甲{_operationResults.GetValueOrDefault(k+":required")}；击杀{_operationResults.GetValueOrDefault(k+":kills")}"
                    : $"步骤{i+1}：{_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝")}");
            }
            rows.Add("手牌："+Names(s["handIds"]));rows.Add("牌库："+Names(s["drawIds"]));
            rows.Add("弃牌："+Names(s["discardIds"]));rows.Add("永久移除："+Names(s["removedIds"]));
            rows.Add($"实体{s["unique"]}/{s["total"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
            if(!string.IsNullOrEmpty(exception))_status.text="按原规则拒绝："+exception.Replace("InvalidOperationException: ","");
        }
        if (_case.turnPowerExpiry)
        {
            string Power(Dictionary<string,string> state)=>$"近战{state.GetValueOrDefault("MeleePool","0")} / 远程{state.GetValueOrDefault("RangedPool","0")} / 攻城{state.GetValueOrDefault("SiegePool","0")} / 格挡{state.GetValueOrDefault("BlockPool","0")}";
            _comparison.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,610);
            _comparison.text="出牌后："+Power(_playedState ?? _before)+"\n回合结束后："+Power(after)
                +"\n下回合攻击："+_operationResults.GetValueOrDefault("operation:2:success","").Replace("False","拒绝").Replace("True","成功")
                +"\n原因："+_operationResults.GetValueOrDefault("operation:2:exception","")
                +$"\n手牌{after["hand"]} / 牌库{after["draw"]} / 弃牌{after["discard"]} / 已打出{after["cards:played"]}"
                +$"\n全部断言{checks.Count(c=>c.passed)}/{checks.Count}";
            var enemy=_context.Enemies.FirstOrDefault();
            _recruitArt.sprite=enemy==null?null:_catalog.enemyArtwork.FirstOrDefault(e=>e.id==enemy.Id)?.artwork;
            _recruitArt.gameObject.SetActive(_recruitArt.sprite!=null);
        }
        if (IsLearning)
        {
            ShowLearningState("");
            var rows=new List<string>();
            if (_playedState!=null) rows.Add("出牌后：影响力"+_playedState["InfluencePool"]+"，未自动购牌");
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                rows.Add($"{i+1}. {LearningOperationLabel(_case.operations[i])}：{_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝")}，余{_operationResults.GetValueOrDefault(k+":influence")}点");
            }
            string Names(IEnumerable<DeedCard> cards)=>string.Join("、",cards.Select(c=>_catalog.cards.FirstOrDefault(e=>e.source.Id==c.Id)?.source.NameCn ?? c.Id));
            rows.Add("实际手牌："+Names(_player.Deck.Hand));
            rows.Add("实际弃牌："+Names(_player.Deck.DiscardPile));
            rows.Add($"已打出{_player.Deck.PlayedCards.Count}；全部断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
            if(!string.IsNullOrEmpty(exception))_status.text="按原规则拒绝："+exception.Replace("InvalidOperationException: ","");
        }
        if (IsBloodline)
        {
            ShowBloodlineState("");
            var s=BloodlineSnapshot();var rows=new List<string>();
            var played=_playedState ?? after;
            rows.Add($"出牌后：手伤{played.GetValueOrDefault("bloodline:wounds")} / 弃伤{played.GetValueOrDefault("bloodline:discardWounds")}；远程{played.GetValueOrDefault("RangedPool")} / 攻城{played.GetValueOrDefault("SiegePool")}");
            if (!_case.enhanced && string.IsNullOrEmpty(_case.expectedException))
            {
                var color=(ManaColor)(_case.option>>8);
                string label=color==ManaColor.Red?"红":color==ManaColor.Blue?"蓝":color==ManaColor.Green?"绿":"白";
                rows.Add($"{label}标记：{_before.GetValueOrDefault("token:"+color)}→{played.GetValueOrDefault("token:"+color)}；{label}晶体：{_before.GetValueOrDefault("crystal:"+color)}→{played.GetValueOrDefault("crystal:"+color)}");
            }
            if (_case.advancedOffer?.Contains("advanced_card_026")==true)
                rows.Add($"学习影响力：{played.GetValueOrDefault("InfluencePool")}→{_context.InfluencePool}；购牌能力{(played.GetValueOrDefault("LearningModes")=="2"?"已获得强化":"未获得")}→{(_context.LearningModes==0?"已到期":"可用")}");
            if (_case.advancedOffer?.Contains("advanced_card_025")==true)
                rows.Add($"红魔力（本卡＋额外）：{_before.GetValueOrDefault("token:Red")}→{played.GetValueOrDefault("token:Red")}；蓝魔力{_before.GetValueOrDefault("token:Blue")}→{played.GetValueOrDefault("token:Blue")}");
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                rows.Add($"步骤{i+1}：{_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝")}；远程{_operationResults.GetValueOrDefault(k+":ranged")} / 攻城{_operationResults.GetValueOrDefault(k+":siege")}");
            }
            string Names(string ids)=>string.Join("、",ids.Split(',').Where(id=>id.Length>0).Select(id=>_catalog.cards.FirstOrDefault(e=>e.source.Id==id)?.source.NameCn ?? id));
            rows.Add("手中原卡："+Names(s["handActions"]));rows.Add("弃牌原卡："+Names(s["discardActions"]));
            rows.Add("高级供给："+Names(s["advanced"]));
            rows.Add($"实体{s["unique"]}/{s["total"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
            if(!string.IsNullOrEmpty(exception))_status.text="按原规则拒绝："+exception.Replace("InvalidOperationException: ","");
        }
        if (IsDecompose)
        {
            ShowDecomposeState("");
            string Mana(Dictionary<string,string> state,string prefix)=>string.Join("/",new[]{"Red","Blue","Green","White"}.Select(color=>state.GetValueOrDefault(prefix+color,"0")));
            var played=_playedState ?? after;
            var rows=new List<string>{"颜色顺序：红 / 蓝 / 绿 / 白",
                $"晶体：出牌前{Mana(_before,"crystal:")}→出牌后{Mana(played,"crystal:")}",
                $"标记：出牌前{Mana(_before,"token:")}→出牌后{Mana(played,"token:")}",
                $"当前晶体{Mana(after,"crystal:")}；标记{Mana(after,"token:")}"};
            if(played.GetValueOrDefault("SpentCrystals:Red","0")!="0")
                rows.Add("本次已支付红晶体："+played["SpentCrystals:Red"]+"（上方包含支付与奖励的净变化）");
            for(int i=0;i<(_case.operations?.Length ?? 0);i++)
            {
                string k="operation:"+i;
                rows.Add($"步骤{i+1}：{_operationResults.GetValueOrDefault(k+":success","完成").Replace("True","成功").Replace("False","拒绝")}；移动{_operationResults.GetValueOrDefault(k+":move")} / 影响{_operationResults.GetValueOrDefault(k+":influence")} / 近战{_operationResults.GetValueOrDefault(k+":attack")} / 格挡{_operationResults.GetValueOrDefault(k+":block")}");
            }
            var s=DecomposeSnapshot();
            rows.Add("永久移除："+string.Join("、",_player.Deck.RemovedCards.Select(c=>_catalog.cards.FirstOrDefault(e=>e.source.Id==c.Id)?.source.NameCn ?? c.Id)));
            rows.Add($"实体{s["unique"]}/{s["total"]}；断言{checks.Count(c=>c.passed)}/{checks.Count}");
            _comparison.text=string.Join("\n",rows);
            if(!string.IsNullOrEmpty(exception))_status.text="按原规则拒绝："+exception.Replace("InvalidOperationException: ","");
        }
        if (IsOverload)
        {
            ShowOverloadState("");
            var played = _playedState ?? after;
            string Powers(Dictionary<string,string> s) => $"移{s.GetValueOrDefault("MovementPool","0")} 影{s.GetValueOrDefault("InfluencePool","0")} 攻{s.GetValueOrDefault("MeleePool","0")} 挡{s.GetValueOrDefault("BlockPool","0")} 远{s.GetValueOrDefault("RangedPool","0")} 城{s.GetValueOrDefault("SiegePool","0")}";
            var rows = new List<string> { "原效果与真实结算（实际 / 独立预期）",
                "出牌后：" + Powers(played), "当前：" + Powers(after),
                $"红标记：{_before.GetValueOrDefault("token:Red")}→{played.GetValueOrDefault("token:Red")}→{after.GetValueOrDefault("token:Red")}" };
            rows.AddRange(checks.Where(c => c.key.StartsWith("played:") || new[] { "MeleePool", "BlockPool", "SiegePool", "MovementPool", "InfluencePool", "spellOffer" }.Contains(c.key))
                .Select((c,i) => new { c,i }).GroupBy(x => x.i/2)
                .Select(g => string.Join("；", g.Select(x => (x.c.key.StartsWith("played:") ? "出牌"+Label(x.c.key.Substring(7)) : Label(x.c.key)) + " " + x.c.actual + "/" + x.c.expected))));
            rows.AddRange(checks.Where(c => c.key.StartsWith("operation:") && c.key.EndsWith(":success"))
                .Select(c => "步骤" + (int.Parse(c.key.Split(':')[1])+1) + " " + c.actual + "/" + c.expected));
            if (_case.extraHandCards?.Contains("advanced_card_025") == true)
            {
                string Names(IEnumerable<string> ids) => string.Join("、", ids.Select(id=>_catalog.cards.FirstOrDefault(e=>e.source.Id==id)?.source.NameCn ?? id));
                rows.Add("实际弃牌：" + Names(_player.Deck.DiscardPile.Select(c=>c.Id)));
                rows.Add("剩余法术：" + Names(_context.SpellSupply.Offer.Select(c=>c.Id)));
            }
            if (_combatResult != null)
                rows.Add($"格挡总值{_combatResult.PrintedPower} / 需求{_combatResult.RequiredPower}；本次伤牌{_combatResult.Wounds}；剩余格挡{_context.BlockPool}");
            rows.Add($"实体 {ArtifactFixtureSnapshot()["unique"]}/{ArtifactFixtureSnapshot()["total"]}；断言 {checks.Count(c=>c.passed)}/{checks.Count}");
            if (_case.overloadSelections != null)
            {
                rows.Clear();
                rows.Add("逐次原效果结算（实际 / 独立预期）");
                rows.Add("选项顺序：" + string.Join("→", _case.overloadSelections.Select(s=>s.option)));
                rows.Add("出牌后：" + Powers(played));
                rows.Add("当前：" + Powers(after));
                rows.Add($"红标记 {_before.GetValueOrDefault("token:Red")}→{after.GetValueOrDefault("token:Red")}");
                rows.Add("魔晶红/蓝/绿/白：" + string.Join(" / ", new[]{"Red","Blue","Green","White"}.Select(c=>after.GetValueOrDefault("crystal:"+c,"0"))));
                rows.Add("卡区：" + string.Join(" / ", _artifactFixtureInstances.Select((c,i)=>ArtifactFixtureSnapshot().GetValueOrDefault("zone"+i))));
                rows.Add("法术许可：" + string.Join("、", _context.SpellColors));
                rows.Add($"选择已清理 {after.GetValueOrDefault("overload:choicesCleared")}；实体 {after.GetValueOrDefault("overload:unique")}/{after.GetValueOrDefault("overload:total")}");
                rows.Add($"独立断言 {checks.Count(c=>c.passed)}/{checks.Count}；后续操作 {_operationResults.Count(c=>c.Key.EndsWith(":success") && c.Value=="True")}/{_case.operations?.Length ?? 0}");
            }
            if (_case.overloadStepwise)
            {
                rows.Clear();
                rows.Add($"动态续选：剩余 {_context.PendingRepeatedEffect?.Remaining ?? 0} 次；第一步 {Powers(played)}");
                rows.Add("最终：" + Powers(after));
                if (_combatResult != null)
                    rows.Add($"实际格挡{_combatResult.PrintedPower} / 需求{_combatResult.RequiredPower}；伤牌{_combatResult.Wounds}，剩余格挡{_context.BlockPool}");
                if (_case.extraHandCards?.FirstOrDefault() == "basic_card_000")
                    rows.Add("移动力逐步：" + played.GetValueOrDefault("MovementPool") + "→" + string.Join("→", Enumerable.Range(0,_case.operations?.Length ?? 0)
                        .Select(i=>_operationResults.GetValueOrDefault("operation:"+i+":move","-"))) + $"；最终坐标({_player.Position.Q},{_player.Position.R})");
                rows.AddRange(checks.Where(c=>c.key.StartsWith("token:") || c.key.StartsWith("crystal:") || c.key=="draw" || c.key=="hand" || c.key=="cards:removed")
                    .Select((c,i)=>new{c,i}).GroupBy(x=>x.i/3).Select(g=>string.Join("；",g.Select(x=>Label(x.c.key)+" "+x.c.actual+"/"+x.c.expected))));
                rows.AddRange(Enumerable.Range(0,_case.operations?.Length ?? 0).Select(i=>
                    $"步骤{i+1}：{_operationResults.GetValueOrDefault("operation:"+i+":success","-")}；剩余{_operationResults.GetValueOrDefault("operation:"+i+":remaining","-")}次"));
                rows.Add($"独立断言 {checks.Count(c=>c.passed)}/{checks.Count}；实体 {after.GetValueOrDefault("overload:unique")}/{after.GetValueOrDefault("overload:total")}");
            }
            if (IsOverloadSpellPermission)
            {
                ShowOfferedSpell(_case.operations.Last(o=>o.kind=="CastOffer").targetIndex);
                rows.Clear();
                rows.Add($"真实供给施法：远程{_context.RangedPool} / 攻城{_context.SiegePool}；剩余许可{_context.SpellColors.Count}");
                rows.Add($"本战新伤{_context.BattleWoundsReceived}；昏迷{_context.IsKnockedOut}；手牌{_player.Deck.Hand.Count} / 弃牌{_player.Deck.DiscardPile.Count}");
                rows.AddRange(checks.Where(c=>c.key.StartsWith("token:") || c.key.StartsWith("crystal:") || c.key=="RangedPool" || c.key=="SiegePool" || c.key=="Wounds")
                    .Select((c,i)=>new{c,i}).GroupBy(x=>x.i/3).Select(g=>string.Join("；",g.Select(x=>Label(x.c.key)+" "+x.c.actual+"/"+x.c.expected))));
                rows.AddRange(Enumerable.Range(0,_case.operations.Length).GroupBy(i=>i/2).Select(g=>string.Join("；",g.Select(i=>
                    $"步骤{i+1}:{_operationResults.GetValueOrDefault("operation:"+i+":success","-")} 许可{_operationResults.GetValueOrDefault("operation:"+i+":permissions","-")}"))));
                rows.Add(RepeatOfferText());
                rows.Add($"实体 {after.GetValueOrDefault("overload:unique")}/{after.GetValueOrDefault("overload:total")}；断言 {checks.Count(c=>c.passed)}/{checks.Count}");
            }
            _comparison.text = string.Join("\n", rows);
            if (!string.IsNullOrEmpty(exception)) _status.text = "按原规则拒绝：" + exception.Replace("InvalidOperationException: ", "");
        }
        if (_case.basicAcceptance)
            ShowBasicAcceptanceState($"基本分支断言 {checks.Count(c=>c.passed)}/{checks.Count}；详见独立预期报告");
        SaveResults();
    }

    private string PortalFixture() => $"位置({_player.Position.Q},{_player.Position.R})；传送余距{_context.TeleportRange}，下次补牌加成{_context.NextDrawBonus}；手牌{_player.Deck.Hand.Count}/牌库{_player.Deck.DrawPileCount}"
        + "\n地点：" + (_context.MovementMap.Places.Count == 0 ? "已揭示森林格" : string.Join(" / ", _context.MovementMap.Places.Select(p => $"({p.Key.Q},{p.Key.R}) {p.Value.Type}，驻军{(p.Value.Enemy != null)}")));

    private string WindFixture() => $"位置({_player.Position.Q},{_player.Position.R})；移动力{_context.MovementPool}；白/黑魔力{_player.Mana.Tokens.GetValueOrDefault(ManaColor.White)}/{_player.Mana.Tokens.GetValueOrDefault(ManaColor.Black)}；{_context.DayPart}"
        + (_case.enhanced ? $"\n所选敌人[{string.Join(",", _case.spellTargets ?? Enumerable.Range(0, Math.Max(0,_case.option)).ToArray())}]；额外移动力费用{_case.option * (_case.option - 1) / 2}"
            : "\n所选路径：" + string.Join("→", (_case.travelPath ?? Array.Empty<CardVerificationHex>()).Select(h => $"({h.q},{h.r})")));

    private string WeaponFixture() => $"当前阶段{_context.CombatPhase}；手牌{_player.Deck.Hand.Count}，已打出{_player.Deck.PlayedCards.Count}，弃牌{_player.Deck.DiscardPile.Count}，移除{_player.Deck.RemovedCards.Count}"
        + $"\n额外名望：剑{_context.SwordFamePerKill}/弓{_context.BowFamePerKill}；攻击笔数{_context.CombatPower.Attacks.Count}，弓选项[{string.Join(",", _context.CombatPower.Attacks.Select(a => a.StarBowChoice))}]；敌人护甲[{string.Join(",", _context.Enemies.Select(e => e.Armor))}]";

    private string TomeFixture() => $"原法术供给：{string.Join(" / ",_context.SpellSupply.Offer.Select(s=>s.Id))}；所选索引{_case.option}；{_context.DayPart}"
        + $"\n弃牌费用：{_context.CardToDiscard?.Id ?? "已支付"}；法术选项{_context.TomeSpellOption}；焚毁{_case.enhanced}；供给不移出、不加入牌库";

    private string MeditationFixture() => $"{(_case.enhanced ? "选择真实弃牌" : "随机回收弃牌")} → {(_case.option == 0 ? "牌库顶" : "牌库底")}；下次补牌上限+2"
        + $"\n手牌{_player.Deck.Hand.Count} / 牌库{_player.Deck.DrawPileCount} / 弃牌{_player.Deck.DiscardPile.Count} / 已打出{_player.Deck.PlayedCards.Count}；待选[{string.Join(",", _context.CardsFromDiscard.Select(c => c.Id))}]";

    private string TunnelFixture() => $"位置({_player.Position.Q},{_player.Position.R})；移动力{_context.MovementPool}；战斗{_context.InBattle}；待结束突袭{_context.TunnelAssaultActive}"
        + "\n路径：" + string.Join("→",(_case.travelPath ?? Array.Empty<CardVerificationHex>()).Select(h=>$"({h.q},{h.r})")) + $"；地道不触发肆虐；本例目标{(_context.TunnelTargetPlayer != null ? "其他玩家（战斗待联验）" : "地图地点")}";

    private void ShowGuardToken()
    {
        if (_case.enemySupplyIds == null) return;
        var token = _context.TunnelDefender ?? _context.MovementMap.Places.Values.FirstOrDefault(p=>p.Enemy != null)?.Enemy;
        var art = token == null ? null : _catalog.enemyArtwork.FirstOrDefault(e=>e.id==token.Id)?.artwork;
        _guardArt.sprite = art; _guardArt.gameObject.SetActive(art != null);
        string AbilityName(Ability ability) => ability switch
        {
            Ability.PhysicalResist => "物抗", Ability.FireResist => "火抗", Ability.IceResist => "冰抗",
            Ability.ColdFireResist => "冰火抗", Ability.Brutal => "残暴", Ability.Poison => "剧毒",
            Ability.Fortified => "城防", Ability.Swift => "迅捷", Ability.Paralyze => "麻痹",
            Ability.Regenerate => "再生", Ability.MagicResist => "魔抗", _ => ability.ToString()
        };
        _guardInfo.text = token == null ? "守军尚未抽取" : $"原标记 {token.Id}\n{token.TokenColor} 护甲{token.Armor}\n{token.AttackElement}攻击{token.Attack}\n名望{token.Fame}；{string.Join("/",token.Abilities.Select(AbilityName))}";
        if (token != null && _context.CombatSite?.Type == PlaceType.City && _context.CombatSite.CityColor != CityColor.None)
        {
            var profile = CityCombatRules.DefenseProfile(token,_context);
            _guardInfo.text += $"\n本战护甲{CombatMath.EffectiveArmor(token,0,_context)} 攻击{profile.Attack}\n{string.Join("/",profile.Abilities.Select(AbilityName))}";
        }
        var defenders = _context.TunnelDefenders.Count > 0 ? _context.TunnelDefenders
            : _context.MovementMap.Places.Values.FirstOrDefault(p=>p.Defenders.Count > 0)?.Defenders;
        var second = defenders?.Skip(1).FirstOrDefault();
        _secondGuardArt.sprite = second == null ? null : _catalog.enemyArtwork.FirstOrDefault(e=>e.id==second.Id)?.artwork;
        _secondGuardArt.gameObject.SetActive(_secondGuardArt.sprite != null);
        if (second != null)
        {
            _guardArt.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,72);
            _guardArt.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,72);
            _guardInfo.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,154);
            string Describe(Monster guard, int number) => $"{number}. {guard.Id} {guard.TokenColor}\n护甲{guard.Armor} {guard.AttackElement}攻{guard.Attack} 名望{guard.Fame}\n技能：{(guard.Abilities.Count == 0 ? "无" : string.Join("/",guard.Abilities))}";
            _guardInfo.text = Describe(token,1) + "\n" + Describe(second,2);
        }
        else
        {
            _guardArt.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,154);
            _guardArt.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,154);
            _guardInfo.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,154);
        }
    }

    private Dictionary<string,string> TunnelWorldSnapshot()
    {
        var site = _context.TunnelTargetSite;
        var guard = _context.TunnelDefender;
        var profile = guard == null ? null : CityCombatRules.DefenseProfile(guard,_context);
        return new Dictionary<string,string>
        {
            ["cityColor"] = (site?.CityColor ?? CityColor.None).ToString(),
            ["effectiveArmor"] = (guard == null ? 0 : CombatMath.EffectiveArmor(guard,0,_context)).ToString(),
            ["effectiveAttack"] = (profile?.Attack ?? 0).ToString(),
            ["cityAbilities"] = string.Join(",",profile?.Abilities ?? Array.Empty<Ability>()),
            ["cityBonusActive"] = (_context.CombatSite != null && _context.InBattle).ToString(),
            ["battleWounds"] = _context.BattleWoundsReceived.ToString(),
            ["knockedOut"] = _context.IsKnockedOut.ToString(),
            ["nonWoundHand"] = _player.Deck.Hand.Count(c=>c.Type!=CardType.Wound).ToString(),
            ["skillsUsed"] = string.Join(",",_player.Skills.Select(s=>SkillActions.IsUsed(_player,s))),
            ["unitsReady"] = string.Join(",",_player.Units.Select(u=>u.IsReady)),
            ["enemy"] = (site?.Enemy != null).ToString(), ["conquered"] = (site?.Conquered == true).ToString(),
            ["owner"] = site?.OwnerId?.ToString() ?? "none", ["replaceGuard"] = (site?.GuardNeedsReplacement == true).ToString(),
            ["shields"] = (site?.CityShields.Count ?? 0).ToString(), ["pendingReward"] = _context.PendingSpellReward.ToString(),
            ["shieldOrder"] = string.Join(",",site?.CityShields ?? new List<int>()),
            ["guards"] = string.Join(",",site?.Defenders.Select(m=>m.Id) ?? Array.Empty<string>()),
            ["guardCount"] = (site?.Defenders.Count ?? 0).ToString(),
            ["secondGuardArt"] = (_secondGuardArt.sprite != null && _catalog.enemyArtwork.Any(e=>e.id==_context.TunnelDefenders.Skip(1).FirstOrDefault()?.Id && e.artwork==_secondGuardArt.sprite)).ToString(),
            ["reputation"] = _player.Reputation.ToString(), ["hand"] = _player.Deck.Hand.Count.ToString(),
            ["draw"] = _player.Deck.DrawPileCount.ToString(), ["drawTop"] = _player.Deck.DrawPile.FirstOrDefault()?.Id ?? "none",
            ["handSpells"] = string.Join(",",_player.Deck.Hand.Where(c=>c.Type==CardType.Spell).Select(c=>c.Id)),
            ["guard"] = _context.TunnelDefender?.Id ?? "none",
            ["guardArmor"] = (_context.TunnelDefender?.Armor ?? 0).ToString(),
            ["guardArt"] = (_guardArt.sprite != null && _catalog.enemyArtwork.Any(e=>e.id==_context.TunnelDefender?.Id && e.artwork==_guardArt.sprite)).ToString(),
            ["enemyDraw"] = (_context.EnemySupply?.DrawPile.Count ?? 0).ToString(),
            ["enemyDiscard"] = string.Join(",",_context.EnemySupply?.DiscardPile.Select(m=>m.Id) ?? Array.Empty<string>()),
            ["enemyInUse"] = (_context.EnemySupply?.InUse.Count ?? 0).ToString(),
            ["pendingSites"] = _context.MovementMap.Places.Values.Count(p=>p.GuardNeedsReplacement).ToString(),
            ["offer"] = string.Join(",",_context.SpellSupply.Offer.Select(s=>s.Id)),
            ["locationBonus"] = FortifiedSiteActions.HandBonus(_player,_context.MovementMap).ToString()
        };
    }

    private void Next() { if (_index + 1 < _cases.Length) { _index++; LoadCase(); } }

    private void SaveResults()
    {
        // Immutable checkpoints preserve interrupted runs without truncating a
        // report that an indexer or reviewer may currently have memory-mapped.
        string checkpoint = Path.Combine(_outputRoot, "checkpoints", (++_checkpoint).ToString("D4") + ".json");
        File.WriteAllText(checkpoint, JsonUtility.ToJson(_results.Last(), true));
        if (_results.Count != _cases.Length) return;
        var report = new CardVerificationResults { startedAt = _started, finishedAt = DateTime.UtcNow.ToString("o"), batch = batch, suiteSha256 = _suiteSha256,
            status = _results.Count != _cases.Length ? "running" : _results.Any(r => r.status == "failed") ? "has_failures" : _results.Any(r => r.status == "partial") ? "partial" : "passed", cases = _results.ToArray() };
        string temporary = Path.Combine(_outputRoot, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
        string destination = Path.Combine(_outputRoot, "effects.json");
        if (File.Exists(destination)) File.Replace(temporary, destination, null);
        else File.Move(temporary, destination);
    }

    private static CardVerificationValue[] Values(Dictionary<string, string> values) => values.OrderBy(p => p.Key).Select(p => new CardVerificationValue { key = p.Key, value = p.Value }).ToArray();
    private string Label(string key)
    {
        if (key.StartsWith("operation:"))
        {
            var parts = key.Split(':');
            return $"操作{int.Parse(parts[1]) + 1}" + (parts[2] switch { "cost" => "费用", "effective" => "有效值", "kills" => "击杀", "wounds" => "敌人创伤", "influence" => "剩余影响力", "discount" => "一次折扣", "units" => "队伍数", _ => "成功" });
        }
        if (key == "CharmRecruitDiscount") return "魅惑一次招募折扣";
        if (_labels.TryGetValue(key, out var label)) return label;
        string[] colors = { "Red", "Blue", "Green", "White", "Gold", "Black" };
        string[] names = { "红", "蓝", "绿", "白", "金", "黑" };
        for (int i = 0; i < colors.Length; i++)
        {
            if (key == "token:" + colors[i]) return names[i] + "色魔力";
            if (key == "crystal:" + colors[i]) return names[i] + "色魔晶";
        }
        return key;
    }

    private void Set(string key, string value)
    {
        if (key.StartsWith("armorReduction:")) { _context.ArmorReduction[int.Parse(key.Substring(15))] = int.Parse(value); return; }
        if (key.StartsWith("terrainCost:")) { _context.TerrainCostOverride[(TerrainType)Enum.Parse(typeof(TerrainType), key.Substring(12))] = int.Parse(value); return; }
        if (key.StartsWith("token:")) { _player.Mana.Tokens[(ManaColor)Enum.Parse(typeof(ManaColor), key.Substring(6))] = int.Parse(value); return; }
        if (key.StartsWith("crystal:")) { _player.Mana.Crystals[(ManaColor)Enum.Parse(typeof(ManaColor), key.Substring(8))] = int.Parse(value); return; }
        object target = key.StartsWith("player:") ? _player : _context;
        string name = key.Replace("player:", "");
        var property = target.GetType().GetProperty(name) ?? throw new InvalidOperationException("Unknown fixture field: " + key);
        if (value == "<none>") { property.SetValue(target, null); return; }
        property.SetValue(target, property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, value) : Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture));
    }

    private Dictionary<string, string> Snapshot()
    {
        var result = new Dictionary<string, string>();
        if (_case.basicAcceptance)
        {
            foreach (var entry in ArtifactFixtureSnapshot()) result["basic:"+entry.Key] = entry.Value;
            if (IsMysticBox)
                foreach (var entry in MysticBoxSnapshot()) result[entry.Key] = entry.Value;
            if (IsWarp)
            {
                result["warp:turnOrder"] = string.Join(",", _warpTurnOrder);
                result["warp:asideIds"] = string.Join(",", _player.Deck.RoundAsideCards.Select(c => c.Id));
                result["warp:asideCount"] = _player.Deck.RoundAsideCards.Count.ToString();
                result["warp:exploredIds"] = string.Join(",", _warpExplored.Select(t => t.Id));
                result["warp:tilesSame"] = _warpExplored.All(t => _warpOriginalTiles.Any(o => ReferenceEquals(o, t))
                    && _context.MovementMap.Placed.Values.Any(p => ReferenceEquals(p, t))).ToString();
                result["warp:rampaging"] = (_context.RampagingBattle != null).ToString();
                result["warp:guardAlive"] = (_warpGuard != null && _context.MovementMap.Places.Values
                    .Any(p => ReferenceEquals(p.Enemy, _warpGuard))).ToString();
                result["warp:guardId"] = _warpGuard?.Id ?? "";
            }
            if (IsMindRead)
                foreach (var entry in MindSnapshot()) result[entry.Key] = entry.Value;
            if (IsColdStare && _case.option == 1)
            {
                var target = _context.Enemies[_case.selectedEnemyIndex];
                var profile = CardCombatActions.DefenseProfile(target, _case.selectedEnemyIndex, _context, _case.selectedEnemyAttackIndex);
                result["cold:target"] = target.Id;
                result["cold:suppress"] = _context.ColdStareSuppresses(target, _case.selectedEnemyAttackIndex).ToString();
                result["cold:skip"] = _context.ColdStareSkips(target).ToString();
                result["cold:element"] = profile.AttackElement.ToString();
                result["cold:power"] = profile.Attack.ToString();
            }
            result["steady:pending"] = _context.SteadyTempoReturns.Count.ToString();
            result["steady:selected"] = (_artifactFixtureInstances.Length > 0 && _context.SteadyTempoReturns.Contains(_artifactFixtureInstances[0])).ToString();
            result["draw:ids"] = string.Join(",", _player.Deck.DrawPile.Select(c => c.Id));
            if (_case.cardId == "advanced_card_039")
                result["mountain:safe"] = CardTravelActions.IsSafeDestination(_player, _context, _player.Position, _context.OtherPlayers).ToString();
            foreach (ManaColor color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                result["spent:" + color] = _context.SpentCrystals.GetValueOrDefault(color).ToString();
            for (int i=0; i<_context.OtherPlayers.Count; i++)
            {
                var other = _context.OtherPlayers[i];
                result[$"other:{i}:wounds"] = other.Wounds.ToString();
                result[$"other:{i}:handWounds"] = other.Deck.Hand.Count(c=>c.Type==CardType.Wound).ToString();
                foreach (ManaColor color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
                {
                    result[$"other:{i}:crystal:{color}"] = other.Mana.Crystals.GetValueOrDefault(color).ToString();
                    result[$"other:{i}:token:{color}"] = other.Mana.Tokens.GetValueOrDefault(color).ToString();
                }
                result[$"other:{i}:hand"] = other.Deck.Hand.Count.ToString();
                result[$"other:{i}:played"] = other.Deck.PlayedCards.Count.ToString();
                result[$"other:{i}:discard"] = other.Deck.DiscardPile.Count.ToString();
                if (IsMindRead)
                {
                    result[$"other:{i}:handIds"] = string.Join(",", other.Deck.Hand.Select(c => c.Id));
                    result[$"other:{i}:discardIds"] = string.Join(",", other.Deck.DiscardPile.Select(c => c.Id));
                }
                result[$"other:{i}:curseActive"] = other.ManaCurseColor.HasValue.ToString();
            }
            result["spellOffer:ids"] = string.Join(",", _context.SpellSupply.Offer.Select(c => c.Id));
            result["spellOffer:sameInstances"] = (_context.SpellSupply.Offer.Count == _originalSpellOffer.Length
                && _context.SpellSupply.Offer.Select((c,i) => ReferenceEquals(c, _originalSpellOffer[i])).All(same => same)).ToString();
            if (_originalManaDice.Length > 0)
            {
                var source = _context.ManaSource;
                var claims = source.RoundClaims.Where(pair => ReferenceEquals(pair.Value, _player)).Select(pair => pair.Key).ToArray();
                var actual = source.Dice.Concat(source.RoundClaims.Keys)
                    .Concat(source.TurnClaims.Keys).ToArray();
                result["mana:sourceCount"] = source.Dice.Count.ToString();
                result["mana:heldCount"] = claims.Length.ToString();
                result["mana:heldFaces"] = string.Join(",", claims.Select(d => d.Face));
                result["mana:ordinaryHeld"] = (_player.HeldManaDie != null).ToString();
                result["mana:turnHeld"] = source.TurnClaims.Count(p => ReferenceEquals(p.Value, _player)).ToString();
                for (int i = 0; i < _originalManaDice.Length; i++)
                {
                    var die = _originalManaDice[i];
                    result[$"mana:die{i}:face"] = die.Face.ToString();
                    result[$"mana:die{i}:zone"] = source.Dice.Contains(die) ? "source"
                        : source.TurnClaims.ContainsKey(die) ? "turn" : source.RoundClaims.ContainsKey(die) ? "round" : "missing";
                }
                result["mana:identity"] = (actual.Length == _originalManaDice.Length
                    && actual.Distinct().Count() == actual.Length && _originalManaDice.All(actual.Contains)).ToString();
                result["mana:perTurn:Red"] = _player.TokensPerTurn.GetValueOrDefault(ManaColor.Red).ToString();
            }
        }
        if (_battleGame != null) result["GamePhase"] = _battleGame.CurrentPhase.ToString();
        result["otherUnits:ready"] = string.Join(";", _context.OtherPlayers.Select(p => string.Join(",", p.Units.Select(u => u.IsReady))));
        result["units:ready"] = string.Join(",", _player.Units.Select(u => u.IsReady));
        result["units:wounds"] = string.Join(",", _player.Units.Select(u => u.Wounds));
        for (int i = 0; i < _context.Enemies.Count; i++)
        {
            var enemy = _context.Enemies[i];
            result["enemyArmor:" + i] = CombatMath.EffectiveArmor(enemy, i, _context).ToString();
            result[$"enemy:{i}:attackCount"] = enemy.AttackCount.ToString();
            for (int a = 0; a < enemy.AttackCount; a++) result[$"enemy:{i}:attack:{a}"] = enemy.GetAttack(a).Value.ToString();
        }
        result["dice:faces"] = string.Join(",", _context.LastManaRolls);
        result["dice:count"] = _context.LastManaRolls.Count.ToString();
        result["HealingPool"] = _context.Healing.Total.ToString();
        result["GrailHealing"] = _context.Healing.Grail.ToString();
        foreach (var target in new object[] { _context, _player })
            foreach (var p in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = p.GetValue(target);
                if (value is int || value is bool || value is Enum || value is string) result[p.Name] = value.ToString();
                else if (value is IDictionary dict)
                {
                    result[p.Name + ":count"] = dict.Count.ToString();
                    foreach (DictionaryEntry e in dict) result[p.Name + ":" + e.Key] = e.Value?.ToString() ?? "";
                }
                else if (value is IEnumerable list && value is not string)
                {
                    var items = list.Cast<object>().ToArray(); result[p.Name + ":count"] = items.Length.ToString();
                    foreach (var e in items) if (e is Enum || e is int) result[p.Name + ":" + e] = "True";
                }
            }
        foreach (ManaColor color in Enum.GetValues(typeof(ManaColor)))
        {
            result["token:" + color] = _player.Mana.Tokens.GetValueOrDefault(color).ToString();
            result["crystal:" + color] = _player.Mana.Crystals.GetValueOrDefault(color).ToString();
        }
        result["move:selectedCost"] = MovementService.GetCost(new AxialCoord(1, 0), _context.MovementMap, _context.DayPart, _context).ToString();
        result["move:otherCost"] = MovementService.GetCost(new AxialCoord(2, 0), _context.MovementMap, _context.DayPart, _context).ToString();
        result["position:q"] = _player.Position.Q.ToString(); result["position:r"] = _player.Position.R.ToString();
        if (_case.cardId == "magic_004")
        {
            result["tunnel:playerTarget"] = (_context.TunnelTargetPlayer != null).ToString();
            foreach (var entry in TunnelWorldSnapshot()) result["world:" + entry.Key] = entry.Value;
        }
        result["unitCanActivate"] = _damageUnits[0].CanActivate.ToString();
        result["readyUnits"] = _player.Units.Count(u => u.IsReady).ToString();
        foreach (var operation in _operationResults) result[operation.Key] = operation.Value;
        result["attackElement"] = string.Join("+", _context.CombatPower.Attacks.Select(a => a.Element).Distinct().OrderBy(e => e));
        result["blockElement"] = string.Join("+", _context.CombatPower.Blocks.Select(a => a.Element).Distinct().OrderBy(e => e));
        if (_playedState != null)
            foreach (var entry in _playedState.Where(p => p.Key.EndsWith("Pool") || p.Key.EndsWith("Element") || p.Key.StartsWith("token:")))
                result["played:" + entry.Key] = entry.Value;
        if (_case.basicAcceptance && _playedState != null)
            foreach (var expected in _case.expected.Where(e => e.key.StartsWith("played:")))
                if (_playedState.TryGetValue(expected.key.Substring(7), out var observed)) result[expected.key] = observed;
        if (_case.cardId == "magic_002" && _playedState != null)
            foreach (string key in new[] { "draw:ids", "discard:ids", "cards:uniqueInstances", "NextDrawBonus", "draw", "hand", "cards:played" })
                result["played:" + key] = _playedState[key];
        if (_case.cardId == "magic_004" && _playedState != null)
            foreach (string key in new[] { "position:q", "position:r", "InBattle", "IgnoreSiteFortifications", "IgnoreFortified", "TunnelAssaultActive", "hand", "cards:played" })
                result["played:" + key] = _playedState[key];
        if (_combatResult != null)
        {
            result["combat:printed"] = _combatResult.PrintedPower.ToString();
            result["combat:effective"] = _combatResult.EffectivePower.ToString();
            result["combat:required"] = _combatResult.RequiredPower.ToString();
            result["combat:kills"] = _combatResult.Kills.ToString();
            result["combat:fame"] = _combatResult.Fame.ToString();
            result["combat:wounds"] = _combatResult.Wounds.ToString();
        }
        result["hand"] = _player.Deck.Hand.Count.ToString();
        result["draw"] = _player.Deck.DrawPileCount.ToString();
        result["cards:played"] = _player.Deck.PlayedCards.Count.ToString();
        result["cards:removed"] = _player.Deck.RemovedCards.Count.ToString();
        result["removed:ids"] = string.Join(",", _player.Deck.RemovedCards.Select(c => c.Id));
        result["cards:total"] = (_player.Deck.Hand.Count + _player.Deck.DrawPileCount + _player.Deck.PlayedCards.Count + _player.Deck.DiscardPile.Count).ToString();
        if (IsBannerScenario)
            foreach (var entry in BannerSnapshot()) result["banner:" + entry.Key] = entry.Value;
        if (_case.cardId == "magic_002")
        {
            result["draw:ids"] = string.Join(",", _player.Deck.DrawPile.Select(c => c.Id));
            var all = _player.Deck.Hand.Concat(_player.Deck.DrawPile).Concat(_player.Deck.PlayedCards).Concat(_player.Deck.DiscardPile).ToArray();
            result["cards:uniqueInstances"] = all.Where((c,i) => !all.Take(i).Any(other => ReferenceEquals(c,other))).Count().ToString();
        }
        if (_case.recruitOffer != null)
        {
            result["recruit:offer"] = string.Join(",", _recruitOffer.Select(c => c.Id));
            result["recruit:owned"] = string.Join(",", _player.Units.Select(u => u.Card.Id));
            result["recruit:art"] = _recruitArt.sprite?.name ?? "";
            result["recruit:freeSlots"] = _player.FreeSlots.ToString();
            result["recruit:ready"] = string.Join(",",_player.Units.Select(u=>u.IsReady));
            result["recruit:disbanded"] = _damageUnits[0].IsDestroyed.ToString();
            result["recruit:interactionReversed"] = _context.InteractionReversed.ToString();
        }
        result["handWounds"] = _player.Deck.Hand.Count(c => c.Type == CardType.Wound).ToString();
        result["discard"] = _player.Deck.DiscardPile.Count.ToString();
        if (_case.cardId == "advanced_card_025" || _case.cardId == "items_023")
        {
            result["spell:offer"] = string.Join(",", _context.SpellSupply.Offer.Select(s => s.Id));
            result["spell:art"] = _recruitArt.sprite?.name ?? "";
        }
        if (_case.discardIndices != null || _case.playFromHand || _case.cardId == "advanced_card_029" || _case.cardId == "advanced_card_025")
        {
            result["hand:ids"] = string.Join(",", _player.Deck.Hand.Select(c => c.Id));
            result["discard:ids"] = string.Join(",", _player.Deck.DiscardPile.Select(c => c.Id));
        }
        result["unitReady"] = _damageUnits[0].IsReady.ToString(); result["unitWounds"] = _damageUnits[0].Wounds.ToString();
        result["unitLevel"] = _damageUnits[0].Card.Level.ToString();
        result["unitPhysicalResist"] = _damageUnits[0].PhysicalResistTemp.ToString();
        result["advancedOffer"] = _context.AdvActionSupply.Offer.Count.ToString(); result["spellOffer"] = _context.SpellSupply.Offer.Count.ToString();
        result["skillOffer"] = _context.SkillSupply.Offer.Count.ToString();
        if (IsHeadband)
        {
            var skills = _context.SkillSupply.Offer.Concat(_player.Skills).ToArray();
            result["skillOffer:sources"] = string.Join(",", _context.SkillSupply.Offer.Select(s => s.SourceId));
            result["skills:sources"] = string.Join(",", _player.Skills.Select(s => s.SourceId));
            result["headband:selectedZone"] = _player.Skills.Any(s => ReferenceEquals(s, _headbandSelected)) ? "本人技能" : "公共供应";
            result["headband:identity"] = (skills.Length == _headbandOfferInstances.Length && _headbandOfferInstances
                .All(s => skills.Count(other => ReferenceEquals(s, other)) == 1)).ToString();
        }
        if (_case.cardId == "items_010")
            foreach (var entry in ArtifactFixtureSnapshot()) result["gold:"+entry.Key] = entry.Value;
        if (_case.cardId == "items_009")
            foreach (var entry in WrathSnapshot()) result["wrath:"+entry.Key] = entry.Value;
        if (IsAmulet)
            foreach (var entry in AmuletSnapshot()) result["amulet:"+entry.Key] = entry.Value;
        if (IsSupplyExchange)
            foreach (var entry in WisdomSnapshot()) result["wisdom:"+entry.Key] = entry.Value;
        if (IsRitualLifecycle)
            foreach (var entry in RitualSnapshot()) result["ritual:"+entry.Key] = entry.Value;
        if (IsLearning)
            foreach (var entry in LearningSnapshot()) result["learning:"+entry.Key] = entry.Value;
        if (IsBloodline)
            foreach (var entry in BloodlineSnapshot()) result["bloodline:"+entry.Key] = entry.Value;
        if (IsDecompose)
            foreach (var entry in DecomposeSnapshot()) result["decompose:"+entry.Key] = entry.Value;
        if (IsOverload)
        {
            foreach (var entry in DecomposeSnapshot()) result["overload:"+entry.Key] = entry.Value;
            result["overload:spells"] = string.Join(",", _context.SpellSupply.Offer.Select(c=>c.Id));
            result["overload:choicesCleared"] = (_context.OverloadSelections == null).ToString();
            result["overload:spellColors"] = string.Join(",", _context.SpellColors);
            result["overload:remaining"] = (_context.PendingRepeatedEffect?.Remaining ?? 0).ToString();
            result["overload:healing"] = _context.Healing.Total.ToString();
            result["overload:battleWounds"] = _context.BattleWoundsReceived.ToString();
            result["overload:knockedOut"] = _context.IsKnockedOut.ToString();
            result["overload:offerIdentity"] = (_originalSpellOffer.Length==_context.SpellSupply.Offer.Count
                && _originalSpellOffer.Where((card,i)=>!ReferenceEquals(card,_context.SpellSupply.Offer[i])).Count()==0).ToString();
        }
        if (IsDecompose && _playedState!=null)
            foreach(var entry in _playedState.Where(p=>p.Key.StartsWith("decompose:")))
                result["played:"+entry.Key]=entry.Value;
        if (IsOverload && _playedState!=null)
            foreach(var entry in _playedState.Where(p=>p.Key.StartsWith("overload:") || new[]{"hand","draw","discard:ids"}.Contains(p.Key)))
                result["played:"+entry.Key]=entry.Value;
        if (IsBloodline && _playedState!=null)
            foreach(var entry in _playedState.Where(p=>p.Key.StartsWith("bloodline:")))
                result["played:"+entry.Key]=entry.Value;
        if (IsLearning && _playedState!=null)
            foreach(var entry in _playedState.Where(p=>p.Key.StartsWith("learning:")))
                result["played:"+entry.Key]=entry.Value;
        if (IsRitualLifecycle && _playedState!=null)
            foreach(var entry in _playedState.Where(p=>p.Key.StartsWith("ritual:") || p.Key.StartsWith("token:")))
                result["played:"+entry.Key]=entry.Value;
        if (IsSupplyExchange && _playedState!=null)
            foreach (var entry in _playedState.Where(p=>p.Key.StartsWith("wisdom:") || p.Key.StartsWith("crystal:") || (_case.cardId=="advanced_card_027" && p.Key.StartsWith("token:"))))
                result["played:"+entry.Key]=entry.Value;
        return result;
    }

    public Dictionary<string, string> ReadAutomationState()
    {
        _comparison.ForceMeshUpdate(); _fixture.ForceMeshUpdate(); _printed.ForceMeshUpdate(); _guardInfo.ForceMeshUpdate();
        return new()
        {
        ["ui:textFits"] = (!_comparison.isTextTruncated && !_comparison.isTextOverflowing
            && !_fixture.isTextTruncated && !_fixture.isTextOverflowing
            && !_printed.isTextTruncated && !_printed.isTextOverflowing
            && !_guardInfo.isTextTruncated && !_guardInfo.isTextOverflowing).ToString(),
        ["caseId"] = _case.id, ["cardId"] = _entry.source.Id, ["art"] = _art.sprite?.name ?? "",
        ["operationCount"] = _operationIndex.ToString(), ["played"] = _played.ToString(), ["ui:fixture"] = _fixture.text,
        ["ui:materialArt"] = _materialArt.gameObject.activeInHierarchy ? _materialArt.sprite?.name ?? "" : "",
        ["ui:combatSummary"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("结算：")) ?? "",
        ["ui:resolveChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("不屈选择：")) ?? "",
        ["ui:enemyArtId"] = _catalog.enemyArtwork.FirstOrDefault(e => e.artwork == _recruitArt.sprite)?.id ?? "",
        ["ui:coldChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("冷峻选择：")) ?? "",
        ["ui:mindChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("思维选择：")) ?? "",
        ["ui:mindPlayers"] = string.Join("|", _comparison.text.Split('\n').Where(line => line.StartsWith("玩家"))),
        ["ui:boxReveal"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("揭示原神器：")) ?? "",
        ["ui:boxZone"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("宝盒本体：")) ?? "",
        ["ui:boxUse"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("借用方式：")) ?? "",
        ["ui:boxBanner"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("借旗：")) ?? "",
        ["ui:boxRecruit"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("实际招募：")) ?? "",
        ["ui:boxSupply"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("供应顺序：")) ?? "",
        ["ui:warpPermission"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("扭曲许可：")) ?? "",
        ["ui:warpOrder"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("实际回合顺序：")) ?? "",
        ["ui:warpAside"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("暂存至轮末：")) ?? "",
        ["ui:warpExplore"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("实际探索：")) ?? "",
        ["ui:warpGuard"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("原肆虐敌：")) ?? "",
        ["ui:warpWounds"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("肆虐实际承伤：")) ?? "",
        ["ui:headbandChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("头环选择：")) ?? "",
        ["ui:headbandOwner"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("原技能归属：")) ?? "",
        ["ui:headbandSkillArt"] = _headbandSkillArt.gameObject.activeInHierarchy ? _headbandSkillArt.sprite?.name ?? "" : "",
        ["ui:coldAttack"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("出牌后攻击：")) ?? "",
        ["ui:surgeDraw"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("新伤抽牌：")) ?? "",
        ["ui:explosiveArmor"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("实际减甲：")) ?? "",
        ["ui:manaPowerChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("魔晶力量选择：")) ?? "",
        ["ui:mountainRefill"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("回合末：")) ?? "",
        ["ui:ambushFirst"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("伏击首效：")) ?? "",
        ["ui:bloodChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("鲜血选色：")) ?? "",
        ["ui:steadyChoice"] = _comparison.text.Split('\n').FirstOrDefault(line => line.StartsWith("回收选择：")) ?? "",
        ["ui:crystalRefunds"] = string.Join("|", _comparison.text.Split('\n').Where(line => line.StartsWith("回合结算后魔晶 红/蓝/绿：")).Select(line => line.Substring("回合结算后魔晶 红/蓝/绿：".Length))),
        ["ui:offerArt"] = _recruitArt.gameObject.activeInHierarchy ? _recruitArt.sprite?.name ?? "" : "",
        ["ui:repeatRemaining"] = (_context.PendingRepeatedEffect?.Remaining ?? 0).ToString(),
        ["ui:originalEnemyArt"] = (_recruitArt.gameObject.activeInHierarchy && _recruitArt.sprite != null
            && _catalog.enemyArtwork.Any(a=>a.artwork==_recruitArt.sprite
                && (_case.enemies?.Any(e=>e.originalDefinition && e.id==a.id) ?? false))).ToString(),
        ["selected"] = _selected.ToString(), ["executed"] = _executed.ToString(), ["completed"] = _results.Count.ToString(),
        ["ui:rule"] = _printed.text, ["ui:comparison"] = _comparison.text, ["ui:status"] = _status.text
        };
    }
}
