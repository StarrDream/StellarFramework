using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// SimulationKit 最小可操作样例：分开推进 Game Tick 与 Frame Step，观察固定预算批量派发和过期合并。
    /// </summary>
    public sealed class Example_SimulationKit : MonoBehaviour
    {
        private const int SlotCount = 20;
        private const int MaxBudget = 16;
        private readonly SimulationScheduler _scheduler = new SimulationScheduler(SlotCount);
        private readonly SimulationId[] _ids = new SimulationId[SlotCount];
        private readonly bool[] _registered = new bool[SlotCount];
        private readonly SimulationId[] _dispatchBuffer = new SimulationId[MaxBudget];
        private long _currentTick;
        private int _frameIndex;
        private int _nextId;
        private int _selectedSlot;
        private int _dispatchBudget = 4;
        private int _lastWrittenCount;
        private bool _lastHasBacklog;
        private bool _staggered;
        private string _lastOperation = "尚未执行操作。";
        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private bool _initialized;

        private void Awake() => ResetData(false);

        private void OnEnable()
        {
            if (!_initialized) ResetData(false);
        }

        private void ResetData(bool staggered)
        {
            _scheduler.Clear();
            Array.Clear(_registered, 0, _registered.Length);
            Array.Clear(_ids, 0, _ids.Length);
            _currentTick = 0;
            _frameIndex = 0;
            _nextId = 1;
            _selectedSlot = 0;
            _staggered = staggered;
            _lastWrittenCount = 0;
            _lastHasBacklog = false;
            _lastOperation = staggered
                ? "Reset Staggered：20 个 ID，interval=10，firstDelay 按 ID 分散。"
                : "Reset Burst：20 个 ID，interval=5，全部在 tick=5 到期。";

            for (int i = 0; i < SlotCount; i++)
            {
                RegisterSlot(i, _staggered ? i % 10 : 5);
            }

            _initialized = true;
        }

        private void Advance(long amount)
        {
            _currentTick += amount;
            _lastOperation = string.Format("Advance +{0}：当前 tick={1}，尚未自动派发。", amount, _currentTick);
        }

        private void FrameStep()
        {
            _frameIndex++;
            CollectOneBatch("Frame Step");
        }

        private void DrainCurrentTick()
        {
            CollectOneBatch("Manual Drain");
        }

        private void CollectOneBatch(string operation)
        {
            Array.Clear(_dispatchBuffer, 0, _dispatchBuffer.Length);
            SimulationCollectResult result = _scheduler.CollectDue(
                _currentTick, _dispatchBuffer.AsSpan(0, _dispatchBudget));
            _lastWrittenCount = result.WrittenCount;
            _lastHasBacklog = result.HasBacklog;
            _lastOperation = string.Format(
                "{0}：Frame={1}，Game Tick={2}，本次只 Collect 一次，写入 {3} 个 ID，HasBacklog={4}。",
                operation, _frameIndex, _currentTick, result.WrittenCount, result.HasBacklog);
        }

        private void SetBudget(int budget)
        {
            _dispatchBudget = budget;
            _lastOperation = string.Format("Budget={0}：下一次 Drain 最多写入 {0} 个 ID。", budget);
        }

        private void RegisterOne()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_registered[i]) continue;
                RegisterSlot(i, _staggered ? _nextId % 10 : 5);
                _lastOperation = string.Format("Register ID {0}：{1}。", _ids[i], _scheduler.TryGetNextDueTick(_ids[i], out long due) ? "NextDue=" + due : "失败");
                return;
            }

            _lastOperation = "Register：样例槽位已满，请先 Unregister。";
        }

        private void RegisterSlot(int slot, long firstDelay)
        {
            SimulationId id = new SimulationId(_nextId++);
            long interval = _staggered ? 10 : 5;
            SimulationMutationResult result = _scheduler.TryRegister(id, _currentTick, interval, firstDelay);
            if (result.Success)
            {
                _ids[slot] = id;
                _registered[slot] = true;
                _selectedSlot = slot;
            }
            else
            {
                _lastOperation = string.Format("Register ID {0} 失败：{1}。", id, result.Error);
            }
        }

        private void UnregisterSelected()
        {
            int slot = FindSelectedSlot();
            if (slot < 0)
            {
                _lastOperation = "Unregister：没有已注册的 ID。";
                return;
            }

            SimulationId id = _ids[slot];
            SimulationMutationResult result = _scheduler.TryUnregister(id);
            if (result.Success) _registered[slot] = false;
            _lastOperation = string.Format("Unregister ID {0}：{1}。", id, result);
        }

        private void SetIntervalSelected()
        {
            int slot = FindSelectedSlot();
            if (slot < 0)
            {
                _lastOperation = "SetInterval：没有已注册的 ID。";
                return;
            }

            SimulationId id = _ids[slot];
            long interval = _staggered ? 15 : 3;
            SimulationMutationResult result = _scheduler.TrySetInterval(id, _currentTick, interval);
            _lastOperation = string.Format("SetInterval ID {0} -> {1}：{2}。", id, interval, result);
        }

        private int FindSelectedSlot()
        {
            if (_selectedSlot >= 0 && _selectedSlot < SlotCount && _registered[_selectedSlot]) return _selectedSlot;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_registered[i])
                {
                    _selectedSlot = i;
                    return i;
                }
            }

            return -1;
        }

        private void OnGUI()
        {
            EnsureStyles();
            Rect area = new Rect(16f, 16f, 540f, Screen.height - 32f);
            GUILayout.BeginArea(area, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("SimulationKit / Playable Sample", _titleStyle);
            GUILayout.Label("纯 C# 批量调度：业务自己推进 tick，SimulationScheduler 只返回到期 ID。", _bodyStyle);

            GUILayout.Space(6f);
            GUILayout.Label("MODE", _sectionStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Burst")) ResetData(false);
                if (GUILayout.Button("Reset Staggered")) ResetData(true);
            }

            GUILayout.Label("REALTIME FRAME-SPREADING", _sectionStyle);
            GUILayout.Label(
                "Frame Step 代表一次实时更新周期：只调用一次 CollectDue，不推进 Game Tick。HasBacklog=true 时下一帧继续。",
                _bodyStyle);
            if (GUILayout.Button("Frame Step (Collect once)")) FrameStep();

            GUILayout.Label("GAME TICK", _sectionStyle);
            GUILayout.Label("Advance 只推进逻辑时间；Frame Step 与 Game Tick 是两个独立维度。", _bodyStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1")) Advance(1);
                if (GUILayout.Button("+5")) Advance(5);
                if (GUILayout.Button("+20")) Advance(20);
            }

            GUILayout.Label("BUDGET", _sectionStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Budget 1")) SetBudget(1);
                if (GUILayout.Button("Budget 4")) SetBudget(4);
                if (GUILayout.Button("Budget 16")) SetBudget(16);
            }

            GUILayout.Label("EXPLICIT FLUSH / DEBUG", _sectionStyle);
            GUILayout.Label(
                "Manual Drain 在同一 Game Tick 主动再取一批；连续点击可清空 backlog，但不具备 frame spreading。",
                _bodyStyle);
            if (GUILayout.Button("Manual Drain (same tick)")) DrainCurrentTick();

            GUILayout.Label("MUTATION", _sectionStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Register")) RegisterOne();
                if (GUILayout.Button("Unregister")) UnregisterSelected();
                if (GUILayout.Button("SetInterval")) SetIntervalSelected();
            }

            GUILayout.Space(8f);
            GUILayout.Label("STATE", _sectionStyle);
            GUILayout.Label(BuildStateText(), _bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string BuildStateText()
        {
            SimulationId selected = FindSelectedSlot() >= 0 ? _ids[_selectedSlot] : default(SimulationId);
            string nextDue = selected.IsValid && _scheduler.TryGetNextDueTick(selected, out long due)
                ? due.ToString()
                : "<none>";
            string dispatched = "<none>";
            if (_lastWrittenCount > 0)
            {
                dispatched = string.Empty;
                for (int i = 0; i < _lastWrittenCount; i++)
                {
                    if (i > 0) dispatched += ", ";
                    dispatched += _dispatchBuffer[i].Value.ToString();
                }
            }

            return string.Format(
                "Mode: {0}\nGame Tick: {1}\nFrame Step: {2}\nBudget (per CollectDue call): {3}\nRegistered Count: {4}\nSelected ID: {5}\nSelected NextDue: {6}\nLast WrittenCount: {7}\nLast HasBacklog: {8}\nLast IDs: {9}\nLast Operation: {10}",
                _staggered ? "Staggered" : "Burst", _currentTick, _frameIndex, _dispatchBudget, _scheduler.Count,
                selected.IsValid ? selected.ToString() : "<none>", nextDue, _lastWrittenCount,
                _lastHasBacklog, dispatched, _lastOperation);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
    }
}
