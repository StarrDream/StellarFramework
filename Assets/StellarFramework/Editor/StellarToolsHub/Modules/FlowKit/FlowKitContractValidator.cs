using System;
using System.Collections.Generic;
using UnityEditor;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal sealed class FlowContractCatalogSnapshot
    {
        internal readonly List<FlowStrictContractScope> StrictScopes =
            new List<FlowStrictContractScope>();
        internal readonly Dictionary<string, FlowAuthoringContractEntry> Operations =
            new Dictionary<string, FlowAuthoringContractEntry>(StringComparer.Ordinal);
        internal readonly Dictionary<string, FlowAuthoringContractEntry> Signals =
            new Dictionary<string, FlowAuthoringContractEntry>(StringComparer.Ordinal);
        internal readonly Dictionary<string, FlowAuthoringContractEntry> States =
            new Dictionary<string, FlowAuthoringContractEntry>(StringComparer.Ordinal);
        internal readonly Dictionary<string, FlowAuthoringContractEntry> BlackboardKeys =
            new Dictionary<string, FlowAuthoringContractEntry>(StringComparer.Ordinal);
        internal readonly Dictionary<string, FlowAuthoringContractEntry> Bindings =
            new Dictionary<string, FlowAuthoringContractEntry>(StringComparer.Ordinal);

        internal bool HasContractEntries =>
            Operations.Count > 0 || Signals.Count > 0 || States.Count > 0 ||
            BlackboardKeys.Count > 0 || Bindings.Count > 0;

        internal bool HasValidationRules => HasContractEntries || StrictScopes.Count > 0;

        internal bool RejectsUnknownReferences(string flowId)
        {
            for (int i = 0; i < StrictScopes.Count; i++)
            {
                if (StrictScopes[i].AppliesTo(flowId))
                    return true;
            }

            return false;
        }
    }

    internal sealed class FlowStrictContractScope
    {
        private readonly bool _global;
        private readonly HashSet<string> _flowIds;

        internal FlowStrictContractScope(IReadOnlyList<string> flowIds)
        {
            if (flowIds == null || flowIds.Count == 0)
            {
                _global = true;
                return;
            }

            _flowIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < flowIds.Count; i++)
            {
                string flowId = flowIds[i];
                if (!string.IsNullOrWhiteSpace(flowId))
                    _flowIds.Add(flowId);
            }
        }

        internal bool AppliesTo(string flowId)
        {
            if (_global) return true;
            if (_flowIds == null || _flowIds.Count == 0) return false;
            return !string.IsNullOrEmpty(flowId) && _flowIds.Contains(flowId);
        }
    }

    /// <summary>
    /// Project-level contract validation. This is intentionally editor-only:
    /// FlowRunner stays independent from project authoring conventions.
    /// </summary>
    internal static class FlowKitContractValidator
    {
        internal static FlowContractCatalogSnapshot BuildSnapshot(FlowBuildValidationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var snapshot = new FlowContractCatalogSnapshot();
            string[] guids = AssetDatabase.FindAssets("t:FlowAuthoringCatalog");
            Array.Sort(guids, StringComparer.Ordinal);
            result.ContractCatalogCount = guids.Length;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                FlowAuthoringCatalog catalog = AssetDatabase.LoadAssetAtPath<FlowAuthoringCatalog>(path);
                if (catalog == null)
                {
                    result.Errors.Add($"{path}: FlowAuthoringCatalog could not be loaded.");
                    continue;
                }

                AddEntries(snapshot.Operations, catalog.Operations, "Operation", path, 3, result);
                AddEntries(snapshot.Signals, catalog.Signals, "Signal", path, 3, result);
                AddEntries(snapshot.States, catalog.States, "State", path, 3, result);
                AddEntries(snapshot.BlackboardKeys, catalog.BlackboardKeys, "Blackboard", path, 3, result);
                AddEntries(snapshot.Bindings, catalog.Bindings, "Binding", path, 2, result);
                if (catalog.StrictUnknownReferences)
                {
                    ValidateStrictFlowIds(catalog.StrictFlowIds, path, result);
                    snapshot.StrictScopes.Add(new FlowStrictContractScope(catalog.StrictFlowIds));
                }
            }

            if (!snapshot.HasValidationRules)
            {
                result.Warnings.Add(
                    "No Flow authoring contracts are registered; graph IDs compile normally but project-level type contracts are not enforced.");
            }
            else if (!snapshot.HasContractEntries && snapshot.StrictScopes.Count > 0)
            {
                result.Warnings.Add(
                    "Strict Flow contract validation is enabled without any contract entries; all external contract references in the configured scope will be rejected.");
            }

            return snapshot;
        }

        private static void ValidateStrictFlowIds(
            IReadOnlyList<string> flowIds,
            string catalogPath,
            FlowBuildValidationResult result)
        {
            if (flowIds == null || flowIds.Count == 0) return;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < flowIds.Count; i++)
            {
                string flowId = flowIds[i];
                if (string.IsNullOrWhiteSpace(flowId))
                {
                    result.Errors.Add(
                        $"{catalogPath}: strict FlowId at index {i} is empty.");
                    continue;
                }

                if (!unique.Add(flowId))
                    result.Errors.Add(
                        $"{catalogPath}: duplicate strict FlowId '{flowId}'.");
            }
        }

        internal static void ValidateGraph(
            string assetPath,
            FlowGraphData graph,
            FlowContractCatalogSnapshot contracts,
            FlowBuildValidationResult result)
        {
            if (graph == null || contracts == null || result == null) return;

            if (!contracts.HasValidationRules) return;
            bool rejectUnknown = contracts.RejectsUnknownReferences(graph.FlowId);

            if (graph.Nodes == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FlowNodeData node = graph.Nodes[i];
                if (node == null) continue;

                ValidateOperation(assetPath, node, contracts, rejectUnknown, result);
                ValidateSignal(assetPath, node, contracts, rejectUnknown, result);
                ValidateState(assetPath, node, contracts, rejectUnknown, result);
                ValidateBlackboard(assetPath, node, contracts, rejectUnknown, result);
                ValidateBindings(assetPath, node, contracts, rejectUnknown, result);
                ValidateCondition(assetPath, node.Id, node.Condition, contracts, rejectUnknown, result);
            }
        }

        private static void AddEntries(
            IDictionary<string, FlowAuthoringContractEntry> target,
            IReadOnlyList<FlowAuthoringContractEntry> entries,
            string category,
            string catalogPath,
            int minimumSegments,
            FlowBuildValidationResult result)
        {
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                FlowAuthoringContractEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    result.Errors.Add($"{catalogPath}: {category} contract at index {i} has an empty ID.");
                    continue;
                }

                if (!FlowAuthoringIdRules.IsValidContractId(entry.Id, minimumSegments))
                {
                    result.Errors.Add(
                        $"{catalogPath}: {category} ID '{entry.Id}' must use stable lower_snake_case dot segments.");
                }

                if (target.ContainsKey(entry.Id))
                {
                    result.Errors.Add($"{catalogPath}: duplicate project {category} contract ID '{entry.Id}'.");
                    continue;
                }

                target.Add(entry.Id, entry);

                if (category == "Operation")
                    ValidateOperationEntry(entry, catalogPath, result);
                else if (category == "Binding")
                    ValidateBindingEntry(entry, catalogPath, result);
            }
        }

        private static void ValidateOperationEntry(
            FlowAuthoringContractEntry entry,
            string catalogPath,
            FlowBuildValidationResult result)
        {
            if (!string.IsNullOrEmpty(entry.RequiredCapability) &&
                !FlowAuthoringIdRules.IsValidContractId(entry.RequiredCapability, 2))
            {
                result.Errors.Add(
                    $"{catalogPath}: Operation '{entry.Id}' has invalid capability ID '{entry.RequiredCapability}'.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<FlowAuthoringArgumentContract> arguments = entry.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                FlowAuthoringArgumentContract argument = arguments[i];
                if (argument == null || !FlowAuthoringIdRules.IsValidArgumentKey(argument.Key))
                {
                    result.Errors.Add(
                        $"{catalogPath}: Operation '{entry.Id}' has invalid argument key at index {i}.");
                    continue;
                }

                if (!keys.Add(argument.Key))
                    result.Errors.Add(
                        $"{catalogPath}: Operation '{entry.Id}' has duplicate argument '{argument.Key}'.");
            }
        }

        private static void ValidateBindingEntry(
            FlowAuthoringContractEntry entry,
            string catalogPath,
            FlowBuildValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(entry.ExpectedBindingType)) return;

            Type resolved = Type.GetType(entry.ExpectedBindingType, false);
            if (resolved == null)
            {
                TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>();
                for (int i = 0; i < types.Count; i++)
                {
                    Type candidate = types[i];
                    if (string.Equals(candidate.FullName, entry.ExpectedBindingType, StringComparison.Ordinal))
                    {
                        resolved = candidate;
                        break;
                    }
                }
            }

            if (resolved == null || !typeof(UnityEngine.Object).IsAssignableFrom(resolved))
            {
                result.Errors.Add(
                    $"{catalogPath}: Binding '{entry.Id}' ExpectedBindingType " +
                    $"'{entry.ExpectedBindingType}' cannot be resolved to a UnityEngine.Object type.");
            }
        }

        private static void ValidateOperation(
            string assetPath,
            FlowNodeData node,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            if (!string.Equals(node.TypeId, "flow.operation", StringComparison.Ordinal)) return;
            if (!TryGetString(node.Parameters, "operation", out string operationId)) return;

            if (!contracts.Operations.TryGetValue(operationId, out FlowAuthoringContractEntry contract))
            {
                if (rejectUnknown)
                    AddNodeError(result, assetPath, node.Id, $"unknown Operation contract '{operationId}'.");
                return;
            }

            IReadOnlyList<FlowAuthoringArgumentContract> arguments = contract.Arguments;
            var declared = new HashSet<string>(StringComparer.Ordinal);
            declared.Add("operation");
            for (int i = 0; i < arguments.Count; i++)
            {
                FlowAuthoringArgumentContract argument = arguments[i];
                if (argument == null || string.IsNullOrEmpty(argument.Key)) continue;
                declared.Add(argument.Key);

                FlowValue value = FlowValue.None;
                bool exists = node.Parameters != null &&
                              node.Parameters.TryGet(argument.Key, out value);
                if (!exists)
                {
                    if (argument.Required)
                        AddNodeError(result, assetPath, node.Id,
                            $"Operation '{operationId}' is missing required argument '{argument.Key}'.");
                    continue;
                }

                ValidateKind(
                    assetPath,
                    node.Id,
                    $"Operation '{operationId}' argument '{argument.Key}'",
                    argument.ValueKind,
                    value.Kind,
                    result);
            }

            if (contract.AllowAdditionalArguments || node.Parameters?.Entries == null) return;
            for (int i = 0; i < node.Parameters.Entries.Count; i++)
            {
                string key = node.Parameters.Entries[i].Key;
                if (!declared.Contains(key))
                    AddNodeError(result, assetPath, node.Id,
                        $"Operation '{operationId}' uses undeclared argument '{key}'.");
            }
        }

        private static void ValidateSignal(
            string assetPath,
            FlowNodeData node,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            bool isWait = string.Equals(node.TypeId, "flow.wait.signal", StringComparison.Ordinal);
            bool isEmit = string.Equals(node.TypeId, "flow.emit.signal", StringComparison.Ordinal);
            if (!isWait && !isEmit) return;
            if (!TryGetString(node.Parameters, "signal", out string signalId)) return;

            if (!contracts.Signals.TryGetValue(signalId, out FlowAuthoringContractEntry contract))
            {
                if (rejectUnknown)
                    AddNodeError(result, assetPath, node.Id, $"unknown Signal contract '{signalId}'.");
                return;
            }

            if (!isEmit || node.Parameters == null ||
                !node.Parameters.TryGet("payload", out FlowValue payload)) return;

            ValidateKind(assetPath, node.Id, $"Signal '{signalId}' payload",
                contract.ValueKind, payload.Kind, result);
        }

        private static void ValidateState(
            string assetPath,
            FlowNodeData node,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            bool usesState =
                string.Equals(node.TypeId, "flow.wait.state", StringComparison.Ordinal) ||
                string.Equals(node.TypeId, "flow.stable.for", StringComparison.Ordinal);
            if (!usesState || !TryGetString(node.Parameters, "state", out string stateId)) return;

            if (!contracts.States.TryGetValue(stateId, out FlowAuthoringContractEntry contract))
            {
                if (rejectUnknown)
                    AddNodeError(result, assetPath, node.Id, $"unknown State contract '{stateId}'.");
                return;
            }

            if (node.Parameters != null && node.Parameters.TryGet("expected", out FlowValue expected))
                ValidateKind(assetPath, node.Id, $"State '{stateId}' expected value",
                    contract.ValueKind, expected.Kind, result);
        }

        private static void ValidateBlackboard(
            string assetPath,
            FlowNodeData node,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            string keyProperty =
                string.Equals(node.TypeId, "flow.branch.bool", StringComparison.Ordinal) ? "blackboardKey" :
                string.Equals(node.TypeId, "flow.wait.blackboard", StringComparison.Ordinal) ||
                string.Equals(node.TypeId, "flow.set.blackboard", StringComparison.Ordinal) ||
                string.Equals(node.TypeId, "flow.increment.blackboard", StringComparison.Ordinal) ? "key" :
                null;
            if (keyProperty == null || !TryGetString(node.Parameters, keyProperty, out string key)) return;

            if (!contracts.BlackboardKeys.TryGetValue(key, out FlowAuthoringContractEntry contract))
            {
                if (rejectUnknown)
                    AddNodeError(result, assetPath, node.Id, $"unknown Blackboard contract '{key}'.");
                return;
            }

            if (string.Equals(node.TypeId, "flow.set.blackboard", StringComparison.Ordinal) &&
                node.Parameters != null && node.Parameters.TryGet("value", out FlowValue value))
            {
                ValidateKind(assetPath, node.Id, $"Blackboard '{key}' value",
                    contract.ValueKind, value.Kind, result);
            }
        }

        private static void ValidateBindings(
            string assetPath,
            FlowNodeData node,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            if (node.Parameters?.Entries == null) return;

            for (int i = 0; i < node.Parameters.Entries.Count; i++)
            {
                FlowValue value = node.Parameters.Entries[i].Value;
                if (value.Kind != FlowValueKind.BindingReference || !value.BindingReferenceValue.IsValid) continue;

                string bindingId = value.BindingReferenceValue.Id;
                if (!contracts.Bindings.ContainsKey(bindingId) && rejectUnknown)
                    AddNodeError(result, assetPath, node.Id, $"unknown Binding contract '{bindingId}'.");
            }
        }

        private static void ValidateCondition(
            string assetPath,
            string nodeId,
            FlowCondition condition,
            FlowContractCatalogSnapshot contracts,
            bool rejectUnknown,
            FlowBuildValidationResult result)
        {
            if (condition == null) return;

            if (condition.Kind == FlowConditionKind.State && rejectUnknown &&
                !contracts.States.ContainsKey(condition.Key ?? string.Empty))
            {
                AddNodeError(result, assetPath, nodeId,
                    $"condition references unknown State contract '{condition.Key}'.");
            }
            else if (condition.Kind == FlowConditionKind.Blackboard && rejectUnknown &&
                     !contracts.BlackboardKeys.ContainsKey(condition.Key ?? string.Empty))
            {
                AddNodeError(result, assetPath, nodeId,
                    $"condition references unknown Blackboard contract '{condition.Key}'.");
            }

            ValidateCondition(assetPath, nodeId, condition.Left, contracts, rejectUnknown, result);
            ValidateCondition(assetPath, nodeId, condition.Right, contracts, rejectUnknown, result);
            if (condition.Children == null) return;
            for (int i = 0; i < condition.Children.Count; i++)
                ValidateCondition(assetPath, nodeId, condition.Children[i], contracts, rejectUnknown, result);
        }

        private static bool TryGetString(FlowPropertyBag bag, string key, out string value)
        {
            if (bag != null && bag.TryGet(key, out FlowValue raw) &&
                raw.Kind == FlowValueKind.String && !string.IsNullOrWhiteSpace(raw.StringValue))
            {
                value = raw.StringValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static void ValidateKind(
            string assetPath,
            string nodeId,
            string label,
            FlowValueKind expected,
            FlowValueKind actual,
            FlowBuildValidationResult result)
        {
            if (expected == FlowValueKind.Any || expected == actual) return;
            AddNodeError(result, assetPath, nodeId,
                $"{label} expects {expected} but graph contains {actual}.");
        }

        private static void AddNodeError(
            FlowBuildValidationResult result,
            string assetPath,
            string nodeId,
            string message)
        {
            result.Errors.Add($"{assetPath} [{nodeId}]: {message}");
        }
    }
}
