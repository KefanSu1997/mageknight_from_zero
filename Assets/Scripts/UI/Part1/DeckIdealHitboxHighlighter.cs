using UnityEngine;
using UnityEngine.UI;

public sealed class DeckIdealHitboxHighlighter : MonoBehaviour
{
    private const float ActiveHighlightAlpha = 0.65f;
    [SerializeField] private CanvasGroup highlightGroup;
    [SerializeField] private CanvasGroup[] allGroups;

    private Button cachedButton;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
        if (cachedButton == null)
        {
            return;
        }

        cachedButton.onClick.AddListener(ActivateHighlight);
    }

    private void OnDestroy()
    {
        if (cachedButton == null)
        {
            return;
        }

        cachedButton.onClick.RemoveListener(ActivateHighlight);
    }

    public void Setup(CanvasGroup target, CanvasGroup[] groups)
    {
        highlightGroup = target;
        allGroups = groups;

        if (allGroups != null)
        {
            foreach (var group in allGroups)
            {
                SetHighlightAlpha(group, 0f);
            }
        }

        SetHighlightAlpha(highlightGroup, 0f);
    }

    private void ActivateHighlight()
    {
        if (allGroups != null)
        {
            foreach (var group in allGroups)
            {
                SetHighlightAlpha(group, 0f);
            }
        }

        if (highlightGroup != null)
        {
            SetHighlightAlpha(highlightGroup, ActiveHighlightAlpha);
        }
    }

    private static void SetHighlightAlpha(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = Mathf.Clamp01(alpha);
        group.interactable = false;
        group.blocksRaycasts = false;

        var images = group.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return;
        }

        var enabled = alpha > 0f;
        foreach (var image in images)
        {
            if (image != null)
            {
                image.enabled = enabled;
            }
        }
    }
}
