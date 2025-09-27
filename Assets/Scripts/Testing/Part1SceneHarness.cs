using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MK.Logic.Core;

[DefaultExecutionOrder(-200)]
public class Part1SceneHarness : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private Part1TestManager manager;

    [Header("UI 配置")]
    [SerializeField] private string sceneTitle = "Part 1 Systems Test";
    [SerializeField] private bool showDeck = true;
    [SerializeField] private bool showMana = true;
    [SerializeField] private bool showCombat = true;
    [SerializeField] private bool showExploration = true;
    [SerializeField] private bool showRecruitment = true;
    [SerializeField] private bool showFullFlow = true;
    private const float LayoutMargin = 48f;
    private const float SectionSpacing = 24f;
    private const float ControlColumnWidth = 360f;
    private const float HandAreaHeight = 260f;
    private const float DeckViewerOverlayTopMargin = 60f;
    private const float DeckViewerOverlayHeight = 580f;

    private const float DeckViewerOverlayLeftOffset = LayoutMargin + ControlColumnWidth + SectionSpacing;



    [Header("字体配置")]
    [SerializeField] private TMP_FontAsset defaultFontAsset;
    private const string DynamicFontResourcePath = "Fonts/msyh TMP Dynamic";

    [Header("系统绑定")]
    [SerializeField] private bool bindDeckAndHand = true;
    [SerializeField] private bool bindMapContainer = false;
    [SerializeField] private bool bindCombatUI = false;
    [SerializeField] private bool bindRecruitmentUI = false;

    private readonly List<GameObject> temporaryObjects = new();

    private struct DeckZoneUIElements
    {
        public Button DeckButton;
        public Button DiscardButton;
        public TextMeshProUGUI DeckCountLabel;
        public TextMeshProUGUI DiscardCountLabel;
        public TextMeshProUGUI ViewerHeaderLabel;
        public TextMeshProUGUI ViewerBodyLabel;
    }

        private struct DeckViewerOverlayElements
    {
        public GameObject OverlayRoot;
        public Button BackgroundButton;
        public Button CloseButton;
        public TextMeshProUGUI TitleLabel;
        public TextMeshProUGUI SubtitleLabel;
        public RectTransform GridRoot;
    }

    private TMP_FontAsset resolvedFont;

    private void Awake()
    {
        EnsureManager();
        AutoBindSystems();
        BuildUI();
    }

    private void EnsureManager()
    {
        if (manager == null)
        {
            manager = GetComponent<Part1TestManager>();
        }

        if (manager == null)
        {
            manager = gameObject.AddComponent<Part1TestManager>();
        }
    }

    private void AutoBindSystems()
    {
        if (bindDeckAndHand)
        {
            manager.deckRuntime = manager.deckRuntime ?? FindFirstObjectByType<DeckRuntime>();
            manager.handManager = manager.handManager ?? FindFirstObjectByType<HandManager>();
            if (manager.handManager != null)
            {
                manager.handManager.ConfigureDrawOnStart(false);
            }
        }

        if (bindMapContainer)
        {
            manager.mapContainer = manager.mapContainer ?? EnsurePlaceholder("MapContainer");
        }

        if (bindCombatUI)
        {
            manager.combatUI = manager.combatUI ?? EnsurePlaceholder("CombatUI_Placeholder");
        }

        if (bindRecruitmentUI)
        {
            manager.recruitmentUI = manager.recruitmentUI ?? EnsurePlaceholder("RecruitmentUI_Placeholder");
        }
    }

    private GameObject EnsurePlaceholder(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        temporaryObjects.Add(go);
        return go;
    }


    private void BuildUI()
    {
        EnsureEventSystem();

        Canvas canvas = CreateCanvas();
        CleanupExistingLayout(canvas.transform);
        var layoutRoot = CreateLayoutRoot(canvas.transform);

        var controlColumn = CreateControlColumn(layoutRoot);
        var buttonContainer = CreateButtonContainer(controlColumn);
        var buttonOrder = new List<(string label, System.Action<Button> assign, bool visible)>
        {
            ("测试牌库系统", button => manager.testDeckButton = button, showDeck),
            ("测试魔力池", button => manager.testManaButton = button, showMana),
            ("测试战斗系统", button => manager.testCombatButton = button, showCombat),
            ("测试探索系统", button => manager.testExplorationButton = button, showExploration),
            ("测试招募系统", button => manager.testRecruitmentButton = button, showRecruitment),
            ("测试完整流程", button => manager.testFullFlowButton = button, showFullFlow)
        };

        foreach (var entry in buttonOrder)
        {
            var button = CreateButton(buttonContainer, entry.label, entry.visible);
            entry.assign(button);
        }

        var infoColumn = CreateInfoColumn(layoutRoot);
        CreateTitle(infoColumn);

        var manaDisplays = CreateManaDisplay(infoColumn);
        manager.ConfigureManaDisplay(manaDisplays);

        if (showDeck)
        {
            var deckZoneUi = CreateDeckZonesPanel(infoColumn);
            manager.ConfigureDeckZoneUI(
                deckZoneUi.DeckButton,
                deckZoneUi.DiscardButton,
                deckZoneUi.DeckCountLabel,
                deckZoneUi.DiscardCountLabel,
                deckZoneUi.ViewerHeaderLabel,
                deckZoneUi.ViewerBodyLabel);

            var overlayElements = CreateDeckViewerOverlay(canvas.transform);
            manager.ConfigureDeckViewerOverlay(
                overlayElements.OverlayRoot,
                overlayElements.BackgroundButton,
                overlayElements.CloseButton,
                overlayElements.TitleLabel,
                overlayElements.SubtitleLabel,
                overlayElements.GridRoot);
        }
        else
        {
            manager.ConfigureDeckZoneUI(null, null, null, null, null, null);
            manager.ConfigureDeckViewerOverlay(null, null, null, null, null, null);
        }

        manager.testLog = CreateLog(infoColumn, out var logScrollRect);
        manager.logScrollRect = logScrollRect;

        var handRoot = CreateHandArea(layoutRoot);
        BindHandRoot(handRoot);
    }


    private void CleanupExistingLayout(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return;
        }

        var obsoleteNames = new HashSet<string> { "Part1TestLayout", "Part1TestPanel", "TestPanel" };
        for (int i = canvasTransform.childCount - 1; i >= 0; i--)
        {
            var child = canvasTransform.GetChild(i);
            if (child == null || !obsoleteNames.Contains(child.name))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        temporaryObjects.Add(eventSystem);
    }

    private Canvas CreateCanvas()
    {
        var existing = FindFirstObjectByType<Canvas>();
        if (existing != null)
        {
            return existing;
        }

        var canvasGo = new GameObject("Part1TestCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();
        temporaryObjects.Add(canvasGo);
        return canvas;
    }

    private RectTransform CreateLayoutRoot(Transform parent)
    {
        var layoutRoot = new GameObject("Part1TestLayout");
        var rect = layoutRoot.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(LayoutMargin, LayoutMargin);
        rect.offsetMax = new Vector2(-LayoutMargin, -LayoutMargin);

        temporaryObjects.Add(layoutRoot);
        return rect;
    }


    private RectTransform CreateControlColumn(Transform parent)
    {
        var columnGo = new GameObject("ControlColumn");
        var rect = columnGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(0f, HandAreaHeight + SectionSpacing);
        rect.offsetMax = new Vector2(ControlColumnWidth, 0f);

        var background = columnGo.AddComponent<Image>();
        background.color = new Color(0.36f, 0.12f, 0.16f, 0.95f);

        var layout = columnGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 24, 24);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        temporaryObjects.Add(columnGo);
        return rect;
    }


    private RectTransform CreateInfoColumn(Transform parent)
    {
        var columnGo = new GameObject("InfoColumn");
        var rect = columnGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(ControlColumnWidth + SectionSpacing, HandAreaHeight + SectionSpacing);
        rect.offsetMax = new Vector2(0f, 0f);

        var background = columnGo.AddComponent<Image>();
        background.color = new Color(0.12f, 0.15f, 0.22f, 0.94f);

        var layout = columnGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        temporaryObjects.Add(columnGo);
        return rect;
    }
    private RectTransform CreateHandArea(Transform parent)
    {
        var handArea = new GameObject("HandArea");
        var rect = handArea.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, HandAreaHeight);

        var background = handArea.AddComponent<Image>();
        background.color = new Color(0.1f, 0.16f, 0.32f, 0.92f);

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(handArea.transform, false);
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0f, 1f);
        headerRect.offsetMin = new Vector2(24f, -44f);
        headerRect.offsetMax = new Vector2(-24f, -16f);

        var headerText = headerGo.AddComponent<TextMeshProUGUI>();
        headerText.text = "当前手牌";
        headerText.fontSize = 22f;
        headerText.color = new Color(0.85f, 0.92f, 1f);
        headerText.alignment = TextAlignmentOptions.Left;
        ApplyFont(headerText);

        var handRootGo = new GameObject("HandRoot");
        var handRect = handRootGo.AddComponent<RectTransform>();
        handRect.SetParent(handArea.transform, false);
        handRect.anchorMin = new Vector2(0f, 0f);
        handRect.anchorMax = new Vector2(1f, 1f);
        handRect.pivot = new Vector2(0.5f, 0f);
        handRect.offsetMin = new Vector2(24f, 24f);
        handRect.offsetMax = new Vector2(-24f, -72f);

        var guide = handRootGo.AddComponent<Image>();
        guide.color = new Color(0.12f, 0.2f, 0.4f, 0.2f);

        temporaryObjects.Add(handArea);
        return handRect;
    }


    private void CreateTitle(Transform parent)
    {
        var titleGo = new GameObject("Title");
        var rect = titleGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 56f);

        var layoutElement = titleGo.AddComponent<LayoutElement>();
        layoutElement.minHeight = 48f;
        layoutElement.preferredHeight = 56f;

        var text = titleGo.AddComponent<TextMeshProUGUI>();
        text.text = sceneTitle;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        ApplyFont(text);
    }


    private TextMeshProUGUI CreateLog(Transform parent, out ScrollRect scrollRect)
    {
        var logPanel = new GameObject("LogPanel");
        var panelRect = logPanel.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);

        var panelElement = logPanel.AddComponent<LayoutElement>();
        panelElement.minHeight = 280f;
        panelElement.flexibleHeight = 1f;

        var panelBackground = logPanel.AddComponent<Image>();
        panelBackground.color = new Color(0.32f, 0.27f, 0.12f, 0.92f);

        var layout = logPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(logPanel.transform, false);
        headerRect.sizeDelta = new Vector2(0f, 32f);

        var headerElement = headerGo.AddComponent<LayoutElement>();
        headerElement.minHeight = 36f;
        headerElement.preferredHeight = 40f;

        var headerText = headerGo.AddComponent<TextMeshProUGUI>();
        headerText.text = "操作日志";
        headerText.fontSize = 22f;
        headerText.color = new Color(0.95f, 0.92f, 0.75f);
        headerText.alignment = TextAlignmentOptions.Left;
        ApplyFont(headerText);

        var scrollGo = new GameObject("ScrollView");
        var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.SetParent(logPanel.transform, false);
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        var scrollElement = scrollGo.AddComponent<LayoutElement>();
        scrollElement.flexibleHeight = 1f;

        var scrollBackground = scrollGo.AddComponent<Image>();
        scrollBackground.color = new Color(0.2f, 0.18f, 0.08f, 0.85f);

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = new GameObject("Viewport");
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -8f);
        viewportGo.AddComponent<RectMask2D>();

        var textGo = new GameObject("LogText");
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.SetParent(viewportGo.transform, false);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.offsetMin = new Vector2(0f, 0f);
        textRect.offsetMax = new Vector2(0f, 0f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "正在初始化测试面板...\n";
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        ApplyFont(text);

        var fitter = textGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRect;
        scrollRect.content = textRect;

        var scrollbarGo = new GameObject("Scrollbar");
        var scrollbarRect = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRect.SetParent(scrollGo.transform, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-16f, 8f);
        scrollbarRect.offsetMax = new Vector2(-2f, -8f);

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var slidingArea = new GameObject("SlidingArea");
        var slidingRect = slidingArea.AddComponent<RectTransform>();
        slidingRect.SetParent(scrollbarRect, false);
        slidingRect.anchorMin = new Vector2(0f, 0f);
        slidingRect.anchorMax = new Vector2(1f, 1f);
        slidingRect.offsetMin = Vector2.zero;
        slidingRect.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle");
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.SetParent(slidingArea.transform, false);
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.95f, 0.82f, 0.35f, 0.9f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalNormalizedPosition = 1f;

        temporaryObjects.Add(logPanel);
        return text;
    }






        private DeckViewerOverlayElements CreateDeckViewerOverlay(Transform canvasTransform)
    {
        var overlayGo = new GameObject("DeckViewerOverlay");
        var overlayRect = overlayGo.AddComponent<RectTransform>();
        overlayRect.SetParent(canvasTransform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();
        overlayGo.SetActive(false);

        var overlayCanvasGroup = overlayGo.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.interactable = true;
        overlayCanvasGroup.blocksRaycasts = true;

        var overlayImage = overlayGo.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.65f);

        var backgroundButton = overlayGo.AddComponent<Button>();
        backgroundButton.transition = Selectable.Transition.None;

        var dialogGo = new GameObject("Dialog");
        var dialogRect = dialogGo.AddComponent<RectTransform>();
        dialogRect.SetParent(overlayGo.transform, false);
        dialogRect.anchorMin = new Vector2(0f, 1f);
        dialogRect.anchorMax = new Vector2(1f, 1f);
        dialogRect.pivot = new Vector2(0.5f, 1f);
        dialogRect.offsetMin = new Vector2(DeckViewerOverlayLeftOffset, -(DeckViewerOverlayTopMargin + DeckViewerOverlayHeight));
        dialogRect.offsetMax = new Vector2(-LayoutMargin, -DeckViewerOverlayTopMargin);
        dialogRect.sizeDelta = Vector2.zero;

        var dialogImage = dialogGo.AddComponent<Image>();
        dialogImage.color = new Color(0.16f, 0.19f, 0.3f, 0.94f);

        var dialogLayout = dialogGo.AddComponent<VerticalLayoutGroup>();
        dialogLayout.padding = new RectOffset(24, 24, 20, 24);
        dialogLayout.spacing = 12f;
        dialogLayout.childAlignment = TextAnchor.UpperLeft;
        dialogLayout.childControlWidth = true;
        dialogLayout.childForceExpandWidth = true;
        dialogLayout.childControlHeight = true;
        dialogLayout.childForceExpandHeight = true;

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(dialogGo.transform, false);
        headerRect.sizeDelta = new Vector2(0f, 60f);

        var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 18f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandHeight = false;

        var headerElement = headerGo.AddComponent<LayoutElement>();
        headerElement.minHeight = 48f;
        headerElement.preferredHeight = 52f;

        var titleGo = new GameObject("Title");
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.SetParent(headerGo.transform, false);

        var titleLayout = titleGo.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "牌堆预览";
        titleText.fontSize = 30f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleText);

        var closeButtonGo = new GameObject("CloseButton");
        var closeRect = closeButtonGo.AddComponent<RectTransform>();
        closeRect.SetParent(headerGo.transform, false);
        closeRect.sizeDelta = new Vector2(112f, 40f);

        var closeLayout = closeButtonGo.AddComponent<LayoutElement>();
        closeLayout.minWidth = 100f;
        closeLayout.preferredWidth = 112f;
        closeLayout.minHeight = 40f;
        closeLayout.preferredHeight = 40f;

        var closeImage = closeButtonGo.AddComponent<Image>();
        closeImage.color = new Color(0.85f, 0.32f, 0.36f, 0.95f);

        var closeButton = closeButtonGo.AddComponent<Button>();

        var closeTextGo = new GameObject("Text");
        var closeTextRect = closeTextGo.AddComponent<RectTransform>();
        closeTextRect.SetParent(closeButtonGo.transform, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;

        var closeLabel = closeTextGo.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "关闭";
        closeLabel.fontSize = 22f;
        closeLabel.color = Color.white;
        closeLabel.alignment = TextAlignmentOptions.Center;
        ApplyFont(closeLabel);

        var subtitleGo = new GameObject("Subtitle");
        var subtitleRect = subtitleGo.AddComponent<RectTransform>();
        subtitleRect.SetParent(dialogGo.transform, false);

        var subtitleElement = subtitleGo.AddComponent<LayoutElement>();
        subtitleElement.minHeight = 26f;
        subtitleElement.preferredHeight = 26f;

        var subtitleText = subtitleGo.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "共 0 张卡牌。";
        subtitleText.fontSize = 20f;
        subtitleText.color = new Color(0.9f, 0.92f, 0.98f);
        subtitleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(subtitleText);

        var scrollGo = new GameObject("ScrollView");
        var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.SetParent(dialogGo.transform, false);
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        var scrollElement = scrollGo.AddComponent<LayoutElement>();
        scrollElement.flexibleHeight = 1f;

        var scrollBackground = scrollGo.AddComponent<Image>();
        scrollBackground.color = new Color(0.09f, 0.11f, 0.2f, 0.92f);

        var scrollRectComponent = scrollGo.AddComponent<ScrollRect>();
        scrollRectComponent.horizontal = false;
        scrollRectComponent.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = new GameObject("Viewport");
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-22f, -8f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content");
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.SetParent(viewportGo.transform, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var gridLayout = contentGo.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(280f, 420f);
        gridLayout.spacing = new Vector2(18f, 18f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRectComponent.viewport = viewportRect;
        scrollRectComponent.content = contentRect;

        var scrollbarGo = new GameObject("Scrollbar");
        var scrollbarRect = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRect.SetParent(scrollGo.transform, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-12f, 8f);
        scrollbarRect.offsetMax = new Vector2(-2f, -8f);

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var slidingArea = new GameObject("SlidingArea");
        var slidingRect = slidingArea.AddComponent<RectTransform>();
        slidingRect.SetParent(scrollbarRect, false);
        slidingRect.anchorMin = new Vector2(0f, 0f);
        slidingRect.anchorMax = new Vector2(1f, 1f);
        slidingRect.offsetMin = Vector2.zero;
        slidingRect.offsetMax = Vector2.zero;

        var handleGo = new GameObject("Handle");
        var handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.SetParent(slidingRect, false);
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        var handleImage = handleGo.AddComponent<Image>();
        handleImage.color = new Color(0.78f, 0.66f, 0.95f, 0.92f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;

        scrollRectComponent.verticalScrollbar = scrollbar;
        scrollRectComponent.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRectComponent.verticalScrollbarSpacing = -6f;

        temporaryObjects.Add(overlayGo);

        return new DeckViewerOverlayElements
        {
            OverlayRoot = overlayGo,
            BackgroundButton = backgroundButton,
            CloseButton = closeButton,
            TitleLabel = titleText,
            SubtitleLabel = subtitleText,
            GridRoot = contentRect
        };
    }
    private DeckZoneUIElements CreateDeckZonesPanel(Transform parent)
    {
        var panelGo = new GameObject("DeckZonesPanel");
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);

        var panelElement = panelGo.AddComponent<LayoutElement>();
        panelElement.minHeight = 220f;
        panelElement.flexibleHeight = 0f;

        var panelBackground = panelGo.AddComponent<Image>();
        panelBackground.color = new Color(0.18f, 0.16f, 0.28f, 0.94f);

        var panelLayout = panelGo.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(20, 20, 20, 20);
        panelLayout.spacing = 16f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandHeight = true;

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(panelGo.transform, false);
        headerRect.sizeDelta = new Vector2(0f, 36f);

        var headerElement = headerGo.AddComponent<LayoutElement>();
        headerElement.minHeight = 34f;
        headerElement.preferredHeight = 40f;

        var headerText = headerGo.AddComponent<TextMeshProUGUI>();
        headerText.text = "牌组与弃牌区";
        headerText.fontSize = 22f;
        headerText.color = new Color(0.9f, 0.88f, 1f);
        headerText.alignment = TextAlignmentOptions.Left;
        ApplyFont(headerText);

        var zonesRow = new GameObject("ZonesRow");
        var zonesRect = zonesRow.AddComponent<RectTransform>();
        zonesRect.SetParent(panelGo.transform, false);
        zonesRect.sizeDelta = new Vector2(0f, 110f);

        var zonesElement = zonesRow.AddComponent<LayoutElement>();
        zonesElement.minHeight = 110f;
        zonesElement.preferredHeight = 110f;

        var zonesLayout = zonesRow.AddComponent<HorizontalLayoutGroup>();
        zonesLayout.spacing = 12f;
        zonesLayout.childAlignment = TextAnchor.MiddleCenter;
        zonesLayout.childControlWidth = true;
        zonesLayout.childForceExpandWidth = true;
        zonesLayout.childControlHeight = true;
        zonesLayout.childForceExpandHeight = false;

        var deckZone = CreateDeckZoneButton(zonesRow.transform, "DeckZone", "牌组", new Color(0.26f, 0.2f, 0.45f, 0.9f));
        var discardZone = CreateDeckZoneButton(zonesRow.transform, "DiscardZone", "弃牌区", new Color(0.38f, 0.22f, 0.22f, 0.9f));

        var viewerGo = new GameObject("Viewer");
        var viewerRect = viewerGo.AddComponent<RectTransform>();
        viewerRect.SetParent(panelGo.transform, false);

        var viewerElement = viewerGo.AddComponent<LayoutElement>();
        viewerElement.flexibleHeight = 1f;
        viewerElement.minHeight = 160f;

        var viewerBackground = viewerGo.AddComponent<Image>();
        viewerBackground.color = new Color(0.11f, 0.15f, 0.26f, 0.92f);

        var viewerLayout = viewerGo.AddComponent<VerticalLayoutGroup>();
        viewerLayout.padding = new RectOffset(16, 16, 16, 16);
        viewerLayout.spacing = 12f;
        viewerLayout.childAlignment = TextAnchor.UpperLeft;
        viewerLayout.childControlWidth = true;
        viewerLayout.childForceExpandWidth = true;
        viewerLayout.childControlHeight = true;
        viewerLayout.childForceExpandHeight = true;

        var viewerHeaderGo = new GameObject("ViewerHeader");
        var viewerHeaderRect = viewerHeaderGo.AddComponent<RectTransform>();
        viewerHeaderRect.SetParent(viewerGo.transform, false);
        viewerHeaderRect.sizeDelta = new Vector2(0f, 32f);

        var viewerHeaderElement = viewerHeaderGo.AddComponent<LayoutElement>();
        viewerHeaderElement.minHeight = 30f;
        viewerHeaderElement.preferredHeight = 32f;

        var viewerHeaderText = viewerHeaderGo.AddComponent<TextMeshProUGUI>();
        viewerHeaderText.text = "牌堆预览";
        viewerHeaderText.fontSize = 20f;
        viewerHeaderText.color = new Color(0.92f, 0.95f, 1f);
        viewerHeaderText.alignment = TextAlignmentOptions.Left;
        ApplyFont(viewerHeaderText);

        var scrollGo = new GameObject("ViewerScroll");
        var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.SetParent(viewerGo.transform, false);
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        var scrollElement = scrollGo.AddComponent<LayoutElement>();
        scrollElement.flexibleHeight = 1f;

        var scrollBackground = scrollGo.AddComponent<Image>();
        scrollBackground.color = new Color(0.08f, 0.1f, 0.18f, 0.9f);

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = new GameObject("Viewport");
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-22f, -8f);
        viewportGo.AddComponent<RectMask2D>();

        var bodyGo = new GameObject("ViewerBody");
        var bodyRect = bodyGo.AddComponent<RectTransform>();
        bodyRect.SetParent(viewportGo.transform, false);
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        var bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyText.text = "点击上方的牌组或弃牌区以查看详情。";
        bodyText.fontSize = 16f;
        bodyText.color = Color.white;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        ApplyFont(bodyText);

        var fitter = bodyGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRect;
        scrollRect.content = bodyRect;
        scrollRect.verticalNormalizedPosition = 1f;

        var scrollbarGo = new GameObject("Scrollbar");
        var scrollbarRect = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRect.SetParent(scrollGo.transform, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-12f, 8f);
        scrollbarRect.offsetMax = new Vector2(-2f, -8f);

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var slidingArea = new GameObject("SlidingArea");
        var slidingRect = slidingArea.AddComponent<RectTransform>();
        slidingRect.SetParent(scrollbarRect, false);
        slidingRect.anchorMin = new Vector2(0f, 0f);
        slidingRect.anchorMax = new Vector2(1f, 1f);
        slidingRect.offsetMin = Vector2.zero;
        slidingRect.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle");
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.SetParent(slidingRect, false);
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.78f, 0.66f, 0.95f, 0.92f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = -6f;

        temporaryObjects.Add(panelGo);

        return new DeckZoneUIElements
        {
            DeckButton = deckZone.button,
            DiscardButton = discardZone.button,
            DeckCountLabel = deckZone.countLabel,
            DiscardCountLabel = discardZone.countLabel,
            ViewerHeaderLabel = viewerHeaderText,
            ViewerBodyLabel = bodyText
        };
    }

    private (Button button, TextMeshProUGUI countLabel) CreateDeckZoneButton(Transform parent, string objectName, string label, Color backgroundColor)
    {
        var zoneGo = new GameObject(objectName);
        var rect = zoneGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 108f);

        var element = zoneGo.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minHeight = 108f;

        var background = zoneGo.AddComponent<Image>();
        background.color = backgroundColor;

        var button = zoneGo.AddComponent<Button>();

        var content = new GameObject("Content");
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.SetParent(zoneGo.transform, false);
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(12f, 12f);
        contentRect.offsetMax = new Vector2(-12f, -12f);

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var labelGo = new GameObject("Label");
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.SetParent(content.transform, false);

        var labelText = labelGo.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 18f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        ApplyFont(labelText);

        var countGo = new GameObject("Count");
        var countRect = countGo.AddComponent<RectTransform>();
        countRect.SetParent(content.transform, false);

        var countText = countGo.AddComponent<TextMeshProUGUI>();
        countText.text = "0 张";
        countText.fontSize = 28f;
        countText.color = Color.white;
        countText.alignment = TextAlignmentOptions.Center;
        countText.enableWordWrapping = false;
        ApplyFont(countText);

        return (button, countText);
    }


    private Dictionary<ManaColor, TextMeshProUGUI> CreateManaDisplay(Transform parent)
    {
        var containerGo = new GameObject("ManaDisplay");
        var rect = containerGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var element = containerGo.AddComponent<LayoutElement>();
        element.preferredHeight = 160f;
        element.flexibleHeight = 0f;

        var background = containerGo.AddComponent<Image>();
        background.color = new Color(0.12f, 0.28f, 0.18f, 0.92f);

        var layout = containerGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var titleGo = new GameObject("Header");
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.SetParent(containerGo.transform, false);
        titleRect.sizeDelta = new Vector2(0f, 32f);

        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "魔晶池概览";
        titleText.fontSize = 20f;
        titleText.color = new Color(0.9f, 1f, 0.9f);
        titleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleText);

        var valuesGo = new GameObject("Values");
        var valuesRect = valuesGo.AddComponent<RectTransform>();
        valuesRect.SetParent(containerGo.transform, false);

        var valuesLayout = valuesGo.AddComponent<HorizontalLayoutGroup>();
        valuesLayout.padding = new RectOffset(0, 0, 0, 0);
        valuesLayout.spacing = 12f;
        valuesLayout.childAlignment = TextAnchor.MiddleLeft;
        valuesLayout.childControlWidth = true;
        valuesLayout.childForceExpandWidth = true;
        valuesLayout.childControlHeight = true;
        valuesLayout.childForceExpandHeight = false;

        var labels = new Dictionary<ManaColor, TextMeshProUGUI>();
        var colorConfigs = new (ManaColor color, Color background, string label)[]
        {
            (ManaColor.Red,   new Color(0.45f, 0.15f, 0.15f, 0.85f), "红"),
            (ManaColor.Blue,  new Color(0.15f, 0.25f, 0.55f, 0.85f), "蓝"),
            (ManaColor.White, new Color(0.50f, 0.50f, 0.50f, 0.85f), "白"),
            (ManaColor.Green, new Color(0.15f, 0.45f, 0.22f, 0.85f), "绿"),
            (ManaColor.Gold,  new Color(0.55f, 0.42f, 0.12f, 0.85f), "金")
        };

        foreach (var config in colorConfigs)
        {
            var entryGo = new GameObject(config.color + "Entry");
            var entryRect = entryGo.AddComponent<RectTransform>();
            entryRect.SetParent(valuesGo.transform, false);
            entryRect.sizeDelta = new Vector2(0f, 96f);

            var entryElement = entryGo.AddComponent<LayoutElement>();
            entryElement.flexibleWidth = 1f;

            var entryBackground = entryGo.AddComponent<Image>();
            entryBackground.color = config.background;

            var entryLayout = entryGo.AddComponent<VerticalLayoutGroup>();
            entryLayout.padding = new RectOffset(8, 8, 8, 8);
            entryLayout.spacing = 6f;
            entryLayout.childAlignment = TextAnchor.MiddleCenter;
            entryLayout.childControlWidth = true;
            entryLayout.childForceExpandWidth = true;
            entryLayout.childControlHeight = true;
            entryLayout.childForceExpandHeight = false;

            var labelGo = new GameObject("Label");
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.SetParent(entryGo.transform, false);

            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = config.label + "色魔晶";
            labelText.fontSize = 16f;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            ApplyFont(labelText);

            var valueGo = new GameObject("Value");
            var valueRect = valueGo.AddComponent<RectTransform>();
            valueRect.SetParent(entryGo.transform, false);

            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.text = "0";
            valueText.fontSize = 28f;
            valueText.color = Color.white;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.enableWordWrapping = false;
            ApplyFont(valueText);

            labels[config.color] = valueText;
        }

        temporaryObjects.Add(containerGo);
        return labels;
    }

    private RectTransform CreateButtonContainer(Transform parent)
    {
        var containerGo = new GameObject("Buttons");
        var rect = containerGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var layout = containerGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        var element = containerGo.AddComponent<LayoutElement>();
        element.flexibleHeight = 1f;

        temporaryObjects.Add(containerGo);
        return rect;
    }

    private Button CreateButton(RectTransform container, string label, bool visible)
    {
        var buttonGo = new GameObject(label + "Button");
        var rect = buttonGo.AddComponent<RectTransform>();
        rect.SetParent(container, false);
        rect.sizeDelta = new Vector2(0f, 48f);

        var layoutElement = buttonGo.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 48f;
        layoutElement.minHeight = 44f;
        layoutElement.flexibleWidth = 1f;

        var image = buttonGo.AddComponent<Image>();
        image.color = visible ? new Color(0.25f, 0.25f, 0.25f, 0.95f) : new Color(0.18f, 0.18f, 0.18f, 0.6f);

        var button = buttonGo.AddComponent<Button>();
        button.interactable = visible;

        var textGo = new GameObject("Text");
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.SetParent(buttonGo.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = visible ? label : label + "（未启用）";
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        ApplyFont(text);

        return button;
    }
    private void BindHandRoot(RectTransform handRoot)
    {
        if (manager == null || manager.handManager == null || handRoot == null)
        {
            return;
        }

        var handManagerInstance = manager.handManager;
        if (handManagerInstance.handRoot == handRoot)
        {
            handManagerInstance.ConfigureDrawOnStart(false);
            return;
        }

        var previousRoot = handManagerInstance.handRoot;
        handManagerInstance.ConfigureDrawOnStart(false);
        handManagerInstance.handRoot = handRoot;

        if (previousRoot != null && previousRoot != handRoot)
        {
            var cardsToReparent = new List<Transform>();
            for (int i = 0; i < previousRoot.childCount; i++)
            {
                cardsToReparent.Add(previousRoot.GetChild(i));
            }

            foreach (var card in cardsToReparent)
            {
                if (card == null)
                {
                    continue;
                }

                card.SetParent(handRoot, false);
                if (card is RectTransform rect)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
                }
            }
        }

        handManagerInstance.LayoutCardsForEditor();
    }



    private TMP_FontAsset ResolveFont()
    {
        if (defaultFontAsset != null)
        {
            if (defaultFontAsset.name == "msyh SDF")
            {
                var dynamicFallback = Resources.Load<TMP_FontAsset>(DynamicFontResourcePath);
                if (dynamicFallback != null)
                {
                    defaultFontAsset = dynamicFallback;
                }
            }
            return defaultFontAsset;
        }

        if (resolvedFont != null)
        {
            return resolvedFont;
        }

        var dynamicFont = Resources.Load<TMP_FontAsset>(DynamicFontResourcePath);
        if (dynamicFont != null)
        {
            defaultFontAsset = dynamicFont;
            resolvedFont = defaultFontAsset;
            return resolvedFont;
        }

        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var font in allFonts)
        {
            if (font != null && font.name == "msyh SDF")
            {
                resolvedFont = font;
                return resolvedFont;
            }
        }

        resolvedFont = TMP_Settings.defaultFontAsset;
        return resolvedFont;
    }

    private void ApplyFont(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        var font = ResolveFont();
        if (font != null)
        {
            text.font = font;
        }
    }

    private void OnDestroy()
    {
        foreach (var temp in temporaryObjects)
        {
            if (temp != null)
            {
                Destroy(temp);
            }
        }

        temporaryObjects.Clear();
    }
}
