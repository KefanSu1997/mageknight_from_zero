using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MageKnight.SceneAutomation;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

public sealed class UnitVerificationController : MonoBehaviour, ISceneAutomationStateSource
{
    [Serializable] public sealed class Suite { public Case[] cases; }
    [Serializable] public sealed class Case
    {
        public string id, cardId, title, location, enemyElement, resistance, enemyTraits, extraUnitId, changedElement, ignoredAbility;
        public int mana = 2, crystals, influence = 20, wounds, extraSlots, offerCount = 1;
        public bool destroyed, exhausted, unowned;
        public bool noUnitDamage;
        public int unitArmorBonus;
        public Step[] steps;
    }
    [Serializable] public sealed class Step
    {
        public string kind, label;
        public int ability, option;
        public int damage, block;
        public int[] unitTargets;
        public CardVerificationValue[] expected;
    }
    public Sprite[] artwork;
    private Case[] _cases;
    private Case _case;
    private PlayerState _player;
    private ActionContext _context;
    private UnitCard _card;
    private UnitState _unit;
    private List<UnitCard> _offer;
    private List<UnitState> _damageTargets;
    private int _enemyAttack;
    private string _exception, _outcome, _hash;
    private int _index, _step, _effective, _kills, _required;
    private bool _selected;
    private TextMeshProUGUI _title, _rule, _values, _result, _buttonText;
    private Button _execute;
    private Image _art;
    public string LastRule => _rule.text;

    private void Awake()
    {
        CardJsonLoader.SetBaseDirectory(Path.Combine(Application.dataPath, "../resources/text_json"));
        string json = Resources.Load<TextAsset>("CardVerification/unit_cases").text;
        _cases = JsonUtility.FromJson<Suite>(json).cases;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            _hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n")))).Replace("-", "").ToLowerInvariant();
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var root = W.Panel(canvas.transform, "UIRoot", 0, 0, 1920, 1080, new Color32(12, 24, 27, 255));
        W.Text(root, "Heading", "魔法骑士 · 原版部队能力验收", 48, 24, 1760, 60, 34, W.Gold);
        _title = W.Text(root, "Case", "", 48, 90, 1790, 60, 25, W.Cream);
        _art = W.Sprite(root, "OriginalCard", null, 48, 160, 556, 782);
        W.Text(root, "Source", "原版部队图片与原始规则数据", 48, 945, 560, 45, 20, W.Muted);
        _rule = W.Text(root, "Rule", "", 660, 165, 1190, 235, 22, W.Cream);
        _values = W.Text(root, "Values", "", 660, 415, 1190, 345, 23, W.Cream);
        _result = W.Text(root, "Result", "", 660, 770, 1190, 80, 24, W.Gold);
        W.Text(root, "Limits", "本场景验证原部队的招募、主动能力及列明的承伤分支；其余部队、复杂能力和完整冒险仍待联验。", 660, 860, 1190, 78, 20, W.Muted);
        W.Button(root, "Btn_Select", "选择部队与目标", 660, 967, 290, 65).onClick.AddListener(() => { _selected = true; _execute.interactable = _step < _case.steps.Length; });
        _execute = W.Button(root, "Btn_Execute", "执行", 970, 967, 500, 65, true); _execute.onClick.AddListener(Execute);
        _buttonText = _execute.GetComponentInChildren<TextMeshProUGUI>();
        W.Button(root, "Btn_Next", "下一案例", 1490, 967, 340, 65).onClick.AddListener(() =>
        { if (_step == _case.steps.Length && _index + 1 < _cases.Length) { _index++; LoadCase(); } });
        LoadCase();
    }

