using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 让测试目标在起点附近往返移动，便于演示跟随音效和追逐状态。
    /// </summary>
    public class ExamplePingPongMover : MonoBehaviour
    {
        public Vector3 localOffset = new Vector3(0f, 0f, 4f);
        public float speed = 1.2f;

        private Vector3 _startLocalPosition;

        private void Awake()
        {
            _startLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            float t = Mathf.PingPong(Time.time * speed, 1f);
            transform.localPosition = Vector3.Lerp(_startLocalPosition, _startLocalPosition + localOffset, t);
        }
    }
}
