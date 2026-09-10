using UnityEngine;

namespace MageKnight.Scripts.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class RollingBallController : MonoBehaviour
    {
        [SerializeField] private float moveAcceleration = 18f;
        [SerializeField] private float maxSpeed = 8f;
        [SerializeField] private Camera followCamera;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.maxAngularVelocity = 20f;
            if (followCamera == null && Camera.main != null)
            {
                followCamera = Camera.main;
            }
        }

        private void FixedUpdate()
        {
            var moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            var moveDir = GetMoveDirection(moveInput);
            ApplyMovement(moveDir);
            ClampPlanarSpeed();
        }

        private Vector3 GetMoveDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            var referenceCamera = followCamera != null ? followCamera.transform : null;
            var forward = referenceCamera != null
                ? Vector3.ProjectOnPlane(referenceCamera.forward, Vector3.up).normalized
                : Vector3.forward;
            var right = referenceCamera != null
                ? Vector3.ProjectOnPlane(referenceCamera.right, Vector3.up).normalized
                : Vector3.right;

            var direction = forward * moveInput.y + right * moveInput.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void ApplyMovement(Vector3 moveDir)
        {
            if (moveDir.sqrMagnitude <= 0f)
            {
                return;
            }

            var planarVelocity = new Vector3(_rigidbody.velocity.x, 0f, _rigidbody.velocity.z);
            var targetVelocity = moveDir * maxSpeed;
            var accel = (targetVelocity - planarVelocity) * moveAcceleration;
            _rigidbody.AddForce(new Vector3(accel.x, 0f, accel.z), ForceMode.Acceleration);
        }

        private void ClampPlanarSpeed()
        {
            var planarVelocity = new Vector3(_rigidbody.velocity.x, 0f, _rigidbody.velocity.z);
            if (planarVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                var limited = planarVelocity.normalized * maxSpeed;
                _rigidbody.velocity = new Vector3(limited.x, _rigidbody.velocity.y, limited.z);
            }
        }

        public void SetFollowCamera(Camera camera)
        {
            followCamera = camera;
        }
    }
}
