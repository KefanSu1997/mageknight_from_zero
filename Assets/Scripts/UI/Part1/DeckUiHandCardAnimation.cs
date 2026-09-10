using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 控制手牌在悬停、按压与初次出现时的动效，由 DeckUiHandPresenter 读取偏移后统一应用。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckUiHandCardAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("悬停效果")]
    [SerializeField] private float hoverLift = 48f;
    [SerializeField] private float hoverAngle = 6f;
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float smoothingSpeed = 12f;

    [Header("翻牌效果")]
    [SerializeField] private bool playRevealOnEnable = true;
    [SerializeField] private float revealLift = 120f;
    [SerializeField] private float revealDuration = 0.4f;
    [SerializeField] private float revealOvershootScale = 1.12f;
    [SerializeField] private Vector2 revealDelayRange = new Vector2(0.02f, 0.18f);

    [Header("高光效果")]
    [SerializeField] private float normalHighlightAlpha = 0.04f;
    [SerializeField] private float hoverHighlightAlpha = 0.16f;
    [SerializeField] private float pressedHighlightAlpha = 0.28f;
    [SerializeField] private float highlightFadeSpeed = 14f;

    private CardRuntime cardRuntime;
    private float targetLift;
    private float targetAngle;
    private float targetScale = 1f;
    private float currentLift;
    private float currentAngle;
    private float currentScale = 1f;
    private float currentFlipAngle;
    private float highlightAlpha;
    private float targetHighlightAlpha;
    private Coroutine revealRoutine;
    private bool pointerInside;
    private bool pointerPressed;
    private bool isRevealing;
    private bool faceShownThisReveal;
    private Image highlightImage;

    public Vector2 PositionOffset => new Vector2(0f, currentLift);
    public float AngleOffset => currentAngle;
    public float ScaleMultiplier => Mathf.Max(0.75f, currentScale);
    public bool ShouldOverlay => pointerInside || pointerPressed;
    public float YRotationOffset => currentFlipAngle;

    private void Awake()
    {
        cardRuntime = GetComponent<CardRuntime>();
        highlightImage = cardRuntime != null ? cardRuntime.SelectionHighlight : null;
    }

    private void OnEnable()
    {
        ResetAnimationState();

        if (cardRuntime != null)
        {
            cardRuntime.OnCardInfoChanged += HandleCardInfoChanged;
        }

        if (playRevealOnEnable)
        {
            TriggerReveal(false);
        }
    }

    private void OnDisable()
    {
        if (cardRuntime != null)
        {
            cardRuntime.OnCardInfoChanged -= HandleCardInfoChanged;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        isRevealing = false;
    }

    private void Update()
    {
        if (!Application.isPlaying || isRevealing)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float lerp = 1f - Mathf.Exp(-smoothingSpeed * dt);

        currentLift = Mathf.Lerp(currentLift, targetLift, lerp);
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, lerp);
        currentScale = Mathf.Lerp(currentScale, targetScale, lerp);
        currentFlipAngle = Mathf.Lerp(currentFlipAngle, 0f, lerp);

        if (highlightImage != null)
        {
            highlightAlpha = Mathf.Lerp(highlightAlpha, targetHighlightAlpha, 1f - Mathf.Exp(-highlightFadeSpeed * dt));
            var color = highlightImage.color;
            color.a = highlightAlpha;
            highlightImage.color = color;
        }
    }

    private void ResetAnimationState()
    {
        pointerInside = false;
        pointerPressed = false;
        targetLift = 0f;
        targetAngle = 0f;
        targetScale = 1f;
        currentLift = 0f;
        currentAngle = 0f;
        currentScale = 1f;
        isRevealing = false;
        currentFlipAngle = 0f;
        faceShownThisReveal = false;
        highlightAlpha = normalHighlightAlpha;
        targetHighlightAlpha = normalHighlightAlpha;
        ApplyCardFaceVisible(true);
        if (highlightImage != null)
        {
            var color = highlightImage.color;
            color.a = highlightAlpha;
            highlightImage.color = color;
        }
    }

    private void HandleCardInfoChanged(CardSO _)
    {
        TriggerReveal(false);
    }

    /// <summary>
    /// 对外暴露的翻牌入口，bypassDelay 为 true 时立即播放。
    /// </summary>
    public void TriggerReveal(bool bypassDelay)
    {
        if (!playRevealOnEnable && !bypassDelay)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }

        revealRoutine = StartCoroutine(RevealRoutine(bypassDelay));
    }

    private IEnumerator RevealRoutine(bool bypassDelay)
    {
        isRevealing = true;

        float delay = bypassDelay ? 0f : Mathf.Clamp(UnityEngine.Random.Range(revealDelayRange.x, revealDelayRange.y), 0f, 10f);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        float elapsed = 0f;
        float startLift = -Mathf.Abs(revealLift);
        float startAngle = hoverAngle * 1.4f;
        float startScale = revealOvershootScale;
        currentFlipAngle = 180f;
        faceShownThisReveal = false;
        ApplyCardFaceVisible(false);

        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / revealDuration);
            float eased = EaseOutBack(t);
            currentLift = Mathf.LerpUnclamped(startLift, 0f, eased);
            currentAngle = Mathf.LerpUnclamped(startAngle, 0f, eased);
            currentScale = Mathf.LerpUnclamped(startScale, 1f, eased);
            float flipT = 1f - Mathf.Clamp01(elapsed / revealDuration);
            currentFlipAngle = Mathf.Lerp(0f, 180f, flipT);
            if (!faceShownThisReveal && flipT < 0.5f)
            {
                ApplyCardFaceVisible(true);
                faceShownThisReveal = true;
            }
            yield return null;
        }

        currentLift = 0f;
        currentAngle = 0f;
        currentScale = 1f;
        currentFlipAngle = 0f;
        isRevealing = false;
        revealRoutine = null;
        UpdateTargets();
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        UpdateTargets();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
        UpdateTargets();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerPressed = true;
        UpdateTargets();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPressed = false;
        UpdateTargets();
    }

    private void UpdateTargets()
    {
        if (pointerPressed)
        {
            targetLift = hoverLift * 0.4f;
            targetAngle = hoverAngle * 0.4f;
            targetScale = Mathf.Max(0.5f, pressedScale);
            targetHighlightAlpha = pressedHighlightAlpha;
        }
        else if (pointerInside)
        {
            targetLift = hoverLift;
            targetAngle = -hoverAngle;
            targetScale = Mathf.Max(1f, hoverScale);
            targetHighlightAlpha = hoverHighlightAlpha;
        }
        else
        {
            targetLift = 0f;
            targetAngle = 0f;
            targetScale = 1f;
            targetHighlightAlpha = normalHighlightAlpha;
        }
    }

    private void ApplyCardFaceVisible(bool showFront)
    {
        if (cardRuntime == null)
        {
            return;
        }

        if (showFront)
        {
            cardRuntime.ShowCardFront();
        }
        else
        {
            var back = DeckUiThemeCache.CardBackSprite;
            if (back != null)
            {
                cardRuntime.ShowCardBack(back);
            }
        }
    }
}
