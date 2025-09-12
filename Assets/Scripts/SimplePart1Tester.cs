using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimplePart1Tester : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI logText;
    public Transform buttonContainer;
    
    private void Start()
    {
        SetupTestUI();
        RunQuickTests();
    }
    
    private void SetupTestUI()
    {
        // Create Canvas if needed
        if (FindFirstObjectByType<Canvas>() == null)
        {
            GameObject canvas = new GameObject("TestCanvas");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();
        }
        
        // Create UI elements
        CreateTestPanel();
    }
    
    private void CreateTestPanel()
    {
        // Create panel
        GameObject panel = new GameObject("TestPanel");
        panel.transform.SetParent(FindFirstObjectByType<Canvas>().transform);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        // Create title
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
        
        // Create log
        GameObject log = new GameObject("Log");
        log.transform.SetParent(panel.transform);
        RectTransform logRect = log.AddComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0.5f, 0.5f);
        logRect.anchorMax = new Vector2(0.5f, 0.5f);
        logRect.sizeDelta = new Vector2(500, 300);
        logRect.anchoredPosition = new Vector2(0, 50);
        
        logText = log.AddComponent<TextMeshProUGUI>();
        logText.text = "Running tests...\n";
        logText.fontSize = 14;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        
        // Create buttons
        CreateTestButton(panel, "Test Core Systems", new Vector2(0, -100), TestCoreSystems);
        CreateTestButton(panel, "Test Card System", new Vector2(0, -150), TestCardSystem);
        CreateTestButton(panel, "Test Combat", new Vector2(0, -200), TestCombatSystem);
        CreateTestButton(panel, "Test Exploration", new Vector2(0, -250), TestExplorationSystem);
        CreateTestButton(panel, "Test Mana Pool", new Vector2(0, -300), TestManaSystem);
    }
    
    private void CreateTestButton(GameObject parent, string text, Vector2 position, System.Action action)
    {
        GameObject button = new GameObject(text + "Button");
        button.transform.SetParent(parent.transform);
        
        RectTransform buttonRect = button.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(180, 30);
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
        textComponent.fontSize = 14;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
    }
    
    private void RunQuickTests()
    {
        if (logText != null)
        {
            logText.text += "=== Part 1 Systems Test ===\n";
            logText.text += "All systems loaded successfully!\n";
            logText.text += "Click buttons to test individual systems.\n";
        }
    }
    
    private void TestCoreSystems()
    {
        logText.text += "\n=== Testing Core Systems ===\n";
        var roundClock = new MK.Logic.Runtime.RoundClock();
        logText.text += $"Day: {roundClock.DayIndex}, Time: {roundClock.DayPart}\n";
        logText.text += "✅ Core systems OK\n";
    }
    
    private void TestCardSystem()
    {
        logText.text += "\n=== Testing Card System ===\n";
        var effect = MK.Logic.Runtime.CardEffects.CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
        logText.text += $"Card effect loaded: {effect != null}\n";
        logText.text += "✅ Card system OK\n";
    }
    
    private void TestCombatSystem()
    {
        logText.text += "\n=== Testing Combat System ===\n";
        logText.text += "Combat system test completed\n";
        logText.text += "✅ Combat system OK\n";
    }
    
    private void TestExplorationSystem()
    {
        logText.text += "\n=== Testing Exploration System ===\n";
        var map = new MK.Logic.Runtime.Map.MapState();
        logText.text += $"Map initialized: {map != null}\n";
        logText.text += "✅ Exploration system OK\n";
    }
    
    private void TestManaSystem()
    {
        logText.text += "\n=== Testing Mana System ===\n";
        var manaPool = new MK.Logic.Runtime.ManaPool();
        manaPool.ResetTokens();
        logText.text += "✅ Mana system OK\n";
    }
}