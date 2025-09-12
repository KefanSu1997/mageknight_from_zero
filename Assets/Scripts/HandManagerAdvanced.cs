using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandManagerAdvanced : MonoBehaviour
{
    public DeckRuntime deck;
    public CardRuntime cardPrefab;
    public Transform handRoot;
    public GameObject buttonPanel;

    [Header("Debug Buttons")]
    public Button drawButton;
    public Button discardButton;
    public Button playButton;
    public Button shuffleButton;
    public Button recycleButton;
    public Button debugButton;

    public Text deckCountText;
    public Text discardCountText;

    private CardRuntime selectedCard;
    private List<CardRuntime> handCards = new List<CardRuntime>();

    void Start()
    {
        if (handRoot == null)
            handRoot = transform;

        Draw(5);
        SetupDebugButtons();
    }

    void Update()
    {
        UpdateDebugUI();
    }

    private void SetupDebugButtons()
    {
        if (drawButton != null)
            drawButton.onClick.AddListener(() => Draw(1));
        
        if (discardButton != null)
            discardButton.onClick.AddListener(() => DiscardSelected());
        
        if (playButton != null)
            playButton.onClick.AddListener(() => PlaySelected());
        
        if (shuffleButton != null)
            shuffleButton.onClick.AddListener(() => ShuffleDeck());
        
        if (recycleButton != null)
            recycleButton.onClick.AddListener(() => RecycleDiscard());
    }

    public void Draw(int n = 1)
    {
        if (deck == null || cardPrefab == null)
        {
            Debug.LogError("Deck or Card Prefab not assigned!");
            return;
        }

        for (int i = 0; i < n; i++)
        {
            var data = deck.Draw();
            if (data == null)
            {
                Debug.LogWarning("牌组已空，无法继续抽牌！");
                return;
            }

            var card = Instantiate(cardPrefab, handRoot);
            card.Init(data);
            card.GetComponent<Button>().onClick.AddListener(() => SelectCard(card));
            handCards.Add(card);
        }

        ArrangeCards();
    }

    public void SelectCard(CardRuntime card)
    {
        if (selectedCard != null)
        {
            selectedCard.SetSelected(false);
        }
        
        selectedCard = card;
        selectedCard.SetSelected(true);
        Debug.Log($"选择了卡牌: {card.Data.name}");
    }

    public void DiscardSelected()
    {
        if (selectedCard == null)
        {
            Debug.LogWarning("请先选择一张要弃掉的牌！");
            return;
        }

        CardSO cardData = selectedCard.Data;
        handCards.Remove(selectedCard);
        Destroy(selectedCard.gameObject);
        
        deck.Discard(cardData);
        
        selectedCard = null;
        ArrangeCards();
        Debug.Log($"弃掉了卡牌: {cardData.name}");
    }

    public void PlaySelected()
    {
        if (selectedCard == null)
        {
            Debug.LogWarning("请先选择一张要打出的牌！");
            return;
        }

        CardSO cardData = selectedCard.Data;
        handCards.Remove(selectedCard);
        Destroy(selectedCard.gameObject);
        
        deck.Discard(cardData);
        
        Debug.Log($"打出了卡牌: {cardData.name}，效果：模拟卡牌效果已触发");
        
        selectedCard = null;
        ArrangeCards();
    }

    public void ShuffleDeck()
    {
        if (deck != null)
        {
            deck.Shuffle();
            Debug.Log("牌组已洗牌！");
        }
    }

    public void RecycleDiscard()
    {
        if (deck != null)
        {
            // 调用内部的回收逻辑
            var total = deck.discard.Count;
            if (total == 0)
            {
                Debug.Log("弃牌堆为空，无需回收！");
                return;
            }

            // 简单实现：清空弃牌并重新洗牌
            deck.Recycle();
            Debug.Log($"已回收 {total} 张弃牌到牌组！");
        }
    }

    private void ArrangeCards()
    {
        if (handRoot == null) return;
        
        int cardCount = handCards.Count;
        if (cardCount <= 0) return;
        
        float cardWidth = 160f;
        float spacing = 40f;
        
        float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
        float startX = -totalWidth / 2f + cardWidth / 2f;
        
        for (int i = 0; i < cardCount; i++)
        {
            CardRuntime card = handCards[i];
            RectTransform rectTransform = card.GetComponent<RectTransform>();
            
            if (rectTransform != null)
            {
                float xPos = startX + (i * (cardWidth + spacing));
                rectTransform.anchoredPosition = new Vector2(xPos, 0f);
            }
        }
    }

    private void UpdateDebugUI()
    {
        if (deckCountText != null && deck != null)
            deckCountText.text = $"牌组: {deck.drawPile?.Count ?? 0}";
        
        if (discardCountText != null && deck != null)
            discardCountText.text = $"弃牌: {deck.discard?.Count ?? 0}";
    }

    [ContextMenu("Test Draw 1 Card")]
    public void TestDraw()
    {
        Draw(1);
    }

    [ContextMenu("Test Shuffle Deck")]
    public void TestShuffle()
    {
        ShuffleDeck();
    }

    [ContextMenu("Test Recycle Discard")]
    public void TestRecycle()
    {
        RecycleDiscard();
    }
}