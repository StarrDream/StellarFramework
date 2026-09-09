using System;
using System.Collections.Generic;
using UnityEditor;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal static class FlowAuthoringCatalogIndex
    {
        private static readonly SortedSet<string> Operations = new SortedSet<string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> Signals = new SortedSet<string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> States = new SortedSet<string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> BlackboardKeys = new SortedSet<string>(StringComparer.Ordinal);
        private static readonly SortedSet<string> Bindings = new SortedSet<string>(StringComparer.Ordinal);
        private static bool _dirty = true;

        static FlowAuthoringCatalogIndex()
        {
            EditorApplication.projectChanged += MarkDirty;
        }

        public static void MarkDirty() => _dirty = true;

        public static List<string> Collect(string semanticKey, FlowGraphData graph)
        {
            EnsureFresh();
            var result = new SortedSet<string>(StringComparer.Ordinal);
            AddCatalogIds(result, semanticKey);
            AddGraphIds(result, semanticKey, graph);
            return new List<string>(result);
        }

        public static List<string> CollectBindings()
        {
            EnsureFresh();
            return new List<string>(Bindings);
        }

        private static void AddCatalogIds(ISet<string> result, string semanticKey)
        {
            if (semanticKey == "operation") AddRange(result, Operations);
            else if (semanticKey == "signal") AddRange(result, Signals);
            else if (semanticKey == "state") AddRange(result, States);
            else if (semanticKey == "key" || semanticKey == "blackboardKey") AddRange(result, BlackboardKeys);
        }

        private static void AddGraphIds(ISet<string> result, string semanticKey, FlowGraphData graph)
        {
            if (graph?.Nodes == null || string.IsNullOrEmpty(semanticKey)) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FlowPropertyBag bag = graph.Nodes[i]?.Parameters;
                if (bag == null) continue;
                AddString(bag, semanticKey, result);
                if (semanticKey == "key") AddString(bag, "blackboardKey", result);
                else if (semanticKey == "blackboardKey") AddString(bag, "key", result);
            }
        }

        private static void EnsureFresh()
        {
            if (!_dirty) return;
            Operations.Clear();
            Signals.Clear();
            States.Clear();
            BlackboardKeys.Clear();
            Bindings.Clear();

            string[] guids = AssetDatabase.FindAssets("t:FlowAuthoringCatalog");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                FlowAuthoringCatalog catalog = AssetDatabase.LoadAssetAtPath<FlowAuthoringCatalog>(path);
                if (catalog == null) continue;
                AddEntries(Operations, catalog.Operations);
                AddEntries(Signals, catalog.Signals);
                AddEntries(States, catalog.States);
                AddEntries(BlackboardKeys, catalog.BlackboardKeys);
                AddEntries(Bindings, catalog.Bindings);
            }
            _dirty = false;
        }
        private static void AddEntries(ISet<string> target, IReadOnlyList<FlowAuthoringContractEntry> entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                FlowAuthoringContractEntry entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id)) target.Add(entry.Id);
            }
        }

        private static void AddRange(ISet<string> target, IEnumerable<string> values)
        {
            foreach (string value in values) target.Add(value);
        }

        private static void AddString(FlowPropertyBag bag, string key, ISet<string> target)
        {
            if (bag.TryGet(key, out FlowValue value) && value.Kind == FlowValueKind.String &&
                !string.IsNullOrWhiteSpace(value.StringValue)) target.Add(value.StringValue);
        }
    }
}
