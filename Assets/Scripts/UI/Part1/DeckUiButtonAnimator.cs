using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 为测试场景中的按钮提供基础的悬停与按压缩放效果，提升交互反馈。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckUiButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float animationDuration = 0.12f;

    public float HoverScale
    {
        get => hoverScale;
        set => hoverScale = Mathf.Max(0.01f, value);
    }

    public float PressedScale
    {
        get => pressedScale;
        set => pressedScale = Mathf.Max(0.01f, value);
    }

    public float AnimationDuration
    {
        get => animationDuration;
        set => animationDuration = Mathf.Max(0f, value);
    }

    private Coroutine scaleRoutine;
    private bool isPointerInside;
    private Vector3 normalScale = Vector3.one;

    private void Reset()
    {
        target = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target != null)
        {
            normalScale = target.localScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        TweenToScale(normalScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        TweenToScale(normalScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TweenToScale(normalScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        TweenToScale(isPointerInside ? normalScale * hoverScale : normalScale);
    }

    private void TweenToScale(Vector3 targetScale)
    {
        if (target == null)
        {
            return;
        }

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
        }

        scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        var startScale = target.localScale;
        if (animationDuration <= 0f)
        {
            target.localScale = targetScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            target.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        target.localScale = targetScale;
        scaleRoutine = null;
    }
}
