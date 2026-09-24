using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// 高频 Transform 操作扩展。
    /// 只保留 Unity 原生 API 中需要重复拆装 Vector3/Quaternion 才能完成的操作。
    /// </summary>
    public static class TransformUtil
    {
        /// <summary>把本地位置、旋转和缩放恢复为默认值。</summary>
        public static void ResetLocal(this Transform target)
        {
            EnsureTarget(target);
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        /// <summary>切换父节点并把本地变换恢复为默认值。</summary>
        public static void SetParentAndResetLocal(this Transform target, Transform parent)
        {
            EnsureTarget(target);
            target.SetParent(parent, false);
            target.ResetLocal();
        }

        /// <summary>只修改世界位置 X。</summary>
        public static void SetPositionX(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.position;
            position.x = value;
            target.position = position;
        }

        /// <summary>只修改世界位置 Y。</summary>
        public static void SetPositionY(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.position;
            position.y = value;
            target.position = position;
        }

        /// <summary>只修改世界位置 Z。</summary>
        public static void SetPositionZ(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.position;
            position.z = value;
            target.position = position;
        }

        /// <summary>只修改本地位置 X。</summary>
        public static void SetLocalPositionX(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.localPosition;
            position.x = value;
            target.localPosition = position;
        }

        /// <summary>只修改本地位置 Y。</summary>
        public static void SetLocalPositionY(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.localPosition;
            position.y = value;
            target.localPosition = position;
        }

        /// <summary>只修改本地位置 Z。</summary>
        public static void SetLocalPositionZ(this Transform target, float value)
        {
            EnsureTarget(target);
            Vector3 position = target.localPosition;
            position.z = value;
            target.localPosition = position;
        }

        private static void EnsureTarget(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
        }
    }
}
