using UnityEngine;

namespace MageKnight.Scripts.Demo
{
    public class BounceSphere : MonoBehaviour
    {
        private const float BounceHeight = 2f;

        private void Update()
        {
            var y = Mathf.Abs(Mathf.Sin(Time.time)) * BounceHeight;
            transform.position = new Vector3(0f, y, 0f);
        }
    }
}