    private void LoadCase()
    {
        _case = _cases[_index]; _step = 0; _selected = false;
        _card = CardJsonLoader.LoadUnit(_case.cardId); _offer = Enumerable.Repeat(_card, _case.offerCount).ToList();
        _player = new PlayerState(1, "部队验收玩家") { ExtraCommandSlots = _case.extraSlots };
        _context = new ActionContext { InfluencePool = _case.influence, NoUnitDamage = _case.noUnitDamage, UnitArmorBonus = _case.unitArmorBonus };
        if (!string.IsNullOrEmpty(_case.changedElement)) _context.AttackElementChange[0] = (Element)Enum.Parse(typeof(Element), _case.changedElement);
        if (!string.IsNullOrEmpty(_case.ignoredAbility)) _context.IgnoredAbilities[0] = (Ability)Enum.Parse(typeof(Ability), _case.ignoredAbility);
        foreach (var c in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
        { _player.Mana.AddToken(c, _case.mana); _player.Mana.AddCrystal(c, _case.crystals); }
        _unit = new UnitState(_card); _exception = _outcome = ""; _effective = _kills = _required = 0;
        _damageTargets = new List<UnitState> { _unit }; _enemyAttack = 0;
        if (!string.IsNullOrEmpty(_case.extraUnitId)) _damageTargets.Add(new UnitState(CardJsonLoader.LoadUnit(_case.extraUnitId)));
        _art.sprite = artwork.Single(s => s.name == _case.cardId);
        _title.text = $"{_index + 1}/{_cases.Length} · {_card.NameCn} · {_case.title} · {_case.id}";
        _rule.text = $"等级{_card.Level} · 护甲{_card.Armor} · 招募影响力{_card.InfluenceCost} · 地点{_card.RecruitLocationMask}\n" + string.Join("\n", _card.Actions.Select(a => $"{(a.Cost.Length == 0 ? "免费" : string.Join("/", a.Cost))}：{a.PrintedRule}"));
        if (_case.steps.Any(s => s.kind == "suffer"))
            _rule.text += "\n规则书第9—10页：每次承伤一张；抗性先吸收护甲；剩余伤害继续分配。\n毒素替换为两张，麻痹直接消灭；同一部队同场战斗只能承伤一次。";
        _values.text = "选择原版部队，从招募与能力操作开始验收。"; _result.text = "未执行";
        _buttonText.text = _case.steps[0].label; _execute.interactable = false;
    }

    private void Execute()
    {
        if (!_selected || _step >= _case.steps.Length) return;
        var step = _case.steps[_step]; _exception = _outcome = "";
        try
        {
            switch (step.kind)
            {
                case "recruit":
                    _outcome = CardUnitActions.TryRecruit(_player, _context, _offer, 0, (RecruitLocation)Enum.Parse(typeof(RecruitLocation), _case.location)).ToString();
                    if (_player.Units.Count > 0) { _unit = _player.Units[0]; _damageTargets[0] = _unit; }
                    break;
                case "prepare":
                    if (!_case.unowned) _player.Units.Add(_unit);
                    foreach (var other in _damageTargets.Skip(1)) _player.Units.Add(other);
                    _unit.AddWounds(_case.wounds); if (_case.exhausted) _unit.Exhaust(); if (_case.destroyed) _unit.Destroy(); break;
                case "use": CardUnitActions.Use(_player, _context, _unit, step.ability, step.option); break;
                case "battle": _context.InBattle = true; break;
                case "suffer": Suffer(step); break;
                case "endBattle": UnitDamageResolver.EndBattle(_player, _context); break;
                case "natureResistance":
                    _context.TargetUnit = _unit;
                    new MK.Logic.Runtime.CardEffects.NatureForceEffect(false).Execute(_player, _context);
                    break;
                case "nextTurn":
                    new PlayerTurnEngine().EndTurn(_player, DayPart.Day, new ManaSource(1, () => DayPart.Day), _context);
                    new PlayerTurnEngine().StartTurn(_player, DayPart.Day); _context = new ActionContext(); break;
                case "legacyTurn": new TurnEngine().StartTurn(_player, 5); break;
                case "nextRound":
                    new GameEngine(new[] { _player }, 3).StartNewRound(Array.Empty<TacticCard>());
                    _context = new ActionContext(); break;
                case "ready": _unit.Ready(); break;
                case "heal": _unit.Heal(1); break;
                case "resolve": Resolve(); break;
                case "move":
                    var map = new MapState(); var target = new AxialCoord(1, 0);
                    map.Placed[target] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
                    _outcome = new MovementService().TryMoveUsingPool(_player, target, map, _context).ToString(); break;
                default: throw new InvalidOperationException("未知部队验收步骤");
            }
        }
        catch (InvalidOperationException ex) { _exception = ex.Message; }
        _step++;
        var state = ReadAutomationState(); bool passed = true; var lines = new List<string>();
        foreach (var expected in step.expected)
        {
            string actual = state.GetValueOrDefault(expected.key, "缺失"); bool equal = actual == expected.value; passed &= equal;
            lines.Add($"{(equal ? "通过" : "失败")} {Label(expected.key)}：{Display(actual)} / 预期 {Display(expected.value)}");
        }
        _values.text = string.Join("\n", lines); _result.text = $"{step.label} · {(passed ? "本步骤断言通过" : "本步骤存在失败")}";
        _execute.interactable = _step < _case.steps.Length; _buttonText.text = _execute.interactable ? _case.steps[_step].label : "本例已完成";
    }

    private void Resolve()
    {
        var abilities = string.IsNullOrEmpty(_case.resistance) ? Array.Empty<Ability>() : new[] { (Ability)Enum.Parse(typeof(Ability), _case.resistance) };
        var element = string.IsNullOrEmpty(_case.enemyElement) ? Element.Physical : (Element)Enum.Parse(typeof(Element), _case.enemyElement);
        var enemies = new[] { new MK.Logic.Data.Monster("unit_verification_enemy", 3, 3, element, 2, abilities) };
        var result = _context.BlockPool > 0 ? CardCombatActions.Block(_player, _context, enemies, 0)
            : CardCombatActions.Attack(_player, _context, enemies, new[] { 0 }, _context.MeleePool > 0 ? Phase.Melee : Phase.Ranged);
        _effective = result.EffectivePower; _required = result.RequiredPower; _kills = result.Kills;
    }

    private void Suffer(Step step)
    {
        _enemyAttack = step.damage;
        var traits = string.IsNullOrEmpty(_case.enemyTraits) ? Array.Empty<Ability>()
            : _case.enemyTraits.Split(',').Select(s => (Ability)Enum.Parse(typeof(Ability), s)).ToArray();
        var element = string.IsNullOrEmpty(_case.enemyElement) ? Element.Physical : (Element)Enum.Parse(typeof(Element), _case.enemyElement);
        var enemy = new MK.Logic.Data.Monster("unit_damage_rule_fixture", 3, step.damage, element, 2, traits);
        var indices = step.unitTargets ?? new[] { 0 };
        if (indices.Any(i => i < 0 || i >= _damageTargets.Count)) throw new InvalidOperationException("承伤部队索引无效");
        var selected = indices.Select(i => _damageTargets[i]).ToArray();
        var blocks = step.block > 0 ? new[] { new BlockAllocation(0, step.block, Element.Physical) } : Array.Empty<BlockAllocation>();
        BattleResolver.Resolve(_player, new[] { enemy }, blocks, Array.Empty<AttackAllocation>(),
            ctx: _context, damageAssignments: new[] { new UnitDamageAllocation(0, selected) });
    }

    private static string Display(string value) => value == "" ? "无" : value == "True" ? "是" : value == "False" ? "否" : value;
    private static string Label(string key) => key switch
    {
        "exception" => "拒绝原因", "ready" => "部队就绪", "canActivate" => "可以发动", "wounds" => "部队创伤",
        "move" => "移动力", "influence" => "影响力", "melee" => "近战攻击", "ranged" => "远程攻击", "siege" => "攻城攻击", "block" => "格挡",
        "units" => "自有部队", "offer" => "供应区数量", "outcome" => "操作成功", "effective" => "折算值", "required" => "所需值", "kills" => "击杀数",
        "heroWounds" => "英雄创伤", "element" => "攻击元素", "blockElement" => "格挡元素", "position" => "位置Q",
        "Red" => "红色魔力", "Blue" => "蓝色魔力", "Green" => "绿色魔力", "redCrystal" => "红色魔晶",
        "destroyed" => "部队已消灭", "damageAssigned" => "本场已承伤部队", "secondWounds" => "第二支部队伤牌",
        "heroDiscardWounds" => "英雄弃牌区伤牌", "freeSlots" => "空闲指挥槽", "enemyAttack" => "敌人原攻击",
        "physicalResistTemp" => "本场临时物理抗性", _ => key
    };

    public Dictionary<string, string> ReadAutomationState()
    {
        _rule.ForceMeshUpdate(); _values.ForceMeshUpdate();
        return new()
        {
        ["caseId"] = _case.id, ["cardId"] = _card.Id, ["art"] = _art.sprite.name, ["suiteSha256"] = _hash,
        ["selected"] = _selected.ToString(), ["step"] = _step.ToString(), ["exception"] = _exception, ["outcome"] = _outcome,
        ["ready"] = _unit.IsReady.ToString(), ["canActivate"] = _unit.CanActivate.ToString(), ["wounds"] = _unit.Wounds.ToString(),
        ["units"] = _player.Units.Count.ToString(), ["offer"] = _offer.Count.ToString(), ["influence"] = _context.InfluencePool.ToString(),
        ["armor"] = _card.Armor.ToString(), ["level"] = _card.Level.ToString(), ["physicalResist"] = _card.Abilities.Contains(Ability.PhysicalResist).ToString(),
        ["move"] = _context.MovementPool.ToString(), ["position"] = _player.Position.Q.ToString(), ["melee"] = _context.MeleePool.ToString(),
        ["ranged"] = _context.RangedPool.ToString(), ["siege"] = _context.SiegePool.ToString(), ["block"] = _context.BlockPool.ToString(),
        ["element"] = _context.CombatPower.Attacks.FirstOrDefault()?.Element.ToString() ?? "None",
        ["blockElement"] = _context.CombatPower.Blocks.FirstOrDefault()?.Element.ToString() ?? "None",
        ["Red"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString(), ["Blue"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString(),
        ["Green"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Green).ToString(), ["redCrystal"] = _player.Mana.Crystals.GetValueOrDefault(ManaColor.Red).ToString(),
        ["effective"] = _effective.ToString(), ["required"] = _required.ToString(), ["kills"] = _kills.ToString(), ["heroWounds"] = _player.Wounds.ToString(),
        ["destroyed"] = _unit.IsDestroyed.ToString(), ["damageAssigned"] = _context.DamageAssignedUnits.Count.ToString(),
        ["secondWounds"] = (_damageTargets.Count > 1 ? _damageTargets[1].Wounds : 0).ToString(),
        ["heroDiscardWounds"] = _player.DiscardWounds.ToString(), ["freeSlots"] = _player.FreeSlots.ToString(),
        ["enemyAttack"] = _enemyAttack.ToString(), ["physicalResistTemp"] = _unit.PhysicalResistTemp.ToString(),
        ["ui:ruleFits"] = (!_rule.isTextTruncated && !_rule.isTextOverflowing).ToString(),
        ["ui:valuesFits"] = (!_values.isTextTruncated && !_values.isTextOverflowing).ToString(),
        ["ui:rule"] = _rule.text, ["ui:values"] = _values.text, ["ui:result"] = _result.text
        };
    }
}
