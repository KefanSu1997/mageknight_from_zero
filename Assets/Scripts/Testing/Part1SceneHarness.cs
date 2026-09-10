
// Assets\Scripts\Testing\Part1SceneHarness.cs
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
    private const float DeckViewerOverlayTopMargin = 40f;
    private const float DeckViewerOverlayHeight = 720f;
    private const float DeckViewerOverlayHorizontalInset = 40f;



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
        public ScrollRect ScrollRect;
    }

    private struct CombatSideElements
    {
        public Image BannerImage;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Stats;
        public TextMeshProUGUI Abilities;
    }

    private struct CombatPanelElements
    {
        public TextMeshProUGUI ScenarioTitle;
        public TextMeshProUGUI ScenarioDescription;
        public CombatSideElements HeroSide;
        public CombatSideElements EnemySide;
        public TextMeshProUGUI OutcomeLabel;
        public Button NextScenarioButton;
        public Button ResolveButton;
    }

    private struct ExplorationUIElements
    {
        public RectTransform PanelRoot;
        public RectTransform MapRoot;
        public TextMeshProUGUI SummaryLabel;
        public TextMeshProUGUI DetailLabel;
        public TextMeshProUGUI HintLabel;
    }

    private struct RecruitmentUIElements
    {
        public RectTransform PanelRoot;
        public RectTransform AvailableListRoot;
        public RectTransform RecruitedListRoot;
        public TextMeshProUGUI SummaryLabel;
        public TextMeshProUGUI LocationLabel;
        public TextMeshProUGUI ResultLabel;
    }

    private TMP_FontAsset resolvedFont;

    private static int GetMaxOverlaySortingOrder()
    {
        int max = 0;
        // true 表示包含未激活对象，避免漏掉
        var canvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                max = Mathf.Max(max, c.sortingOrder);
        }
        return max;
    }

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

        bool includeHandArea = bindDeckAndHand && showDeck;
        float reservedHandHeight = includeHandArea ? HandAreaHeight : 0f;

        var controlColumn = CreateControlColumn(layoutRoot, reservedHandHeight);
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

        var infoColumn = CreateInfoColumn(layoutRoot, reservedHandHeight);
        CreateTitle(infoColumn);

        Dictionary<ManaColor, TextMeshProUGUI> manaDisplays = null;
        if (showMana)
        {
            manaDisplays = CreateManaDisplay(infoColumn);
        }
        manager.ConfigureManaDisplay(manaDisplays);

        GameObject overlayRoot = null;

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
                overlayElements.GridRoot,
                overlayElements.ScrollRect);
            overlayRoot = overlayElements.OverlayRoot;
#if UNITY_EDITOR && SMOKE_TEST
            SmokeTest(overlayElements.GridRoot);
