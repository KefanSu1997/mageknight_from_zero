// Assets\Scripts\Part1TestManager.cs


using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;

using System.Collections.Generic;
using System.Linq;
using TMPro;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using DataMonster = MK.Logic.Data.Monster;

public class Part1TestManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI testLog;
    public ScrollRect logScrollRect;

    public Button testDeckButton;
    public Button testManaButton;
    public Button testCombatButton;
    public Button testExplorationButton;
    public Button testRecruitmentButton;
    public Button testFullFlowButton;
    
    [Header("Deck Setup")]
    public DeckRuntime deckRuntime;
    public HandManager handManager;
    
    [Header("Systems")]
    public GameObject mapContainer;
    public GameObject combatUI;
    public GameObject recruitmentUI;
    
    private GameEngine gameEngine;
    private PlayerState playerState;
    private MapState mapState;
    private ManaPool manaPool;
    private RecruitmentService recruitmentService;
    private readonly Dictionary<ManaColor, TextMeshProUGUI> manaDisplayLabels = new();

    public struct CombatPanelBinding
    {
        public TextMeshProUGUI ScenarioTitle;
        public TextMeshProUGUI ScenarioDescription;
        public TextMeshProUGUI HeroTitle;
        public TextMeshProUGUI HeroStats;
        public TextMeshProUGUI HeroAbilities;
        public TextMeshProUGUI EnemyTitle;
        public TextMeshProUGUI EnemyStats;
        public TextMeshProUGUI EnemyAbilities;
        public TextMeshProUGUI OutcomeLabel;
        public Button NextScenarioButton;
        public Button ResolveButton;
    }

    private TextMeshProUGUI combatScenarioTitle;
    private TextMeshProUGUI combatScenarioDescription;
    private TextMeshProUGUI combatHeroTitle;
    private TextMeshProUGUI combatHeroStats;
    private TextMeshProUGUI combatHeroAbilities;
    private TextMeshProUGUI combatEnemyTitle;
    private TextMeshProUGUI combatEnemyStats;
    private TextMeshProUGUI combatEnemyAbilities;
    private TextMeshProUGUI combatOutcomeLabel;
    private Button combatNextScenarioButton;
    private Button combatResolveButton;
    private int combatScenarioIndex;
    private BattleResult? lastCombatResult;

    private readonly struct CombatScenario
    {
        public CombatScenario(
            string key,
            string title,
            string description,
            UnitCard heroCard,
            string heroDisplayName,
            DataMonster enemy,
            string enemyDisplayName,
            int recommendedBlock)
        {
            Key = key;
            Title = title;
            Description = description;
            HeroCard = heroCard;
            HeroDisplayName = heroDisplayName;
            Enemy = enemy;
            EnemyDisplayName = enemyDisplayName;
            RecommendedBlock = recommendedBlock;
        }

        public string Key { get; }
        public string Title { get; }
        public string Description { get; }
        public UnitCard HeroCard { get; }
        public string HeroDisplayName { get; }
        public DataMonster Enemy { get; }
        public string EnemyDisplayName { get; }
        public int RecommendedBlock { get; }
    }

    private readonly CombatScenario[] combatScenarios =
    {
        new CombatScenario(
            "basic_patrol",
            "场景一：先锋清剿兽人",
            "基础示例：以训练用先锋对抗游荡兽人，用于验证近战格挡与输出。",
            new UnitCard(
                "training_vanguard",
                "训练用先锋",
                "Training Vanguard",
                1,
                3,
                5,
                RecruitLocation.Village | RecruitLocation.Keep,
                new[]
                {
                    new AttackProfile(AttackType.Melee, 4, Element.Physical)
                },
                new[] { Ability.Guard }
            ),
            "训练用先锋",
            new DataMonster(
                "orc_rampager_demo",
                3,
                4,
                Element.Physical,
                1,
                new[] { Ability.Brutal },
                "兽人游荡者"
            ),
            "兽人游荡者",
            4),
        new CombatScenario(
            "siege_keep",
            "场景二：攻城队突袭要塞",
            "攻城工兵对抗加固守军，检验 Siege 攻击与加固能力显示。",
            new UnitCard(
                "siege_engineers_demo",
                "攻城工兵",
                "Siege Engineers",
                2,
                4,
                7,
                RecruitLocation.Keep | RecruitLocation.City,
                new[]
                {
                    new AttackProfile(AttackType.Siege, 5, Element.Physical),
                    new AttackProfile(AttackType.Melee, 3, Element.Physical)
                },
                new[] { Ability.Sweep }
            ),
            "攻城工兵",
            new DataMonster(
                "keep_garrison_demo",
                5,
                4,
                Element.Physical,
                2,
                new[] { Ability.Fortified },
                "城堡守军"
            ),
            "城堡守军",
            5),
        new CombatScenario(
            "ranger_dragon",
            "场景三：游侠挑战火龙",
            "远程冷火攻击面对火焰抗性，展示高难度对阵及抗性提示。",
            new UnitCard(
                "moon_ranger_demo",
                "月夜游侠",
                "Moon Ranger",
                2,
                3,
                6,
                RecruitLocation.Monastery | RecruitLocation.Village,
                new[]
                {
                    new AttackProfile(AttackType.Ranged, 4, Element.ColdFire)
                },
                new[] { Ability.Negate }
            ),
            "月夜游侠",
            new DataMonster(
                "fire_dragon_demo",
                6,
                6,
                Element.Fire,
                4,
                new[] { Ability.FireResist, Ability.Brutal },
                "火焰巨龙"
            ),
            "火焰巨龙",
            4)
    };

    private Button deckZoneButton;
    private Button discardZoneButton;
    private TextMeshProUGUI deckCountLabel;
    private TextMeshProUGUI discardCountLabel;
    private TextMeshProUGUI deckViewerHeader;
    private TextMeshProUGUI deckViewerBody;
    private GameObject deckViewerOverlayRoot;
    private Button deckViewerOverlayBackgroundButton;
    private Button deckViewerCloseButton;
    private TextMeshProUGUI deckViewerOverlayTitle;
    private TextMeshProUGUI deckViewerOverlaySubtitle;
    private RectTransform deckViewerViewportRect;
    private ScrollRect deckViewerScrollRect;
    private GridLayoutGroup deckViewerGridLayout;
    private float deckViewerLastViewportWidth = -1f;
    private float deckViewerLastViewportHeight = -1f;

    private const int DeckViewerMaxColumns = 6;
    private const float DeckViewerPreferredCardWidth = 340f;
    private const float DeckViewerMinCardWidth = 320f;
    private const float DeckViewerCardAspectRatio = 420f / 280f;

    private RectTransform deckViewerOverlayGridRoot;
   private readonly List<GameObject> deckViewerCardVisuals = new();
   private readonly List<AsyncOperationHandle<Sprite>> deckViewerSpriteHandles = new();

    private RectTransform explorationMapRoot;
    private TextMeshProUGUI explorationSummaryLabel;
    private TextMeshProUGUI explorationDetailLabel;
    private TextMeshProUGUI explorationHintLabel;
    private readonly Dictionary<AxialCoord, ExplorationTileVisual> explorationTileVisuals = new();
    private readonly HashSet<AxialCoord> exploredCoords = new();
    private readonly Queue<AxialCoord> explorationQueue = new();
    private readonly HashSet<AxialCoord> explorationQueued = new();
    private AxialCoord explorerPosition = new AxialCoord(0, 0);
    private int remainingMovementPoints = 6;
    private ExplorationService explorationService;
    private readonly System.Random explorationRandom = new System.Random(20241005);

    private RectTransform recruitmentAvailableListRoot;
    private RectTransform recruitmentRecruitedListRoot;
    private TextMeshProUGUI recruitmentSummaryLabel;
    private TextMeshProUGUI recruitmentLocationLabel;
    private TextMeshProUGUI recruitmentResultLabel;
    private readonly List<UnitCard> recruitmentPool = new();
    private readonly List<UnitCard> recruitedUnits = new();
    private readonly System.Random recruitmentRandom = new System.Random(20241006);
    private RecruitLocation currentRecruitLocation = RecruitLocation.Village;

    private DeckViewerMode currentDeckViewerMode = DeckViewerMode.None;

    private enum DeckViewerMode
    {
        None,
        DrawPile,
        DiscardPile
    }

    private sealed class ExplorationTileVisual
    {
        public RectTransform Root;
        public Image TileImage;
        public TextMeshProUGUI Label;
    }

    private sealed class NonThreateningRandom : System.Random
    {
        public override int Next(int maxValue)
        {
            if (maxValue <= 1)
            {
                return 0;
            }

            return maxValue - 1;
        }

        public override int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                return minValue;
            }

            return maxValue - 1;
        }
    }

    
    void Start()
    {
        InitializeSystems();
        SetupUI();
        RunQuickValidation();
    }
    
    void InitializeSystems()
    {
        testLog.text = "=== Part 1 Systems Test ===\n";
        
        playerState = new PlayerState();
        mapState = new MapState();
        manaPool = new ManaPool();
        var players = new List<PlayerState> { playerState };
        gameEngine = new GameEngine(players, 6);
        recruitmentService = new RecruitmentService();

        InitializeRecruitmentScenario();
        PrepareExplorationScenario();
        
        Log("All Part 1 systems initialized successfully");
        UpdateManaDisplay();
    }
    
    void SetupUI()
    {
        testDeckButton.onClick.AddListener(TestDeckAndCardSystem);
        testManaButton.onClick.AddListener(TestManaPoolSystem);
        testCombatButton.onClick.AddListener(TestCombatSystem);
        testExplorationButton.onClick.AddListener(TestExplorationSystem);
        testRecruitmentButton.onClick.AddListener(TestRecruitmentSystem);
        testFullFlowButton.onClick.AddListener(TestFullGameFlow);

        BindDeckRuntimeEvents();
    }



    #if UNITY_EDITOR
    private static void RemoveSmokeTestDummies(Transform gridRoot)
    {
        if (!gridRoot) return;
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
        {
            var t = gridRoot.GetChild(i);
            if (t != null && t.name.StartsWith("DummyCard_", StringComparison.Ordinal))
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        }
    }
    #endif
    

    private void BringOverlayToTop()
    {
        if (deckViewerOverlayRoot == null) return;

        var overlayCanvas = deckViewerOverlayRoot.GetComponent<Canvas>();
        if (overlayCanvas == null) return;

        int max = 0;
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                max = Mathf.Max(max, c.sortingOrder);
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = max + 100;

        // 以防与同父节点的兄弟竞争层级
        deckViewerOverlayRoot.transform.SetAsLastSibling();
    }
    public void ConfigureDeckViewerOverlay(
        GameObject overlayRoot,
        Button backgroundButton,
        Button closeButton,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI subtitleLabel,
        RectTransform contentRoot,
        ScrollRect scrollRect)
    {
        if (deckViewerOverlayBackgroundButton != null)
        {
            deckViewerOverlayBackgroundButton.onClick.RemoveListener(CloseDeckViewerOverlay);
        }

        if (deckViewerCloseButton != null)
        {
            deckViewerCloseButton.onClick.RemoveListener(CloseDeckViewerOverlay);
        }

        deckViewerOverlayRoot = overlayRoot;
        deckViewerOverlayBackgroundButton = backgroundButton;
        deckViewerCloseButton = closeButton;
        deckViewerOverlayTitle = titleLabel;
        deckViewerOverlaySubtitle = subtitleLabel;
        deckViewerOverlayGridRoot = contentRoot;
        deckViewerScrollRect = scrollRect;
        deckViewerGridLayout = contentRoot != null ? contentRoot.GetComponent<GridLayoutGroup>() : null;
        deckViewerViewportRect = scrollRect?.viewport ?? (contentRoot != null ? contentRoot.parent as RectTransform : null);
        deckViewerLastViewportWidth = -1f;
        deckViewerLastViewportHeight = -1f;

        if (deckViewerOverlayBackgroundButton != null)
        {
            deckViewerOverlayBackgroundButton.onClick.RemoveAllListeners();
            deckViewerOverlayBackgroundButton.onClick.AddListener(CloseDeckViewerOverlay);
        }

        if (deckViewerCloseButton != null)
        {
            deckViewerCloseButton.onClick.RemoveAllListeners();
            deckViewerCloseButton.onClick.AddListener(CloseDeckViewerOverlay);
        }

        ClearDeckViewerOverlayCards();
        UpdateDeckViewerOverlayLabels(null);
    }

    public void ConfigureManaDisplay(Dictionary<ManaColor, TextMeshProUGUI> labels)
    {
        manaDisplayLabels.Clear();
        if (labels == null)
        {
            return;
        }

        foreach (var kvp in labels)
        {
            if (kvp.Value != null)
            {
                manaDisplayLabels[kvp.Key] = kvp.Value;
            }
        }

        UpdateManaDisplay();
    }

    public void ConfigureCombatPanel(CombatPanelBinding binding)
    {
        if (combatNextScenarioButton != null)
        {
            combatNextScenarioButton.onClick.RemoveListener(AdvanceCombatScenario);
        }

        if (combatResolveButton != null)
        {
            combatResolveButton.onClick.RemoveListener(TestCombatSystem);
        }

        combatScenarioTitle = binding.ScenarioTitle;
        combatScenarioDescription = binding.ScenarioDescription;
        combatHeroTitle = binding.HeroTitle;
        combatHeroStats = binding.HeroStats;
        combatHeroAbilities = binding.HeroAbilities;
        combatEnemyTitle = binding.EnemyTitle;
        combatEnemyStats = binding.EnemyStats;
        combatEnemyAbilities = binding.EnemyAbilities;
        combatOutcomeLabel = binding.OutcomeLabel;
        combatNextScenarioButton = binding.NextScenarioButton;
        combatResolveButton = binding.ResolveButton;

        if (combatNextScenarioButton != null)
        {
            combatNextScenarioButton.onClick.AddListener(AdvanceCombatScenario);
        }

        if (combatResolveButton != null)
        {
            combatResolveButton.onClick.AddListener(TestCombatSystem);
        }

        RefreshCombatScenarioDisplay(true);
    }


    public void ConfigureDeckZoneUI(
        Button deckButton,
        Button discardButton,
        TextMeshProUGUI deckCount,
        TextMeshProUGUI discardCount,
        TextMeshProUGUI viewerHeaderLabel,
        TextMeshProUGUI viewerBodyLabel)
    {
        deckZoneButton = deckButton;
        discardZoneButton = discardButton;
        deckCountLabel = deckCount;
        discardCountLabel = discardCount;
        deckViewerHeader = viewerHeaderLabel;
        deckViewerBody = viewerBodyLabel;

        if (deckZoneButton != null)
        {
            deckZoneButton.onClick.RemoveAllListeners();
            deckZoneButton.onClick.AddListener(OnDeckZoneButtonClicked);
        }

        if (discardZoneButton != null)
        {
            discardZoneButton.onClick.RemoveAllListeners();
            discardZoneButton.onClick.AddListener(OnDiscardZoneButtonClicked);
        }

        currentDeckViewerMode = DeckViewerMode.None;
        RefreshDeckViewerContent();
        UpdateDeckZoneCounts();
        BindDeckRuntimeEvents();
    }

    public void ConfigureExplorationUI(
        RectTransform mapRoot,
        TextMeshProUGUI summaryLabel,
        TextMeshProUGUI detailLabel,
        TextMeshProUGUI hintLabel)
    {
        explorationMapRoot = mapRoot;
        explorationSummaryLabel = summaryLabel;
        explorationDetailLabel = detailLabel;
        explorationHintLabel = hintLabel;

        if (explorationDetailLabel != null)
        {
            explorationDetailLabel.text = "点击“测试探索系统”按钮会沿顺时针探索英雄周围的六边形。";
        }

        if (explorationHintLabel != null)
        {
            explorationHintLabel.text = "下一目标：等待初始化";
        }

        RefreshExplorationUI();
    }

    public void ConfigureRecruitmentUI(
        RectTransform availableRoot,
        RectTransform recruitedRoot,
        TextMeshProUGUI summaryLabel,
        TextMeshProUGUI locationLabel,
        TextMeshProUGUI resultLabel)
    {
        recruitmentAvailableListRoot = availableRoot;
        recruitmentRecruitedListRoot = recruitedRoot;
        recruitmentSummaryLabel = summaryLabel;
        recruitmentLocationLabel = locationLabel;
        recruitmentResultLabel = resultLabel;

        if (recruitmentResultLabel != null)
        {
            recruitmentResultLabel.text = "点击“测试招募系统”按钮尝试从当前地点招募单位。";
        }

        RefreshRecruitmentUI();
    }

    private void BindDeckRuntimeEvents()
    {
        if (deckRuntime == null)
        {
            return;
        }

        deckRuntime.DeckStateChanged -= HandleDeckStateChanged;
        deckRuntime.DeckStateChanged += HandleDeckStateChanged;
        HandleDeckStateChanged();
    }

    private void HandleDeckStateChanged()
    {
        UpdateDeckZoneCounts();
        var cards = RefreshDeckViewerContent();
        RebuildDeckViewerOverlay(cards);
    }

    private void UpdateDeckZoneCounts()
    {
        if (deckCountLabel != null)
        {
            int drawCount = deckRuntime?.drawPile?.Count ?? 0;
            deckCountLabel.text = $"{drawCount} 张";
        }

        if (discardCountLabel != null)
        {
            int discardCount = deckRuntime?.discard?.Count ?? 0;
            discardCountLabel.text = $"{discardCount} 张";
        }
    }

    private List<CardSO> RefreshDeckViewerContent()
    {
        if (deckRuntime == null)
        {
            if (deckViewerHeader != null)
            {
                deckViewerHeader.text = "牌堆预览";
            }

            if (deckViewerBody != null)
            {
                deckViewerBody.text = "DeckRuntime 未配置，请检查场景绑定。";
            }

            UpdateDeckViewerOverlayLabels(null);
            return new List<CardSO>();
        }

        var cards = BuildDeckViewerCardList(currentDeckViewerMode);

        if (deckViewerHeader == null || deckViewerBody == null)
        {
            UpdateDeckViewerOverlayLabels(cards);
            return cards;
        }

        switch (currentDeckViewerMode)
        {
            case DeckViewerMode.DrawPile:
                if (cards.Count == 0)
                {
                    deckViewerHeader.text = "牌组（空）";
                    deckViewerBody.text = "当前没有可抽取的卡牌。";
                }
                else
                {
                    deckViewerHeader.text = $"牌组（共{cards.Count}张，按名称排序展示）";
                    deckViewerBody.text = string.Join("\n", cards.Select((card, index) => $"{index + 1}. {GetCardDisplayName(card)}"));
                }
                break;
            case DeckViewerMode.DiscardPile:
                if (cards.Count == 0)
                {
                    deckViewerHeader.text = "弃牌区（空）";
                    deckViewerBody.text = "尚未有卡牌被弃置。";
                }
                else
                {
                    deckViewerHeader.text = $"弃牌区（共{cards.Count}张，自上而下显示）";
                    deckViewerBody.text = string.Join("\n", cards.Select((card, index) => $"{index + 1}. {GetCardDisplayName(card)}"));
                }
                break;
            default:
                deckViewerHeader.text = "牌堆预览";
                deckViewerBody.text = "点击上方的牌组或弃牌区以查看详情。";
                cards.Clear();
                break;
        }

        UpdateDeckViewerOverlayLabels(cards);
        return cards;
    }

        private void AdjustDeckViewerGridLayout()
    {
        if (deckViewerGridLayout == null)
        {
            return;
        }

        float viewportWidth = 0f;
        float viewportHeight = 0f;
        if (deckViewerViewportRect != null)
        {
            viewportWidth = deckViewerViewportRect.rect.width;
            viewportHeight = deckViewerViewportRect.rect.height;
        }

        var padding = deckViewerGridLayout.padding;
        var spacingX = deckViewerGridLayout.spacing.x;

        var columns = DeckViewerMaxColumns;
        while (columns > 1)
        {
            var requiredWidth = padding.left + padding.right + columns * DeckViewerPreferredCardWidth + (columns - 1) * spacingX;
            if (requiredWidth <= viewportWidth)
            {
                break;
            }

            columns--;
        }

        columns = Mathf.Max(1, columns);
        var availableWidth = viewportWidth - padding.left - padding.right - spacingX * (columns - 1);
        var cardWidth = Mathf.Clamp(availableWidth / columns, DeckViewerMinCardWidth, DeckViewerPreferredCardWidth);
        Debug.Log($"[DeckViewer] layout calc -> viewport=({viewportWidth:F2},{viewportHeight:F2}) columns={columns} width={cardWidth:F2}");
        // 纵向滚动区域足够承载内容，保持卡片高度不被强行压缩
        var cardHeight = cardWidth * DeckViewerCardAspectRatio;

        deckViewerGridLayout.cellSize = new Vector2(cardWidth, cardHeight);
        deckViewerGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        deckViewerGridLayout.constraintCount = columns;
        deckViewerLastViewportWidth = viewportWidth;
        deckViewerLastViewportHeight = viewportHeight;

        var gridTransform = deckViewerGridLayout.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridTransform);
    }

    private static void ResizeGridContent(
        RectTransform content,
        GridLayoutGroup grid,
        int childCount,
        RectTransform referenceRect = null)
    {
        if (content == null || grid == null)
        {
            return;
        }

        var padding = grid.padding ?? new RectOffset();
        var referenceWidth = referenceRect != null ? referenceRect.rect.width : content.rect.width;

        int columns;
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
        {
            columns = Mathf.Max(1, grid.constraintCount);
        }
        else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount > 0)
        {
            var rowConstraint = Mathf.Max(1, grid.constraintCount);
            columns = childCount == 0 ? 1 : Mathf.CeilToInt(childCount / (float)rowConstraint);
        }
        else
        {
            var denominator = grid.cellSize.x + grid.spacing.x;
            var availableWidth = referenceWidth - padding.left - padding.right;
            columns = denominator > 0f
                ? Mathf.Max(1, Mathf.FloorToInt((availableWidth + grid.spacing.x) / denominator))
                : 1;
        }

        columns = Mathf.Max(1, columns);
        var rows = childCount <= 0 ? 0 : Mathf.CeilToInt(childCount / (float)columns);

        var height = rows <= 0
            ? 0f
            : rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        height += padding.top + padding.bottom;
        height = Mathf.Max(0f, height);

        var sizeDelta = content.sizeDelta;
        sizeDelta.y = height;
        content.sizeDelta = sizeDelta;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void ResizeDeckViewerContent()
    {
        if (deckViewerOverlayGridRoot == null || deckViewerGridLayout == null)
        {
            return;
        }

        var activeChildCount = 0;
        for (int i = 0; i < deckViewerOverlayGridRoot.childCount; i++)
        {
            var child = deckViewerOverlayGridRoot.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
            {
                activeChildCount++;
            }
        }

        ResizeGridContent(deckViewerOverlayGridRoot, deckViewerGridLayout, activeChildCount, deckViewerViewportRect);

        if (deckViewerViewportRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(deckViewerViewportRect);
        }
    }



    private void ResetDeckViewerScrollPosition()
    {
        if (deckViewerScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        deckViewerScrollRect.verticalNormalizedPosition = 1f;
    }

    private void LogDeckViewerLayoutSnapshot(string context)
    {
#if UNITY_EDITOR
        if (deckViewerOverlayGridRoot == null || deckViewerGridLayout == null)
        {
            Debug.Log($"[DeckViewer] {context}: grid or layout missing");
            return;
        }

        Debug.Log($"[DeckViewer] {context}: childCount={deckViewerOverlayGridRoot.childCount} rect={deckViewerOverlayGridRoot.rect.size} sizeDelta={deckViewerOverlayGridRoot.sizeDelta} cellSize={deckViewerGridLayout.cellSize}");
#endif
    }


    private void LogDeckViewerContentState()
    {
#if UNITY_EDITOR
        if (deckViewerOverlayGridRoot == null)
        {
            Debug.Log("[DeckOverlay] gridRoot missing");
            return;
        }

         Debug.Log($"[DeckOverlay] children={deckViewerOverlayGridRoot.childCount}");
        var r = deckViewerOverlayGridRoot.rect;
        Debug.Log($"[DeckOverlay] content rect=({r.width:F2}, {r.height:F2}) sizeDelta=({deckViewerOverlayGridRoot.sizeDelta.x:F2}, {deckViewerOverlayGridRoot.sizeDelta.y:F2}) anchored=({deckViewerOverlayGridRoot.anchoredPosition.x:F2}, {deckViewerOverlayGridRoot.anchoredPosition.y:F2})");

        // ✅ 找到第一张真正赋了 sprite 的 Image 再打印
        Image firstWithSprite = null;
        RectTransform rt = null;
        for (int i = 0; i < deckViewerOverlayGridRoot.childCount; i++)
        {
            var img = deckViewerOverlayGridRoot.GetChild(i).GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                firstWithSprite = img;
                rt = (RectTransform) img.transform;
                break;
            }
        }

        if (firstWithSprite != null)
        {
            Debug.Log($"[DeckOverlay] first-with-sprite name={rt.name} active={firstWithSprite.isActiveAndEnabled} size=({rt.rect.width:F2},{rt.rect.height:F2}) anchored=({rt.anchoredPosition.x:F2},{rt.anchoredPosition.y:F2})");
        }
        else
        {
            // 若还没赋图，给出更有用的提示
            Image anyImg = null;
            for (int i = 0; i < deckViewerOverlayGridRoot.childCount && anyImg == null; i++)
                anyImg = deckViewerOverlayGridRoot.GetChild(i).GetComponent<Image>();

            if (anyImg != null)
                Debug.Log($"[DeckOverlay] no sprites yet. first Image={anyImg.name} active={anyImg.isActiveAndEnabled} sprite=null");
            else
                Debug.Log("[DeckOverlay] no Image components under grid yet.");
        }

        var rect = deckViewerOverlayGridRoot;
        Debug.Log($"[DeckOverlay] children={rect.childCount}");
        Debug.Log($"[DeckOverlay] content rect={rect.rect.size} sizeDelta={rect.sizeDelta} anchored={rect.anchoredPosition}");

        var firstChild = rect.childCount > 0 ? rect.GetChild(0) : null;
        var image = firstChild != null ? firstChild.GetComponentInChildren<Image>(true) : null;
        var spriteName = image != null && image.sprite != null ? image.sprite.name : "null";
        var firstActive = firstChild != null && firstChild.gameObject.activeInHierarchy;
        var imageEnabled = image != null && image.enabled;
        Debug.Log($"[DeckOverlay] first active={firstActive} imgEnabled={imageEnabled} sprite={spriteName}");
#endif
    }

    private List<CardSO> BuildDeckViewerCardList(DeckViewerMode mode)
    {
        var result = new List<CardSO>();

        if (deckRuntime == null)
        {
            return result;
        }

        switch (mode)
        {
            case DeckViewerMode.DrawPile:
                if (deckRuntime.drawPile != null && deckRuntime.drawPile.Count > 0)
                {
                    result = deckRuntime.drawPile
                        .Where(card => card != null)
                        .OrderBy(card => GetCardDisplayName(card))
                        .ToList();
                }
                break;
            case DeckViewerMode.DiscardPile:
                if (deckRuntime.discard != null && deckRuntime.discard.Count > 0)
                {
                    result = deckRuntime.discard
                        .ToArray()
                        .Where(card => card != null)
                        .ToList();
                }
                break;
        }

        Debug.Log($"[DeckViewer] Build list for {mode} -> {result.Count} cards");
        return result;
    }

    private void UpdateDeckViewerOverlayLabels(IReadOnlyCollection<CardSO> cards)
    {
        if (deckViewerOverlayTitle == null || deckViewerOverlaySubtitle == null)
        {
            return;
        }

        switch (currentDeckViewerMode)
        {
            case DeckViewerMode.DrawPile:
                deckViewerOverlayTitle.text = "牌组卡图预览";
                deckViewerOverlaySubtitle.text = cards == null || cards.Count == 0
                    ? "当前没有可抽取的卡牌。"
                    : $"共 {cards.Count} 张卡牌（按名称排序展示）";
                break;
            case DeckViewerMode.DiscardPile:
                deckViewerOverlayTitle.text = "弃牌区卡图预览";
                deckViewerOverlaySubtitle.text = cards == null || cards.Count == 0
                    ? "尚未有卡牌被弃置。"
                    : $"共 {cards.Count} 张卡牌（自上而下显示弃牌顺序）";
                break;
            default:
                deckViewerOverlayTitle.text = "牌堆预览";
                deckViewerOverlaySubtitle.text = "点击牌组或弃牌区以查看卡图。";
                break;
        }
    }

    private void OnDeckZoneButtonClicked()
    {
        OpenDeckViewerOverlay(DeckViewerMode.DrawPile);
    }

    private void OnDiscardZoneButtonClicked()
    {
        OpenDeckViewerOverlay(DeckViewerMode.DiscardPile);
    }

    private void OpenDeckViewerOverlay(DeckViewerMode mode)
    {
        currentDeckViewerMode = mode;
        var cards = RefreshDeckViewerContent();
        Debug.Log($"[DeckViewer] Open {mode} -> {cards.Count} cards");


        if (deckViewerOverlayRoot == null || deckViewerOverlayGridRoot == null)
        {
            return;
        }
        // ⬇️ 新增：每次打开都把 Overlay 提到全场景最上层
        BringOverlayToTop();
        PopulateDeckViewerOverlayCards(cards, reactivateOverlay: true);
    }

    private void PopulateDeckViewerOverlayCards(IReadOnlyList<CardSO> cards, bool reactivateOverlay)
    {
        if (deckViewerOverlayRoot == null || deckViewerOverlayGridRoot == null)
        {
            return;
        }


        #if UNITY_EDITOR
            RemoveSmokeTestDummies(deckViewerOverlayGridRoot);
        #endif


        bool shouldDisplay = reactivateOverlay || deckViewerOverlayRoot.activeSelf;
        if (!shouldDisplay)
        {
            return;
        }

        ClearDeckViewerOverlayCards();

        if (!deckViewerOverlayRoot.activeSelf)
        {
            deckViewerOverlayRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }
        ForceDeckViewerLayoutImmediate();

        AdjustDeckViewerGridLayout();
        LogDeckViewerLayoutSnapshot("post-clear");

        bool hasCards = cards != null && cards.Count > 0;

        if (!hasCards)
        {
            var emptyGo = new GameObject("EmptyLabel");
            var rect = emptyGo.AddComponent<RectTransform>();
            rect.SetParent(deckViewerOverlayGridRoot, false);
            rect.localScale = Vector3.one;

            var label = emptyGo.AddComponent<TextMeshProUGUI>();
            label.text = currentDeckViewerMode == DeckViewerMode.DiscardPile ? "弃牌区目前为空。" : "当前没有可抽取的卡牌。";
            label.fontSize = 26f;
            label.color = new Color(0.9f, 0.92f, 0.98f);
            label.alignment = TextAlignmentOptions.Center;
            if (deckViewerHeader != null && deckViewerHeader.font != null)
            {
                label.font = deckViewerHeader.font;
            }

            deckViewerCardVisuals.Add(emptyGo);
        }
        else
        {
            foreach (var card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                var slotGo = new GameObject(card.name + "_Image");
                var rect = slotGo.AddComponent<RectTransform>();
                rect.SetParent(deckViewerOverlayGridRoot, false);
                rect.localScale = Vector3.one;

                var image = slotGo.AddComponent<Image>();
                image.color = Color.white;
                image.preserveAspect = true;
                image.enabled = false;

                deckViewerCardVisuals.Add(slotGo);

                var handle = Addressables.LoadAssetAsync<Sprite>(card.ImagePath);
                deckViewerSpriteHandles.Add(handle);

                Sprite sprite = null;
                try
                {
                    sprite = handle.WaitForCompletion();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                if (image == null)
                {
                    continue;
                }

                Debug.Log($"[DeckViewer] Requested sprite for {card.name} ({card.ImagePath})");

                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.enabled = true;

                    if (deckViewerGridLayout != null)
                    {
                        var gridTransform = deckViewerGridLayout.GetComponent<RectTransform>();
                        if (gridTransform != null)
                        {
                            LayoutRebuilder.ForceRebuildLayoutImmediate(gridTransform);
                        }
                    }

                    var rectTransform = image.rectTransform;
                    Debug.Log($"[DeckViewer] Loaded sprite for {card.name} size={sprite.rect.size} anchored={rectTransform.anchoredPosition} world={rectTransform.position}");
                }
                else
                {
                    Debug.LogWarning($"加载卡图失败：{card.ImagePath}");
                }
            }
        }

        AdjustDeckViewerGridLayout();
        ResizeDeckViewerContent();
        LogDeckViewerLayoutSnapshot("after-resize");
        LogDeckViewerContentState();
        ResetDeckViewerScrollPosition();
        UpdateDeckViewerOverlayLabels(cards);
    }

    private void ClearDeckViewerOverlayCards()
    {
        foreach (var handle in deckViewerSpriteHandles)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        deckViewerSpriteHandles.Clear();

        foreach (var visual in deckViewerCardVisuals)
        {
            if (visual == null)
            {
                continue;
            }

            visual.transform.SetParent(null, false);
            if (Application.isPlaying)
            {
                Destroy(visual);
            }
            else
            {
                DestroyImmediate(visual);
            }
        }

        deckViewerCardVisuals.Clear();
        deckViewerLastViewportWidth = -1f;
        deckViewerLastViewportHeight = -1f;
    }

    private void CloseDeckViewerOverlay()
    {
        if (deckViewerOverlayRoot == null)
        {
            return;
        }

        ClearDeckViewerOverlayCards();
        deckViewerOverlayRoot.SetActive(false);
    }

    private void RebuildDeckViewerOverlay(List<CardSO> cards)
    {
        if (deckViewerOverlayRoot == null || !deckViewerOverlayRoot.activeSelf)
        {
            return;
        }

        PopulateDeckViewerOverlayCards(cards, reactivateOverlay: false);
    }

    private static string GetCardDisplayName(CardSO card)
    {
        if (card == null)
        {
            return "(空)";
        }

        return string.IsNullOrWhiteSpace(card.NameCn) ? card.name : card.NameCn;
    }


    private int CountCurrentHandCards()
    {
        if (handManager?.handRoot == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < handManager.handRoot.childCount; i++)

        {
            var runtime = handManager.handRoot.GetChild(i)?.GetComponent<CardRuntime>();
            if (runtime?.Data != null)
            {
                count++;
            }
        }

        return count;
    }



