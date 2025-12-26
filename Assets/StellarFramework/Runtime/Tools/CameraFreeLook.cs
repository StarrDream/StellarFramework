using UnityEngine;

namespace StellarFramework
{
    public class CameraFreeLook : MonoBehaviour
    {
        [Header("移动参数")] public float moveSpeed = 5f;
        public float fastMultiplier = 2f; // 按住 LeftShift 加速
        public float slowMultiplier = 0.5f; // 按住 LeftControl 变慢

        [Header("旋转参数")] public float mouseSensitivity = 3f;
        public float pitchMin = -80f;
        public float pitchMax = 80f;

        [Header("其他")] public bool invertY;
        public bool smoothRotation;
        public float rotationLerpSpeed = 15f;
        private bool isRmbHeld;
        private float pitch;
        private float targetPitch;

        private float targetYaw;

        private float yaw;

        private void Start()
        {
            var e = transform.eulerAngles;
            yaw = targetYaw = e.y;
            pitch = targetPitch = e.x;
            // 确保不锁定/不隐藏
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleMovement();
            HandleRotation();
        }

        private void HandleMovement()
        {
            var h = 0f;
            var v = 0f;

            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;

            var dir = transform.forward * v + transform.right * h;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            var speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;
            if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMultiplier;

            // 可选：Q/E 上下移动（如果需要，取消注释）
            float up = 0f;
            if (Input.GetKey(KeyCode.E)) up += 1f;
            if (Input.GetKey(KeyCode.Q)) up -= 1f;
            dir += Vector3.up * up;

            transform.position += dir * speed * Time.deltaTime;
        }

        private void HandleRotation()
        {
            isRmbHeld = Input.GetMouseButton(1);

            if (isRmbHeld)
            {
                // 不锁定、不隐藏，只读取增量
                var mouseX = Input.GetAxis("Mouse X");
                var mouseY = Input.GetAxis("Mouse Y");

                if (invertY) mouseY = -mouseY;

                targetYaw += mouseX * mouseSensitivity;
                targetPitch -= mouseY * mouseSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);

                if (smoothRotation)
                {
                    yaw = Mathf.Lerp(yaw, targetYaw, rotationLerpSpeed * Time.deltaTime);
                    pitch = Mathf.Lerp(pitch, targetPitch, rotationLerpSpeed * Time.deltaTime);
                }
                else
                {
                    yaw = targetYaw;
                    pitch = targetPitch;
                }

                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            // 松开右键不再更新 yaw/pitch（保持当前朝向）
        }
    }
}