using UnityEngine;

public class DeckUiSineFloat : MonoBehaviour
{
    [field: SerializeField] public Vector2 Amplitude { get; set; } = new Vector2(12f, 22f);
    [field: SerializeField] public Vector2 Frequency { get; set; } = new Vector2(0.2f, 0.18f);
    [SerializeField] private float phaseOffset;

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private bool cached;

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Cache();
    }

    private void Cache()
    {
        if (cached)
        {
            return;
        }

        rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            originalPosition = rectTransform.anchoredPosition;
            if (phaseOffset == 0f)
            {
                phaseOffset = Random.Range(0f, Mathf.PI * 2f);
            }
            cached = true;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || rectTransform == null)
        {
            return;
        }

        float time = Time.time + phaseOffset;
        float offsetX = Mathf.Sin(time * Frequency.x * Mathf.PI * 2f) * Amplitude.x;
        float offsetY = Mathf.Sin(time * Frequency.y * Mathf.PI * 2f) * Amplitude.y;
        rectTransform.anchoredPosition = originalPosition + new Vector2(offsetX, offsetY);
    }

    public void SetPhaseOffset(float offset)
    {
        phaseOffset = offset;
    }
}
