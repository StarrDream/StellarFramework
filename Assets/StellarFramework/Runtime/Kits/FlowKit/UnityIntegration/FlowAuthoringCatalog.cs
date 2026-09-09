using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    [Serializable]
    public sealed class FlowAuthoringContractEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;

        public string Id => id ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Description => description ?? string.Empty;
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
        [SerializeField] private List<FlowAuthoringContractEntry> operations = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> signals = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> states = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> blackboardKeys = new List<FlowAuthoringContractEntry>();
        [SerializeField] private List<FlowAuthoringContractEntry> bindings = new List<FlowAuthoringContractEntry>();

        public IReadOnlyList<FlowAuthoringContractEntry> Operations => operations;
        public IReadOnlyList<FlowAuthoringContractEntry> Signals => signals;
        public IReadOnlyList<FlowAuthoringContractEntry> States => states;
        public IReadOnlyList<FlowAuthoringContractEntry> BlackboardKeys => blackboardKeys;
        public IReadOnlyList<FlowAuthoringContractEntry> Bindings => bindings;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateList(operations, "Operation");
            ValidateList(signals, "Signal");
            ValidateList(states, "State");
            ValidateList(blackboardKeys, "Blackboard");
            ValidateList(bindings, "Binding");
        }
        private void ValidateList(List<FlowAuthoringContractEntry> entries, string category)
        {
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
            }
        }
#endif
    }
}
