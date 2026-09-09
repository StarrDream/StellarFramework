using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public enum FlowJoinPolicy
    {
        All,
        Any,
        NOfM
    }

    /// <summary>并行/竞速执行组的运行态摘要；实际节点激活由 FlowRun 持有。</summary>
    public sealed class FlowExecutionGroup
    {
        private readonly List<FlowNodeHandle> _waiters = new List<FlowNodeHandle>();
        private readonly HashSet<int> _arrivedBranchIds = new HashSet<int>();

        public FlowExecutionGroup(
            long id,
            int expectedBranches,
            FlowJoinPolicy policy,
            int requiredBranches = 0,
            FlowTokenLineage parentLineage = default(FlowTokenLineage))
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (expectedBranches <= 0) throw new ArgumentOutOfRangeException(nameof(expectedBranches));
            int required = policy == FlowJoinPolicy.All ? expectedBranches :
                policy == FlowJoinPolicy.Any ? 1 : requiredBranches;
            if (required <= 0 || required > expectedBranches)
                throw new ArgumentOutOfRangeException(nameof(requiredBranches));
            Id = id;
            ExpectedBranches = expectedBranches;
            Policy = policy;
            RequiredBranches = required;
            ParentLineage = parentLineage;
        }

        public long Id { get; }
        public FlowTokenLineage ParentLineage { get; }
        public int ExpectedBranches { get; }
        public FlowJoinPolicy Policy { get; }
        public int RequiredBranches { get; }
        public bool IsClosed { get; internal set; }
        internal bool JoinOutputClaimed { get; set; }
        public int ArrivedBranches { get; internal set; }
        public IReadOnlyList<FlowNodeHandle> Waiters => _waiters;
        public bool IsReady => ArrivedBranches >= RequiredBranches;

        internal void AddWaiter(FlowNodeHandle handle) => _waiters.Add(handle);

        internal void ClearWaiters() => _waiters.Clear();

        internal bool TryRegisterBranch(int branchId)
        {
            if (!_arrivedBranchIds.Add(branchId)) return false;
            ArrivedBranches++;
            return true;
        }
    }

    public readonly struct FlowCompiledSubPlan
    {
        public string Name { get; }
        public FlowCompiledPlan Plan { get; }

        public FlowCompiledSubPlan(string name, FlowCompiledPlan plan)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("SubFlow 名称不能为空。", nameof(name));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Name = name;
        }
    }

    /// <summary>SubFlow/Composite 的显式依赖图，编译期可用于拒绝递归依赖。</summary>
    public sealed class FlowCompositionDependencyGraph
    {
        private readonly Dictionary<string, List<string>> _edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public void Add(string flowId, string dependencyFlowId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(dependencyFlowId))
                throw new ArgumentException("FlowId 和依赖 FlowId 不能为空。");
            if (!_edges.TryGetValue(flowId, out List<string> dependencies))
            {
                dependencies = new List<string>();
                _edges.Add(flowId, dependencies);
            }

            if (!dependencies.Contains(dependencyFlowId)) dependencies.Add(dependencyFlowId);
        }

        public bool HasCycle(out string cycleFlowId)
        {
            var colors = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string flowId in _edges.Keys)
            {
                if (Visit(flowId, colors, out cycleFlowId)) return true;
            }

            cycleFlowId = null;
            return false;
        }

        private bool Visit(string flowId, Dictionary<string, int> colors, out string cycleFlowId)
        {
            if (colors.TryGetValue(flowId, out int color))
            {
                if (color == 1)
                {
                    cycleFlowId = flowId;
                    return true;
                }

                cycleFlowId = null;
                return false;
            }

            colors[flowId] = 1;
            if (_edges.TryGetValue(flowId, out List<string> dependencies))
            {
                for (int i = 0; i < dependencies.Count; i++)
                {
                    if (Visit(dependencies[i], colors, out cycleFlowId)) return true;
                }
            }

            colors[flowId] = 2;
            cycleFlowId = null;
            return false;
        }
    }

    public enum FlowConditionKind
    {
        Constant,
        Blackboard,
        State,
        Compare,
        Not,
        All,
        Any
    }

    public enum FlowComparisonOperator
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    /// <summary>Type-safe condition AST definition stored in Graph data.</summary>
    [Serializable]
    public sealed class FlowCondition
    {
        public FlowConditionKind Kind;
        public FlowValue Constant;
        public string Key;
        public FlowComparisonOperator Operator;
        public FlowCondition Left;
        public FlowCondition Right;
        public List<FlowCondition> Children = new List<FlowCondition>();

        public static FlowCondition FromBool(bool value) =>
            new FlowCondition { Kind = FlowConditionKind.Constant, Constant = FlowValue.FromBool(value) };

        public static FlowCondition BlackboardValue(string key) =>
            new FlowCondition { Kind = FlowConditionKind.Blackboard, Key = key };

        public static FlowCondition StateValue(string key) =>
            new FlowCondition { Kind = FlowConditionKind.State, Key = key };

        public static FlowCondition Compare(FlowCondition left, FlowComparisonOperator op, FlowCondition right) =>
            new FlowCondition { Kind = FlowConditionKind.Compare, Left = left, Operator = op, Right = right };
    }

    /// <summary>Immutable condition snapshot owned by a compiled FlowPlan.</summary>
    public sealed class FlowCompiledCondition
    {
        private readonly FlowCompiledCondition[] _children;
        private readonly IReadOnlyList<FlowCompiledCondition> _childrenView;

        public FlowConditionKind Kind { get; }
        public FlowValue Constant { get; }
        public string Key { get; }
        public FlowComparisonOperator Operator { get; }
        public FlowCompiledCondition Left { get; }
        public FlowCompiledCondition Right { get; }
        public IReadOnlyList<FlowCompiledCondition> Children => _childrenView;

        private FlowCompiledCondition(FlowCondition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Kind = source.Kind;
            Constant = source.Constant;
            Key = source.Key ?? string.Empty;
            Operator = source.Operator;
            Left = source.Left == null ? null : new FlowCompiledCondition(source.Left);
            Right = source.Right == null ? null : new FlowCompiledCondition(source.Right);
            int count = source.Children == null ? 0 : source.Children.Count;
            _children = new FlowCompiledCondition[count];
            for (int i = 0; i < count; i++)
            {
                FlowCondition child = source.Children[i];
                if (child == null) throw new InvalidOperationException("Condition children cannot contain null.");
                _children[i] = new FlowCompiledCondition(child);
            }
            _childrenView = Array.AsReadOnly(_children);
            Validate();
        }

        public static FlowCompiledCondition Compile(FlowCondition source) =>
            source == null ? null : new FlowCompiledCondition(source);

        private void Validate()
        {
            switch (Kind)
            {
                case FlowConditionKind.Constant:
                    if (Constant.Kind == FlowValueKind.None)
                        throw new InvalidOperationException("Condition constant cannot be None.");
                    break;
                case FlowConditionKind.Blackboard:
                case FlowConditionKind.State:
                    if (string.IsNullOrEmpty(Key))
                        throw new InvalidOperationException("Condition key cannot be empty.");
                    break;
                case FlowConditionKind.Compare:
                    if (Left == null || Right == null)
                        throw new InvalidOperationException("Compare condition requires Left and Right.");
                    break;
                case FlowConditionKind.Not:
                    if (Left == null)
                        throw new InvalidOperationException("Not condition requires Left.");
                    break;
                case FlowConditionKind.All:
                case FlowConditionKind.Any:
                    if (_children.Length == 0)
                        throw new InvalidOperationException("All/Any condition requires at least one child.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind));
            }
        }
    }

    public static class FlowConditionEvaluator
    {
        public static bool Evaluate(FlowCondition condition, FlowBlackboard blackboard, FlowStateStore states)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            switch (condition.Kind)
            {
                case FlowConditionKind.Constant:
                    return condition.Constant.Kind == FlowValueKind.Bool && condition.Constant.BoolValue;
                case FlowConditionKind.Blackboard:
                    return blackboard != null && blackboard.TryGet(condition.Key, out FlowValue board) &&
                        board.Kind == FlowValueKind.Bool && board.BoolValue;
                case FlowConditionKind.State:
                    return states != null && states.TryGet(new FlowStateKey(condition.Key), out FlowStateSnapshot state) &&
                        state.Value.Kind == FlowValueKind.Bool && state.Value.BoolValue;
                case FlowConditionKind.Not:
                    return !Evaluate(condition.Left, blackboard, states);
                case FlowConditionKind.All:
                    for (int i = 0; i < condition.Children.Count; i++)
                        if (!Evaluate(condition.Children[i], blackboard, states)) return false;
                    return true;
                case FlowConditionKind.Any:
                    for (int i = 0; i < condition.Children.Count; i++)
                        if (Evaluate(condition.Children[i], blackboard, states)) return true;
                    return false;
                case FlowConditionKind.Compare:
                    return Compare(Resolve(condition.Left, blackboard, states), condition.Operator,
                        Resolve(condition.Right, blackboard, states));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool Evaluate(FlowCompiledCondition condition, FlowBlackboard blackboard, FlowStateStore states)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            switch (condition.Kind)
            {
                case FlowConditionKind.Constant:
                    return condition.Constant.Kind == FlowValueKind.Bool && condition.Constant.BoolValue;
                case FlowConditionKind.Blackboard:
                    return blackboard != null && blackboard.TryGet(condition.Key, out FlowValue board) &&
                        board.Kind == FlowValueKind.Bool && board.BoolValue;
                case FlowConditionKind.State:
                    return states != null && states.TryGet(new FlowStateKey(condition.Key), out FlowStateSnapshot state) &&
                        state.Value.Kind == FlowValueKind.Bool && state.Value.BoolValue;
                case FlowConditionKind.Not:
                    return !Evaluate(condition.Left, blackboard, states);
                case FlowConditionKind.All:
                    for (int i = 0; i < condition.Children.Count; i++)
                        if (!Evaluate(condition.Children[i], blackboard, states)) return false;
                    return true;
                case FlowConditionKind.Any:
                    for (int i = 0; i < condition.Children.Count; i++)
                        if (Evaluate(condition.Children[i], blackboard, states)) return true;
                    return false;
                case FlowConditionKind.Compare:
                    return Compare(Resolve(condition.Left, blackboard, states), condition.Operator,
                        Resolve(condition.Right, blackboard, states));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static FlowValue Resolve(FlowCondition condition, FlowBlackboard blackboard, FlowStateStore states)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            switch (condition.Kind)
            {
                case FlowConditionKind.Constant: return condition.Constant;
                case FlowConditionKind.Blackboard:
                    if (blackboard == null || !blackboard.TryGet(condition.Key, out FlowValue board))
                        throw new InvalidOperationException("Blackboard condition key was not found: " + condition.Key);
                    return board;
                case FlowConditionKind.State:
                    if (states == null || !states.TryGet(new FlowStateKey(condition.Key), out FlowStateSnapshot state))
                        throw new InvalidOperationException("State condition key was not found: " + condition.Key);
                    return state.Value;
                default:
                    return FlowValue.FromBool(Evaluate(condition, blackboard, states));
            }
        }

        private static FlowValue Resolve(FlowCompiledCondition condition, FlowBlackboard blackboard, FlowStateStore states)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            switch (condition.Kind)
            {
                case FlowConditionKind.Constant: return condition.Constant;
                case FlowConditionKind.Blackboard:
                    if (blackboard == null || !blackboard.TryGet(condition.Key, out FlowValue board))
                        throw new InvalidOperationException("Blackboard condition key was not found: " + condition.Key);
                    return board;
                case FlowConditionKind.State:
                    if (states == null || !states.TryGet(new FlowStateKey(condition.Key), out FlowStateSnapshot state))
                        throw new InvalidOperationException("State condition key was not found: " + condition.Key);
                    return state.Value;
                default:
                    return FlowValue.FromBool(Evaluate(condition, blackboard, states));
            }
        }

        private static bool Compare(FlowValue left, FlowComparisonOperator op, FlowValue right)
        {
            if (left.TryGetNumber(out double leftNumber) && right.TryGetNumber(out double rightNumber))
            {
                switch (op)
                {
                    case FlowComparisonOperator.Equal: return leftNumber == rightNumber;
                    case FlowComparisonOperator.NotEqual: return leftNumber != rightNumber;
                    case FlowComparisonOperator.Greater: return leftNumber > rightNumber;
                    case FlowComparisonOperator.GreaterOrEqual: return leftNumber >= rightNumber;
                    case FlowComparisonOperator.Less: return leftNumber < rightNumber;
                    case FlowComparisonOperator.LessOrEqual: return leftNumber <= rightNumber;
                }
            }

            if (op == FlowComparisonOperator.Equal) return left.Equals(right);
            if (op == FlowComparisonOperator.NotEqual) return !left.Equals(right);
            throw new InvalidOperationException("Non-numeric FlowValue supports only Equal/NotEqual.");
        }
    }
}
