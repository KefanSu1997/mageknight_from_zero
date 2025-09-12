using UnityEngine;

public class HandManager : MonoBehaviour
{
    public DeckRuntime deck;          // Inspector 拖入
    public CardRuntime cardPrefab;    // 拖 CardView.prefab
    public Transform handRoot;        // Canvas 下水平布局空物体

    void Awake()
    {
        if (deck == null)
        {
            Debug.LogError("HandManager: DeckRuntime not assigned!");
        }
        if (cardPrefab == null)
        {
            Debug.LogError("HandManager: CardRuntime prefab not assigned!");
        }
        if (handRoot == null)
        {
            Debug.LogWarning("HandManager: handRoot not assigned, defaulting to this transform.");
            handRoot = transform;
        }
    }

    void Start () => Draw(5);         // 开局抽五张

    public void Draw(int n = 1)
    {
        if (deck == null || cardPrefab == null)
        {
            Debug.LogError("Deck or Card Prefab not set up in HandManager, cannot draw cards.");
            return;
        }

        for (int i = 0; i < n; i++)
        {
            var data = deck.Draw();
            if (data == null)
            {
                Debug.LogWarning("HandManager: Deck is empty, cannot draw more cards.");
                return;             // deck ran out
            }
            Debug.Log($"HandManager drawing card: {data.name}");
            var view = Instantiate(cardPrefab, handRoot);
            view.Init(data);
        }
        
        // 重新排列手牌
        ArrangeCards();
    }
    
    private void ArrangeCards()
    {
        if (handRoot == null) return;
        
        int cardCount = handRoot.childCount;
        if (cardCount <= 0) return;
        
        // 定义卡牌布局和间距
        float cardWidth = cardPrefab.GetComponent<RectTransform>()?.rect.width ?? 150f;
        float spacing = 30f; // 卡牌之间的间距
        
        // 计算总宽度，确保手牌水平居中
        float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
        
        // 计算起始位置（水平居中放置）
        float startX = -totalWidth / 2f + cardWidth / 2f;
        
        // 排列每张卡牌
        for (int i = 0; i < cardCount; i++)
        {
            Transform card = handRoot.GetChild(i);
            
            // 计算位置
            float xPos = startX + (i * (cardWidth + spacing));
            
            // 设置位置
            RectTransform rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(xPos, 0f);
            }
            else
            {
                card.localPosition = new Vector3(xPos, 0f, 0f);
            }
            
            // 设置层级，让卡牌略微错开显示
            card.SetSiblingIndex(i);
        }
    }
    
    // 供Unity编辑器调用的方法，用于手动重新排列当前手牌
    [ContextMenu("Arrange Cards")]
    public void LayoutCardsForEditor()
    {
        ArrangeCards();
    }
}
