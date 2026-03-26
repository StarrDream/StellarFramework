using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.ActionEngine
{
    /// <summary>
    /// 动作引擎运行器
    /// 职责：驱动生命周期钩子，递归处理嵌套组，管理取消令牌
    /// </summary>
    public static class ActionEngineRunner
    {
        public static async UniTask Play(GameObject root, ActionEngineAsset asset, CancellationToken token = default)
        {
            if (asset == null || root == null) return;
            // 强制要求使用克隆后的资源进行播放，防止多实例冲突
            await ExecuteGroup(root, asset.RootGroup, token);
        }

        private static async UniTask ExecuteGroup(GameObject root, ActionGroupData group, CancellationToken token)
        {
            // 1. 触发组开始钩子 (OnGroupStart)
            group.InvokeGroupStart();

            if (group.Mode == GroupExecutionMode.Sequence)
            {
                // 串行：依次等待
                foreach (var step in group.Steps) await ExecuteStep(root, step, token);
                foreach (var subGroup in group.SubGroups) await ExecuteGroup(root, subGroup, token);
            }
            else
            {
                // 并行：同时启动
                var tasks = new List<UniTask>();
                foreach (var step in group.Steps) tasks.Add(ExecuteStep(root, step, token));
                foreach (var subGroup in group.SubGroups) tasks.Add(ExecuteGroup(root, subGroup, token));
                await UniTask.WhenAll(tasks);
            }

            // 2. 触发组完成钩子 (OnGroupComplete)
            group.InvokeGroupComplete();
        }

        private static async UniTask ExecuteStep(GameObject root, ActionStepData step, CancellationToken token)
        {
            if (step.Strategy == null) return;

            // 寻址
            GameObject target = string.IsNullOrEmpty(step.TargetPath)
                ? root
                : root.transform.Find(step.TargetPath)?.gameObject;

            if (target == null)
            {
                LogKit.LogWarning($"[ActionEngine] 找不到目标路径: {step.TargetPath}");
                return;
            }

            // 延迟处理
            if (step.Delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(step.Delay), cancellationToken: token);

            // 3. 触发步骤开始钩子 (OnStart)
            step.InvokeStart();

            // 4. 执行策略并驱动 OnUpdate
            // 创建一个进度汇报器，将策略内部的百分比直接映射到 InvokeUpdate
            var progress = new Progress<float>(p => step.InvokeUpdate(p));

            await step.Strategy.Execute(target, step, token, progress);

            // 5. 触发步骤完成钩子 (OnComplete)
            step.InvokeComplete();
        }
    }
}