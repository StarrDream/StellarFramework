using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    ///     简易第一人称控制器
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MiniFPS_RightDragLook : MonoBehaviour
    {
        public Transform cam;
        public float speed = 5f;
        public float sensitivity = 120f; // 度/秒
        public float verticalClamp = 85f;
        private readonly float gravity = -9.81f;

        private CharacterController cc;
        private bool looking; // 是否当前在旋转视角（按住右键）

        private float pitch;
        private float vy;

        private void Start()
        {
            cc = GetComponent<CharacterController>();
            if (!cam) cam = Camera.main.transform;
            // 启动时不锁定、不隐藏鼠标
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleLookToggle();
            HandleLook();
            HandleMove();
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleLookToggle()
        {
            // 按下右键开始锁定并进入视角模式
            if (Input.GetMouseButtonDown(1))
            {
                looking = true;
                Cursor.lockState = CursorLockMode.Locked; // 锁定到屏幕中心
                Cursor.visible = false; // 视角模式下隐藏鼠标（如果你也想显示，删掉这行）
            }

            // 松开右键退出视角模式
            if (Input.GetMouseButtonUp(1))
            {
                looking = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleLook()
        {
            if (!looking) return;

            var mx = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            var my = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            transform.Rotate(Vector3.up * mx);

            pitch -= my;
            pitch = Mathf.Clamp(pitch, -verticalClamp, verticalClamp);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            var x = Input.GetAxisRaw("Horizontal");
            var z = Input.GetAxisRaw("Vertical");

            var dir = transform.right * x + transform.forward * z;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (cc.isGrounded && vy < 0f) vy = -2f; // 贴地
            vy += gravity * Time.deltaTime;

            var velocity = dir * speed + Vector3.up * vy;
            cc.Move(velocity * Time.deltaTime);
        }
    }
}