using System.Collections.Generic;
using System.Text;
using MK.Logic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>牌库与魔力场景的桌面视图；沿用现有系统绑定和自动化按钮路径。</summary>
public sealed class DeckManaTableView : MonoBehaviour
{
    private static readonly Color Ink = new Color32(10, 23, 24, 238);
    private static readonly Color Gold = new Color32(199, 170, 112, 255);
    private static readonly Color Cream = new Color32(233, 229, 211, 255);
    private static readonly Color Muted = new Color32(176, 193, 183, 255);
    private TMP_FontAsset _font;
    private RectTransform _handRoot;
    private GameObject _emptySlots;
    private TextMeshProUGUI _handCount;
    private TextMeshProUGUI _handHint;
    private int _lastHandCount = -1;
    private TextMeshProUGUI _journal;
    private TextMeshProUGUI _sourceLog;
    private ScrollRect _journalScroll;
    private string _lastLog;

    public RectTransform HandRoot => _handRoot;

    public static DeckManaTableView Create(Canvas canvas, TMP_FontAsset font, Part1TestManager manager)
    {
        canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var backdrop = new GameObject("TableBackdrop", typeof(RectTransform), typeof(RawImage));
        backdrop.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)backdrop.transform);
        var texture = Resources.Load<Texture2D>("UI/DeckManaTable/table_background_v1");
        backdrop.GetComponent<RawImage>().texture = texture;
        backdrop.GetComponent<RawImage>().raycastTarget = false;
        if (texture == null) Debug.LogError("DeckManaTableView: table background is missing.");

        var root = Rect(canvas.transform, "Part1TestLayout", 0, 0, 1920, 1080);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.anchoredPosition = Vector2.zero;
        var view = root.gameObject.AddComponent<DeckManaTableView>();
        view._font = font;
        view.Build(manager);
        return view;
    }

    private void Build(Part1TestManager manager)
    {
        Text(transform, "Eyebrow", "M A G E   K N I G H T", 422, 49, 820, 28, 17, Gold);
        Text(transform, "Title", "秘法行旅", 418, 82, 850, 61, 46, Cream);
        Text(transform, "Subtitle", "整备牌组  /  凝聚魔力  /  开启旅程", 423, 151, 850, 30, 18, Muted);
        Text(transform, "Section", "牌库与魔力", 1580, 104, 272, 32, 23, Gold, TextAlignmentOptions.Right);
        Line(transform, 423, 191, 1428, 1, new Color(Gold.r, Gold.g, Gold.b, .35f));

        var control = Panel(transform, "ControlColumn", 56, 319, 306, 701, Ink);
        Text(control, "Title", "营地行动", 25, 23, 255, 36, 25, Cream);
        Text(control, "Subtitle", "每一次整备，都是新的可能。", 25, 65, 255, 44, 16, Muted);
        var buttons = Rect(control, "Buttons", 24, 123, 258, 146);
        manager.testDeckButton = Action(buttons, "测试牌库系统Button", "抽取手牌", "01", 0, true);
        manager.testManaButton = Action(buttons, "测试魔力池Button", "凝聚魔晶", "02", 78, false);
        Text(control, "ActionHint", "重新抽牌会先弃置当前手牌", 25, 278, 255, 30, 14, Muted);
        Line(control, 25, 324, 256, 1, new Color(Gold.r, Gold.g, Gold.b, .28f));
        Text(control, "LogTitle", "旅途手记", 25, 345, 255, 32, 21, Gold);
        _journal = CreateLog(control, out _journalScroll);
        manager.logScrollRect = _journalScroll;

        // 旧控制器会为六个入口注册监听；未使用的入口保留隐藏绑定。
        var bindings = Rect(control, "HiddenBindings", 0, 0, 0, 0);
        manager.testCombatButton = HiddenButton(bindings, "Combat");
        manager.testExplorationButton = HiddenButton(bindings, "Exploration");
        manager.testRecruitmentButton = HiddenButton(bindings, "Recruitment");
        manager.testFullFlowButton = HiddenButton(bindings, "FullFlow");
        var viewerHeader = Text(bindings, "ViewerHeader", "", 0, 0, 0, 0, 20, Cream);
        var viewerBody = Text(bindings, "ViewerBody", "", 0, 0, 0, 0, 20, Cream);
        _sourceLog = Text(bindings, "SourceLog", "", 0, 0, 0, 0, 17, Muted);
        manager.testLog = _sourceLog;
        bindings.gameObject.SetActive(false);

        var info = Rect(transform, "InfoColumn", 414, 217, 1450, 382);
        manager.ConfigureManaDisplay(CreateMana(info));
        var zones = Rect(info, "DeckZonesPanel", 0, 143, 1450, 239);
        var row = Rect(zones, "ZonesRow", 0, 0, 1450, 239);
        var deck = CreatePile(row, "DeckZone", "牌组", "DRAW PILE", 0, false);
        var discard = CreatePile(row, "DiscardZone", "弃牌区", "DISCARD PILE", 741, true);
        manager.ConfigureDeckZoneUI(deck.button, discard.button, deck.count, discard.count, viewerHeader, viewerBody);
        BuildHand();
    }

    private Dictionary<ManaColor, TextMeshProUGUI> CreateMana(Transform parent)
    {
        var bar = Panel(parent, "ManaDisplay", 0, 0, 1450, 112, new Color32(10, 27, 27, 228));
        Text(bar, "Header", "魔晶池", 28, 21, 155, 32, 24, Cream);
        Text(bar, "Caption", "MANA RESERVE", 29, 61, 165, 24, 13, Muted);
        var colors = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White, ManaColor.Green, ManaColor.Gold };
        var names = new[] { "红色", "蓝色", "白色", "绿色", "金色" };
        var tints = new[] { new Color32(207, 112, 97, 255), new Color32(103, 167, 195, 255),
            new Color32(220, 227, 216, 255), new Color32(110, 185, 151, 255), new Color32(223, 188, 102, 255) };
        var values = new Dictionary<ManaColor, TextMeshProUGUI>();
        for (int i = 0; i < colors.Length; i++)
        {
            var entry = Rect(bar, colors[i] + "Entry", 214 + 239 * i, 12, 217, 88);
            Line(entry, 0, 14, 1, 60, new Color(Gold.r, Gold.g, Gold.b, .16f));
            var gem = Image(entry, "Gem", 25, 31, 25, 25, tints[i]);
            gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Image(gem.transform, "Facet", 2, 2, 10, 21, new Color(1, 1, 1, .28f));
            Text(entry, "Label", names[i], 75, 10, 130, 25, 16, Muted);
            values[colors[i]] = Text(entry, "Value", "0", 73, 38, 130, 44, 34, Cream);
        }
        return values;
    }

    private (Button button, TextMeshProUGUI count) CreatePile(Transform parent, string name, string title, string subtitle, float x, bool discard)
    {
        var panel = Panel(parent, name, x, 0, 709, 239, new Color32(12, 27, 27, 190));
        var button = BindButton(panel.GetComponent<Image>(), false);
        var texture = Resources.Load<Texture2D>("UI/DeckManaTable/card_back_v1");
        if (texture == null) Debug.LogError("DeckManaTableView: card back is missing.");
        for (int i = discard ? 0 : 2; i >= 0; i--)
        {
            var card = Rect(panel, "CardBack" + i, 25 + i * 7, 14 + i * 3, 139, 210);
            var art = card.gameObject.AddComponent<RawImage>();
            art.texture = texture;
            art.color = discard ? new Color(.66f, .73f, .68f, .65f) : Color.white;
            art.raycastTarget = false;
            card.localRotation = Quaternion.Euler(0, 0, discard ? -5 : i * -3);
        }
        Text(panel, "Eyebrow", subtitle, 213, 29, 410, 23, 13, Gold);
        Text(panel, "Title", title, 210, 59, 390, 40, 30, Cream);
        var count = Text(panel, "Count", "0 张", 209, 105, 400, 55, 43, Cream);
        Text(panel, "Hint", "点击检视卡牌", 213, 179, 398, 30, 17, Muted);
        Text(panel, "Arrow", "›", 651, 95, 30, 45, 35, Gold);
        return (button, count);
    }

    private void BuildHand()
    {
        var area = Rect(transform, "HandArea", 414, 625, 1450, 400);
        Text(area, "Header", "当前手牌", 12, 0, 340, 43, 29, Cream);
        _handCount = Text(area, "Count", "00 / 手牌", 1180, 6, 260, 32, 19, Gold, TextAlignmentOptions.Right);
        Line(area, 13, 53, 1422, 1, new Color(Gold.r, Gold.g, Gold.b, .32f));
        _emptySlots = Rect(area, "EmptySlots", 0, 0, 1450, 400).gameObject;
        for (int i = 0; i < 5; i++)
        {
            var slot = Panel(_emptySlots.transform, "Slot" + i, 115 + i * 250, 74, 220, 306, new Color32(8, 20, 20, 125));
            Text(slot, "Number", "0" + (i + 1), 0, 102, 220, 57, 32, new Color(Gold.r, Gold.g, Gold.b, .45f), TextAlignmentOptions.Center);
            Text(slot, "Hint", "待抽取", 0, 167, 220, 31, 17, Muted, TextAlignmentOptions.Center);
        }
        _handRoot = Rect(area, "HandRoot", 0, 0, 1450, 320);
        _handRoot.anchorMin = new Vector2(0, 0);
        _handRoot.anchorMax = new Vector2(1, 0);
        _handRoot.pivot = new Vector2(.5f, 0);
        _handRoot.offsetMin = new Vector2(0, 18);
        _handRoot.offsetMax = new Vector2(0, 338);
        _handHint = Text(transform, "HandHint", "从左侧抽取手牌，开启本次旅程", 427, 1035, 1428, 25, 14, Muted, TextAlignmentOptions.Center);
    }

    private void LateUpdate()
    {
        if (_sourceLog != null && _sourceLog.text != _lastLog)
        {
            _lastLog = _sourceLog.text;
            _journal.text = FormatJournal(_lastLog);
            Canvas.ForceUpdateCanvases();
            _journalScroll.verticalNormalizedPosition = 0;
        }
        int count = _handRoot == null ? 0 : _handRoot.childCount;
        if (count == _lastHandCount) return;
        _lastHandCount = count;
        _emptySlots.SetActive(count == 0);
        _handCount.text = count.ToString("00") + " / 手牌";
        _handHint.text = count == 0 ? "从左侧抽取手牌，开启本次旅程" : "点击上方牌组或弃牌区，检视完整卡牌";
    }

    private static string FormatJournal(string raw)
    {
        // 控制器的完整诊断仍在 Console 和隐藏源文本中；桌面只显示行动记录。
        var result = new StringBuilder();
        foreach (var line in raw.Split('\n'))
        {
            var entry = line.Trim();
            if (entry.Length == 0 || entry.StartsWith("===") || entry.StartsWith("Running quick validation")
                || entry.StartsWith("Card effects loaded successfully") || entry.StartsWith("Mana ") || entry.StartsWith("Day "))
                continue;
            if (entry.StartsWith("All Part 1 systems initialized")) entry = "营地已就绪。\n选择一个行动，开始整备。\n";
            if (entry.StartsWith("随机抽出了新的手牌：")) entry = "本次手牌\n" + entry.Substring("随机抽出了新的手牌：".Length);
            if (entry.StartsWith("已完成弃牌并抽出")) continue;
            result.AppendLine(entry);
        }
        return result.ToString();
    }

    private TextMeshProUGUI CreateLog(Transform parent, out ScrollRect scroll)
    {
        var root = Rect(parent, "LogPanel", 25, 397, 256, 274);
        scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22;
        var viewport = Rect(root, "Viewport", 0, 0, 256, 274);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var log = Text(viewport, "LogText", "正在整备…", 0, 0, 250, 274, 17, Muted, TextAlignmentOptions.TopLeft);
        log.textWrappingMode = TextWrappingModes.Normal;
        log.lineSpacing = 8;
        log.paragraphSpacing = 8;
        var fitter = log.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = log.rectTransform;
        return log;
    }

    private Button Action(Transform parent, string name, string label, string index, float y, bool primary)
    {
        var rect = Panel(parent, name, 0, y, 258, 64, primary ? new Color32(67, 83, 66, 255) : new Color32(20, 42, 41, 255));
        var button = BindButton(rect.GetComponent<Image>(), primary);
        Text(rect, "Index", index, 17, 21, 32, 23, 13, Gold);
        Text(rect, "Text", label, 54, 16, 180, 32, 22, Cream);
        return button;
    }

    private static Button HiddenButton(Transform parent, string name) => Rect(parent, name, 0, 0, 0, 0).gameObject.AddComponent<Button>();

    private static Button BindButton(Image image, bool primary)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.3f, 1.14f, 1);
        colors.pressedColor = new Color(.73f, .82f, .75f, 1);
        colors.selectedColor = primary ? new Color(1.1f, 1.15f, 1, 1) : Color.white;
        colors.fadeDuration = .12f;
        button.colors = colors;
        return button;
    }

    public static void StyleOverlay(GameObject root)
    {
        root.GetComponent<Image>().color = new Color32(3, 10, 11, 237);
        var dialog = (RectTransform)root.transform.Find("Dialog");
        dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
        dialog.anchoredPosition = Vector2.zero;
        dialog.sizeDelta = new Vector2(1700, 900);
        foreach (var image in dialog.GetComponentsInChildren<Image>(true))
        {
            image.color = image.name == "CloseButton" ? new Color32(43, 67, 59, 255) : Ink;
            if (image.name == "Handle") image.color = Gold;
        }
        Border(dialog, 1700, 900);
        var header = dialog.Find("Header").GetComponent<HorizontalLayoutGroup>();
        header.childForceExpandWidth = false;
        header.transform.Find("Title").GetComponent<LayoutElement>().flexibleWidth = 1;
        foreach (var label in dialog.GetComponentsInChildren<TextMeshProUGUI>(true)) label.color = Cream;
        var closeLabel = header.transform.Find("CloseButton/Text").GetComponent<TextMeshProUGUI>();
        closeLabel.text = "返回桌面";
        closeLabel.fontSize = 19;
    }

    private TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var image = Image(parent, name, x, y, width, height, color);
        Border(image.rectTransform, width, height);
        return image.rectTransform;
    }

    private static void Border(Transform parent, float width, float height)
    {
        var tint = new Color(Gold.r, Gold.g, Gold.b, .32f);
        Line(parent, 0, 0, width, 1, tint);
        Line(parent, 0, height - 1, width, 1, tint);
        Line(parent, 0, 0, 1, height, tint);
        Line(parent, width - 1, 0, 1, height, tint);
    }

    private static void Line(Transform parent, float x, float y, float width, float height, Color color) => Image(parent, "Rule", x, y, width, height, color);

    private static Image Image(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
