using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework
{
    public static class GameObjectExtensions
    {
        #region 显示/隐藏

        public static GameObject Show(this Component component)
        {
            component.gameObject.SetActive(true);
            return component.gameObject;
        }

        public static GameObject Hide(this Component component)
        {
            component.gameObject.SetActive(false);
            return component.gameObject;
        }

        public static GameObject ShowOrHide(this Component component)
        {
            var go = component.gameObject;
            go.SetActive(!go.activeSelf);
            return go;
        }

        public static GameObject Show(this GameObject go)
        {
            go.SetActive(true);
            return go;
        }

        public static GameObject Hide(this GameObject go)
        {
            go.SetActive(false);
            return go;
        }

        public static GameObject ShowOrHide(this GameObject go)
        {
            go.SetActive(!go.activeSelf);
            return go;
        }

        #endregion

        #region 查找与获取

        /// <summary>
        ///     智能查找GameObject（按名字、标签）
        /// </summary>
        public static GameObject SmartFind(string identifier)
        {
            var obj = GameObject.Find(identifier);
            if (obj != null) return obj;

            try
            {
                obj = GameObject.FindGameObjectWithTag(identifier);
                if (obj != null) return obj;
            }
            catch
            {
            }

            return null;
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                LogKit.LogError("GetOrAddComponent: GameObject is null");
                return null;
            }

            var component = gameObject.GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>();
            return component;
        }

        public static T GetOrAddComponent<T>(this Transform transform) where T : Component
        {
            return transform.gameObject.GetOrAddComponent<T>();
        }

        #endregion

        #region 递归查找子物体

        public static GameObject FindChildByName(this GameObject parent, string name, bool includeInactive = true, int maxDepth = -1)
        {
            if (parent == null) return null;
            var result = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            return result?.gameObject;
        }

        public static Transform FindChildByName(this Transform parent, string name, bool includeInactive = true, int maxDepth = -1)
        {
            return FindChildByNameIterative(parent, name, includeInactive, maxDepth);
        }

        public static List<GameObject> FindChildrenByNameContains(this GameObject parent, string namePart, bool includeInactive = true, int maxDepth = -1)
        {
            var results = new List<GameObject>();
            if (parent == null) return results;
            FindChildrenByNameContainsIterative(parent.transform, namePart, results, includeInactive, maxDepth);
            return results;
        }

        public static T GetChildComponentByName<T>(this Component parent, string name, bool includeInactive = true, int maxDepth = -1) where T : Component
        {
            if (parent == null) return null;
            var targetTransform = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            return targetTransform != null ? targetTransform.GetComponent<T>() : null;
        }

        public static T GetChildComponentByName<T>(this GameObject parent, string name, bool includeInactive = true, int maxDepth = -1) where T : Component
        {
            if (parent == null) return null;
            var targetTransform = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            return targetTransform != null ? targetTransform.GetComponent<T>() : null;
        }

        public static T GetChildComponent<T>(this GameObject parent, bool includeInactive = true) where T : Component
        {
            if (parent == null) return null;
            return parent.GetComponentInChildren<T>(includeInactive);
        }

        public static List<T> GetChildComponents<T>(this GameObject parent, bool includeInactive = true) where T : Component
        {
            if (parent == null) return new List<T>();
            return parent.GetComponentsInChildren<T>(includeInactive).ToList();
        }

        // 私有辅助方法
        private static Transform FindChildByNameIterative(Transform parent, string name, bool includeInactive, int maxDepth)
        {
            if (parent == null) return null;
            var queue = new Queue<(Transform, int)>();
            queue.Enqueue((parent, 0));

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                foreach (Transform child in current)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                    if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
                    if (maxDepth < 0 || depth < maxDepth) queue.Enqueue((child, depth + 1));
                }
            }

            return null;
        }

        private static void FindChildrenByNameContainsIterative(Transform parent, string namePart, List<GameObject> results, bool includeInactive, int maxDepth)
        {
            var queue = new Queue<(Transform, int)>();
            queue.Enqueue((parent, 0));

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                foreach (Transform child in current)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                    if (child.name.Contains(namePart)) results.Add(child.gameObject);
                    if (maxDepth < 0 || depth < maxDepth) queue.Enqueue((child, depth + 1));
                }
            }
        }

        #endregion

        #region 销毁

        public static void SafeDestroy(this Component component)
        {
            if (component != null && component.gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(component);
                else Object.DestroyImmediate(component);
            }
        }

        public static void SafeDestroy(this GameObject gameObject)
        {
            if (gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(gameObject);
                else Object.DestroyImmediate(gameObject);
            }
        }

        public static void SafeDestroy(this GameObject gameObject, float delay)
        {
            if (gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(gameObject, delay);
                else Object.DestroyImmediate(gameObject);
            }
        }

        #endregion
    }
}