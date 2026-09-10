using UnityEngine;

namespace MageKnight.Scripts.Demo
{
    public class RollingBallCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);
        [SerializeField] private float followLerp = 5f;
        [SerializeField] private float lookLerp = 10f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerp * Time.deltaTime);

            var desiredDirection = (target.position - transform.position).normalized;
            var blendedDirection = Vector3.Slerp(transform.forward, desiredDirection, lookLerp * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(blendedDirection, Vector3.up);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
