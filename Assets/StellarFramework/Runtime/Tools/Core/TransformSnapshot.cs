using System;
using UnityEngine;

namespace StellarFramework.RuntimeTools
{
    /// <summary>
    /// Transform 的本地空间快照。
    /// 用于预览、拖拽、临时动画或调试后恢复原始位置/旋转/缩放。
    /// </summary>
    [Serializable]
    public readonly struct TransformSnapshot
    {
        /// <summary>创建一个本地空间快照。</summary>
        public TransformSnapshot(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        /// <summary>本地位置。</summary>
        public Vector3 LocalPosition { get; }

        /// <summary>本地旋转。</summary>
        public Quaternion LocalRotation { get; }

        /// <summary>本地缩放。</summary>
        public Vector3 LocalScale { get; }

        /// <summary>捕获目标 Transform 的本地状态。</summary>
        /// <exception cref="ArgumentNullException">target 为空时抛出。</exception>
        public static TransformSnapshot Capture(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new TransformSnapshot(target.localPosition, target.localRotation, target.localScale);
        }

        /// <summary>把快照恢复到目标 Transform。</summary>
        /// <exception cref="ArgumentNullException">target 为空时抛出。</exception>
        public void Restore(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.localPosition = LocalPosition;
            target.localRotation = LocalRotation;
            target.localScale = LocalScale;
        }
    }
}