#endif
        }
        else
        {
            manager.ConfigureDeckZoneUI(null, null, null, null, null, null);
            manager.ConfigureDeckViewerOverlay(null, null, null, null, null, null, null);
        }

        if (showExploration)
        {
            var explorationUi = CreateExplorationPanel(infoColumn);
            manager.ConfigureExplorationUI(
                explorationUi.MapRoot,
                explorationUi.SummaryLabel,
                explorationUi.DetailLabel,
                explorationUi.HintLabel);
        }
        else
        {
            manager.ConfigureExplorationUI(null, null, null, null);
        }

        if (showRecruitment)
        {
            var recruitmentUi = CreateRecruitmentPanel(infoColumn);
            manager.ConfigureRecruitmentUI(
                recruitmentUi.AvailableListRoot,
                recruitmentUi.RecruitedListRoot,
                recruitmentUi.SummaryLabel,
                recruitmentUi.LocationLabel,
                recruitmentUi.ResultLabel);
        }
        else
        {
            manager.ConfigureRecruitmentUI(null, null, null, null, null);
        }

        if (showCombat)
        {
            var combatPanel = CreateCombatPanel(infoColumn);
            manager.ConfigureCombatPanel(new Part1TestManager.CombatPanelBinding
            {
                ScenarioTitle = combatPanel.ScenarioTitle,
                ScenarioDescription = combatPanel.ScenarioDescription,
                HeroTitle = combatPanel.HeroSide.Title,
                HeroStats = combatPanel.HeroSide.Stats,
                HeroAbilities = combatPanel.HeroSide.Abilities,
                EnemyTitle = combatPanel.EnemySide.Title,
                EnemyStats = combatPanel.EnemySide.Stats,
                EnemyAbilities = combatPanel.EnemySide.Abilities,
                OutcomeLabel = combatPanel.OutcomeLabel,
                NextScenarioButton = combatPanel.NextScenarioButton,
                ResolveButton = combatPanel.ResolveButton
            });
        }
        else
        {
            manager.ConfigureCombatPanel(default);
        }

        manager.testLog = CreateLog(infoColumn, out var logScrollRect);
        manager.logScrollRect = logScrollRect;

        RectTransform handRoot = null;
        if (includeHandArea)
        {
            handRoot = CreateHandArea(layoutRoot);
        }
        BindHandRoot(handRoot);

        if (overlayRoot != null)
        {
            overlayRoot.transform.SetAsLastSibling();
        }
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
        // 始终创建专用浮窗 Canvas，避免复用场景中带有不同渲染模式的 Canvas 导致遮挡
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvas.pixelPerfect = false;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

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


    private RectTransform CreateControlColumn(Transform parent, float reservedHandHeight)
    {
        var columnGo = new GameObject("ControlColumn");
        var rect = columnGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        float bottomOffset = reservedHandHeight > 0f ? reservedHandHeight + SectionSpacing : 0f;
        rect.offsetMin = new Vector2(0f, bottomOffset);
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


    private RectTransform CreateInfoColumn(Transform parent, float reservedHandHeight)
    {
        var columnGo = new GameObject("InfoColumn");
        var rect = columnGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        float bottomOffset = reservedHandHeight > 0f ? reservedHandHeight + SectionSpacing : 0f;
        rect.offsetMin = new Vector2(ControlColumnWidth + SectionSpacing, bottomOffset);
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

        var modalCanvas = overlayGo.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        // 动态压过场景中所有 ScreenSpaceOverlay 画布
        modalCanvas.sortingOrder = GetMaxOverlaySortingOrder() + 100; 
        overlayGo.AddComponent<GraphicRaycaster>();

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
        dialogRect.offsetMin = new Vector2(DeckViewerOverlayHorizontalInset, -(DeckViewerOverlayTopMargin + DeckViewerOverlayHeight));
        dialogRect.offsetMax = new Vector2(-DeckViewerOverlayHorizontalInset, -DeckViewerOverlayTopMargin);
        // dialogRect.sizeDelta = Vector2.zero;

        var dialogImage = dialogGo.AddComponent<Image>();
        dialogImage.color = new Color(0.16f, 0.19f, 0.3f, 0.94f);

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(dialogGo.transform, false);
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        const float headerHeight = 64f;
        headerRect.offsetMin = new Vector2(24f, -headerHeight);
        headerRect.offsetMax = new Vector2(-24f, 0f);

        var headerBackground = headerGo.AddComponent<Image>();
        headerBackground.color = new Color(0.08f, 0.15f, 0.32f, 0.95f);

        var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 18f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.padding = new RectOffset(24, 24, 0, 0);

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
        closeRect.sizeDelta = new Vector2(140f, 44f);

        var closeLayout = closeButtonGo.AddComponent<LayoutElement>();
        closeLayout.minWidth = 120f;
        closeLayout.preferredWidth = 140f;
        closeLayout.minHeight = 44f;
        closeLayout.preferredHeight = 44f;

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

        var bodyGo = new GameObject("Body");
        var bodyRect = bodyGo.AddComponent<RectTransform>();
        bodyRect.SetParent(dialogGo.transform, false);
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        const float bodyPadding = 24f;
        const float bodyTopOffset = headerHeight + 16f;
        bodyRect.offsetMin = new Vector2(bodyPadding, bodyPadding);
        bodyRect.offsetMax = new Vector2(-bodyPadding, -bodyTopOffset);

        var bodyBackground = bodyGo.AddComponent<Image>();
        bodyBackground.color = new Color(0.11f, 0.15f, 0.26f, 0.92f);

        var subtitleGo = new GameObject("Subtitle");
        var subtitleRect = subtitleGo.AddComponent<RectTransform>();
        subtitleRect.SetParent(bodyRect, false);
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0f, 1f);
        const float subtitleHeight = 36f;
        subtitleRect.offsetMin = new Vector2(16f, -subtitleHeight);
        subtitleRect.offsetMax = new Vector2(-16f, 0f);

        var subtitleText = subtitleGo.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "共 0 张卡牌。";
        subtitleText.fontSize = 20f;
        subtitleText.color = new Color(0.9f, 0.92f, 0.98f);
        subtitleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(subtitleText);

        var scrollGo = new GameObject("ScrollView");
        var scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.SetParent(bodyRect, false);
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(16f, 16f);
        scrollRectTransform.offsetMax = new Vector2(-16f, -(subtitleHeight + 24f));

        var scrollBackground = scrollGo.AddComponent<Image>();
        scrollBackground.color = new Color(0.08f, 0.1f, 0.18f, 0.9f);

        var scrollRectComponent = scrollGo.AddComponent<ScrollRect>();
        scrollRectComponent.horizontal = false;
        scrollRectComponent.movementType = ScrollRect.MovementType.Clamped;

        var viewportGo = new GameObject("Viewport");
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.SetParent(scrollGo.transform, false);
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-28f, -10f);
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
        gridLayout.cellSize = new Vector2(320f, 480f);
        gridLayout.spacing = new Vector2(24f, 32f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 3;
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
        scrollbarRect.offsetMin = new Vector2(-16f, 10f);
        scrollbarRect.offsetMax = new Vector2(-4f, -10f);

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
        scrollRectComponent.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRectComponent.verticalScrollbarSpacing = -8f;
        scrollRectComponent.verticalNormalizedPosition = 1f;

        temporaryObjects.Add(overlayGo);

        return new DeckViewerOverlayElements
        {
            OverlayRoot = overlayGo,
            BackgroundButton = backgroundButton,
            CloseButton = closeButton,
            TitleLabel = titleText,
            SubtitleLabel = subtitleText,
            GridRoot = contentRect,
            ScrollRect = scrollRectComponent
        };
    }

