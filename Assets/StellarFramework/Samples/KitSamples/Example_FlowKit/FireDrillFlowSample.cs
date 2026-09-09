using System;
using System.Collections.Generic;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;
using UnityEngine;

namespace StellarFramework.Samples.FlowKit
{
    /// <summary>
    /// FlowKit 可运行消防演练 Sample。
    /// Operation 表示 FlowKit -> Gameplay 的命令；Signal/State 表示 Gameplay -> FlowKit 的事实回报。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireDrillFlowSample : MonoBehaviour
    {
        [SerializeField] private FlowHost host;
        [SerializeField] private TextAsset graphJson;
        [SerializeField] private bool autoStart = true;

        private readonly bool[] _rolePrompted = new bool[4];
        private readonly bool[] _readyPublished = new bool[4];
        private readonly bool[] _taskActive = new bool[4];
        private readonly bool[] _taskCompleted = new bool[4];
        private readonly List<string> _eventLog = new List<string>(32);
        private FlowRun _run;
        private Vector2 _scroll;

        private bool _safetyCheckActive;
        private bool _safetyPassed;
        private string _lastInstruction = "等待流程启动";

        private bool _failCommanderStart;
        private bool _failExtinguisherAStart;
        private bool _failExtinguisherBStart;
        private bool _failEvacuatorStart;
        private bool _failSafetyCheck;

        private static readonly string[] RoleNames =
        {
            "1号·指挥员", "2号·灭火员A", "3号·灭火员B", "4号·疏散员"
        };

        private static readonly string[] TaskStateIds =
        {
            FireDrillSampleIds.CommanderReported,
            FireDrillSampleIds.ExtinguisherACompleted,
            FireDrillSampleIds.ExtinguisherBCompleted,
            FireDrillSampleIds.EvacuationCompleted
        };

        private void Start()
        {
            if (autoStart) RestartFlow();
        }

        public void RestartFlow()
        {
            if (host == null) throw new InvalidOperationException("FireDrillFlowSample 缺少 FlowHost 引用。");
            if (graphJson == null) throw new InvalidOperationException("FireDrillFlowSample 缺少 FireDrillWorkflow4P.flow.json。");
            if (!host.IsInitialized) throw new InvalidOperationException("FlowHost 尚未初始化。");

            host.Runner.CancelAll();
            ResetExternalFacts();
            ResetLocalPresentation();
            _run = host.StartGraphAsset(graphJson);
            if (_run == null)
                throw new InvalidOperationException("消防演练 Graph 编译失败，请查看 Console / FlowKit Validation。");

            AppendLog("FlowKit", "消防演练 Run 已启动。");
        }

        private void ResetExternalFacts()
        {
            for (int i = 0; i < FireDrillSampleIds.CompletionStates.Length; i++)
            {
                host.Services.States.Set(
                    new FlowStateKey(FireDrillSampleIds.CompletionStates[i]),
                    FlowValue.FromBool(false),
                    FlowStateLifetime.External);
            }
        }

        private void ResetLocalPresentation()
        {
            Array.Clear(_rolePrompted, 0, _rolePrompted.Length);
            Array.Clear(_readyPublished, 0, _readyPublished.Length);
            Array.Clear(_taskActive, 0, _taskActive.Length);
            Array.Clear(_taskCompleted, 0, _taskCompleted.Length);
            _safetyCheckActive = false;
            _safetyPassed = false;
            _lastInstruction = "等待 FlowKit 下发第一条 Operation";
            _eventLog.Clear();
        }

        internal bool ShouldFailOperation(string operationId, out string reason)
        {
            bool fail =
                (operationId == FireDrillSampleIds.CommanderStart && _failCommanderStart) ||
                (operationId == FireDrillSampleIds.ExtinguisherAStart && _failExtinguisherAStart) ||
                (operationId == FireDrillSampleIds.ExtinguisherBStart && _failExtinguisherBStart) ||
                (operationId == FireDrillSampleIds.EvacuatorStart && _failEvacuatorStart) ||
                (operationId == FireDrillSampleIds.SafetyCheck && _failSafetyCheck);

            reason = fail ? $"Sample 模拟 Operation 失败: {operationId}" : string.Empty;
            return fail;
        }

        internal void ReceiveOperationCommand(string operationId, in FlowOperationRequest request)
        {
            AppendLog("FlowKit → Gameplay", operationId);
            switch (operationId)
            {
                case FireDrillSampleIds.BriefingShow:
                    _lastInstruction = "显示演练说明：4 人协同完成指挥、灭火与疏散。";
                    break;
                case FireDrillSampleIds.RolesAssign:
                    _lastInstruction = "分配 4 人角色并等待每名成员确认就绪。";
                    break;
                case FireDrillSampleIds.Player1Prompt:
                    ActivateRolePrompt(0, "1号指挥员请确认就绪。");
                    break;
                case FireDrillSampleIds.Player2Prompt:
                    ActivateRolePrompt(1, "2号灭火员A请确认就绪。");
                    break;
                case FireDrillSampleIds.Player3Prompt:
                    ActivateRolePrompt(2, "3号灭火员B请确认就绪。");
                    break;
                case FireDrillSampleIds.Player4Prompt:
                    ActivateRolePrompt(3, "4号疏散员请确认就绪。");
                    break;
                case FireDrillSampleIds.AlarmStart:
                    _lastInstruction = "消防警报启动，进入 4 人并行任务阶段。";
                    break;
                case FireDrillSampleIds.CommanderStart:
                    ActivateTask(0, "1号：完成现场指挥并上报。");
                    break;
                case FireDrillSampleIds.ExtinguisherAStart:
                    ActivateTask(1, "2号：前往 A 区执行灭火。完成后回报。 ");
                    break;
                case FireDrillSampleIds.ExtinguisherBStart:
                    ActivateTask(2, "3号：前往 B 区执行灭火。完成后回报。 ");
                    break;
                case FireDrillSampleIds.EvacuatorStart:
                    ActivateTask(3, "4号：执行人员疏散。完成后回报。 ");
                    break;
                case FireDrillSampleIds.SafetyCheck:
                    _safetyCheckActive = true;
                    _lastInstruction = "4 人任务均已完成，请进行最终安全复核。";
                    break;
                case FireDrillSampleIds.SummaryShow:
                    _lastInstruction = "安全复核通过，显示演练总结。";
                    break;
                case FireDrillSampleIds.CommanderFailure:
                case FireDrillSampleIds.ExtinguisherAFailure:
                case FireDrillSampleIds.ExtinguisherBFailure:
                case FireDrillSampleIds.EvacuatorFailure:
                    _lastInstruction = "进入角色任务失败处理，随后 Flow 进入显式失败终点。";
                    break;
            }
        }

        internal void ReceiveOperationFailure(string operationId, string reason)
        {
            _lastInstruction = $"Operation 失败：{operationId}";
            AppendLog("Gameplay → FlowKit / OperationResult.Failed", reason);
        }

        private void ActivateRolePrompt(int index, string instruction)
        {
            _rolePrompted[index] = true;
            _lastInstruction = instruction;
        }

        private void ActivateTask(int index, string instruction)
        {
            _taskActive[index] = true;
            _lastInstruction = instruction;
        }

        private void PublishReady(int index)
        {
            if (!_rolePrompted[index] || _readyPublished[index] || host == null) return;
            host.Services.Signals.Publish(
                new FlowSignalId(FireDrillSampleIds.ReadySignals[index]),
                FlowSignalScope.Host,
                payload: FlowValue.FromInt(index + 1));
            _readyPublished[index] = true;
            AppendLog("Gameplay → FlowKit / Signal", FireDrillSampleIds.ReadySignals[index]);
        }

        private void CompleteTask(int index)
        {
            if (!_taskActive[index] || _taskCompleted[index] || host == null) return;
            host.Services.States.Set(
                new FlowStateKey(TaskStateIds[index]),
                FlowValue.FromBool(true),
                FlowStateLifetime.External);
            _taskCompleted[index] = true;
            AppendLog("Gameplay → FlowKit / State", TaskStateIds[index] + " = true");
        }

        private void PassSafetyCheck()
        {
            if (!_safetyCheckActive || _safetyPassed || host == null) return;
            host.Services.States.Set(
                new FlowStateKey(FireDrillSampleIds.SafetyPassed),
                FlowValue.FromBool(true),
                FlowStateLifetime.External);
            _safetyPassed = true;
            AppendLog("Gameplay → FlowKit / State", FireDrillSampleIds.SafetyPassed + " = true");
        }

        private void AppendLog(string direction, string message)
        {
            _eventLog.Add($"[{direction}] {message}");
            if (_eventLog.Count > 40) _eventLog.RemoveAt(0);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, Mathf.Min(940f, Screen.width - 32f), Screen.height - 32f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("FlowKit 消防演练 Sample（4人）", GUI.skin.box);
            GUILayout.Label("核心模式：FlowKit 通过 Operation 下发命令；玩法系统通过 Signal / State / OperationResult 回报事实。", GUI.skin.label);

            string status = _run == null ? "未启动" : _run.Status.ToString();
            GUILayout.Label("Run 状态：" + status);
            if (_run != null && _run.LastError != null)
                GUILayout.Label("失败原因：" + _run.LastError, GUI.skin.box);

            GUILayout.Space(6f);
            GUILayout.Label("当前 FlowKit → Gameplay 指令：");
            GUILayout.Label(_lastInstruction, GUI.skin.box);

            GUILayout.Space(8f);
            if (GUILayout.Button("重新开始消防演练")) RestartFlow();

            GUILayout.Space(10f);
            GUILayout.Label("异常路径模拟（在任务启动前勾选）", GUI.skin.box);
            _failCommanderStart = GUILayout.Toggle(_failCommanderStart, "1号·指挥任务 Operation.failed");
            _failExtinguisherAStart = GUILayout.Toggle(_failExtinguisherAStart, "2号·灭火A Operation.failed");
            _failExtinguisherBStart = GUILayout.Toggle(_failExtinguisherBStart, "3号·灭火B Operation.failed");
            _failEvacuatorStart = GUILayout.Toggle(_failEvacuatorStart, "4号·疏散 Operation.failed");
            _failSafetyCheck = GUILayout.Toggle(_failSafetyCheck, "安全复核 Operation.failed");

            GUILayout.Space(10f);
            GUILayout.Label("玩法 → FlowKit：角色就绪 Signal", GUI.skin.box);
            for (int i = 0; i < RoleNames.Length; i++)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = _rolePrompted[i] && !_readyPublished[i] && _run != null && !_run.IsTerminal;
                if (GUILayout.Button(_readyPublished[i]
                        ? RoleNames[i] + " 已发送 Ready"
                        : RoleNames[i] + "：确认就绪"))
                    PublishReady(i);
                GUI.enabled = previousEnabled;
            }

            GUILayout.Space(10f);
            GUILayout.Label("玩法 → FlowKit：任务完成 State", GUI.skin.box);
            for (int i = 0; i < RoleNames.Length; i++)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = _taskActive[i] && !_taskCompleted[i] && _run != null && !_run.IsTerminal;
                if (GUILayout.Button(_taskCompleted[i]
                        ? RoleNames[i] + " 任务已完成"
                        : RoleNames[i] + "：完成当前任务"))
                    CompleteTask(i);
                GUI.enabled = previousEnabled;
            }

            GUILayout.Space(10f);
            GUILayout.Label("玩法 → FlowKit：最终安全复核", GUI.skin.box);
            bool safetyEnabled = GUI.enabled;
            GUI.enabled = _safetyCheckActive && !_safetyPassed && _run != null && !_run.IsTerminal;
            if (GUILayout.Button(_safetyPassed ? "安全复核已通过" : "提交：安全复核通过"))
                PassSafetyCheck();
            GUI.enabled = safetyEnabled;

            GUILayout.Space(10f);
            GUILayout.Label("双向通讯日志", GUI.skin.box);
            int start = Mathf.Max(0, _eventLog.Count - 16);
            for (int i = start; i < _eventLog.Count; i++)
                GUILayout.Label(_eventLog[i]);

            GUILayout.Space(8f);
            GUILayout.Label("提示：勾选任一 Operation.failed 后重新开始，再完成 4 人 Ready，可观察红色失败分支。", GUI.skin.label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
