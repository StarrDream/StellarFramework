using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 为对象池示例中的子弹提供简单的前进表现。
    /// </summary>
    public class ExampleForwardMover : MonoBehaviour
    {
        public Vector3 direction = Vector3.forward;
        public float speed = 8f;
        public float loopDistance = 12f;

        private Vector3 _origin;

        private void OnEnable()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            transform.position += direction.normalized * (speed * Time.deltaTime);

            if (loopDistance > 0f && Vector3.Distance(_origin, transform.position) > loopDistance)
            {
                transform.position = _origin;
            }
        }
    }
}
