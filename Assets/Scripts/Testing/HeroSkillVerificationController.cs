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

/// <summary>Real skill commands and lifecycle events; expected values are authored separately in JSON.</summary>
public sealed class HeroSkillVerificationController : MonoBehaviour, ISceneAutomationStateSource
{
    [Serializable] public sealed class Suite { public Case[] cases; }
    [Serializable] public sealed class Case
    {
        public string id, title, day, resistance;
        public int skillIndex;
        public bool unowned;
        public Step[] steps;
    }
    [Serializable] public sealed class Step
    {
        public string kind, label;
        public int option;
        public CardVerificationValue[] expected;
    }

    public Sprite originalSheet;
    private Case[] _cases;
    private Case _case;
    private int _index, _stepIndex;
    private PlayerState _player;
    private ActionContext _context;
    private SkillCard _skill;
    private string _exception = "", _outcome = "", _suiteHash;
    private bool _selected;
    private int _effective, _kills;
    private TextMeshProUGUI _title, _rule, _values, _result, _nextLabel;
    private Button _execute;
    public string LastRule => _rule.text;

    private void Awake()
    {
        CardJsonLoader.SetBaseDirectory(Path.Combine(Application.dataPath, "../resources/text_json"));
        var json = Resources.Load<TextAsset>("CardVerification/skill_cases").text;
        _cases = JsonUtility.FromJson<Suite>(json).cases;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            _suiteHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n")))).Replace("-", "").ToLowerInvariant();
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var root = W.Panel(canvas.transform, "UIRoot", 0, 0, 1920, 1080, new Color32(12, 24, 27, 255));
        W.Text(root, "Heading", "魔法骑士 · 原版英雄技能验收", 48, 24, 1750, 55, 34, W.Gold);
        _title = W.Text(root, "Case", "", 48, 88, 1780, 65, 25, W.Cream);
        W.Sprite(root, "OriginalSheet", originalSheet, 48, 160, 556, 782);
        W.Text(root, "Source", "原始技能表 skill_000 · 保留原图", 48, 945, 565, 45, 20, W.Muted);
        _rule = W.Text(root, "Rule", "", 660, 168, 1190, 145, 27, W.Cream);
        _values = W.Text(root, "Values", "", 660, 330, 1190, 380, 23, W.Cream);
        _result = W.Text(root, "Result", "", 660, 720, 1190, 120, 24, W.Gold);
        W.Text(root, "Limits", "本批覆盖前五个技能及使用标记生命周期；其他技能、部队能力与完整冒险界面仍待联验。", 660, 845, 1190, 90, 21, W.Muted);
        W.Button(root, "Btn_Select", "选择原版技能", 660, 967, 290, 65).onClick.AddListener(Select);
        _execute = W.Button(root, "Btn_Execute", "执行", 970, 967, 500, 65, true);
        _execute.onClick.AddListener(Execute); _nextLabel = _execute.GetComponentInChildren<TextMeshProUGUI>();
        W.Button(root, "Btn_Next", "下一案例", 1490, 967, 340, 65).onClick.AddListener(Next);
        LoadCase();
    }

    private void LoadCase()
    {
        _case = _cases[_index]; _stepIndex = 0; _selected = false;
        _player = new PlayerState(1, "技能验收玩家");
        _skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[_case.skillIndex];
        if (!_case.unowned) _player.Skills.Add(_skill);
        NewContext(); _exception = _outcome = ""; _effective = _kills = 0;
        _title.text = $"{_index + 1}/{_cases.Length} · {_skill.Id} · {_case.title} · {_case.id}";
        _rule.text = _skill.PrintedRule;
        _values.text = "请选择左侧原表中的技能，再按右下按钮执行当前步骤。";
        _result.text = "未执行"; _execute.interactable = false;
        _nextLabel.text = _case.steps[0].label;
    }

    private void NewContext() => _context = new ActionContext { DayPart = (DayPart)Enum.Parse(typeof(DayPart), _case.day) };
    private void Select() { _selected = true; _execute.interactable = _stepIndex < _case.steps.Length; }
    private void Next() { if (_stepIndex == _case.steps.Length && _index + 1 < _cases.Length) { _index++; LoadCase(); } }

