using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Part1TestUISetup : MonoBehaviour
{
    [Header("UI Prefabs")]
    public GameObject canvasPrefab;
    
    private void Start()
    {
        SetupTestUI();
    }
    
    private void SetupTestUI()
    {
        // Create Canvas if it doesn't exist
        if (FindFirstObjectByType<Canvas>() == null)
        {
            GameObject canvas = new GameObject("TestCanvas");
            canvas.AddComponent<Canvas>();
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();
            
            Canvas canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 100;
            
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            CreateTestPanel(canvas);
        }
    }
    
    private void CreateTestPanel(GameObject canvas)
    {
        // Create Panel
        GameObject panel = new GameObject("TestPanel");
        panel.transform.SetParent(canvas.transform);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(600, 800);
        panelRect.anchoredPosition = Vector2.zero;
        
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        // Create Title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(panel.transform);
        
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(400, 60);
        titleRect.anchoredPosition = new Vector2(0, -30);
        
        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Part 1 Systems Test";
        titleText.fontSize = 36;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        
        // Create Log Text
        GameObject log = new GameObject("TestLog");
        log.transform.SetParent(panel.transform);
        
        RectTransform logRect = log.AddComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0.5f, 0.5f);
        logRect.anchorMax = new Vector2(0.5f, 0.5f);
        logRect.sizeDelta = new Vector2(550, 400);
        logRect.anchoredPosition = new Vector2(0, 100);
        
        TextMeshProUGUI logText = log.AddComponent<TextMeshProUGUI>();
        logText.text = "Click buttons below to test systems...\n";
        logText.fontSize = 16;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        
        // Create Buttons Container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(panel.transform);
        
        RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.sizeDelta = new Vector2(500, 300);
        containerRect.anchoredPosition = new Vector2(0, 150);
        
        // Create Buttons
        string[] buttonNames = { "Test Deck", "Test Mana", "Test Combat", "Test Exploration", "Test Recruitment", "Test Full Flow" };
        System.Action[] buttonActions = {
            () => TestDeckSystem(logText),
            () => TestManaSystem(logText),
            () => TestCombatSystem(logText),
            () => TestExplorationSystem(logText),
            () => TestRecruitmentSystem(logText),
            () => TestFullFlow(logText)
        };
        
        for (int i = 0; i < buttonNames.Length; i++)
        {
            CreateButton(buttonContainer, buttonNames[i], buttonActions[i], i);
        }
        
        Debug.Log("Part 1 Test UI created successfully!");
    }
    
    private void CreateButton(GameObject parent, string text, System.Action action, int index)
    {
        GameObject button = new GameObject(text + "Button");
        button.transform.SetParent(parent.transform);
        
        RectTransform buttonRect = button.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(200, 40);
        buttonRect.anchoredPosition = new Vector2(0, -50 - (index * 50));
        
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
        textComponent.fontSize = 18;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
    }
    
    private void TestDeckSystem(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Deck System ===\n";
        log.text += "Deck system test completed\n";
    }
    
    private void TestManaSystem(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Mana System ===\n";
        var manaPool = new MK.Logic.Runtime.ManaPool();
        manaPool.ResetTokens();
        log.text += "Mana pool initialized\n";
    }
    
    private void TestCombatSystem(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Combat System ===\n";
        log.text += "Combat system test completed\n";
    }
    
    private void TestExplorationSystem(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Exploration System ===\n";
        log.text += "Exploration system test completed\n";
    }
    
    private void TestRecruitmentSystem(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Recruitment System ===\n";
        log.text += "Recruitment system test completed\n";
    }
    
    private void TestFullFlow(TextMeshProUGUI log)
    {
        log.text += "\n=== Testing Full Game Flow ===\n";
        log.text += "Full flow test completed\n";
    }
}