using UnityEngine;

namespace StellarFramework
{
    public static class RectTransformExtensions
    {
        /// <summary>
        /// 设置宽度 (保持高度不变)
        /// </summary>
        public static void SetWidth(this RectTransform rt, float width)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        /// <summary>
        /// 设置高度 (保持宽度不变)
        /// </summary>
        public static void SetHeight(this RectTransform rt, float height)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        /// <summary>
        /// 同时设置宽高
        /// </summary>
        public static void SetSize(this RectTransform rt, float width, float height)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        /// <summary>
        /// 设置锚点位置 X
        /// </summary>
        public static void SetAnchoredX(this RectTransform rt, float x)
        {
            var pos = rt.anchoredPosition;
            pos.x = x;
            rt.anchoredPosition = pos;
        }

        /// <summary>
        /// 设置锚点位置 Y
        /// </summary>
        public static void SetAnchoredY(this RectTransform rt, float y)
        {
            var pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;
        }

        /// <summary>
        /// 充满父容器 (类似于编辑器里的 Stretch-Stretch)
        /// </summary>
        public static void FillParent(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}