#if UNITY_EDITOR
    private void SmokeTest(RectTransform gridRoot)
    {
        if (gridRoot == null)
        {
            Debug.LogWarning("[DeckOverlay] SmokeTest skipped: gridRoot missing");
            return;
        }

        const int dummyCount = 8;
        for (int i = 0; i < dummyCount; i++)
        {
            var dummy = new GameObject($"DummyCard_{i}");
            var rectTransform = dummy.AddComponent<RectTransform>();
            rectTransform.SetParent(gridRoot, false);

            var image = dummy.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(0.2f, 0.6f, 0.9f, 1f);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRoot);
        var scrollRect = gridRoot.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
#endif
    private DeckZoneUIElements CreateDeckZonesPanel(Transform parent)
    {
        var panelGo = new GameObject("DeckZonesPanel");
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);

        var panelElement = panelGo.AddComponent<LayoutElement>();
        panelElement.minHeight = 280f;
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
        zonesRect.sizeDelta = new Vector2(0f, 140f);

        var zonesElement = zonesRow.AddComponent<LayoutElement>();
        zonesElement.minHeight = 140f;
        zonesElement.preferredHeight = 140f;

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
        viewerElement.minHeight = 240f;

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

    private CombatPanelElements CreateCombatPanel(Transform parent)
    {
        var panelGo = new GameObject("CombatPanel");
        var rect = panelGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var element = panelGo.AddComponent<LayoutElement>();
        element.minHeight = 340f;
        element.flexibleHeight = 0f;

        var background = panelGo.AddComponent<Image>();
        background.color = new Color(0.14f, 0.18f, 0.32f, 0.92f);

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        temporaryObjects.Add(panelGo);

        var headerGo = new GameObject("Header");
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.SetParent(panelGo.transform, false);

        var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandHeight = false;

        var headerElement = headerGo.AddComponent<LayoutElement>();
        headerElement.minHeight = 48f;

        var titleGo = new GameObject("ScenarioTitle");
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.SetParent(headerGo.transform, false);

        var titleElement = titleGo.AddComponent<LayoutElement>();
        titleElement.flexibleWidth = 1f;

        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "战斗演示";
        titleText.fontSize = 26f;
        titleText.color = new Color(0.95f, 0.97f, 1f);
        titleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleText);

        var nextButtonGo = new GameObject("Btn_NextScenario");
        var nextRect = nextButtonGo.AddComponent<RectTransform>();
        nextRect.SetParent(headerGo.transform, false);
        nextRect.sizeDelta = new Vector2(180f, 44f);

        var nextImage = nextButtonGo.AddComponent<Image>();
        nextImage.color = new Color(0.36f, 0.24f, 0.56f, 0.95f);

        var nextLayout = nextButtonGo.AddComponent<LayoutElement>();
        nextLayout.preferredWidth = 180f;
        nextLayout.minHeight = 44f;

        var nextButton = nextButtonGo.AddComponent<Button>();

        var nextLabelGo = new GameObject("Text");
        var nextLabelRect = nextLabelGo.AddComponent<RectTransform>();
        nextLabelRect.SetParent(nextButtonGo.transform, false);
        nextLabelRect.anchorMin = Vector2.zero;
        nextLabelRect.anchorMax = Vector2.one;
        nextLabelRect.offsetMin = new Vector2(12f, 0f);
        nextLabelRect.offsetMax = new Vector2(-12f, 0f);

        var nextLabel = nextLabelGo.AddComponent<TextMeshProUGUI>();
        nextLabel.text = "切换对阵";
        nextLabel.fontSize = 20f;
        nextLabel.color = Color.white;
        nextLabel.alignment = TextAlignmentOptions.Center;
        ApplyFont(nextLabel);

        var descriptionGo = new GameObject("ScenarioDescription");
        var descriptionRect = descriptionGo.AddComponent<RectTransform>();
        descriptionRect.SetParent(panelGo.transform, false);

        var descriptionElement = descriptionGo.AddComponent<LayoutElement>();
        descriptionElement.minHeight = 48f;

        var descriptionText = descriptionGo.AddComponent<TextMeshProUGUI>();
        descriptionText.text = "展示 BattleResolver 的基础流程。";
        descriptionText.fontSize = 18f;
        descriptionText.color = new Color(0.86f, 0.9f, 1f);
        descriptionText.alignment = TextAlignmentOptions.Left;
        descriptionText.enableWordWrapping = true;
        ApplyFont(descriptionText);

        var sidesGo = new GameObject("SidesRow");
        var sidesRect = sidesGo.AddComponent<RectTransform>();
        sidesRect.SetParent(panelGo.transform, false);

        var sidesLayout = sidesGo.AddComponent<HorizontalLayoutGroup>();
        sidesLayout.spacing = 18f;
        sidesLayout.childAlignment = TextAnchor.UpperLeft;
        sidesLayout.childControlWidth = true;
        sidesLayout.childForceExpandWidth = true;
        sidesLayout.childControlHeight = true;
        sidesLayout.childForceExpandHeight = false;

        var sidesElement = sidesGo.AddComponent<LayoutElement>();
        sidesElement.minHeight = 220f;

        var heroElements = CreateCombatSideCard(sidesGo.transform, "HeroCard", new Color(0.18f, 0.28f, 0.48f, 0.92f));
        var enemyElements = CreateCombatSideCard(sidesGo.transform, "EnemyCard", new Color(0.38f, 0.16f, 0.2f, 0.92f));

        var footerGo = new GameObject("Footer");
        var footerRect = footerGo.AddComponent<RectTransform>();
        footerRect.SetParent(panelGo.transform, false);

        var footerLayout = footerGo.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 12f;
        footerLayout.childAlignment = TextAnchor.MiddleLeft;
        footerLayout.childControlWidth = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandHeight = false;

        var footerElement = footerGo.AddComponent<LayoutElement>();
        footerElement.minHeight = 52f;

        var outcomeGo = new GameObject("OutcomeLabel");
        var outcomeRect = outcomeGo.AddComponent<RectTransform>();
        outcomeRect.SetParent(footerGo.transform, false);

        var outcomeElement = outcomeGo.AddComponent<LayoutElement>();
        outcomeElement.flexibleWidth = 1f;

        var outcomeText = outcomeGo.AddComponent<TextMeshProUGUI>();
        outcomeText.text = "等待战斗结算...";
        outcomeText.fontSize = 20f;
        outcomeText.color = new Color(0.86f, 0.9f, 1f);
        outcomeText.alignment = TextAlignmentOptions.Left;
        ApplyFont(outcomeText);

        var resolveButtonGo = new GameObject("Btn_Resolve");
        var resolveRect = resolveButtonGo.AddComponent<RectTransform>();
        resolveRect.SetParent(footerGo.transform, false);
        resolveRect.sizeDelta = new Vector2(180f, 44f);

        var resolveImage = resolveButtonGo.AddComponent<Image>();
        resolveImage.color = new Color(0.28f, 0.42f, 0.2f, 0.95f);

        var resolveLayout = resolveButtonGo.AddComponent<LayoutElement>();
        resolveLayout.preferredWidth = 180f;
        resolveLayout.minHeight = 44f;

        var resolveButton = resolveButtonGo.AddComponent<Button>();

        var resolveLabelGo = new GameObject("Text");
        var resolveLabelRect = resolveLabelGo.AddComponent<RectTransform>();
        resolveLabelRect.SetParent(resolveButtonGo.transform, false);
        resolveLabelRect.anchorMin = Vector2.zero;
        resolveLabelRect.anchorMax = Vector2.one;
        resolveLabelRect.offsetMin = new Vector2(12f, 0f);
        resolveLabelRect.offsetMax = new Vector2(-12f, 0f);

        var resolveLabel = resolveLabelGo.AddComponent<TextMeshProUGUI>();
        resolveLabel.text = "立即结算";
        resolveLabel.fontSize = 20f;
        resolveLabel.color = Color.white;
        resolveLabel.alignment = TextAlignmentOptions.Center;
        ApplyFont(resolveLabel);

        return new CombatPanelElements
        {
            ScenarioTitle = titleText,
            ScenarioDescription = descriptionText,
            HeroSide = heroElements,
            EnemySide = enemyElements,
            OutcomeLabel = outcomeText,
            NextScenarioButton = nextButton,
            ResolveButton = resolveButton
        };
    }

    private CombatSideElements CreateCombatSideCard(Transform parent, string objectName, Color backgroundColor)
    {
        var cardGo = new GameObject(objectName);
        var rect = cardGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var element = cardGo.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minWidth = 0f;
        element.minHeight = 220f;

        var background = cardGo.AddComponent<Image>();
        background.color = backgroundColor;

        var layout = cardGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var titleGo = new GameObject("Title");
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.SetParent(cardGo.transform, false);

        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = objectName == "HeroCard" ? "我方部队" : "敌方单位";
        titleText.fontSize = 22f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleText);

        var statsGo = new GameObject("Stats");
        var statsRect = statsGo.AddComponent<RectTransform>();
        statsRect.SetParent(cardGo.transform, false);

        var statsText = statsGo.AddComponent<TextMeshProUGUI>();
        statsText.text = "--";
        statsText.fontSize = 18f;
        statsText.color = new Color(0.92f, 0.95f, 1f);
        statsText.alignment = TextAlignmentOptions.Left;
        statsText.enableWordWrapping = true;
        ApplyFont(statsText);

        var abilitiesGo = new GameObject("Abilities");
        var abilitiesRect = abilitiesGo.AddComponent<RectTransform>();
        abilitiesRect.SetParent(cardGo.transform, false);

        var abilitiesText = abilitiesGo.AddComponent<TextMeshProUGUI>();
        abilitiesText.text = "能力：--";
        abilitiesText.fontSize = 16f;
        abilitiesText.color = new Color(0.82f, 0.88f, 1f);
        abilitiesText.alignment = TextAlignmentOptions.Left;
        abilitiesText.enableWordWrapping = true;
        ApplyFont(abilitiesText);

        temporaryObjects.Add(cardGo);

        return new CombatSideElements
        {
            BannerImage = background,
            Title = titleText,
            Stats = statsText,
            Abilities = abilitiesText
        };
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

    private ExplorationUIElements CreateExplorationPanel(Transform parent)
    {
        var panelGo = new GameObject("ExplorationPanel");
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);

        var panelElement = panelGo.AddComponent<LayoutElement>();
        panelElement.minHeight = 320f;
        panelElement.flexibleHeight = 0f;

        var panelBackground = panelGo.AddComponent<Image>();
        panelBackground.color = new Color(0.16f, 0.24f, 0.33f, 0.92f);

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var header = CreateSectionLabel(panelGo.transform, "ExplorationHeader", "探索与地图", 22f, new Color(0.92f, 0.97f, 1f), TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;

        var summary = CreateSectionLabel(panelGo.transform, "ExplorationSummary", "等待初始化……", 18f, new Color(0.86f, 0.92f, 1f), TextAlignmentOptions.Left);

        var mapContainer = new GameObject("MapRoot");
        var mapRect = mapContainer.AddComponent<RectTransform>();
        mapRect.SetParent(panelGo.transform, false);
        mapRect.anchorMin = new Vector2(0f, 0f);
        mapRect.anchorMax = new Vector2(1f, 0f);
        mapRect.pivot = new Vector2(0.5f, 0f);
        mapRect.sizeDelta = new Vector2(0f, 240f);

        var mapElement = mapContainer.AddComponent<LayoutElement>();
        mapElement.minHeight = 220f;
        mapElement.preferredHeight = 240f;

        var mapBackground = mapContainer.AddComponent<Image>();
        mapBackground.color = new Color(0.09f, 0.13f, 0.2f, 0.9f);

        var detail = CreateSectionLabel(panelGo.transform, "ExplorationDetail", "点击“测试探索系统”按钮会依次探索英雄周围的六边形。", 17f, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Left);

        var hint = CreateSectionLabel(panelGo.transform, "ExplorationHint", "下一目标：待准备", 16f, new Color(0.8f, 0.87f, 1f), TextAlignmentOptions.Left);
        hint.fontStyle = FontStyles.Italic;

        temporaryObjects.Add(panelGo);

        return new ExplorationUIElements
        {
            PanelRoot = panelRect,
            MapRoot = mapRect,
            SummaryLabel = summary,
            DetailLabel = detail,
            HintLabel = hint
        };
    }

    private RecruitmentUIElements CreateRecruitmentPanel(Transform parent)
    {
        var panelGo = new GameObject("RecruitmentPanel");
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.SetParent(parent, false);

        var panelElement = panelGo.AddComponent<LayoutElement>();
        panelElement.minHeight = 320f;
        panelElement.flexibleHeight = 0f;

        var panelBackground = panelGo.AddComponent<Image>();
        panelBackground.color = new Color(0.27f, 0.2f, 0.26f, 0.92f);

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var header = CreateSectionLabel(panelGo.transform, "RecruitmentHeader", "招募面板", 22f, new Color(0.97f, 0.94f, 0.86f), TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;

        var location = CreateSectionLabel(panelGo.transform, "RecruitmentLocation", "当前地点：等待探索", 18f, new Color(0.94f, 0.88f, 0.94f), TextAlignmentOptions.Left);
        var summary = CreateSectionLabel(panelGo.transform, "RecruitmentSummary", "队伍尚未招募任何单位。", 17f, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.Left);

        var listsRow = new GameObject("RecruitmentLists");
        var listsRect = listsRow.AddComponent<RectTransform>();
        listsRect.SetParent(panelGo.transform, false);

        var listsElement = listsRow.AddComponent<LayoutElement>();
        listsElement.minHeight = 200f;
        listsElement.preferredHeight = 220f;

        var listsLayout = listsRow.AddComponent<HorizontalLayoutGroup>();
        listsLayout.spacing = 12f;
        listsLayout.childAlignment = TextAnchor.UpperLeft;
        listsLayout.childControlWidth = true;
        listsLayout.childForceExpandWidth = true;
        listsLayout.childControlHeight = true;
        listsLayout.childForceExpandHeight = false;

        var availableList = CreateRecruitmentColumn(listsRow.transform, "可招募单位");
        var recruitedList = CreateRecruitmentColumn(listsRow.transform, "已招募单位");

        var result = CreateSectionLabel(panelGo.transform, "RecruitmentResult", "点击“测试招募系统”按钮，从当前地点尝试招募一名单位。", 16f, new Color(0.98f, 0.9f, 0.78f), TextAlignmentOptions.Left);
        result.fontStyle = FontStyles.Italic;

        temporaryObjects.Add(panelGo);

        return new RecruitmentUIElements
        {
            PanelRoot = panelRect,
            AvailableListRoot = availableList,
            RecruitedListRoot = recruitedList,
            SummaryLabel = summary,
            LocationLabel = location,
            ResultLabel = result
        };
    }

    private RectTransform CreateRecruitmentColumn(Transform parent, string headerText)
    {
        var columnGo = new GameObject(headerText);
        var rect = columnGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 200f);

        var element = columnGo.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;

        var background = columnGo.AddComponent<Image>();
        background.color = new Color(0.12f, 0.13f, 0.2f, 0.88f);

        var layout = columnGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        var header = CreateSectionLabel(columnGo.transform, headerText + "Header", headerText, 18f, new Color(0.94f, 0.96f, 1f), TextAlignmentOptions.Left);
        header.fontStyle = FontStyles.Bold;

        var contentGo = new GameObject("Content");
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.SetParent(columnGo.transform, false);

        var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandHeight = false;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return contentRect;
    }

    private TextMeshProUGUI CreateSectionLabel(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        ApplyFont(label);
        return label;
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

#if UNITY_EDITOR
        var pathSegments = new System.Collections.Generic.Stack<string>();
        var currentTransform = buttonGo.transform;
        while (currentTransform != null)
        {
            pathSegments.Push(currentTransform.name);
            currentTransform = currentTransform.parent;
        }

        Debug.Log($"[Part1SceneHarness] Button created: {string.Join("/", pathSegments)}");
#endif

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
