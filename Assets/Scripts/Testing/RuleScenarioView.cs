using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>四场景共享排版与状态绑定。规则计算全部留在逻辑层。</summary>
public sealed partial class RuleScenarioView
{
    private static readonly Color Ink = new Color32(10, 25, 25, 244);
    private static readonly Color Gold = new Color32(201, 171, 113, 255);
    private static readonly Color Cream = new Color32(236, 233, 218, 255);
    private static readonly Color Muted = new Color32(174, 193, 184, 255);
    private readonly TMP_FontAsset _font;
    private readonly Action<string> _act;
    private readonly RectTransform _root;
    private readonly RuleScenarioKind _kind;
    private readonly Dictionary<string, List<TextMeshProUGUI>> _values = new();
    private readonly Dictionary<string, Image> _choices = new();
    private readonly Dictionary<string, RuleHexGraphic> _hexes = new();
    private readonly Dictionary<string, TextMeshProUGUI> _hexLabels = new();
    private readonly Dictionary<int, TextMeshProUGUI> _unitStatuses = new();
    private readonly TextMeshProUGUI _message;
    private readonly TextMeshProUGUI _rule;
    private readonly TextMeshProUGUI _journal;
    private readonly TextMeshProUGUI _caseLabel;
    private RectTransform _pawn;
    private TextMeshProUGUI _journeyPhase;

