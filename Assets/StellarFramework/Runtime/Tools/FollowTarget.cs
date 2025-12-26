using StellarFramework;
using UnityEngine;

/// <summary>
/// 物体跟随工具 - 在不改变层级结构的情况下，让物体像子物体一样跟随目标
/// </summary>
public class FollowTarget : MonoBehaviour
{
    [Header("跟随设置")] [Tooltip("要跟随的目标物体")] public Transform target;

    [Header("偏移设置")] [Tooltip("相对于目标的位置偏移")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("相对于目标的旋转偏移（欧拉角）")] public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("相对于目标的缩放因子")] public Vector3 scaleFactor = Vector3.one;

    [Header("跟随选项")] [Tooltip("是否跟随位置")] public bool followPosition = true;

    [Tooltip("是否跟随旋转")] public bool followRotation = true;

    [Tooltip("是否跟随缩放")] public bool followScale = false;

    [Header("平滑设置")] [Tooltip("位置平滑速度（0表示不平滑，立即跟随）")] [Range(0f, 50f)]
    public float positionSmoothSpeed = 0f;

    [Tooltip("旋转平滑速度（0表示不平滑，立即跟随）")] [Range(0f, 50f)]
    public float rotationSmoothSpeed = 0f;

    [Tooltip("缩放平滑速度（0表示不平滑，立即跟随）")] [Range(0f, 50f)]
    public float scaleSmoothSpeed = 0f;

    // 存储初始偏移量
    private Vector3 initialPositionOffset;
    private Quaternion initialRotationOffset;
    private Vector3 initialScaleFactor;
    private Vector3 targetInitialScale;
    private bool offsetInitialized = false;

    void Start()
    {
        if (target != null && !offsetInitialized)
        {
            CaptureCurrentOffset();
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // 如果还没有初始化偏移量，先初始化
        if (!offsetInitialized)
        {
            CaptureCurrentOffset();
        }

        // 跟随位置
        if (followPosition)
        {
            Vector3 targetPosition = target.position + target.rotation * (positionOffset + initialPositionOffset);

            if (positionSmoothSpeed > 0)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionSmoothSpeed);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        // 跟随旋转
        if (followRotation)
        {
            Quaternion targetRotation = target.rotation * initialRotationOffset * Quaternion.Euler(rotationOffset);

            if (rotationSmoothSpeed > 0)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }

        // 跟随缩放（使用缩放因子）
        if (followScale)
        {
            // 计算目标相对于初始缩放的变化比例
            Vector3 scaleRatio = new Vector3(
                targetInitialScale.x != 0 ? target.localScale.x / targetInitialScale.x : 1f,
                targetInitialScale.y != 0 ? target.localScale.y / targetInitialScale.y : 1f,
                targetInitialScale.z != 0 ? target.localScale.z / targetInitialScale.z : 1f
            );

            // 应用缩放因子
            Vector3 targetScale = Vector3.Scale(Vector3.Scale(initialScaleFactor, scaleFactor), scaleRatio);

            if (scaleSmoothSpeed > 0)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSmoothSpeed);
            }
            else
            {
                transform.localScale = targetScale;
            }
        }
    }

    /// <summary>
    /// 捕获当前相对于目标的偏移量
    /// </summary>
    [ContextMenu("捕获当前偏移")]
    public void CaptureCurrentOffset()
    {
        if (target == null)
        {
            LogKit.LogWarning("请先设置目标物体！");
            return;
        }

        // 计算当前相对位置
        initialPositionOffset = Quaternion.Inverse(target.rotation) * (transform.position - target.position) - positionOffset;

        // 计算当前相对旋转
        initialRotationOffset = Quaternion.Inverse(target.rotation) * transform.rotation * Quaternion.Inverse(Quaternion.Euler(rotationOffset));

        // 记录目标的初始缩放
        targetInitialScale = target.localScale;

        // 计算当前缩放因子
        initialScaleFactor = transform.localScale;

        offsetInitialized = true;

        LogKit.Log($"已捕获偏移量 - 位置: {initialPositionOffset}, 旋转: {initialRotationOffset.eulerAngles}, 缩放因子: {scaleFactor}");
    }

    /// <summary>
    /// 重置偏移量
    /// </summary>
    [ContextMenu("重置偏移")]
    public void ResetOffset()
    {
        positionOffset = Vector3.zero;
        rotationOffset = Vector3.zero;
        scaleFactor = Vector3.one;
        initialPositionOffset = Vector3.zero;
        initialRotationOffset = Quaternion.identity;
        initialScaleFactor = Vector3.one;
        targetInitialScale = Vector3.one;
        offsetInitialized = false;

        LogKit.Log("偏移量已重置");
    }

    /// <summary>
    /// 将当前物体移动到目标位置（无偏移）
    /// </summary>
    [ContextMenu("移动到目标位置")]
    public void MoveToTarget()
    {
        if (target == null)
        {
            LogKit.LogWarning("请先设置目标物体！");
            return;
        }

        transform.position = target.position;
        transform.rotation = target.rotation;
        transform.localScale = target.localScale;
        ResetOffset();
    }
}