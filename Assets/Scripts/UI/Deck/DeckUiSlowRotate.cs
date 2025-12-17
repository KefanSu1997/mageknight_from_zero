using UnityEngine;

/// <summary>
/// 持续缓慢旋转的简单组件，适合光圈、粒子叠层。
/// </summary>
public class DeckUiSlowRotate : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private bool randomizeSign = true;

    public float RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    private void Awake()
    {
        if (randomizeSign && Random.value > 0.5f)
        {
            rotationSpeed = -rotationSpeed;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        transform.Rotate(0f, 0f, rotationSpeed * Time.unscaledDeltaTime, Space.Self);
    }
}