    public RuleScenarioView(Canvas canvas, TMP_FontAsset font, RuleScenarioKind kind, Action<string> act)
    {
        _font = font; _act = act; _kind = kind;
        var backdrop = Picture(canvas.transform, "Backdrop", "UI/DeckManaTable/table_background_v1", 0, 0, 1920, 1080);
        var br = (RectTransform)backdrop.transform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        _root = Rect(canvas.transform, "UIRoot", 0, 0, 1920, 1080);
        _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.one * .5f;
        _root.anchoredPosition = Vector2.zero;
        Panel(_root, "Shade", 0, 0, 1920, 1080, new Color(0.015f, .04f, .04f, .42f), false);
        string[] titles = { "林间交锋", "迷雾边境", "村庄盟约", "远征初章" };
        string[] subtitles = { "算准每一次格挡，让每一点攻击都有意义。", "看清地形与昼夜，在有限的移动力中选择道路。", "以影响力赢得盟友，以指挥槽管理部队。", "从探索到招募，再到战斗：完成两个连续回合。" };
        Text(_root, "Eyebrow", "M A G E   K N I G H T   /   秘法行旅", 58, 34, 760, 26, 16, Gold);
        Text(_root, "Title", titles[(int)kind], 54, 75, 620, 62, 46, Cream);
        Text(_root, "Subtitle", subtitles[(int)kind], 60, 150, 1100, 30, 19, Muted);
        Text(_root, "Chapter", "规则演练  /  " + ((int)kind + 1).ToString("00"), 1530, 146, 325, 30, 19, Gold, TextAlignmentOptions.Right);
        string[] tabs = { "战斗", "探索", "招募", "完整流程" };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var tab = Button(_root, "Nav" + i, tabs[i], 1054 + i * 203, 67, 190, 58, i == (int)kind);
            tab.onClick.AddListener(() => Navigate((RuleScenarioKind)index));
        }
        Line(_root, 58, 201, 1804, new Color(Gold.r, Gold.g, Gold.b, .45f));
        var side = Panel(_root, "Sidebar", 48, 226, 282, 600, Ink);
        Text(side, "Title", kind == RuleScenarioKind.Journey ? "本次目标" : "练习案例", 24, 25, 236, 36, 25, Cream);
        _caseLabel = Text(side, "CaseInfo", "", 24, 75, 234, 126, 18, Muted);
        var cases = Rect(side, "Cases", 22, 215, 238, 285);
        if (kind == RuleScenarioKind.Combat)
        {
            Choice(cases, "normal", "基础格挡", 0); Choice(cases, "swift", "迅捷敌人", 66);
            Choice(cases, "brutal", "残暴敌人", 132); Choice(cases, "fire", "元素攻防", 198);
        }
        else if (kind == RuleScenarioKind.Exploration)
        {
            Command(cases, "day", "白昼案例", "day", 0, 0, 238, 56);
            Command(cases, "night", "夜间案例", "night", 0, 70, 238, 56);
            Text(cases, "Note", "切换案例会重置棋盘与资源。\n\n金色棋子代表当前位置。\n\n先选地块，再确认行动。", 4, 150, 229, 132, 17, Muted);
        }
        else if (kind == RuleScenarioKind.Recruitment)
        {
            Choice(cases, "village", "初入村庄", 0); Choice(cases, "reputation", "声望交涉", 76);
            Choice(cases, "monastery", "修道院", 152); Choice(cases, "capacity", "指挥槽不足", 228);
        }
        else
            Text(cases, "Goals", "01  揭示平原村庄\n\n02  进入村庄并招募\n\n03  结束第一回合\n\n04  部队协同强化攻击", 4, 4, 230, 275, 20, Cream);
        Command(side, "reset", "重新开始", "reset", 22, 520, 238, 54);
        var right = Panel(_root, "Ledger", 1434, 226, 438, 600, Ink);
        Text(right, "Title", "行动账本", 26, 24, 382, 39, 25, Gold);
        _message = Text(right, "Message", "", 26, 82, 384, 106, 22, Cream);
        Line(right, 26, 207, 384, new Color(Gold.r, Gold.g, Gold.b, .30f));
        Text(right, "RuleTitle", "规则依据", 26, 231, 384, 30, 18, Gold);
        _rule = Text(right, "Rule", "", 26, 278, 384, 148, 18, Muted);
        Line(right, 26, 447, 384, new Color(Gold.r, Gold.g, Gold.b, .25f));
        _journal = Text(right, "Journal", "", 26, 469, 384, 112, 15, Muted);
        var actions = Panel(_root, "Actions", 48, 848, 1824, 184, Ink);
        switch (kind)
        {
            case RuleScenarioKind.Combat: BuildCombat(actions); break;
            case RuleScenarioKind.Exploration: BuildExploration(actions); break;
            case RuleScenarioKind.Recruitment: BuildRecruitment(actions); break;
            default: BuildJourney(actions); break;
        }
        Text(_root, "Footnote", "教学局 · 训练卡与固定初始资源见牌面；正式规则参考 MKUE。操作结果可在右侧账本核对。", 64, 1044, 1770, 22, 14, Muted);
    }

    public void Refresh(RuleScenarioSession session)
    {
        var state = session.ReadState();
        foreach (var pair in _values)
            foreach (var label in pair.Value) label.text = state.GetValueOrDefault(pair.Key, "—");
        _message.text = session.Message; _message.color = session.LastAccepted ? Cream : new Color32(241, 163, 140, 255);
        _rule.text = session.Rule;
        _journal.text = string.Join("\n", session.Journal.TakeLast(2));
        string selected = state.GetValueOrDefault("case", "");
        foreach (var pair in _choices) pair.Value.color = pair.Key == selected ? new Color32(63, 78, 62, 255) : new Color32(22, 42, 41, 255);
        RefreshContent(session, state);
    }

    public Dictionary<string, string> ReadDisplayedValues() => _values.ToDictionary(p => p.Key, p => p.Value[0].text);

    private static void Navigate(RuleScenarioKind kind)
    {
#if UNITY_EDITOR
        string[] names = { "Combat", "Exploration", "Recruitment", "Journey" };
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Part1/Part1_" + names[(int)kind] + "Rules.unity",
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
    }

    private void Choice(Transform parent, string id, string title, float y)
    {
        var button = Command(parent, "case_" + id, title, "case:" + id, 0, y, 238, 54);
        _choices[id] = button.GetComponent<Image>();
    }

    private Button Command(Transform p, string id, string title, string action, float x, float y, float w, float h, bool primary = false)
    {
        var button = Button(p, "Btn_" + id, title, x, y, w, h, primary);
        button.onClick.AddListener(() => _act(action));
        return button;
    }

    private Button Button(Transform p, string name, string title, float x, float y, float w, float h, bool primary)
    {
        var panel = Panel(p, name, x, y, w, h, primary ? new Color32(117, 91, 49, 255) : new Color32(22, 42, 41, 255));
        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.15f); colors.pressedColor = new Color(.72f, .76f, .72f); button.colors = colors;
        Text(panel, "Label", title, 8, 4, w - 16, h - 8, 20, Cream, TextAlignmentOptions.Center);
        return button;
    }

    private void Metric(Transform p, string key, string title, float x, float y, float w, float h = 106)
    {
        var panel = Panel(p, "Metric_" + key, x, y, w, h, Ink);
        Text(panel, "Caption", title, 17, 12, w - 34, 27, 16, Muted);
        ValueLabel(panel, key, 17, 44, w - 34, h - 48, 34);
    }

    private TextMeshProUGUI ValueLabel(Transform p, string key, float x, float y, float w, float h, int size)
    {
        var label = Text(p, "Value_" + key, "0", x, y, w, h, size, Cream);
        if (!_values.ContainsKey(key)) _values[key] = new();
        _values[key].Add(label); return label;
    }

    private RawImage Picture(Transform p, string name, string resource, float x, float y, float w, float h)
    {
        var rect = Rect(p, name, x, y, w, h); var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>(resource); image.raycastTarget = false;
        if (image.texture == null) { Debug.LogError("Missing scenario illustration: " + resource); return image; }
        float source = (float)image.texture.width / image.texture.height, target = w / h;
        image.uvRect = source > target ? new Rect((1 - target / source) / 2, 0, target / source, 1) : new Rect(0, (1 - source / target) / 2, 1, source / target);
        return image;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }

    private static RectTransform Panel(Transform p, string name, float x, float y, float w, float h, Color tint, bool border = true)
    {
        var r = Rect(p, name, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = tint; image.raycastTarget = false;
        if (border)
        {
            // Outline 会把整块半透明面板重复叠色；独立细线只绘制边缘。
            var edge = new Color(Gold.r, Gold.g, Gold.b, .45f);
            Panel(r, "TopEdge", 0, 0, w, 1, edge, false);
            Panel(r, "BottomEdge", 0, h - 1, w, 1, edge, false);
            Panel(r, "LeftEdge", 0, 0, 1, h, edge, false);
            Panel(r, "RightEdge", w - 1, 0, 1, h, edge, false);
        }
        return r;
    }

    private TextMeshProUGUI Text(Transform p, string name, string text, float x, float y, float w, float h, int size, Color tint,
        TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var label = Rect(p, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.fontSize = size; label.text = text; label.color = tint; label.alignment = align;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void Line(Transform p, float x, float y, float w, Color tint) => Panel(p, "Rule", x, y, w, 1, tint, false);
}
