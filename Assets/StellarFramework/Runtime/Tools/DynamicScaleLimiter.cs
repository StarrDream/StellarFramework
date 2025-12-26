using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[ExecuteAlways]
public class DynamicScaleLimiter : MonoBehaviour
{
    [System.Serializable]
    public class LimitNode
    {
        [Tooltip("Scale 大小")] public float scale;
        public Vector2 xRange; // x=Min, y=Max
        public Vector2 zRange; // x=Min, y=Max
    }

    [Header("控制开关")] [Tooltip("勾选此项：在编辑模式下实时限制 Scale 和 Position。\n取消勾选：方便你配置数据，不产生干扰。")]
    public bool previewInEditor = true;

    [Header("配置区域")] public List<LimitNode> limitNodes = new List<LimitNode>();

    [Header("设置")] [Tooltip("Scale 的最大值 (同时用于限制物体大小和计算)")]
    public float maxScale = 8f;

    [Tooltip("是否锁定 Y 轴为 0")] public bool lockYToZero = true;

    private List<LimitNode> _sortedNodes;

    void OnValidate()
    {
        // 数据变更时重新排序，确保第一个元素永远是最小 Scale
        if (limitNodes != null && limitNodes.Count > 0)
        {
            _sortedNodes = limitNodes.OrderBy(n => n.scale).ToList();
        }
    }

    void Start()
    {
        OnValidate();
    }

    void LateUpdate()
    {
        // 1. 编辑模式下，如果没开预览，直接返回（防止闪烁）
        if (!Application.isPlaying && !previewInEditor) return;

        // 如果没有配置数据，不执行
        if (_sortedNodes == null || _sortedNodes.Count == 0) return;

        // ==================== 第一步：限制 Scale 大小 ====================

        // 获取配置中的最小 Scale (列表第一个)
        float minScaleLimit = _sortedNodes[0].scale;
        float maxScaleLimit = maxScale;

        // 获取当前 Scale (假设均匀缩放，取 X)
        float currentScale = transform.localScale.x;
        float originalScale = currentScale;

        // 执行 Scale 限制
        if (currentScale < minScaleLimit) currentScale = minScaleLimit;
        if (currentScale > maxScaleLimit) currentScale = maxScaleLimit;

        // 只有当 Scale 真的需要改变时才赋值 (优化性能，减少编辑器重绘)
        if (Mathf.Abs(currentScale - originalScale) > 0.0001f)
        {
            transform.localScale = Vector3.one * currentScale;
        }

        // ==================== 第二步：根据 Scale 限制 Position ====================

        float minX, maxX, minZ, maxZ;
        CalculateBounds(currentScale, out minX, out maxX, out minZ, out maxZ);

        Vector3 currentPos = transform.localPosition;
        Vector3 targetPos = currentPos;

        // 计算目标位置
        targetPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
        targetPos.z = Mathf.Clamp(currentPos.z, minZ, maxZ);

        if (lockYToZero) targetPos.y = 0;

        // 只有位置需要改变时才赋值
        if (Vector3.Distance(currentPos, targetPos) > 0.0001f)
        {
            transform.localPosition = targetPos;
        }
    }

    private void CalculateBounds(float scale, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        // 只有一个点的情况
        if (_sortedNodes.Count == 1)
        {
            var n = _sortedNodes[0];
            minX = n.xRange.x;
            maxX = n.xRange.y;
            minZ = n.zRange.x;
            maxZ = n.zRange.y;
            return;
        }

        // Scale 小于等于第一个点 (虽然上面限制了 Scale，但为了算法健壮性保留这个判断)
        if (scale <= _sortedNodes[0].scale)
        {
            var n = _sortedNodes[0];
            minX = n.xRange.x;
            maxX = n.xRange.y;
            minZ = n.zRange.x;
            maxZ = n.zRange.y;
            return;
        }

        // 寻找区间进行插值
        for (int i = 0; i < _sortedNodes.Count - 1; i++)
        {
            LimitNode p1 = _sortedNodes[i];
            LimitNode p2 = _sortedNodes[i + 1];

            if (scale >= p1.scale && scale <= p2.scale)
            {
                float t = Mathf.InverseLerp(p1.scale, p2.scale, scale);
                minX = Mathf.Lerp(p1.xRange.x, p2.xRange.x, t);
                maxX = Mathf.Lerp(p1.xRange.y, p2.xRange.y, t);
                minZ = Mathf.Lerp(p1.zRange.x, p2.zRange.x, t);
                maxZ = Mathf.Lerp(p1.zRange.y, p2.zRange.y, t);
                return;
            }
        }

        // 超过最后一个点，进行外推 (Extrapolation)
        LimitNode last = _sortedNodes[_sortedNodes.Count - 1];
        LimitNode prev = _sortedNodes[_sortedNodes.Count - 2];
        float tUnclamped = (scale - prev.scale) / (last.scale - prev.scale);

        minX = Mathf.LerpUnclamped(prev.xRange.x, last.xRange.x, tUnclamped);
        maxX = Mathf.LerpUnclamped(prev.xRange.y, last.xRange.y, tUnclamped);
        minZ = Mathf.LerpUnclamped(prev.zRange.x, last.zRange.x, tUnclamped);
        maxZ = Mathf.LerpUnclamped(prev.zRange.y, last.zRange.y, tUnclamped);
    }
}