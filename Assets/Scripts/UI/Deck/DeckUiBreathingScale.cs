using UnityEngine;

/// <summary>
/// 让 UI 元素做轻微的呼吸缩放，用于魔法阵、光效等。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckUiBreathingScale : MonoBehaviour
{
    [SerializeField] private float scaleAmplitude = 0.02f;
    [SerializeField] private float speed = 0.75f;
    [SerializeField] private bool randomizePhase = true;

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private float phase;

    public float ScaleAmplitude
    {
        get => scaleAmplitude;
        set => scaleAmplitude = Mathf.Max(0f, value);
    }

    public float Speed
    {
        get => speed;
        set => speed = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            baseScale = rectTransform.localScale;
        }

        if (randomizePhase)
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnEnable()
    {
        if (randomizePhase)
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || rectTransform == null)
        {
            return;
        }

        float t = Mathf.Sin((Time.unscaledTime * speed) + phase);
        float scaleOffset = 1f + t * scaleAmplitude;
        rectTransform.localScale = baseScale * scaleOffset;
    }
}
