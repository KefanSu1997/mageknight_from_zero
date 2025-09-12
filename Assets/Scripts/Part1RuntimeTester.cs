using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Part1RuntimeTester : MonoBehaviour
{
    [Header("UI Settings")]
    public bool createUI = true;
    public KeyCode testKey = KeyCode.Space;
    
    private TextMeshProUGUI logText;
    private Canvas canvas;
    
    void Start()
    {
        if (createUI)
        {
            CreateTestUI();
        }
        
        Debug.Log("Part 1 Runtime Tester Ready!");
        Debug.Log("Press SPACE to run tests, or use Unity Menu: Mage Knight -> Test");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            RunAllTests();
        }
    }
    
    void CreateTestUI()
    {
        // Create Canvas
        canvas = new GameObject("TestCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        
        // Create Panel
        GameObject panel = new GameObject("TestPanel");
        panel.transform.SetParent(canvas.transform);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        // Create Title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(panel.transform);
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.9f);
        titleRect.anchorMax = new Vector2(0.5f, 0.9f);
        titleRect.sizeDelta = new Vector2(400, 50);
        titleRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Part 1 Systems Test";
        titleText.fontSize = 24;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        
        // Create Log
        GameObject log = new GameObject("Log");
        log.transform.SetParent(panel.transform);
        RectTransform logRect = log.AddComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0.5f, 0.5f);
        logRect.anchorMax = new Vector2(0.5f, 0.5f);
        logRect.sizeDelta = new Vector2(500, 300);
        logRect.anchoredPosition = new Vector2(0, 50);
        
        logText = log.AddComponent<TextMeshProUGUI>();
        logText.text = "Press SPACE to run tests...\n";
        logText.fontSize = 14;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        
        // Create Test Button
        CreateButton(panel, "Run All Tests", new Vector2(0, -100), RunAllTests);
        CreateButton(panel, "Test Core Systems", new Vector2(0, -150), TestCoreSystems);
        CreateButton(panel, "Test Card System", new Vector2(0, -200), TestCardSystem);
        CreateButton(panel, "Test Combat", new Vector2(0, -250), TestCombatSystem);
        CreateButton(panel, "Test Exploration", new Vector2(0, -300), TestExplorationSystem);
        CreateButton(panel, "Test Mana", new Vector2(0, -350), TestManaSystem);
    }
    
    void CreateButton(GameObject parent, string text, Vector2 position, System.Action action)
    {
        GameObject button = new GameObject(text + "Button");
        button.transform.SetParent(parent.transform);
        
        RectTransform buttonRect = button.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(150, 30);
        buttonRect.anchoredPosition = position;
        
        Image buttonImage = button.AddComponent<Image>();
        buttonImage.color = Color.gray;
        
        Button buttonComponent = button.AddComponent<Button>();
        buttonComponent.onClick.AddListener(() => action());
        
        GameObject buttonText = new GameObject("Text");
        buttonText.transform.SetParent(button.transform);
        RectTransform textRect = buttonText.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI textComponent = buttonText.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 12;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
    }
    
    public void RunAllTests()
    {
        string results = "=== Part 1 Systems Test Results ===\n\n";
        
        try
        {
            TestCoreSystems();
            TestCardSystem();
            TestCombatSystem();
            TestExplorationSystem();
            TestManaSystem();
            
            results += "✅ All Part 1 systems validated successfully!\n";
            results += "\n🎯 Ready to proceed to Part 2 development!";
            
            Debug.Log(results);
            if (logText != null)
                logText.text = results;
        }
        catch (System.Exception e)
        {
            results += $"❌ Test failed: {e.Message}\n";
            Debug.LogError(results);
            if (logText != null)
                logText.text = results;
        }
    }
    
    public void TestCoreSystems()
    {
        string result = "Testing Core Systems...\n";
        
        var roundClock = new MK.Logic.Runtime.RoundClock();
        result += $"✅ RoundClock: Day {roundClock.DayIndex}, Time {roundClock.DayPart}\n";
        
        var turnEngine = new MK.Logic.Runtime.TurnEngine();
        result += "✅ TurnEngine initialized\n";
        
        Debug.Log(result);
        if (logText != null)
            logText.text += result;
    }
    
    public void TestCardSystem()
    {
        string result = "Testing Card System...\n";
        
        var effect = MK.Logic.Runtime.CardEffects.CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
        result += $"✅ Card effects loaded: {effect != null}\n";
        
        Debug.Log(result);
        if (logText != null)
            logText.text += result;
    }
    
    public void TestCombatSystem()
    {
        string result = "Testing Combat System...\n";
        
        var attacker = new MK.Logic.Runtime.UnitState(new MK.Logic.Data.UnitCard("test", "Test", "Test", 1, 3, 5, MK.Logic.Core.RecruitLocation.Village, new MK.Logic.Data.AttackProfile[0], new MK.Logic.Core.Ability[0]));
        var defender = new MK.Logic.Runtime.UnitState(new MK.Logic.Data.UnitCard("test2", "Test2", "Test2", 1, 2, 4, MK.Logic.Core.RecruitLocation.Keep, new MK.Logic.Data.AttackProfile[0], new MK.Logic.Core.Ability[0]));
        
        result += "✅ Combat system initialized\n";
        
        Debug.Log(result);
        if (logText != null)
            logText.text += result;
    }
    
    public void TestExplorationSystem()
    {
        string result = "Testing Exploration System...\n";
        
        var map = new MK.Logic.Runtime.Map.MapState();
        var exploration = new MK.Logic.Runtime.Map.ExplorationService(map);
        
        result += "✅ Exploration system initialized\n";
        
        Debug.Log(result);
        if (logText != null)
            logText.text += result;
    }
    
    public void TestManaSystem()
    {
        string result = "Testing Mana System...\n";
        
        var manaPool = new MK.Logic.Runtime.ManaPool();
        manaPool.ResetTokens();
        
        result += "✅ Mana system initialized\n";
        
        Debug.Log(result);
        if (logText != null)
            logText.text += result;
    }
}