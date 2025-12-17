using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AgentASceneAutomationRig : MonoBehaviour
{
    [SerializeField] private RotateCube rotateCube;
    [SerializeField] private MeshRenderer cubeRenderer;

    private Button toggleRotationButton;
    private Text toggleRotationLabel;
    private Button randomizeColorButton;

    private bool isRotationEnabled = true;

    private void Awake()
    {
        Debug.Log("[AgentA Rig] Awake - preparing automation rig UI.");

        if (rotateCube == null)
        {
            rotateCube = UnityEngine.Object.FindFirstObjectByType<RotateCube>();
            Debug.Log($"[AgentA Rig] Located RotateCube: {(rotateCube != null ? rotateCube.name : "<null>")}");
        }

        if (cubeRenderer == null && rotateCube != null)
        {
            cubeRenderer = rotateCube.GetComponent<MeshRenderer>();
            Debug.Log($"[AgentA Rig] Cube renderer resolved: {(cubeRenderer != null ? cubeRenderer.name : "<null>")}");
        }

        EnsureEventSystem();
        CreateUi();
        UpdateToggleButtonLabel();

        var togglePath = "AutomationCanvas/Buttons/Btn_ToggleRotation";
        var randomizePath = "AutomationCanvas/Buttons/Btn_RandomizeColor";
        Debug.Log($"[AgentA Rig] Toggle button found immediately after creation: {GameObject.Find(togglePath) != null}");
        Debug.Log($"[AgentA Rig] Randomize button found immediately after creation: {GameObject.Find(randomizePath) != null}");
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    private void CreateUi()
    {
        var canvasGo = new GameObject("AutomationCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        var buttonsRoot = new GameObject("Buttons");
        buttonsRoot.transform.SetParent(canvasGo.transform, false);
        var buttonsRect = buttonsRoot.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.5f, 0f);
        buttonsRect.anchorMax = new Vector2(0.5f, 0f);
        buttonsRect.pivot = new Vector2(0.5f, 0f);
        buttonsRect.anchoredPosition = new Vector2(0f, 40f);

        (toggleRotationButton, toggleRotationLabel) = CreateButton(buttonsRoot.transform, "Btn_ToggleRotation", new Vector2(0f, 120f), "暂停旋转");
        toggleRotationButton.onClick.AddListener(ToggleRotation);

        (randomizeColorButton, _) = CreateButton(buttonsRoot.transform, "Btn_RandomizeColor", new Vector2(0f, 40f), "切换颜色");
        randomizeColorButton.onClick.AddListener(RandomizeCubeColor);

        Debug.Log("[AgentA Rig] UI buttons instantiated under AutomationCanvas.");
    }

    private (Button button, Text label) CreateButton(Transform parent, string name, Vector2 anchoredPosition, string label)
    {
        var buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(parent, false);

        var rect = buttonGo.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 60f);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;

        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.18f, 0.18f, 0.18f, 0.85f);

        var button = buttonGo.AddComponent<Button>();

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);

        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;

        return (button, text);
    }

    private void ToggleRotation()
    {
        if (rotateCube == null)
        {
            return;
        }

        isRotationEnabled = !isRotationEnabled;
        rotateCube.enabled = isRotationEnabled;
        UpdateToggleButtonLabel();
    }

    private void UpdateToggleButtonLabel()
    {
        if (toggleRotationLabel == null)
        {
            return;
        }

        toggleRotationLabel.text = isRotationEnabled ? "暂停旋转" : "恢复旋转";
    }

    private void RandomizeCubeColor()
    {
        if (cubeRenderer == null)
        {
            return;
        }

        var material = cubeRenderer.material;
        material.color = new Color(Random.value, Random.value, Random.value);
    }
}
