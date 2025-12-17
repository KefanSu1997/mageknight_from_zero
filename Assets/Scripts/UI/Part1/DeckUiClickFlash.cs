using System.Collections;
using UnityEngine;

/// <summary>
/// 简单的闪光反馈组件，可在按钮点击时播放短暂的发光动画。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DeckUiClickFlash : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.08f;
    [SerializeField] private float fadeOutDuration = 0.32f;
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private CanvasGroup canvasGroup;
    private Coroutine playRoutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 播放一次闪烁效果。
    /// </summary>
    public void PlayFlash()
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        // Fade out with curve
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeOutDuration);
            float curve = fadeOutCurve != null ? fadeOutCurve.Evaluate(t) : 1f - t;
            canvasGroup.alpha = Mathf.Clamp01(curve);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        playRoutine = null;
    }
}
