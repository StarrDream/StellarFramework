using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework
{
    public static class TransformExtensions
    {
        [Serializable]
        public struct TransformStruct
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        #region 数据存取

        public static TransformStruct SaveTransformData(this Transform transform)
        {
            return new TransformStruct
            {
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale
            };
        }

        public static Transform LoadTransformData(this Transform transform, TransformStruct @struct)
        {
            transform.localPosition = @struct.position;
            transform.localRotation = @struct.rotation;
            transform.localScale = @struct.scale;
            return transform;
        }

        #endregion

        #region 重置与设置

        public static Transform ResetTransform(this GameObject go) => ResetTransform(go.transform);

        public static Transform ResetTransform(this Transform transform)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            return transform;
        }

        public static Transform ResetLocalTransform(this GameObject go) => ResetLocalTransform(go.transform);

        public static Transform ResetLocalTransform(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            return transform;
        }

        public static Transform SetLocalPosition(this Transform transform, Vector3 position)
        {
            transform.localPosition = position;
            return transform;
        }

        public static Transform SetPosition(this Transform transform, Vector3 position)
        {
            transform.position = position;
            return transform;
        }

        public static Transform SetLocalRotation(this Transform transform, Quaternion rotation)
        {
            transform.localRotation = rotation;
            return transform;
        }

        public static Transform SetLocalScale(this Transform transform, Vector3 scale)
        {
            transform.localScale = scale;
            return transform;
        }

        public static Transform CopyTransform(this Transform target, Transform source, bool includeScale = true)
        {
            target.position = source.position;
            target.rotation = source.rotation;
            if (includeScale) target.localScale = source.localScale;
            return target;
        }

        #endregion

        #region 层级与标签

        public static void SetLayerRecursively(this GameObject gameObject, int layer)
        {
            if (gameObject == null) return;
            gameObject.layer = layer;
            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null) child.gameObject.SetLayerRecursively(layer);
            }
        }

        public static void SetTagRecursively(this GameObject gameObject, string tag)
        {
            if (gameObject == null) return;
            try
            {
                gameObject.tag = tag;
            }
            catch (UnityException e)
            {
                LogKit.LogWarning($"Failed to set tag '{tag}': {e.Message}");
            }

            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null) child.gameObject.SetTagRecursively(tag);
            }
        }

        #endregion

        #region 子物体操作

        public static void ClearChildren(this Component parent) => ClearChildren(parent.transform);
        public static void ClearChildren(this GameObject parent) => ClearChildren(parent.transform);

        public static Transform ClearChildren(this Transform parent)
        {
            if (parent == null) return parent;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null) child.gameObject.SafeDestroy();
            }

            return parent;
        }

        public static void ClearChildrenImmediate(this Transform parent)
        {
            if (parent == null) return;
            while (parent.childCount > 0)
            {
                var child = parent.GetChild(0);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }

        public static string GetFullPath(this Transform transform)
        {
            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        #endregion
    }
}