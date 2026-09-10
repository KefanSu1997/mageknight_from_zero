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
    private ActionSystem _system;
    private int _index;
    private bool _selected;
    private bool _executed;
    private bool _played;
    private Dictionary<string, string> _playedState;
    private CardCombatActions.Result _combatResult;
    private string _started;
    private string _suiteSha256;
    private string _outputRoot;
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
        _entry = _catalog.cards.Single(e => e.source.Id == _case.cardId);
        _selected = _executed = _played = false;
        _playedState = null; _combatResult = null;
        _play.GetComponentInChildren<TextMeshProUGUI>().text = "执行所选效果";
        _player = new PlayerState(1, "验收玩家");
        _context = new ActionContext { CurrentTerrain = TerrainType.Forest, DayPart = _case.enhanced && _entry.source is SpellCardSO ? DayPart.Night : DayPart.Day };
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
        for (int i = 0; i < 10; i++) _player.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
        _player.Deck.GainToDiscard(new DeedCard("basic_card_008", CardType.Action));
        _player.Deck.GainToDiscard(new DeedCard("basic_card_016", CardType.Action));
        // A declared fixture, not a new gameplay card: two exhausted level-2 units.
        var unit = new UnitCard("verification_unit", "验收部队", "Fixture", 2, 4, 6, RecruitLocation.Village,
            Array.Empty<AttackProfile>(), Array.Empty<Ability>());
        _player.Units.Add(new UnitState(unit)); _player.Units.Add(new UnitState(unit));
        foreach (var u in _player.Units) u.Exhaust();
        _context.CardToDiscard = _player.Deck.Hand[1]; _context.CardToRemove = _player.Deck.Hand[1];
        _context.CardToRecycle = source;
        _context.CardsToDiscard.AddRange(_player.Deck.Hand.Skip(1).Take(2));
        _context.CardsFromDiscard.AddRange(_player.Deck.DiscardPile);
        _context.AdvActionSupply = new AdvActionSupply();
        _context.AdvActionSupply.SetDeck(new[] { new AdvActionCard("advanced_card_000"), new AdvActionCard("advanced_card_001"), new AdvActionCard("advanced_card_003") });
        _context.AdvActionSupply.Refill(false);
        _context.SpellSupply = new SpellSupply();
        _context.SpellSupply.SetDeck(new[] { new SpellCard("magic_009"), new SpellCard("magic_015"), new SpellCard("magic_005") });
        _context.SpellSupply.Refill();
        _context.SkillSupply = new SkillSupply();
        _context.SkillSupply.SetDeck(CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")); _context.SkillSupply.Refill();
        _context.ArtifactSupply = new ArtifactSupply();
        _context.ArtifactSupply.SetDeck(new[] { new ArtifactCard("items_004"), new ArtifactCard("items_010") });
        _context.ManaSource = new ManaSource(1, () => _context.DayPart, new System.Random(20260910));
        _context.Enemies = new[] { new Monster("fixture_enemy", 5, 4, Element.Physical, 3, Array.Empty<Ability>()) };
        if (_case.enemies != null && _case.enemies.Length > 0)
            _context.Enemies = _case.enemies.Select(e => e.Create()).ToList();
        _context.EngagedEnemies = _context.Enemies.Count;
        foreach (var value in _case.setup ?? Array.Empty<CardVerificationValue>()) Set(value.key, value.value);
        _before = Snapshot();
        _title.text = $"{_index + 1}/{_cases.Length}   {_entry.source.NameCn}   {_case.cardId}   ·   {_case.title}";
        _art.sprite = _entry.artwork;
        _printed.text = PrintedEffect();
        _fixture.text = $"初始：手牌{_before["hand"]}（伤牌{_before["handWounds"]}）· 弃牌{_before["discard"]} · 魔力 红{_before["token:Red"]}/蓝{_before["token:Blue"]}/绿{_before["token:Green"]}/白{_before["token:White"]}/金{_before["token:Gold"]}/黑{_before["token:Black"]}\n目标：2级耗竭部队 / 敌人护甲5、攻击4；{_context.CurrentTerrain}，{_context.DayPart}；选项 {_case.option}";
        _comparison.text = "独立规则预期（执行后绝对值）\n" + string.Join("\n", _case.expected.Take(9).Select(e => $"{Label(e.key)}：{e.value}"));
        _status.text = "待操作：先选择卡牌和目标，再执行";
        _limits.text = "范围：" + ((_case.limitations?.Length ?? 0) == 0 ? "本例的即时效果分支；整卡结论需汇总全部案例。" : string.Join("；", _case.limitations));
        if (!string.IsNullOrEmpty(_case.combatPhase))
            _fixture.text = "目标：" + string.Join(" / ", _context.Enemies.Select(e => $"{e.Id} 护甲{e.Armor} 攻击{e.Attack}({e.AttackElement}) [{string.Join(",", e.Abilities)}]"))
                + $"\n{_case.combatPhase}阶段 · 城防地点={_context.FortifiedSite} · {_context.DayPart}";
        _play.interactable = false;
    }

    private string PrintedEffect()
    {
        if (_entry.source is ActionCardSO action) return (_case.enhanced ? action.EnhancedEffect : action.BaseEffect) ?? "";
        if (_entry.source is SpellCardSO spell) return (_case.enhanced ? spell.Bottom?.Effect : spell.Top?.Effect) ?? "";
        if (_entry.source is ItemCardSO item) return (_case.enhanced ? item.OncePerRound : item.AssignEffect) ?? "";
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

    private void Select()
    {
        if (_executed) return;
        _selected = true; _context.TargetUnit = _player.Units[0];
        _status.text = "已选择原卡和夹具目标，可以执行"; _play.interactable = true;
    }

    private void Execute()
    {
        if (!_selected || _executed) return;
        if (_played) { ResolveCombat(); return; }
        _before = Snapshot();
        string exception = "";
        string executionPath = "ActionSystem.Play";
        try
        {
            int option = string.IsNullOrEmpty(_case.effectChoice) ? _case.option : (int)Enum.Parse(typeof(ActionEffectId), _case.effectChoice);
            if (_entry.source is ItemCardSO)
            {
                // Items have existing effects but no ItemCardData play service. Audit
                // the real module explicitly; do not report this as game integration.
                int index = int.Parse(_entry.source.Id.Substring("items_".Length));
                var id = (ActionEffectId)Enum.Parse(typeof(ActionEffectId), ItemEffects[index] + (_case.enhanced ? "Once" : "Assign"));
                var effect = CardEffectFactory.Get(id);
                executionPath = "CardEffectFactory / " + effect.GetType().Name + " (module only)";
                effect.Execute(_player, _context, option);
            }
            else _system.Play(Data(_entry.source, _case.enhanced), _player, _context, _case.enhanced, option);
            if (!string.IsNullOrEmpty(_case.followup))
            {
                var next = _catalog.cards.Single(e => e.source.Id == _case.followup);
                _system.Play(Data(next.source, _case.followupEnhanced), _player, _context, _case.followupEnhanced);
            }
        }
        catch (Exception e) { exception = e.GetType().Name + ": " + e.Message; }
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
            _status.text = "按规则拒绝操作 · 资源守恒断言通过：" + exception.Substring(exception.IndexOf(": ", StringComparison.Ordinal) + 2);
        if (_combatResult != null)
        {
            var costs = checks.Where(c => c.key.StartsWith("token:") || c.key.StartsWith("crystal:"));
            _comparison.text = "战斗结算：实际结果 / 独立规则预期\n" + string.Join("\n", checks
                .Where(c => c.key.StartsWith("combat:")).Select(c => $"{(c.passed ? "通过" : "不符")}  {Label(c.key)}：{c.actual} / {c.expected}"))
                + "\n费用：" + string.Join("；", costs.Select(c => $"{Label(c.key)} {_before[c.key]}→{c.actual} [预期{c.expected}]"))
                + $"\n剩余资源：近战{_context.MeleePool} 远程{_context.RangedPool} 攻城{_context.SiegePool} 格挡{_context.BlockPool}；手牌伤牌 {_before["handWounds"]}→{_player.Wounds}";
            if (status == "partial") _status.text = "本例操作与数值通过 · 整卡仍有待验证范围";
        }
        _status.color = status == "failed" ? new Color32(244, 142, 118, 255) : W.Gold;
        SaveResults();
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
        foreach (var target in new object[] { _context, _player })
            foreach (var p in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = p.GetValue(target);
                if (value is int || value is bool || value is Enum || value is string) result[p.Name] = value.ToString();
                else if (value is IDictionary dict)
                    foreach (DictionaryEntry e in dict) result[p.Name + ":" + e.Key] = e.Value?.ToString() ?? "";
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
        result["attackElement"] = string.Join("+", _context.CombatPower.Attacks.Select(a => a.Element).Distinct().OrderBy(e => e));
        result["blockElement"] = string.Join("+", _context.CombatPower.Blocks.Select(a => a.Element).Distinct().OrderBy(e => e));
        if (_playedState != null)
            foreach (var entry in _playedState.Where(p => p.Key.EndsWith("Pool") || p.Key.EndsWith("Element")))
                result["played:" + entry.Key] = entry.Value;
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
        result["handWounds"] = _player.Deck.Hand.Count(c => c.Type == CardType.Wound).ToString();
        result["discard"] = _player.Deck.DiscardPile.Count.ToString();
        result["unitReady"] = _player.Units[0].IsReady.ToString(); result["unitWounds"] = _player.Units[0].Wounds.ToString();
        result["unitLevel"] = _player.Units[0].Card.Level.ToString();
        result["unitPhysicalResist"] = _player.Units[0].PhysicalResistTemp.ToString();
        result["advancedOffer"] = _context.AdvActionSupply.Offer.Count.ToString(); result["spellOffer"] = _context.SpellSupply.Offer.Count.ToString();
        result["skillOffer"] = _context.SkillSupply.Offer.Count.ToString();
        return result;
    }

    public Dictionary<string, string> ReadAutomationState() => new()
    {
        ["caseId"] = _case.id, ["cardId"] = _entry.source.Id, ["art"] = _art.sprite?.name ?? "",
        ["played"] = _played.ToString(), ["ui:fixture"] = _fixture.text,
        ["selected"] = _selected.ToString(), ["executed"] = _executed.ToString(), ["completed"] = _results.Count.ToString(),
        ["ui:rule"] = _printed.text, ["ui:comparison"] = _comparison.text, ["ui:status"] = _status.text
    };
}
