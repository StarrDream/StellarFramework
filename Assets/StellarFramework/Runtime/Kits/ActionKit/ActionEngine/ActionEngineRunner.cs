using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.ActionEngine
{
    public class ObjectSnapshot
    {
        public bool IsActive;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;

        public bool HasCanvasGroup;
        public float CanvasGroupAlpha;

        public bool HasImage;
        public Color ImageColor;

        public ObjectSnapshot(GameObject target)
        {
            if (target == null) return;
            IsActive = target.activeSelf;
            LocalPosition = target.transform.localPosition;
            LocalRotation = target.transform.localRotation;
            LocalScale = target.transform.localScale;

            var cg = target.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                HasCanvasGroup = true;
                CanvasGroupAlpha = cg.alpha;
            }

            var img = target.GetComponent<Image>();
            if (img != null)
            {
                HasImage = true;
                ImageColor = img.color;
            }
        }

        public void Restore(GameObject target)
        {
            if (target == null) return;
            target.SetActive(IsActive);
            target.transform.localPosition = LocalPosition;
            target.transform.localRotation = LocalRotation;
            target.transform.localScale = LocalScale;

            if (HasCanvasGroup)
            {
                var cg = target.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = CanvasGroupAlpha;
            }

            if (HasImage)
            {
                var img = target.GetComponent<Image>();
                if (img != null) img.color = ImageColor;
            }
        }
    }

    public static class ActionEngineRunner
    {
        private static readonly Dictionary<GameObject, Dictionary<GameObject, ObjectSnapshot>> _rootSnapshots =
            new Dictionary<GameObject, Dictionary<GameObject, ObjectSnapshot>>();

        #region 快照管理 (绑定期)

        /// <summary>
        /// 初始化快照：在资产加载或目标绑定时调用
        /// </summary>
        public static void InitSnapshot(GameObject rootTarget, ActionEngineAsset asset, bool forceOverwrite = false)
        {
            if (rootTarget == null || asset == null || asset.RootNode == null) return;

            if (!forceOverwrite && _rootSnapshots.ContainsKey(rootTarget)) return;

            var snapshotDict = new Dictionary<GameObject, ObjectSnapshot>();
            CollectTargets(rootTarget, asset.RootNode, snapshotDict);
            _rootSnapshots[rootTarget] = snapshotDict;
        }

        private static void CollectTargets(GameObject rootTarget, ActionNodeData node,
            Dictionary<GameObject, ObjectSnapshot> dict)
        {
            if (node == null) return;
            GameObject target = ResolveTarget(rootTarget, node.TargetPath);
            if (target != null && !dict.ContainsKey(target))
            {
                dict[target] = new ObjectSnapshot(target);
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children) CollectTargets(rootTarget, child, dict);
            }
        }

        public static void RestoreSnapshot(GameObject rootTarget)
        {
            if (rootTarget == null || !_rootSnapshots.TryGetValue(rootTarget, out var dict)) return;
            foreach (var kvp in dict)
            {
                kvp.Value.Restore(kvp.Key);
            }
        }

        #endregion

        public static async UniTask Play(GameObject rootTarget, ActionEngineAsset asset, bool isReverse,
            CancellationToken token)
        {
            if (rootTarget == null || asset == null || asset.RootNode == null) return;

            // 运行时容错：如果从未初始化过快照，则补抓一次
            if (!_rootSnapshots.ContainsKey(rootTarget))
            {
                InitSnapshot(rootTarget, asset);
            }

            // 1. 强制还原快照，确保推演基准绝对干净
            RestoreSnapshot(rootTarget);

            // 2. 瞬间推演整棵树，固化每个节点的 Start/End
            FastForwardTree(rootTarget, asset.RootNode);

            if (!isReverse)
            {
                // 3. 正放：推演完后，物体在终点，必须再次还原快照回到起点，然后开始正播
                RestoreSnapshot(rootTarget);
                await RunNode(rootTarget, asset.RootNode, false, token);
            }
            else
            {
                // 3. 倒放：推演完后，物体刚好在终点，直接开始后序遍历倒放
                await RunNode(rootTarget, asset.RootNode, true, token);
            }
        }

        private static void FastForwardTree(GameObject rootTarget, ActionNodeData node)
        {
            if (node == null) return;
            GameObject target = ResolveTarget(rootTarget, node.TargetPath);

            if (node.Strategy is IFastForwardable ff)
            {
                ff.FastForward(target, node);
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children) FastForwardTree(rootTarget, child);
            }
        }

        private static async UniTask RunNode(GameObject rootTarget, ActionNodeData node, bool isReverse,
            CancellationToken token)
        {
            if (node == null) return;
            GameObject target = ResolveTarget(rootTarget, node.TargetPath);

            if (!isReverse)
            {
                node.InvokeStart();
                if (node.Strategy != null)
                {
                    await node.Strategy.Execute(target, node, token, false, new Progress<float>(node.InvokeUpdate));
                }

                node.InvokeComplete();

                if (node.Children != null && node.Children.Count > 0)
                {
                    var tasks = new List<UniTask>();
                    foreach (var child in node.Children) tasks.Add(RunNode(rootTarget, child, false, token));
                    await UniTask.WhenAll(tasks);
                }
            }
            else
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    var tasks = new List<UniTask>();
                    foreach (var child in node.Children) tasks.Add(RunNode(rootTarget, child, true, token));
                    await UniTask.WhenAll(tasks);
                }

                node.InvokeStart();
                if (node.Strategy != null)
                {
                    await node.Strategy.Execute(target, node, token, true, new Progress<float>(node.InvokeUpdate));
                }

                node.InvokeComplete();
            }
        }

        private static GameObject ResolveTarget(GameObject rootTarget, string path)
        {
            if (string.IsNullOrEmpty(path)) return rootTarget;
            Transform t = rootTarget.transform.Find(path);
            if (t == null)
            {
                Debug.LogError($"[ActionEngine] 寻址失败！在 {rootTarget.name} 下找不到路径: {path}");
                return null;
            }

            return t.gameObject;
        }

        /// <summary>
        /// 清理快照字典，防止 GameObject 销毁后产生内存泄漏
        /// </summary>
        public static void ClearSnapshot(GameObject rootTarget)
        {
            if (rootTarget != null)
            {
                _rootSnapshots.Remove(rootTarget);
            }
        }
    }
}