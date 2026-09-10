using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    public DeckRuntime deck;          // Inspector 拖入
    public CardRuntime cardPrefab;    // 拖 CardView.prefab

    private bool hasAnchorTemplate;
    private int anchorTemplateCount;
    private readonly List<Vector2> anchorTemplatePositions = new();

    public Transform handRoot;        // Canvas 下水平布局空物体
    [SerializeField] private bool drawOnStart = false;

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

    void Start ()
    {
        if (drawOnStart)
        {
            Draw(5);         // 开局抽五张
        }
    }

    public void ConfigureDrawOnStart(bool shouldDraw) => drawOnStart = shouldDraw;

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

    public void ClearHand(bool sendToDiscard = false)
    {
        if (handRoot == null)
        {
            return;
        }

        var toRemove = new List<Transform>();
        List<CardSO> cardsToDiscard = sendToDiscard ? new List<CardSO>() : null;

        for (int i = handRoot.childCount - 1; i >= 0; i--)
        {
            var child = handRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (sendToDiscard)
            {
                var runtime = child.GetComponent<CardRuntime>();
                if (runtime?.Data != null)
                {
                    cardsToDiscard?.Add(runtime.Data);
                }
            }

            toRemove.Add(child);
        }

        foreach (var child in toRemove)
        {
            if (child == null)
            {
                continue;
            }

            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        if (sendToDiscard && deck != null && cardsToDiscard != null)
        {
            foreach (var card in cardsToDiscard)
            {
                deck.Discard(card);
            }
        }
    }


    public void ResetHand(int cardsToDraw = 5)
    {
        ClearHand(sendToDiscard: true);
        Draw(cardsToDraw);
    }



    private void ArrangeCards()
    {
        if (handRoot == null)
        {
            return;
        }

        var handRootRect = handRoot as RectTransform;
        var cardRects = new List<RectTransform>();
        for (int i = 0; i < handRoot.childCount; i++)
        {
            var child = handRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            var go = child.gameObject;
            if (go == null || !go.activeSelf)
            {
                continue;
            }

            if (child is RectTransform rect)
            {
                cardRects.Add(rect);
            }
        }

        if (cardRects.Count == 0)
        {
            return;
        }

        if (hasAnchorTemplate && anchorTemplateCount == cardRects.Count && anchorTemplatePositions.Count == cardRects.Count)
        {
            for (int i = 0; i < cardRects.Count; i++)
            {
                var rect = cardRects[i];
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.localScale = Vector3.one;
                rect.anchoredPosition = anchorTemplatePositions[i];
                rect.SetSiblingIndex(i);
            }
            return;
        }

        float cardWidth = cardPrefab != null
            ? cardPrefab.GetComponent<RectTransform>()?.rect.width ?? 150f
            : 150f;

        if (cardRects[0].rect.width > 0f)
        {
            cardWidth = cardRects[0].rect.width;
        }

        const float spacing = 30f;
        float totalWidth = (cardRects.Count * cardWidth) + ((cardRects.Count - 1) * spacing);
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < cardRects.Count; i++)
        {
            var rect = cardRects[i];
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0f);
            rect.SetSiblingIndex(i);
        }

        anchorTemplatePositions.Clear();
        for (int i = 0; i < cardRects.Count; i++)
        {
            var rect = cardRects[i];
            if (rect != null)
            {
                anchorTemplatePositions.Add(rect.anchoredPosition);
            }
        }
        anchorTemplateCount = cardRects.Count;
        hasAnchorTemplate = true;
    }




    
    // 供Unity编辑器调用的方法，用于手动重新排列当前手牌
    [ContextMenu("Arrange Cards")]
    public void LayoutCardsForEditor()
    {
        ArrangeCards();
    }
}
