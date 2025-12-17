using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让 UI 高光产生柔和脉冲效果，用于牌堆/弃牌区等提示。
/// </summary>
[RequireComponent(typeof(Graphic))]
public class DeckUiGlowPulse : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 1.2f;
    [SerializeField] [Range(0f, 1f)] private float minAlpha = 0.18f;
    [SerializeField] [Range(0f, 1f)] private float maxAlpha = 0.65f;
    [SerializeField] private bool randomizePhase = true;

    private Graphic graphic;
    private Color baseColor;
    private float phase;

    public float PulseSpeed
    {
        get => pulseSpeed;
        set => pulseSpeed = Mathf.Max(0f, value);
    }

    public float MinAlpha
    {
        get => minAlpha;
        set
        {
            minAlpha = Mathf.Clamp01(value);
            if (maxAlpha < minAlpha)
            {
                maxAlpha = minAlpha;
            }
        }
    }

    public float MaxAlpha
    {
        get => maxAlpha;
        set => maxAlpha = Mathf.Clamp01(Mathf.Max(minAlpha, value));
    }

    private void Awake()
    {
        CacheGraphic();
        if (randomizePhase)
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnEnable()
    {
        CacheGraphic();
        if (randomizePhase)
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnDisable()
    {
        if (graphic != null)
        {
            graphic.color = new Color(baseColor.r, baseColor.g, baseColor.b, minAlpha);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || graphic == null)
        {
            return;
        }

        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed + phase) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        var color = baseColor;
        color.a = alpha;
        graphic.color = color;
    }

    private void CacheGraphic()
    {
        if (graphic == null)
        {
            graphic = GetComponent<Graphic>();
        }

        if (graphic != null)
        {
            baseColor = graphic.color;
        }
    }
}
