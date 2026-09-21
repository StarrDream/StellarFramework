using System;
using System.Collections.Generic;
using UnityEngine;
using StellarFramework.FlowKit;

namespace StellarFramework.FlowKit.Unity
{
    public enum FlowExternalCallKind
    {
        Command,
        Query,
        AsyncRequest,
        Presentation,
        Resource,
        Network,
        Other
    }

    [Serializable]
    public sealed class FlowAuthoringArgumentContract
    {
        [SerializeField] private string key;
        [SerializeField] private FlowValueKind valueKind = FlowValueKind.Any;
        [SerializeField] private bool required;

        public string Key => key ?? string.Empty;
        public FlowValueKind ValueKind => valueKind;
        public bool Required => required;
    }

    [Serializable]
    public sealed class FlowAuthoringContractEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;
        [Header("Value Contract")]
        [SerializeField] private FlowValueKind valueKind = FlowValueKind.Any;
        [Header("Operation Contract")]
        [SerializeField] private FlowExternalCallKind externalCallKind = FlowExternalCallKind.Command;
        [SerializeField] private FlowValueKind resultKind = FlowValueKind.Any;
        [SerializeField] private string requiredCapability;
        [SerializeField] private bool allowAdditionalArguments;
        [SerializeField] private List<FlowAuthoringArgumentContract> arguments = new List<FlowAuthoringArgumentContract>();
        [Header("Binding Contract")]
        [SerializeField] private string expectedBindingType;

        public string Id => id ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Description => description ?? string.Empty;
        public FlowValueKind ValueKind => valueKind;
        public FlowExternalCallKind ExternalCallKind => externalCallKind;
        public FlowValueKind ResultKind => resultKind;
        public string RequiredCapability => requiredCapability ?? string.Empty;
        public bool AllowAdditionalArguments => allowAdditionalArguments;
        public IReadOnlyList<FlowAuthoringArgumentContract> Arguments =>
            arguments ?? (IReadOnlyList<FlowAuthoringArgumentContract>)Array.Empty<FlowAuthoringArgumentContract>();
        public string ExpectedBindingType => expectedBindingType ?? string.Empty;
    }

    /// <summary>
    /// Stable authoring ID rules shared by project scaffolding and editor validation.
    /// Runtime does not depend on these conventions.
    /// </summary>
    public static class FlowAuthoringIdRules
    {
        public static bool IsValidContractId(string value, int minimumSegments = 3)
        {
            if (string.IsNullOrWhiteSpace(value) || minimumSegments < 1) return false;

            int segmentCount = 1;
            int segmentLength = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '.')
                {
                    if (segmentLength == 0) return false;
                    segmentCount++;
                    segmentLength = 0;
                    continue;
                }

                if (segmentLength == 0 && (c < 'a' || c > 'z')) return false;
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_';
                if (!valid) return false;
                segmentLength++;
            }

            return segmentLength > 0 && segmentCount >= minimumSegments;
        }

        public static bool IsValidArgumentKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_';
                if (!valid || (i == 0 && c >= '0' && c <= '9')) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Optional project authoring manifest used by FlowKit editor pickers and build validation.
    /// It does not register runtime adapters and is never required by FlowRunner.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FlowAuthoringCatalog",
        menuName = "StellarFramework/FlowKit/Authoring Catalog")]
    public sealed class FlowAuthoringCatalog : ScriptableObject
    {
        [Header("Validation Scope")]
        [Tooltip("When enabled, graphs in this catalog scope treat unknown Operation/Signal/State/Blackboard/Binding IDs as validation errors. Known IDs are always type-checked whether strict mode is enabled or not.")]
        [SerializeField] private bool strictUnknownReferences;
        [Tooltip("Exact FlowId values covered by strict validation. Empty means all FlowIds.")]
        [SerializeField] private List<string> strictFlowIds = new List<string>();
        [SerializeField] private List<FlowAuthoringContractEntry> operations = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> signals = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> states = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> blackboardKeys = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> bindings = new List<FlowAuthoringContractEntry>();

        public bool StrictUnknownReferences => strictUnknownReferences;
        public IReadOnlyList<string> StrictFlowIds =>
            strictFlowIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<FlowAuthoringContractEntry> Operations =>
            operations ?? (IReadOnlyList<FlowAuthoringContractEntry>)Array.Empty<FlowAuthoringContractEntry>();
        public IReadOnlyList<FlowAuthoringContractEntry> Signals =>
            signals ?? (IReadOnlyList<FlowAuthoringContractEntry>)Array.Empty<FlowAuthoringContractEntry>();
        public IReadOnlyList<FlowAuthoringContractEntry> States =>
            states ?? (IReadOnlyList<FlowAuthoringContractEntry>)Array.Empty<FlowAuthoringContractEntry>();
        public IReadOnlyList<FlowAuthoringContractEntry> BlackboardKeys =>
            blackboardKeys ?? (IReadOnlyList<FlowAuthoringContractEntry>)Array.Empty<FlowAuthoringContractEntry>();
        public IReadOnlyList<FlowAuthoringContractEntry> Bindings =>
            bindings ?? (IReadOnlyList<FlowAuthoringContractEntry>)Array.Empty<FlowAuthoringContractEntry>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateStrictFlowIds();
            ValidateList(operations, "Operation");
            ValidateList(signals, "Signal");
            ValidateList(states, "State");
            ValidateList(blackboardKeys, "Blackboard");
            ValidateList(bindings, "Binding");
        }

        private void ValidateStrictFlowIds()
        {
            if (strictFlowIds == null) return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < strictFlowIds.Count; i++)
            {
                string flowId = strictFlowIds[i];
                if (string.IsNullOrWhiteSpace(flowId))
                {
                    Debug.LogError($"Flow Authoring Catalog strict FlowId at index {i} is empty.", this);
                    continue;
                }

                if (!ids.Add(flowId))
                    Debug.LogError($"Flow Authoring Catalog contains duplicate strict FlowId: {flowId}", this);
            }
        }

        private void ValidateList(List<FlowAuthoringContractEntry> entries, string category)
        {
            if (entries == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                FlowAuthoringContractEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    Debug.LogError($"Flow Authoring Catalog {category} entry at index {i} has an empty ID.", this);
                    continue;
                }

                if (!ids.Add(entry.Id))
                    Debug.LogError($"Flow Authoring Catalog contains duplicate {category} ID: {entry.Id}", this);

                int minimumSegments = category == "Binding" ? 2 : 3;
                if (!FlowAuthoringIdRules.IsValidContractId(entry.Id, minimumSegments))
                    Debug.LogError(
                        $"Flow Authoring Catalog {category} ID must use lower_snake_case dot segments: {entry.Id}",
                        this);

                if (category != "Operation") continue;

                var argumentKeys = new HashSet<string>(StringComparer.Ordinal);
                IReadOnlyList<FlowAuthoringArgumentContract> arguments = entry.Arguments;
                for (int j = 0; j < arguments.Count; j++)
                {
                    FlowAuthoringArgumentContract argument = arguments[j];
                    if (argument == null || !FlowAuthoringIdRules.IsValidArgumentKey(argument.Key))
                    {
                        Debug.LogError(
                            $"Flow Authoring Catalog Operation '{entry.Id}' has invalid argument key at index {j}.",
                            this);
                        continue;
                    }

                    if (!argumentKeys.Add(argument.Key))
                        Debug.LogError(
                            $"Flow Authoring Catalog Operation '{entry.Id}' contains duplicate argument '{argument.Key}'.",
                            this);
                }
            }
        }
#endif
    }
}
