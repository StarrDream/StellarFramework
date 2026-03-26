using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework
{
    public static class GameObjectExtensions
    {
        #region 显示/隐藏

        public static GameObject Show(this Component component)
        {
            if (component == null)
            {
                LogKit.LogError("[GameObjectExtensions] Show(Component) 失败: component 为空");
                return null;
            }

            GameObject go = component.gameObject;
            if (go == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] Show(Component) 失败: gameObject 为空, ComponentType={component.GetType().Name}");
                return null;
            }

            go.SetActive(true);
            return go;
        }

        public static GameObject Hide(this Component component)
        {
            if (component == null)
            {
                LogKit.LogError("[GameObjectExtensions] Hide(Component) 失败: component 为空");
                return null;
            }

            GameObject go = component.gameObject;
            if (go == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] Hide(Component) 失败: gameObject 为空, ComponentType={component.GetType().Name}");
                return null;
            }

            go.SetActive(false);
            return go;
        }

        public static GameObject ShowOrHide(this Component component)
        {
            if (component == null)
            {
                LogKit.LogError("[GameObjectExtensions] ShowOrHide(Component) 失败: component 为空");
                return null;
            }

            GameObject go = component.gameObject;
            if (go == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] ShowOrHide(Component) 失败: gameObject 为空, ComponentType={component.GetType().Name}");
                return null;
            }

            go.SetActive(!go.activeSelf);
            return go;
        }

        public static GameObject Show(this GameObject go)
        {
            if (go == null)
            {
                LogKit.LogError("[GameObjectExtensions] Show(GameObject) 失败: go 为空");
                return null;
            }

            go.SetActive(true);
            return go;
        }

        public static GameObject Hide(this GameObject go)
        {
            if (go == null)
            {
                LogKit.LogError("[GameObjectExtensions] Hide(GameObject) 失败: go 为空");
                return null;
            }

            go.SetActive(false);
            return go;
        }

        public static GameObject ShowOrHide(this GameObject go)
        {
            if (go == null)
            {
                LogKit.LogError("[GameObjectExtensions] ShowOrHide(GameObject) 失败: go 为空");
                return null;
            }

            go.SetActive(!go.activeSelf);
            return go;
        }

        #endregion

        #region 查找与获取

        /// <summary>
        /// 智能查找 GameObject
        /// 我先按名字查找，再按标签查找。
        /// 这里不再使用 try-catch 掩盖非法 Tag，而是显式校验输入，保持错误语义清晰可追踪。
        /// </summary>
        public static GameObject SmartFind(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                LogKit.LogError("[GameObjectExtensions] SmartFind 失败: identifier 为空");
                return null;
            }

            GameObject obj = GameObject.Find(identifier);
            if (obj != null)
            {
                return obj;
            }

            if (!IsTagDefined(identifier))
            {
                return null;
            }

            return GameObject.FindGameObjectWithTag(identifier);
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetOrAddComponent 失败: gameObject 为空, ComponentType={typeof(T).Name}");
                return null;
            }

            T component = gameObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = gameObject.AddComponent<T>();
            if (component == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetOrAddComponent 失败: AddComponent 返回为空, GameObject={gameObject.name}, ComponentType={typeof(T).Name}");
                return null;
            }

            return component;
        }

        public static T GetOrAddComponent<T>(this Transform transform) where T : Component
        {
            if (transform == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetOrAddComponent(Transform) 失败: transform 为空, ComponentType={typeof(T).Name}");
                return null;
            }

            return transform.gameObject.GetOrAddComponent<T>();
        }

        #endregion

        #region 递归查找子物体

        public static GameObject FindChildByName(this GameObject parent, string name, bool includeInactive = true,
            int maxDepth = -1)
        {
            if (parent == null)
            {
                LogKit.LogError($"[GameObjectExtensions] FindChildByName(GameObject) 失败: parent 为空, ChildName={name}");
                return null;
            }

            Transform result = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            return result != null ? result.gameObject : null;
        }

        public static Transform FindChildByName(this Transform parent, string name, bool includeInactive = true,
            int maxDepth = -1)
        {
            if (parent == null)
            {
                LogKit.LogError($"[GameObjectExtensions] FindChildByName(Transform) 失败: parent 为空, ChildName={name}");
                return null;
            }

            return FindChildByNameIterative(parent, name, includeInactive, maxDepth);
        }

        public static List<GameObject> FindChildrenByNameContains(this GameObject parent, string namePart,
            bool includeInactive = true, int maxDepth = -1)
        {
            var results = new List<GameObject>();

            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContains 失败: parent 为空, NamePart={namePart}");
                return results;
            }

            if (string.IsNullOrWhiteSpace(namePart))
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContains 失败: namePart 为空, Parent={parent.name}");
                return results;
            }

            FindChildrenByNameContainsIterative(parent.transform, namePart, results, includeInactive, maxDepth);
            return results;
        }

        public static T GetChildComponentByName<T>(this Component parent, string name, bool includeInactive = true,
            int maxDepth = -1) where T : Component
        {
            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetChildComponentByName(Component) 失败: parent 为空, ChildName={name}, ComponentType={typeof(T).Name}");
                return null;
            }

            Transform targetTransform = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            if (targetTransform == null)
            {
                return null;
            }

            return targetTransform.GetComponent<T>();
        }

        public static T GetChildComponentByName<T>(this GameObject parent, string name, bool includeInactive = true,
            int maxDepth = -1) where T : Component
        {
            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetChildComponentByName(GameObject) 失败: parent 为空, ChildName={name}, ComponentType={typeof(T).Name}");
                return null;
            }

            Transform targetTransform = FindChildByNameIterative(parent.transform, name, includeInactive, maxDepth);
            if (targetTransform == null)
            {
                return null;
            }

            return targetTransform.GetComponent<T>();
        }

        public static T GetChildComponent<T>(this GameObject parent, bool includeInactive = true) where T : Component
        {
            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetChildComponent 失败: parent 为空, ComponentType={typeof(T).Name}");
                return null;
            }

            return parent.GetComponentInChildren<T>(includeInactive);
        }

        public static List<T> GetChildComponents<T>(this GameObject parent, bool includeInactive = true)
            where T : Component
        {
            var result = new List<T>();

            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] GetChildComponents 失败: parent 为空, ComponentType={typeof(T).Name}");
                return result;
            }

            T[] components = parent.GetComponentsInChildren<T>(includeInactive);
            if (components == null || components.Length == 0)
            {
                return result;
            }

            result.AddRange(components);
            return result;
        }

        private static Transform FindChildByNameIterative(this Transform parent, string name, bool includeInactive,
            int maxDepth)
        {
            if (parent == null)
            {
                LogKit.LogError($"[GameObjectExtensions] FindChildByNameIterative 失败: parent 为空, ChildName={name}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                LogKit.LogError($"[GameObjectExtensions] FindChildByNameIterative 失败: name 为空, Parent={parent.name}");
                return null;
            }

            if (maxDepth < -1)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildByNameIterative 失败: maxDepth 非法, Parent={parent.name}, Name={name}, MaxDepth={maxDepth}");
                return null;
            }

            var queue = new Queue<TransformDepth>(16);
            queue.Enqueue(new TransformDepth(parent, 0));

            while (queue.Count > 0)
            {
                TransformDepth current = queue.Dequeue();

                int childCount = current.Transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = current.Transform.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    if (!includeInactive && !child.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }

                    if (maxDepth < 0 || current.Depth < maxDepth)
                    {
                        queue.Enqueue(new TransformDepth(child, current.Depth + 1));
                    }
                }
            }

            return null;
        }

        private static void FindChildrenByNameContainsIterative(this Transform parent, string namePart,
            List<GameObject> results, bool includeInactive, int maxDepth)
        {
            if (parent == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContainsIterative 失败: parent 为空, NamePart={namePart}");
                return;
            }

            if (results == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContainsIterative 失败: results 为空, Parent={parent.name}, NamePart={namePart}");
                return;
            }

            if (string.IsNullOrWhiteSpace(namePart))
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContainsIterative 失败: namePart 为空, Parent={parent.name}");
                return;
            }

            if (maxDepth < -1)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] FindChildrenByNameContainsIterative 失败: maxDepth 非法, Parent={parent.name}, NamePart={namePart}, MaxDepth={maxDepth}");
                return;
            }

            var queue = new Queue<TransformDepth>(16);
            queue.Enqueue(new TransformDepth(parent, 0));

            while (queue.Count > 0)
            {
                TransformDepth current = queue.Dequeue();

                int childCount = current.Transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = current.Transform.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    if (!includeInactive && !child.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (child.name.Contains(namePart, StringComparison.Ordinal))
                    {
                        results.Add(child.gameObject);
                    }

                    if (maxDepth < 0 || current.Depth < maxDepth)
                    {
                        queue.Enqueue(new TransformDepth(child, current.Depth + 1));
                    }
                }
            }
        }

        private readonly struct TransformDepth
        {
            public readonly Transform Transform;
            public readonly int Depth;

            public TransformDepth(Transform transform, int depth)
            {
                Transform = transform;
                Depth = depth;
            }
        }

        #endregion

        #region 销毁

        public static void SafeDestroy(this Component component)
        {
            if (component == null)
            {
                LogKit.LogError("[GameObjectExtensions] SafeDestroy(Component) 失败: component 为空");
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(component);
            }
            else
            {
                Object.DestroyImmediate(component);
            }
        }

        public static void SafeDestroy(this GameObject gameObject)
        {
            if (gameObject == null)
            {
                LogKit.LogError("[GameObjectExtensions] SafeDestroy(GameObject) 失败: gameObject 为空");
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        public static void SafeDestroy(this GameObject gameObject, float delay)
        {
            if (gameObject == null)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] SafeDestroy(GameObject, delay) 失败: gameObject 为空, Delay={delay}");
                return;
            }

            if (delay < 0f)
            {
                LogKit.LogError(
                    $"[GameObjectExtensions] SafeDestroy(GameObject, delay) 失败: delay 非法, GameObject={gameObject.name}, Delay={delay}");
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject, delay);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        #endregion

        #region Private

        /// <summary>
        /// 检查 Tag 是否在项目中定义
        /// 我显式遍历已定义标签，避免通过异常流控制业务逻辑。
        /// </summary>
        private static bool IsTagDefined(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            string[] tags = UnityEditorInternalBridge.GetTags();
            if (tags == null || tags.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Unity Tag 查询桥接
    /// 我把 Editor 与 Runtime 的差异隔离在这里，避免主逻辑散落宏判断。
    /// </summary>
    internal static class UnityEditorInternalBridge
    {
        public static string[] GetTags()
        {
#if UNITY_EDITOR
            return UnityEditorInternal.InternalEditorUtility.tags;
#else
            return null;
#endif
        }
    }
}