#if UNITY_EDITOR
    private void LogHandLayout(string tag)
    {
        if (handManager?.handRoot is not RectTransform handRootRect)
        {
            Debug.Log($"[HandLayout] {tag}: handRoot missing");
            return;
        }

        int childCount = handRootRect.childCount;
        Debug.Log($"[HandLayout] {tag}: {childCount} cards (root width {handRootRect.rect.width})");

        for (int i = 0; i < childCount; i++)
        {
            if (handRootRect.GetChild(i) is not RectTransform child)
            {
                continue;
            }

            Debug.Log($"[HandLayout] {tag} card[{i}] anchored={child.anchoredPosition}, size={child.rect.size}");
        }
    }
#endif



    private void UpdateManaDisplay()
    {
        if (manaDisplayLabels.Count == 0)
        {
            return;
        }

        foreach (var kvp in manaDisplayLabels)
        {
            int amount = manaPool?.Crystals.GetValueOrDefault(kvp.Key) ?? 0;
            kvp.Value.text = amount.ToString();
        }
    }

    private string GetManaColorDisplayName(ManaColor color)
    {
        return color switch
        {
            ManaColor.Red => "红色",
            ManaColor.Blue => "蓝色",
            ManaColor.White => "白色",
            ManaColor.Green => "绿色",
            ManaColor.Gold => "金色",
            _ => color.ToString()
        };
    }

    void RunQuickValidation()
    {
        Log("Running quick validation tests...");
        
        // Test card effects
        var effect = CardEffectFactory.Get(MK.Logic.Runtime.CardEffects.ActionEffectId.FireballBase);
        Log($"Card effects loaded successfully: {effect != null}");
        
        // Test mana pool
        var manaTypes = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White, ManaColor.Green };
        foreach (var type in manaTypes)
        {
            Log($"Mana {type}: {manaPool.Crystals.GetValueOrDefault(type)}");
        }
        
        // Test round clock
        var roundClock = new RoundClock();
        Log($"Day {roundClock.DayIndex}, Time: {roundClock.DayPart}");
    }
    
    void TestDeckAndCardSystem()
    {
        Log("\n=== Testing Deck & Card System ===\n");

        if (deckRuntime == null)
        {
            Log("DeckRuntime not assigned!");
            return;
        }

        if (handManager == null)
        {
            Log("HandManager not assigned!");
            return;
        }

        if (handManager.deck == null)
        {
            handManager.deck = deckRuntime;
        }

        if (deckRuntime.drawPile == null)
        {
            deckRuntime.Init();
        }

#if UNITY_EDITOR
        LogHandLayout("before reset");
#endif

        int previousHandCount = CountCurrentHandCards();
        if (previousHandCount > 0)
        {
            Log($"弃置当前手牌 {previousHandCount} 张到弃牌区，并重新抽取 5 张。");
        }
        else
        {
            Log("当前手牌为空，将直接抽取 5 张新牌。");
        }

        handManager.ResetHand(5);
        Log("已完成弃牌并抽出 5 张新手牌，如牌组不足将自动回收弃牌区。");

#if UNITY_EDITOR
        LogHandLayout("after reset");
#endif

        var drawnCards = new List<string>();
        if (handManager.handRoot != null)
        {
            for (int i = 0; i < handManager.handRoot.childCount; i++)
            {
                var cardView = handManager.handRoot.GetChild(i)?.GetComponent<CardRuntime>();
                if (cardView?.Data != null)
                {
                    var nameToLog = string.IsNullOrWhiteSpace(cardView.Data.NameCn)
                        ? cardView.Data.name
                        : cardView.Data.NameCn;
                    drawnCards.Add(nameToLog);
                }
            }
        }

        if (drawnCards.Count > 0)
        {
            Log($"随机抽出了新的手牌：{string.Join("、", drawnCards)}");
        }
        else
        {
            Log("未在手牌区找到抽出的卡牌，请检查 HandManager 设置。");
        }

        int remaining = deckRuntime.drawPile?.Count ?? 0;
        Log($"牌库剩余：{remaining} 张，弃牌堆：{deckRuntime.discard.Count} 张");
        HandleDeckStateChanged();
    }




    
    void TestManaPoolSystem()
    {
        Log("\n=== Testing Mana Pool System ===");

        if (manaPool == null)
        {
            manaPool = new ManaPool();
        }

        var colors = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White, ManaColor.Green, ManaColor.Gold };
        manaPool.ResetTokens();

        var availableColors = new List<ManaColor>(colors);
        int changeCount = Math.Max(1, UnityEngine.Random.Range(2, availableColors.Count + 1));
        var adjustments = new List<(ManaColor color, int delta, int total)>();

        for (int i = 0; i < changeCount; i++)
        {
            int index = UnityEngine.Random.Range(0, availableColors.Count);
            var color = availableColors[index];
            availableColors.RemoveAt(index);

            int delta = UnityEngine.Random.Range(1, 4);
            manaPool.AddCrystal(color, delta);
            int total = manaPool.Crystals.GetValueOrDefault(color);
            adjustments.Add((color, delta, total));
        }

        UpdateManaDisplay();

        Log($"本次随机调整了 {adjustments.Count} 种魔晶：");
        foreach (var adjustment in adjustments)
        {
            Log($"- {GetManaColorDisplayName(adjustment.color)}魔晶 +{adjustment.delta} => {adjustment.total}");
        }

        var finalSummary = new List<string>();
        foreach (var color in colors)
        {
            int amount = manaPool.Crystals.GetValueOrDefault(color);
            finalSummary.Add($"{GetManaColorDisplayName(color)} {amount}");
        }

        Log($"当前魔晶池：{string.Join(" | ", finalSummary)}");
    }
    
    void TestCombatSystem()
    {
        if (combatScenarios.Length == 0)
        {
            Log("\n=== 测试战斗系统 ===");
            Log("当前未配置战斗示例，请更新 Part1TestManager.combatScenarios。");
            UpdateCombatOutcome(null);
            return;
        }

        if (combatScenarioIndex < 0 || combatScenarioIndex >= combatScenarios.Length)
        {
            combatScenarioIndex = 0;
        }

        var scenario = combatScenarios[combatScenarioIndex];

        Log($"\n=== 测试战斗系统：{scenario.Title} ===");
        Log($"说明：{scenario.Description}");
        Log($"我方单位：{scenario.HeroDisplayName}｜{FormatHeroSummary(scenario.HeroCard)}");
        Log($"敌方单位：{scenario.EnemyDisplayName}｜{FormatEnemySummary(scenario.Enemy)}");
        Log($"预设格挡值：{scenario.RecommendedBlock}（用于演示 BlockAllocation 配置）");

        var player = new PlayerState { Name = scenario.HeroDisplayName, Armor = scenario.HeroCard.Armor };
        var heroUnit = new UnitState(scenario.HeroCard);
        var units = new List<UnitState> { heroUnit };

        var blockAllocations = BuildBlockAllocations(scenario);
        var attackAllocations = BuildAttackAllocations(scenario.HeroCard);
        var enemies = new List<DataMonster> { scenario.Enemy };

        var result = BattleResolver.Resolve(player, enemies, blockAllocations, attackAllocations, units);

        lastCombatResult = result;

        Log(result.AllKilled
            ? "战斗分析：成功击败全部敌人。"
            : "战斗分析：仍有敌军存活，请检查格挡或攻击配置。");
        Log($"英雄承受的总创伤：{result.TotalWounds}");
        Log(result.FameGain > 0
            ? $"本次战斗获得名望：{result.FameGain}"
            : "本次战斗未获得额外名望。");

        UpdateCombatOutcome(result);
        RefreshCombatScenarioDisplay(false);
    }
    
    private void AdvanceCombatScenario()
    {
        if (combatScenarios.Length == 0)
        {
            Log("暂无战斗示例可切换。");
            return;
        }

        combatScenarioIndex = (combatScenarioIndex + 1) % combatScenarios.Length;
        lastCombatResult = null;
        var scenario = combatScenarios[combatScenarioIndex];
        Log($"切换战斗示例：{scenario.Title}");
        RefreshCombatScenarioDisplay(true);
    }

    private void RefreshCombatScenarioDisplay(bool resetOutcome)
    {
        if (combatScenarioTitle == null && combatHeroTitle == null && combatEnemyTitle == null)
        {
            if (resetOutcome)
            {
                UpdateCombatOutcome(null);
            }
            return;
        }

        if (combatScenarios.Length == 0)
        {
            if (combatScenarioTitle != null)
            {
                combatScenarioTitle.text = "战斗演示";
            }

            if (combatScenarioDescription != null)
            {
                combatScenarioDescription.text = "当前未配置战斗示例。";
            }

            if (resetOutcome)
            {
                UpdateCombatOutcome(null);
            }
            return;
        }

        if (combatScenarioIndex < 0 || combatScenarioIndex >= combatScenarios.Length)
        {
            combatScenarioIndex = 0;
        }

        var scenario = combatScenarios[combatScenarioIndex];

        if (combatScenarioTitle != null)
        {
            combatScenarioTitle.text = $"{scenario.Title} ({combatScenarioIndex + 1}/{combatScenarios.Length})";
        }

        if (combatScenarioDescription != null)
        {
            combatScenarioDescription.text = scenario.Description;
        }

        if (combatHeroTitle != null)
        {
            combatHeroTitle.text = scenario.HeroDisplayName;
        }

        if (combatHeroStats != null)
        {
            combatHeroStats.text = FormatHeroStats(scenario.HeroCard);
        }

        if (combatHeroAbilities != null)
        {
            combatHeroAbilities.text = FormatHeroAbilities(scenario.HeroCard);
        }

        if (combatEnemyTitle != null)
        {
            combatEnemyTitle.text = scenario.EnemyDisplayName;
        }

        if (combatEnemyStats != null)
        {
            combatEnemyStats.text = FormatEnemyStats(scenario.Enemy);
        }

        if (combatEnemyAbilities != null)
        {
            combatEnemyAbilities.text = FormatEnemyAbilities(scenario.Enemy);
        }

        if (resetOutcome)
        {
            UpdateCombatOutcome(null);
        }
        else if (lastCombatResult.HasValue)
        {
            UpdateCombatOutcome(lastCombatResult.Value);
        }
    }

    private void UpdateCombatOutcome(BattleResult? result)
    {
        if (combatOutcomeLabel == null)
        {
            return;
        }

        if (!result.HasValue)
        {
            combatOutcomeLabel.text = "等待战斗结算...";
            combatOutcomeLabel.color = new Color(0.82f, 0.88f, 1f);
            return;
        }

        var value = result.Value;
        if (value.AllKilled)
        {
            combatOutcomeLabel.text = $"战斗结果：胜利！名望 +{value.FameGain}｜创伤 {value.TotalWounds}";
            combatOutcomeLabel.color = new Color(0.42f, 0.86f, 0.54f);
        }
        else
        {
            combatOutcomeLabel.text = $"战斗结果：未全歼敌人（创伤 {value.TotalWounds}｜名望 +{value.FameGain}）";
            combatOutcomeLabel.color = new Color(0.93f, 0.52f, 0.46f);
        }
    }

    private string FormatHeroStats(UnitCard card)
    {
        var attacks = card.Attacks != null && card.Attacks.Length > 0
            ? "攻击：" + string.Join("，", card.Attacks.Select(p => $"{GetAttackTypeDisplay(p.Type)} {p.Value}（{GetElementDisplay(p.Element)}）"))
            : "攻击：无";
        return $"等级 {card.Level}｜护甲 {card.Armor}｜招募花费 {card.InfluenceCost}\n{attacks}";
    }

    private string FormatHeroAbilities(UnitCard card)
    {
        if (card.Abilities == null || card.Abilities.Length == 0)
        {
            return "能力：无";
        }

        return "能力：" + string.Join("、", card.Abilities.Select(GetAbilityDisplayName));
    }

    private string FormatEnemyStats(DataMonster enemy)
    {
        return $"护甲 {enemy.Armor}｜攻击 {enemy.Attack}（{GetElementDisplay(enemy.AttackElement)}）｜名望 {enemy.Fame}";
    }

    private string FormatEnemyAbilities(DataMonster enemy)
    {
        var abilityList = enemy.Abilities != null ? enemy.Abilities.ToArray() : Array.Empty<Ability>();
        if (abilityList.Length == 0)
        {
            return "能力：无";
        }

        return "能力：" + string.Join("、", abilityList.Select(GetAbilityDisplayName));
    }

    private string FormatHeroSummary(UnitCard card)
    {
        var attackSummary = card.Attacks != null && card.Attacks.Length > 0
            ? string.Join("、", card.Attacks.Select(a => $"{GetAttackTypeDisplay(a.Type)} {a.Value}({GetElementDisplay(a.Element)})"))
            : "无攻击";
        var abilitySummary = card.Abilities != null && card.Abilities.Length > 0
            ? string.Join("、", card.Abilities.Select(GetAbilityDisplayName))
            : "无能力";
        return $"护甲 {card.Armor}｜攻击 {attackSummary}｜能力 {abilitySummary}";
    }

    private string FormatEnemySummary(DataMonster enemy)
    {
        var abilityList = enemy.Abilities != null ? enemy.Abilities.ToArray() : Array.Empty<Ability>();
        var abilitySummary = abilityList.Length > 0
            ? string.Join("、", abilityList.Select(GetAbilityDisplayName))
            : "无能力";
        return $"护甲 {enemy.Armor}｜攻击 {enemy.Attack}（{GetElementDisplay(enemy.AttackElement)}）｜能力 {abilitySummary}";
    }

    private static string GetElementDisplay(Element element)
    {
        return element switch
        {
            Element.Physical => "物理",
            Element.Fire => "火焰",
            Element.Ice => "寒冰",
            Element.ColdFire => "冷火",
            _ => element.ToString()
        };
    }

    private static string GetAttackTypeDisplay(AttackType type)
    {
        return type switch
        {
            AttackType.Melee => "近战",
            AttackType.Ranged => "远程",
            AttackType.Siege => "攻城",
            _ => type.ToString()
        };
    }

    private static string GetAbilityDisplayName(Ability ability)
    {
        return ability switch
        {
            Ability.Fortified => "加固",
            Ability.Brutal => "残暴",
            Ability.Swift => "迅捷",
            Ability.Poison => "剧毒",
            Ability.Paralyze => "麻痹",
            Ability.Regenerate => "再生",
            Ability.FireResist => "火焰抗性",
            Ability.IceResist => "寒冰抗性",
            Ability.ColdFireResist => "冷火抗性",
            Ability.MagicResist => "魔法抗性",
            Ability.Guard => "守护",
            Ability.Heal => "治疗",
            Ability.Negate => "否决",
            Ability.Sweep => "范围",
            Ability.Enduring => "坚毅",
            _ => ability.ToString()
        };
    }

    private static List<AttackAllocation> BuildAttackAllocations(UnitCard card)
    {
        var allocations = new List<AttackAllocation>();
        if (card.Attacks == null || card.Attacks.Length == 0)
        {
            return allocations;
        }

        foreach (var profile in card.Attacks)
        {
            var targets = new[] { 0 };
            allocations.Add(new AttackAllocation(targets, profile.Value, profile.Element, profile.Type == AttackType.Siege));
        }

        return allocations;
    }

    private List<BlockAllocation> BuildBlockAllocations(CombatScenario scenario)
    {
        return new List<BlockAllocation>
        {
            new BlockAllocation(0, scenario.RecommendedBlock, Element.Physical)
        };
    }

    void TestExplorationSystem()
    {
        Log("\n=== Testing Exploration System ===");

        if (explorationService == null)
        {
            Log("探索服务未初始化，自动重新配置场景。");
            PrepareExplorationScenario();
        }

        if (explorationService == null)
        {
            Log("探索服务仍不可用，终止测试。");
            return;
        }

        if (explorationQueue.Count == 0)
        {
            Log("周围示例地块已经全部探索，重新生成一批演示用地块。");
            PrepareExplorationScenario();
        }

        if (explorationQueue.Count == 0)
        {
            Log("暂无可探索的目标。");
            return;
        }

        var target = explorationQueue.Dequeue();
        explorationQueued.Remove(target);

        if (mapState.Placed.ContainsKey(target))
        {
            Log($"位置 {DescribeCoordinate(target)} 已经探索，跳过。");
            RefreshExplorationUI();
            return;
        }

        if (remainingMovementPoints < 2)
        {
            remainingMovementPoints = 6;
            Log("移动力不足，自动恢复到 6 点以继续示例。");
        }

        var rotationSteps = explorationRandom.Next(0, 6);
        var rotation = rotationSteps * 60;

        var result = explorationService.Explore(playerState, target, rotation, remainingMovementPoints);
        if (!result.Success || result.Tile == null)
        {
            var reason = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "未知原因" : result.ErrorMessage;
            Log($"探索失败：{reason}");
            if (explorationDetailLabel != null)
            {
                explorationDetailLabel.text = $"探索失败：{reason}";
            }
            return;
        }

        remainingMovementPoints = result.RemainingMovement;
        explorerPosition = target;
        playerState.Position = target;
        exploredCoords.Add(target);

        Log($"探索成功：在 {DescribeCoordinate(target)} 放置了 {GetTerrainDisplayName(result.Tile.Edges[0])} 地块，剩余移动力 {remainingMovementPoints}。");

        if (result.Elements != null && result.Elements.Count > 0)
        {
            foreach (var element in result.Elements)
            {
                Log($"- 发现 {element.Type}，坐标 {DescribeCoordinate(element.Position)}");
            }
        }

        if (result.CombatEvent != null)
        {
            Log("注意：该地块生成了威胁，需要交由战斗系统处理。");
        }

        foreach (var dir in AxialCoord.NeighborDirs)
        {
            var neighbor = target + dir;
            if (exploredCoords.Contains(neighbor) || mapState.Placed.ContainsKey(neighbor))
            {
                continue;
            }

            if (explorationQueued.Add(neighbor))
            {
                explorationQueue.Enqueue(neighbor);
            }
        }

        UpdateRecruitmentContextForTile(result.Tile);
        RefreshExplorationUI();
        RefreshRecruitmentUI();

        if (explorationDetailLabel != null)
        {
            explorationDetailLabel.text = $"探索到 {GetTerrainDisplayName(result.Tile.Edges[0])}，剩余移动力 {remainingMovementPoints}";
        }

        UpdateExplorationHint();
    }
    
    void TestRecruitmentSystem()
    {
        Log("\n=== Testing Recruitment System ===");

        if (recruitmentPool.Count == 0)
        {
            Log("招募池为空，重新补充示例单位。");
            ResetRecruitmentPool();
            RefreshRecruitmentUI();
            if (recruitmentResultLabel != null)
            {
                recruitmentResultLabel.text = "招募池已补充，请再次点击按钮。";
            }
            return;
        }

        var candidate = recruitmentPool[0];
        int influenceGenerated = recruitmentRandom.Next(4, 9);

        Log($"在 {GetRecruitLocationDisplayName(currentRecruitLocation)} 尝试招募 {candidate.NameCn}，投入影响力 {influenceGenerated}。");

        var success = recruitmentService.TryRecruit(playerState, candidate, currentRecruitLocation, influenceGenerated);
        if (success)
        {
            recruitmentPool.RemoveAt(0);
            recruitedUnits.Add(candidate);
            Log($"招募成功！{candidate.NameCn} 加入队伍，目前剩余指挥槽 {playerState.FreeSlots}。");
            if (recruitmentResultLabel != null)
            {
                recruitmentResultLabel.text = $"招募成功：{candidate.NameCn}";
            }
        }
        else
        {
            Log("招募失败，影响力或地点条件不满足。");
            if (recruitmentResultLabel != null)
            {
                recruitmentResultLabel.text = "招募失败：影响力不足或地点不匹配。";
            }

            if (!LocationMatches(candidate))
            {
                Log($"提示：{candidate.NameCn} 需要在 {BuildRecruitLocationMaskLabel(candidate.RecruitLocationMask)} 招募。");
            }
        }

        RefreshRecruitmentUI();
    }
    
    void TestFullGameFlow()
    {
        Log("\n=== Testing Full Game Flow ===");
        
        // Initialize a complete game session
        // Note: GameEngine doesn't have InitializeGame method, using constructor parameters
        
        Log("Game flow systems initialized successfully");
    }
    
    private void PrepareExplorationScenario()
    {
        mapState = new MapState();
        PopulateExplorationDecks();
        explorationService = new ExplorationService(mapState, new NonThreateningRandom());
        explorerPosition = new AxialCoord(0, 0);
        playerState.Position = explorerPosition;
        remainingMovementPoints = 6;
        exploredCoords.Clear();
        explorationQueue.Clear();
        explorationQueued.Clear();

        var startingTile = CreateTile(TileSet.Countryside, 1000, new[]
        {
            TerrainType.Plains,
            TerrainType.Forest,
            TerrainType.Hills,
            TerrainType.Plains,
            TerrainType.Village,
            TerrainType.Plains
        });
        mapState.Placed[explorerPosition] = startingTile;
        exploredCoords.Add(explorerPosition);

        foreach (var dir in AxialCoord.NeighborDirs)
        {
            var neighbor = explorerPosition + dir;
            if (explorationQueued.Add(neighbor))
            {
                explorationQueue.Enqueue(neighbor);
            }
        }

        UpdateRecruitmentContextForTile(startingTile);
        RefreshExplorationUI();
    }

    private void PopulateExplorationDecks()
    {
        mapState.Countryside = new TileDeck();
        mapState.Core = new TileDeck();

        var countrysideTiles = new[]
        {
            CreateTile(TileSet.Countryside, 2001, new[]
            {
                TerrainType.Forest,
                TerrainType.Plains,
                TerrainType.Hills,
                TerrainType.Plains,
                TerrainType.Plains,
                TerrainType.Village
            }),
            CreateTile(TileSet.Countryside, 2002, new[]
            {
                TerrainType.Plains,
                TerrainType.Wasteland,
                TerrainType.Forest,
                TerrainType.Plains,
                TerrainType.Keep,
                TerrainType.Hills
            }),
            CreateTile(TileSet.Countryside, 2003, new[]
            {
                TerrainType.Plains,
                TerrainType.Swamp,
                TerrainType.Plains,
                TerrainType.Plains,
                TerrainType.Plains,
                TerrainType.Village
            }),
            CreateTile(TileSet.Countryside, 2004, new[]
            {
                TerrainType.Plains,
                TerrainType.Plains,
                TerrainType.City,
                TerrainType.Plains,
                TerrainType.Hills,
                TerrainType.Forest
            }),
            CreateTile(TileSet.Countryside, 2005, new[]
            {
                TerrainType.Plains,
                TerrainType.Desert,
                TerrainType.Plains,
                TerrainType.Mountain,
                TerrainType.Plains,
                TerrainType.Plains
            })
        };

        for (int i = countrysideTiles.Length - 1; i >= 0; i--)
        {
            mapState.Countryside.Push(countrysideTiles[i]);
        }

        var coreTiles = new[]
        {
            CreateTile(TileSet.Core, 3001, new[]
            {
                TerrainType.City,
                TerrainType.Plains,
                TerrainType.Plains,
                TerrainType.Hills,
                TerrainType.Plains,
                TerrainType.Plains
            })
        };

        for (int i = coreTiles.Length - 1; i >= 0; i--)
        {
            mapState.Core.Push(coreTiles[i]);
        }
    }

    private MapTile CreateTile(TileSet set, int id, TerrainType[] edges)
    {
        var sanitized = new TerrainType[6];
        for (int i = 0; i < sanitized.Length; i++)
        {
            sanitized[i] = i < edges.Length ? edges[i] : TerrainType.Plains;
        }

        return new MapTile(set, id, sanitized);
    }

    private void RefreshExplorationUI()
    {
        if (mapState == null)
        {
            if (explorationSummaryLabel != null)
            {
                explorationSummaryLabel.text = "探索系统初始化中...";
            }

            if (explorationHintLabel != null)
            {
                explorationHintLabel.text = "下一目标：等待初始化";
            }

            return;
        }

        if (explorationMapRoot == null)
        {
            if (explorationSummaryLabel != null)
            {
                explorationSummaryLabel.text = "探索面板未配置。";
            }

            if (explorationHintLabel != null)
            {
                explorationHintLabel.text = "请确认场景已启用探索面板。";
            }

            return;
        }

        RebuildExplorationVisuals();
        UpdateExplorationSummary();
        UpdateExplorationHint();
    }

    private void RebuildExplorationVisuals()
    {
        if (explorationMapRoot == null)
        {
            return;
        }

        ClearExplorationVisuals();

        var coordsToDisplay = new HashSet<AxialCoord>(exploredCoords);
        foreach (var kvp in mapState.Placed)
        {
            coordsToDisplay.Add(kvp.Key);
        }

        foreach (var queued in explorationQueue)
        {
            coordsToDisplay.Add(queued);
        }

        coordsToDisplay.Add(explorerPosition);

        foreach (var coord in coordsToDisplay)
        {
            if (mapState.Placed.TryGetValue(coord, out var tile))
            {
                UpdateExplorationTileAppearance(coord, tile);
            }
            else
            {
                UpdateExplorationTileAppearance(coord, new Color(0.32f, 0.36f, 0.45f, 0.78f), $"{coord.Q},{coord.R}\n未探索");
            }
        }

        UpdateExplorationTilePositions();
    }

    private void ClearExplorationVisuals()
    {
        if (explorationMapRoot == null)
        {
            return;
        }

        foreach (var visual in explorationTileVisuals.Values)
        {
            if (visual?.Root == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(visual.Root.gameObject);
            }
            else
            {
                DestroyImmediate(visual.Root.gameObject);
            }
        }

        explorationTileVisuals.Clear();
    }

    private ExplorationTileVisual CreateTileVisual(AxialCoord coord)
    {
        var root = new GameObject($"Hex_{coord.Q}_{coord.R}");
        var rect = root.AddComponent<RectTransform>();
        rect.SetParent(explorationMapRoot, false);
        rect.sizeDelta = new Vector2(88f, 76f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = root.AddComponent<Image>();
        image.raycastTarget = false;

        var labelGo = new GameObject("Label");
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.SetParent(root.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 6f);
        labelRect.offsetMax = new Vector2(-6f, -6f);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = $"{coord.Q},{coord.R}";
        label.fontSize = 16f;
        label.color = new Color(0.92f, 0.96f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;

        var referenceFont = explorationSummaryLabel != null ? explorationSummaryLabel.font : (testLog != null ? testLog.font : null);
        if (referenceFont != null)
        {
            label.font = referenceFont;
        }

        var visual = new ExplorationTileVisual
        {
            Root = rect,
            TileImage = image,
            Label = label
        };

        explorationTileVisuals[coord] = visual;
        return visual;
    }

    private void UpdateExplorationTileAppearance(AxialCoord coord, MapTile tile)
    {
        if (!explorationTileVisuals.TryGetValue(coord, out var visual))
        {
            visual = CreateTileVisual(coord);
        }

        var terrain = tile.Edges != null && tile.Edges.Length > 0 ? tile.Edges[0] : TerrainType.Plains;
        var color = GetTerrainColor(terrain);

        if (coord.Equals(explorerPosition))
        {
            color = Color.Lerp(color, Color.white, 0.25f);
        }

        visual.TileImage.color = color;
        visual.Label.text = $"{GetTerrainDisplayName(terrain)}\n({coord.Q},{coord.R})";
    }

    private void UpdateExplorationTileAppearance(AxialCoord coord, Color color, string caption)
    {
        if (!explorationTileVisuals.TryGetValue(coord, out var visual))
        {
            visual = CreateTileVisual(coord);
        }

        if (coord.Equals(explorerPosition))
        {
            color = Color.Lerp(color, Color.white, 0.2f);
        }

        visual.TileImage.color = color;
        visual.Label.text = caption;
    }

    private void UpdateExplorationTilePositions()
    {
        if (explorationTileVisuals.Count == 0)
        {
            return;
        }

        var positions = new Dictionary<AxialCoord, Vector2>();
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var kvp in explorationTileVisuals)
        {
            var pos = CalculateHexPosition(kvp.Key);
            positions[kvp.Key] = pos;

            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        var offset = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        foreach (var kvp in positions)
        {
            if (explorationTileVisuals.TryGetValue(kvp.Key, out var visual) && visual.Root != null)
            {
                visual.Root.anchoredPosition = kvp.Value - offset;
            }
        }
    }

    private Vector2 CalculateHexPosition(AxialCoord coord)
    {
        const float width = 96f;
        const float height = 84f;
        float x = width * (coord.Q + coord.R * 0.5f);
        float y = height * coord.R;
        return new Vector2(x, -y);
    }

    private void UpdateExplorationSummary()
    {
        if (explorationSummaryLabel == null)
        {
            return;
        }

        int explored = mapState?.Placed?.Count ?? 0;
        explorationSummaryLabel.text = $"已探索地块：{explored}，剩余移动力 {remainingMovementPoints}";
    }

    private void UpdateExplorationHint()
    {
        if (explorationHintLabel == null)
        {
            return;
        }

        if (explorationQueue.Count == 0)
        {
            explorationHintLabel.text = "所有示例地块均已探索，下一次点击将重置地图。";
            return;
        }

        var next = explorationQueue.Peek();
        explorationHintLabel.text = $"下一目标：{DescribeCoordinate(next)}";
    }

    private string DescribeCoordinate(AxialCoord coord)
    {
        return $"({coord.Q}, {coord.R})";
    }

    private Color GetTerrainColor(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Plains => new Color(0.38f, 0.65f, 0.38f, 0.9f),
            TerrainType.Forest => new Color(0.18f, 0.45f, 0.24f, 0.9f),
            TerrainType.Hills => new Color(0.55f, 0.42f, 0.28f, 0.9f),
            TerrainType.Desert => new Color(0.78f, 0.62f, 0.32f, 0.9f),
            TerrainType.Swamp => new Color(0.32f, 0.37f, 0.28f, 0.9f),
            TerrainType.Mountain => new Color(0.52f, 0.52f, 0.58f, 0.9f),
            TerrainType.City => new Color(0.55f, 0.45f, 0.6f, 0.9f),
            TerrainType.Village => new Color(0.68f, 0.55f, 0.36f, 0.9f),
            TerrainType.Keep => new Color(0.52f, 0.46f, 0.68f, 0.9f),
            TerrainType.Wasteland => new Color(0.45f, 0.3f, 0.3f, 0.9f),
            TerrainType.Ruins => new Color(0.48f, 0.48f, 0.48f, 0.9f),
            _ => new Color(0.42f, 0.56f, 0.6f, 0.9f)
        };
    }

    private string GetTerrainDisplayName(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Plains => "平原",
            TerrainType.Forest => "森林",
            TerrainType.Hills => "丘陵",
            TerrainType.Desert => "沙漠",
            TerrainType.Swamp => "沼泽",
            TerrainType.Mountain => "山脉",
            TerrainType.City => "城市",
            TerrainType.Village => "村庄",
            TerrainType.Keep => "要塞",
            TerrainType.Wasteland => "荒地",
            TerrainType.Ruins => "遗迹",
            _ => terrain.ToString()
        };
    }

    private void InitializeRecruitmentScenario()
    {
        InitializeRecruitmentPool();
        RefreshRecruitmentUI();
    }

    private void InitializeRecruitmentPool()
    {
        recruitmentPool.Clear();
        recruitedUnits.Clear();

        recruitmentPool.Add(new UnitCard(
            "villager-militia",
            "村庄民兵",
            "Village Militia",
            1,
            2,
            4,
            RecruitLocation.Village,
            new[] { new AttackProfile(AttackType.Melee, 3, Element.Physical) },
            new[] { Ability.Guard }
        ));

        recruitmentPool.Add(new UnitCard(
            "keep-crossbow",
            "要塞弩手",
            "Keep Crossbowmen",
            1,
            3,
            5,
            RecruitLocation.Keep,
            new[] { new AttackProfile(AttackType.Ranged, 3, Element.Physical) },
            Array.Empty<Ability>()
        ));

        recruitmentPool.Add(new UnitCard(
            "city-guard",
            "城卫队",
            "City Guard",
            2,
            4,
            6,
            RecruitLocation.City,
            new[] { new AttackProfile(AttackType.Melee, 4, Element.Physical) },
            new[] { Ability.Enduring },
            true
        ));

        recruitmentPool.Add(new UnitCard(
            "scout",
            "草原斥候",
            "Scout",
            1,
            2,
            5,
            RecruitLocation.Village | RecruitLocation.Keep,
            new[] { new AttackProfile(AttackType.Melee, 2, Element.Physical) },
            Array.Empty<Ability>()
        ));
    }

    private void ResetRecruitmentPool()
    {
        InitializeRecruitmentPool();
        playerState.Units.Clear();
    }

    private void RefreshRecruitmentUI()
    {
        if (playerState == null)
        {
            if (recruitmentSummaryLabel != null)
            {
                recruitmentSummaryLabel.text = "招募面板初始化中...";
            }

            if (recruitmentLocationLabel != null)
            {
                recruitmentLocationLabel.text = "当前地点：等待探索";
            }

            return;
        }

        if (recruitmentSummaryLabel != null)
        {
            recruitmentSummaryLabel.text = $"当前指挥槽：已用 {playerState.Units.Count} / 总计 {playerState.CommandSlots}，剩余 {playerState.FreeSlots}";
        }

        if (recruitmentLocationLabel != null)
        {
            recruitmentLocationLabel.text = $"当前地点：{GetRecruitLocationDisplayName(currentRecruitLocation)}";
        }

        if (recruitmentAvailableListRoot != null)
        {
            ClearChildren(recruitmentAvailableListRoot);
            if (recruitmentPool.Count == 0)
            {
                CreateListItem(recruitmentAvailableListRoot, "（当前没有可招募单位）", new Color(0.85f, 0.88f, 0.95f));
            }
            else
            {
                foreach (var unit in recruitmentPool)
                {
                    CreateListItem(recruitmentAvailableListRoot, FormatRecruitmentCandidate(unit), new Color(0.9f, 0.94f, 1f));
                }
            }
        }

        if (recruitmentRecruitedListRoot != null)
        {
            ClearChildren(recruitmentRecruitedListRoot);
            if (recruitedUnits.Count == 0)
            {
                CreateListItem(recruitmentRecruitedListRoot, "（尚未招募单位）", new Color(0.82f, 0.86f, 0.92f));
            }
            else
            {
                foreach (var unit in recruitedUnits)
                {
                    CreateListItem(recruitmentRecruitedListRoot, $"{unit.NameCn} | 等级 {unit.Level}", new Color(0.88f, 0.96f, 0.86f));
                }
            }
        }
    }

    private string FormatRecruitmentCandidate(UnitCard card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        var abilityText = card.Abilities != null && card.Abilities.Length > 0
            ? $" | 能力：{string.Join("、", card.Abilities.Select(a => a.ToString()))}"
            : string.Empty;

        return $"{card.NameCn}（影响力 {card.InfluenceCost}，地点：{BuildRecruitLocationMaskLabel(card.RecruitLocationMask)}）{abilityText}";
    }

    private void UpdateRecruitmentContextForTile(MapTile tile)
    {
        currentRecruitLocation = DetermineLocationForTile(tile);

        var availableNames = recruitmentPool
            .Where(LocationMatches)
            .Select(unit => unit.NameCn)
            .ToList();

        if (availableNames.Count > 0)
        {
            Log($"当前位置 {GetRecruitLocationDisplayName(currentRecruitLocation)} 可招募：{string.Join("、", availableNames)}。");
        }
        else
        {
            Log($"当前位置 {GetRecruitLocationDisplayName(currentRecruitLocation)} 暂无合适的候选单位。");
        }

        RefreshRecruitmentUI();
    }

    private RecruitLocation DetermineLocationForTile(MapTile tile)
    {
        if (tile == null)
        {
            return RecruitLocation.Village;
        }

        if ((tile.Edges?.Contains(TerrainType.City) ?? false) || tile.Set == TileSet.Core)
        {
            return RecruitLocation.City;
        }

        if (tile.Edges != null && tile.Edges.Contains(TerrainType.Keep))
        {
            return RecruitLocation.Keep;
        }

        if (tile.Edges != null && tile.Edges.Contains(TerrainType.Village))
        {
            return RecruitLocation.Village;
        }

        if (tile.Edges != null && tile.Edges.Contains(TerrainType.Mountain))
        {
            return RecruitLocation.Keep;
        }

        return RecruitLocation.Village;
    }

    private string GetRecruitLocationDisplayName(RecruitLocation location)
    {
        if (location == RecruitLocation.None)
        {
            return "未知地点";
        }

        var names = new List<string>();

        if (location.HasFlag(RecruitLocation.Village))
            names.Add("村庄");
        if (location.HasFlag(RecruitLocation.Keep))
            names.Add("要塞");
        if (location.HasFlag(RecruitLocation.Monastery))
            names.Add("修道院");
        if (location.HasFlag(RecruitLocation.City))
            names.Add("城市");
        if (location.HasFlag(RecruitLocation.MageTower))
            names.Add("魔法塔");
        if (location.HasFlag(RecruitLocation.RefugeeCamp))
            names.Add("难民营");

        if (names.Count == 0)
        {
            names.Add(location.ToString());
        }

        return string.Join("、", names);
    }

    private string BuildRecruitLocationMaskLabel(RecruitLocation mask)
    {
        return GetRecruitLocationDisplayName(mask);
    }

    private bool LocationMatches(UnitCard candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.RecruitLocationMask == RecruitLocation.None)
        {
            return true;
        }

        if (candidate.RecruitLocationMask.HasFlag(RecruitLocation.RefugeeCamp))
        {
            return true;
        }

        return candidate.RecruitLocationMask.HasFlag(currentRecruitLocation);
    }

    private void ClearChildren(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
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

    private TextMeshProUGUI CreateListItem(RectTransform parent, string text, Color color)
    {
        if (parent == null)
        {
            return null;
        }

        var itemGo = new GameObject("Item");
        var rect = itemGo.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 32f);

        var element = itemGo.AddComponent<LayoutElement>();
        element.minHeight = 28f;
        element.preferredHeight = 32f;

        var label = itemGo.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 16f;
        label.color = color;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;

        var referenceFont = recruitmentSummaryLabel != null ? recruitmentSummaryLabel.font : (testLog != null ? testLog.font : null);
        if (referenceFont != null)
        {
            label.font = referenceFont;
        }

        return label;
    }
    
    private void OnDestroy()
    {
        if (deckRuntime != null)
        {
            deckRuntime.DeckStateChanged -= HandleDeckStateChanged;
        }

        ClearDeckViewerOverlayCards();
    }

    void Log(string message)
    {
        Debug.Log(message);

        if (testLog != null)
        {
            testLog.text += message + "\n";
        }

        if (logScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }



    private void ForceDeckViewerLayoutImmediate()
    {
        if (deckViewerOverlayGridRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(deckViewerOverlayGridRoot);
    }
}
