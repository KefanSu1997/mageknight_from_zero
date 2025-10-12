using UnityEngine;

public class RotateCube : MonoBehaviour
{
    private const float RotationSpeed = 30f;

    private void Update()
    {
        transform.Rotate(Vector3.up * RotationSpeed * Time.deltaTime);
    }
}
