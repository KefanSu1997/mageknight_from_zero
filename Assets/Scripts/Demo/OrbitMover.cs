using UnityEngine;

namespace MageKnight.Demo
{
    public class OrbitMover : MonoBehaviour
    {
        [SerializeField] private Transform orbitCenter;
        [SerializeField] private float orbitRadius = 2f;
        [SerializeField] private float angularSpeed = 60f;
        [SerializeField] private float heightOffset = 0.5f;

        private float currentAngle;

        private void Reset()
        {
            if (orbitCenter == null)
            {
                TryFindCenter();
            }

            if (orbitCenter != null)
            {
                InitializeOrbit();
            }
        }

        private void Awake()
        {
            if (orbitCenter == null)
            {
                TryFindCenter();
            }
        }

        private void Start()
        {
            if (orbitCenter != null)
            {
                InitializeOrbit();
            }
        }

        private void Update()
        {
            if (orbitCenter == null || orbitRadius <= Mathf.Epsilon)
            {
                return;
            }

            float input = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(input) < 0.001f)
            {
                return;
            }

            currentAngle += input * angularSpeed * Mathf.Deg2Rad * Time.deltaTime;
            UpdatePosition();
        }

        private void InitializeOrbit()
        {
            Vector3 offset = transform.position - orbitCenter.position;
            orbitRadius = new Vector2(offset.x, offset.z).magnitude;
            heightOffset = offset.y;

            if (orbitRadius <= Mathf.Epsilon)
            {
                orbitRadius = 1f;
                currentAngle = 0f;
            }
            else
            {
                currentAngle = Mathf.Atan2(offset.z, offset.x);
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            Vector3 centerPos = orbitCenter.position;
            float x = Mathf.Cos(currentAngle) * orbitRadius;
            float z = Mathf.Sin(currentAngle) * orbitRadius;
            Vector3 targetPosition = new Vector3(centerPos.x + x, centerPos.y + heightOffset, centerPos.z + z);
            transform.position = targetPosition;

            Vector3 lookTarget = new Vector3(centerPos.x, targetPosition.y, centerPos.z);
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        private void TryFindCenter()
        {
            if (transform.parent == null)
            {
                return;
            }

            Transform candidate = transform.parent.Find("CenterCylinder");
            if (candidate != null)
            {
                orbitCenter = candidate;
            }
        }
    }
}