    private void Execute()
    {
        if (!_selected || _stepIndex >= _case.steps.Length) return;
        var step = _case.steps[_stepIndex]; _exception = _outcome = "";
        try
        {
            switch (step.kind)
            {
                case "use": SkillActions.Use(_player, _context, _skill, step.option); break;
                case "reloadUse":
                    // Reacquiring the same original definition must not reset its usage marker.
                    _player.Skills.Remove(_skill);
                    _skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[_case.skillIndex];
                    _player.Skills.Add(_skill);
                    SkillActions.Use(_player, _context, _skill, step.option); break;
                case "nextTurn":
                    new PlayerTurnEngine().EndTurn(_player, _context.DayPart, new ManaSource(1, () => _context.DayPart), _context);
                    new PlayerTurnEngine().StartTurn(_player, _context.DayPart); NewContext(); break;
                case "nextRound":
                    new GameEngine(new[] { _player }, 3).StartNewRound(Array.Empty<TacticCard>());
                    NewContext(); break;
                case "move":
                    var map = new MapState(); var target = new AxialCoord(1, 0);
                    map.Placed[target] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
                    _outcome = new MovementService().TryMoveUsingPool(_player, target, map, _context).ToString(); break;
                case "attack":
                    var abilities = new List<Ability>();
                    if (_case.skillIndex == 1) abilities.Add(Ability.Fortified);
                    if (!string.IsNullOrEmpty(_case.resistance)) abilities.Add((Ability)Enum.Parse(typeof(Ability), _case.resistance));
                    var enemy = new MK.Logic.Data.Monster("skill_verification_target", _case.skillIndex == 1 ? 1 : 2, 1, Element.Physical, 2, abilities.ToArray());
                    var result = CardCombatActions.Attack(_player, _context, new[] { enemy }, new[] { 0 }, _case.skillIndex == 1 ? Phase.Ranged : Phase.Melee);
                    _effective = result.EffectivePower; _kills = result.Kills; break;
                default: throw new InvalidOperationException("未知验收操作");
            }
        }
        catch (InvalidOperationException ex) { _exception = ex.Message; }
        _stepIndex++;
        var state = ReadAutomationState();
        var lines = new List<string>(); bool passed = true;
        foreach (var expected in step.expected)
        {
            string actual = state.TryGetValue(expected.key, out var value) ? value : "缺少状态";
            bool equal = actual == expected.value; passed &= equal;
            lines.Add($"{(equal ? "通过" : "失败")}  {Label(expected.key)}：{Display(actual)}  /  预期 {Display(expected.value)}");
        }
        _values.text = string.Join("\n", lines);
        _result.text = $"步骤 {_stepIndex}/{_case.steps.Length} · {step.label}\n{(passed ? "本步骤断言通过" : "本步骤存在失败，不得计为通过")}";
        _execute.interactable = _stepIndex < _case.steps.Length;
        _nextLabel.text = _execute.interactable ? _case.steps[_stepIndex].label : "本例已完成";
    }

    private static string Display(string value) => value == "" ? "无" : value == "True" ? "是" : value == "False" ? "否" : value;
    private static string Label(string key) => key switch
    {
        "exception" => "拒绝原因", "used" => "技能已使用", "move" => "移动力", "influence" => "影响力",
        "siege" => "攻城攻击", "melee" => "近战攻击", "ranged" => "远程攻击", "element" => "攻击元素",
        "redCrystal" => "红色魔晶", "redToken" => "红色魔力", "blackToken" => "黑色魔力",
        "position" => "位置Q", "outcome" => "移动成功", "effective" => "折算攻击", "kills" => "击杀数", "fame" => "名望",
        _ => key
    };

    public Dictionary<string, string> ReadAutomationState() => new()
    {
        ["caseId"] = _case.id, ["sourceId"] = _skill.SourceId, ["art"] = originalSheet?.name ?? "",
        ["suiteSha256"] = _suiteHash, ["selected"] = _selected.ToString(), ["step"] = _stepIndex.ToString(),
        ["exception"] = _exception, ["used"] = SkillActions.IsUsed(_player, _skill).ToString(),
        ["move"] = _context.MovementPool.ToString(), ["influence"] = _context.InfluencePool.ToString(),
        ["siege"] = _context.SiegePool.ToString(), ["melee"] = _context.MeleePool.ToString(), ["ranged"] = _context.RangedPool.ToString(),
        ["element"] = _context.CombatPower.Attacks.FirstOrDefault()?.Element.ToString() ?? "None",
        ["redCrystal"] = _player.Mana.Crystals.GetValueOrDefault(ManaColor.Red).ToString(),
        ["redToken"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString(),
        ["blackToken"] = _player.Mana.Tokens.GetValueOrDefault(ManaColor.Black).ToString(),
        ["position"] = _player.Position.Q.ToString(), ["outcome"] = _outcome,
        ["effective"] = _effective.ToString(), ["kills"] = _kills.ToString(), ["fame"] = _player.Fame.ToString(),
        ["ui:rule"] = _rule.text, ["ui:values"] = _values.text, ["ui:result"] = _result.text
    };
}
