using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A / D
        float v = Input.GetAxisRaw("Vertical");   // W / S

        Vector3 desired = new Vector3(h, 0f, v).normalized * moveSpeed;

#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = new Vector3(desired.x, _rb.linearVelocity.y, desired.z);
#else
        _rb.velocity = new Vector3(desired.x, _rb.velocity.y, desired.z);
#endif
    }
}
