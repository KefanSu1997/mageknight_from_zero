using UnityEngine;

public class DeckUiRotateGraphic : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 12f;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void Update()
    {
        if (!Application.isPlaying || rectTransform == null)
        {
            return;
        }

        rectTransform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
