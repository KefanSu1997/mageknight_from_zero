using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 让手牌按照 SlotContainer 中的占位槽贴合展示，并提供平滑过渡。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckUiHandPresenter : MonoBehaviour
{
    [SerializeField] private HandManager handManager;
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private float followSpeed = 18f;
    [SerializeField] private float fallbackSpacing = 160f;
    [SerializeField] private bool autoAddAnimations = true;

    private RectTransform handRoot;
    private readonly List<RectTransform> slotRects = new();
    private readonly List<RectTransform> cardRects = new();
    private readonly Dictionary<RectTransform, int> cardLayoutOrder = new();
    private bool initialized;

    private void Awake()
    {
        handRoot = transform as RectTransform;
        if (handRoot == null)
        {
            Debug.LogError("DeckUiHandPresenter 需要挂载在 RectTransform 上。");
        }
    }

    public void Configure(HandManager manager, RectTransform slotsRoot)
    {
        handManager = manager;
        slotContainer = slotsRoot;
        handRoot = transform as RectTransform;
        RefreshSlots();
        initialized = false;
    }

    public void ForceSnapLayout()
    {
        RefreshSlots();
        cardLayoutOrder.Clear();
        SnapAllCardsImmediate();
    }

    private void OnEnable()
    {
        RefreshSlots();
    }

    private void LateUpdate()
    {
        if (handRoot == null)
        {
            return;
        }

        AlignCards();
    }

    private void RefreshSlots()
    {
        slotRects.Clear();
        if (slotContainer == null)
        {
            return;
        }

        for (int i = 0; i < slotContainer.childCount; i++)
        {
            if (slotContainer.GetChild(i) is RectTransform slot)
            {
                slotRects.Add(slot);
            }
        }
    }

    private void CollectCards()
    {
        cardRects.Clear();
        if (handRoot == null)
        {
            return;
        }

        for (int i = 0; i < handRoot.childCount; i++)
        {
            if (handRoot.GetChild(i) is RectTransform rect)
            {
                cardRects.Add(rect);

                if (autoAddAnimations && rect.GetComponent<CardRuntime>() != null && rect.GetComponent<DeckUiHandCardAnimation>() == null)
                {
                    var animator = rect.gameObject.AddComponent<DeckUiHandCardAnimation>();
                    animator.TriggerReveal(true);
                }

                if (!cardLayoutOrder.ContainsKey(rect))
                {
                    cardLayoutOrder[rect] = cardLayoutOrder.Count;
                }
            }
        }

        if (cardLayoutOrder.Count != cardRects.Count)
        {
            // 清理已移除的手牌引用
            var keys = new List<RectTransform>(cardLayoutOrder.Keys);
            foreach (var key in keys)
            {
                if (!cardRects.Contains(key))
                {
                    cardLayoutOrder.Remove(key);
                }
            }
        }

        if (cardRects.Count > 1)
        {
            cardRects.Sort(CompareByLayoutOrder);

            for (int i = 0; i < cardRects.Count; i++)
            {
                var rect = cardRects[i];
                if (rect == null)
                {
                    continue;
                }

                cardLayoutOrder[rect] = i;
            }
        }
    }

    private void AlignCards()
    {
        CollectCards();
        if (cardRects.Count == 0)
        {
            return;
        }

        if (!initialized)
        {
            SnapAllCardsImmediate();
            return;
        }

        float lerpFactor = Time.unscaledDeltaTime * followSpeed;
        for (int i = 0; i < cardRects.Count; i++)
        {
            MoveCardTowardsSlot(cardRects[i], i, lerpFactor);
        }
    }

    private void SnapAllCardsImmediate()
    {
        CollectCards();
        for (int i = 0; i < cardRects.Count; i++)
        {
            SnapCardToSlot(cardRects[i], i);
        }

        initialized = true;
    }

    private void MoveCardTowardsSlot(RectTransform card, int index, float lerpFactor)
    {
        if (card == null)
        {
            return;
        }

        EnsureCardAnchor(card);
        int layoutIndex = GetLayoutIndex(card, index);
        int totalCards = cardRects.Count;
        Vector2 baseTarget = GetTargetPosition(layoutIndex);
        float baseAngle = GetCardAngle(layoutIndex, totalCards);
        float baseScale = GetCardScale(layoutIndex, totalCards);

        var animator = card.GetComponent<DeckUiHandCardAnimation>();
        Vector2 animatedOffset = Vector2.zero;
        float angleOffset = 0f;
        float scaleMultiplier = 1f;
        bool shouldOverlay = false;

        if (animator != null)
        {
            animatedOffset = animator.PositionOffset;
            angleOffset = animator.AngleOffset;
            scaleMultiplier = animator.ScaleMultiplier;
            shouldOverlay = animator.ShouldOverlay;
        }

        float t = Mathf.Clamp01(lerpFactor);
        if (!Application.isPlaying)
        {
            t = 1f;
        }
        Vector2 finalTarget = baseTarget + animatedOffset;
        Vector2 current = card.anchoredPosition;
        card.anchoredPosition = Vector2.Lerp(current, finalTarget, t);

        float finalAngle = baseAngle + angleOffset;
        float yAngle = animator != null ? animator.YRotationOffset : 0f;
        var targetRotation = Quaternion.Euler(0f, yAngle, finalAngle);
        card.localRotation = Quaternion.Slerp(card.localRotation, targetRotation, t);

        float finalScale = baseScale * scaleMultiplier;
        card.localScale = Vector3.Lerp(card.localScale, Vector3.one * finalScale, t);

        AdjustSiblingOrder(card, layoutIndex, shouldOverlay);
    }

    private void SnapCardToSlot(RectTransform card, int index)
    {
        if (card == null)
        {
            return;
        }

        EnsureCardAnchor(card);
        int layoutIndex = GetLayoutIndex(card, index);
        int totalCards = cardRects.Count;
        Vector2 baseTarget = GetTargetPosition(layoutIndex);
        float baseAngle = GetCardAngle(layoutIndex, totalCards);
        float baseScale = GetCardScale(layoutIndex, totalCards);

        var animator = card.GetComponent<DeckUiHandCardAnimation>();
        Vector2 animatedOffset = animator != null ? animator.PositionOffset : Vector2.zero;
        float angleOffset = animator != null ? animator.AngleOffset : 0f;
        float scaleMultiplier = animator != null ? animator.ScaleMultiplier : 1f;
        bool shouldOverlay = animator != null && animator.ShouldOverlay;

        card.anchoredPosition = baseTarget + animatedOffset;
        float yAngle = animator != null ? animator.YRotationOffset : 0f;
        card.localRotation = Quaternion.Euler(0f, yAngle, baseAngle + angleOffset);
        card.localScale = Vector3.one * (baseScale * scaleMultiplier);

        AdjustSiblingOrder(card, layoutIndex, shouldOverlay);
    }

    private void EnsureCardAnchor(RectTransform card)
    {
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
        card.pivot = new Vector2(0.5f, 0f);
    }

    private Vector2 GetTargetPosition(int layoutIndex)
    {
        Vector2 target;
        if (slotRects.Count > 0)
        {
            var slot = slotRects[Mathf.Clamp(layoutIndex, 0, slotRects.Count - 1)];
            Vector3 worldPoint = slot.TransformPoint(new Vector3(slot.rect.width * 0.5f, 0f, 0f));
            Vector3 localPoint = handRoot.InverseTransformPoint(worldPoint);
            target = new Vector2(localPoint.x, localPoint.y);
        }
        else
        {
            float totalWidth = (cardRects.Count - 1) * fallbackSpacing;
            float startX = -totalWidth / 2f;
            target = new Vector2(startX + layoutIndex * fallbackSpacing, 0f);
        }

        target.y += GetCardVerticalOffset(layoutIndex, cardRects.Count);
        return target;
    }

    private float GetNormalizedOffset(int index, int total)
    {
        if (total <= 1)
        {
            return 0f;
        }

        float mid = (total - 1) * 0.5f;
        return (index - mid) / Mathf.Max(1f, mid);
    }

    private float GetCardAngle(int index, int total) => 0f;

    private float GetCardVerticalOffset(int index, int total) => 0f;

    private float GetCardScale(int index, int total) => 1f;

    private int GetLayoutIndex(RectTransform card, int fallback)
    {
        if (cardLayoutOrder.TryGetValue(card, out var order))
        {
            return Mathf.Clamp(order, 0, Mathf.Max(0, cardRects.Count - 1));
        }

        return Mathf.Clamp(fallback, 0, Mathf.Max(0, cardRects.Count - 1));
    }

    private int CompareByLayoutOrder(RectTransform a, RectTransform b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int orderA = cardLayoutOrder.TryGetValue(a, out var storedA)
            ? storedA
            : a.GetSiblingIndex();
        int orderB = cardLayoutOrder.TryGetValue(b, out var storedB)
            ? storedB
            : b.GetSiblingIndex();

        int comparison = orderA.CompareTo(orderB);
        if (comparison != 0)
        {
            return comparison;
        }

        return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
    }

    private void AdjustSiblingOrder(RectTransform card, int layoutIndex, bool overlay)
    {
        int desiredIndex = overlay ? cardRects.Count - 1 : Mathf.Clamp(layoutIndex, 0, Mathf.Max(0, cardRects.Count - 1));

        if (card.GetSiblingIndex() != desiredIndex)
        {
            card.SetSiblingIndex(desiredIndex);
        }
    }
}
