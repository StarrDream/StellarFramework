using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    [Serializable]
    internal sealed class FlowEditorMetadataData
    {
        public int Version = 2;
        public List<FlowEditorNodeMetadata> Nodes = new List<FlowEditorNodeMetadata>();
    }

    [Serializable]
    internal sealed class FlowEditorNodeMetadata
    {
        public string NodeId;
        public float X;
        public float Y;
        public bool Collapsed;
        public string DisplayName;
        public string Description;
    }

    [Serializable]
    internal sealed class FlowEditorClipboardData
    {
        public int Version = 2;
        public string RuntimeGraphJson;
        public List<FlowEditorNodeMetadata> Positions = new List<FlowEditorNodeMetadata>();
    }

    internal sealed class FlowGraphDocument
    {
        internal const string ClipboardPrefix = "STELLAR_FLOWKIT_CLIPBOARD_V1:";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly FlowNodeRegistry _registry;

        private FlowGraphDocument(FlowNodeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public FlowGraphData Graph { get; private set; }
        public FlowEditorMetadataData Metadata { get; private set; }
        public string RuntimePath { get; private set; }
        public bool IsDirty { get; private set; }

        public string EditorMetadataPath => string.IsNullOrEmpty(RuntimePath)
            ? string.Empty
            : Path.ChangeExtension(RuntimePath, "editor.json");

        public static FlowGraphDocument CreateNew(FlowNodeRegistry registry)
        {
            var document = new FlowGraphDocument(registry)
            {
                Graph = new FlowGraphData
                {
                    FlowId = "new.flow",
                    SchemaVersion = 1,
                    Metadata = new FlowGraphMetadata { DisplayName = "New Flow" }
                },
                Metadata = new FlowEditorMetadataData()
            };
            FlowNodeData entry = document.AddNode("flow.entry", new Vector2(80f, 120f));
            document.Graph.EntryNodeId = entry.Id;
            document.IsDirty = false;
            return document;
        }

        public static FlowGraphDocument Load(string runtimePath, FlowNodeRegistry registry)
        {
            if (string.IsNullOrEmpty(runtimePath)) throw new ArgumentException("Path cannot be empty.", nameof(runtimePath));
            if (!File.Exists(runtimePath)) throw new FileNotFoundException("Flow graph file not found.", runtimePath);

            var document = new FlowGraphDocument(registry)
            {
                RuntimePath = runtimePath,
                Graph = FlowGraphJson.FromJson(File.ReadAllText(runtimePath, Encoding.UTF8)),
                Metadata = new FlowEditorMetadataData(),
                IsDirty = false
            };

            string editorPath = document.EditorMetadataPath;
            if (File.Exists(editorPath))
            {
                FlowEditorMetadataData metadata = JsonUtility.FromJson<FlowEditorMetadataData>(File.ReadAllText(editorPath, Encoding.UTF8));
                if (metadata != null) document.Metadata = metadata;
            }

            document.EnsureMetadata();
            return document;
        }
        public void SetRuntimePath(string path)
        {
            RuntimePath = path ?? string.Empty;
        }

        public FlowNodeData AddNode(string typeId, Vector2 position)
        {
            var id = new FlowNodeTypeId(typeId);
            if (!_registry.TryGetDescriptor(id, out FlowNodeDescriptor descriptor))
                throw new InvalidOperationException($"Unknown Flow node type: {typeId}");

            var node = new FlowNodeData
            {
                Id = "node_" + Guid.NewGuid().ToString("N"),
                TypeId = descriptor.TypeId.Value,
                DefinitionVersion = descriptor.Version,
                Parameters = new FlowPropertyBag(),
                Options = new FlowNodeOptions()
            };
            Graph.Nodes.Add(node);
            Metadata.Nodes.Add(new FlowEditorNodeMetadata
            {
                NodeId = node.Id,
                X = position.x,
                Y = position.y
            });
            MarkDirty();
            return node;
        }
        public bool RemoveNode(string nodeId)
        {
            int index = FindNodeIndex(nodeId);
            if (index < 0) return false;
            Graph.Nodes.RemoveAt(index);
            for (int i = Graph.Edges.Count - 1; i >= 0; i--)
            {
                FlowEdgeData edge = Graph.Edges[i];
                if (edge.FromNodeId == nodeId || edge.ToNodeId == nodeId) Graph.Edges.RemoveAt(i);
            }
            for (int i = Metadata.Nodes.Count - 1; i >= 0; i--)
            {
                if (Metadata.Nodes[i].NodeId == nodeId) Metadata.Nodes.RemoveAt(i);
            }
            if (Graph.EntryNodeId == nodeId) Graph.EntryNodeId = string.Empty;
            MarkDirty();
            return true;
        }

        public void AddEdge(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            if (FindNodeIndex(fromNodeId) < 0 || FindNodeIndex(toNodeId) < 0)
                throw new InvalidOperationException("Cannot create an edge with missing nodes.");
            for (int i = 0; i < Graph.Edges.Count; i++)
            {
                FlowEdgeData edge = Graph.Edges[i];
                if (edge.FromNodeId == fromNodeId && edge.FromPortId == fromPortId &&
                    edge.ToNodeId == toNodeId && edge.ToPortId == toPortId) return;
            }
            Graph.Edges.Add(new FlowEdgeData
            {
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                ToNodeId = toNodeId,
                ToPortId = toPortId
            });
            MarkDirty();
        }

        public bool RemoveEdge(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            for (int i = 0; i < Graph.Edges.Count; i++)
            {
                FlowEdgeData edge = Graph.Edges[i];
                if (edge.FromNodeId == fromNodeId && edge.FromPortId == fromPortId &&
                    edge.ToNodeId == toNodeId && edge.ToPortId == toPortId)
                {
                    Graph.Edges.RemoveAt(i);
                    MarkDirty();
                    return true;
                }
            }
            return false;
        }

        public void SetParameter(string nodeId, string key, FlowValue value)
        {
            FlowNodeData node = FindNode(nodeId) ?? throw new InvalidOperationException($"Node not found: {nodeId}");
            node.Parameters.Set(key, value);
            MarkDirty();
        }
        public bool RemoveParameter(string nodeId, string key)
        {
            FlowNodeData node = FindNode(nodeId);
            if (node == null || node.Parameters?.Entries == null) return false;
            for (int i = 0; i < node.Parameters.Entries.Count; i++)
            {
                if (!string.Equals(node.Parameters.Entries[i].Key, key, StringComparison.Ordinal)) continue;
                node.Parameters.Entries.RemoveAt(i);
                MarkDirty();
                return true;
            }
            return false;
        }

        public void SetNodePosition(string nodeId, Vector2 position)
        {
            FlowEditorNodeMetadata metadata = GetOrCreateMetadata(nodeId);
            if (Mathf.Approximately(metadata.X, position.x) && Mathf.Approximately(metadata.Y, position.y)) return;
            metadata.X = position.x;
            metadata.Y = position.y;
            MarkDirty();
        }

        public Vector2 GetNodePosition(string nodeId, int fallbackIndex)
        {
            FlowEditorNodeMetadata metadata = GetOrCreateMetadata(nodeId);
            if (Mathf.Approximately(metadata.X, 0f) && Mathf.Approximately(metadata.Y, 0f) && fallbackIndex > 0)
            {
                metadata.X = 80f + (fallbackIndex % 4) * 260f;
                metadata.Y = 120f + (fallbackIndex / 4) * 170f;
            }
            return new Vector2(metadata.X, metadata.Y);
        }

        public string GetNodeDisplayName(string nodeId) =>
            GetOrCreateMetadata(nodeId).DisplayName ?? string.Empty;

        public string GetNodeDescription(string nodeId) =>
            GetOrCreateMetadata(nodeId).Description ?? string.Empty;

        public void SetNodeDisplayName(string nodeId, string displayName)
        {
            FlowEditorNodeMetadata metadata = GetOrCreateMetadata(nodeId);
            string value = displayName ?? string.Empty;
            if (string.Equals(metadata.DisplayName ?? string.Empty, value, StringComparison.Ordinal)) return;
            metadata.DisplayName = value;
            MarkDirty();
        }

        public void SetNodeDescription(string nodeId, string description)
        {
            FlowEditorNodeMetadata metadata = GetOrCreateMetadata(nodeId);
            string value = description ?? string.Empty;
            if (string.Equals(metadata.Description ?? string.Empty, value, StringComparison.Ordinal)) return;
            metadata.Description = value;
            MarkDirty();
        }

        public FlowNodeData FindNode(string nodeId)
        {
            int index = FindNodeIndex(nodeId);
            return index >= 0 ? Graph.Nodes[index] : null;
        }

        public void SetFlowId(string flowId)
        {
            if (string.IsNullOrWhiteSpace(flowId)) throw new ArgumentException("FlowId cannot be empty.", nameof(flowId));
            if (Graph.FlowId == flowId) return;
            Graph.FlowId = flowId.Trim();
            MarkDirty();
        }

        public void SetEntryNode(string nodeId)
        {
            if (FindNode(nodeId) == null) throw new InvalidOperationException($"Node not found: {nodeId}");
            if (string.Equals(Graph.EntryNodeId, nodeId, StringComparison.Ordinal)) return;
            Graph.EntryNodeId = nodeId;
            MarkDirty();
        }

        public void SetCondition(string nodeId, FlowCondition condition)
        {
            FlowNodeData node = FindNode(nodeId) ?? throw new InvalidOperationException($"Node not found: {nodeId}");
            node.Condition = CloneCondition(condition);
            MarkDirty();
        }

        public void SetDisplayName(string displayName)
        {
            if (Graph.Metadata == null) Graph.Metadata = new FlowGraphMetadata();
            string value = displayName ?? string.Empty;
            if (Graph.Metadata.DisplayName == value) return;
            Graph.Metadata.DisplayName = value;
            MarkDirty();
        }

        public void SetDescription(string description)
        {
            if (Graph.Metadata == null) Graph.Metadata = new FlowGraphMetadata();
            string value = description ?? string.Empty;
            if (Graph.Metadata.Description == value) return;
            Graph.Metadata.Description = value;
            MarkDirty();
        }

        public string CreateClipboard(IReadOnlyList<string> nodeIds)
        {
            if (nodeIds == null || nodeIds.Count == 0) return string.Empty;
            var selected = new HashSet<string>(nodeIds, StringComparer.Ordinal);
            var clipboardGraph = new FlowGraphData
            {
                FlowId = "editor.clipboard",
                SchemaVersion = Graph.SchemaVersion,
                EntryNodeId = selected.Contains(Graph.EntryNodeId) ? Graph.EntryNodeId : string.Empty
            };
            var data = new FlowEditorClipboardData();
            for (int i = 0; i < Graph.Nodes.Count; i++)
            {
                FlowNodeData node = Graph.Nodes[i];
                if (node == null || !selected.Contains(node.Id)) continue;
                clipboardGraph.Nodes.Add(CloneNode(node));
                FlowEditorNodeMetadata metadata = GetOrCreateMetadata(node.Id);
                data.Positions.Add(new FlowEditorNodeMetadata
                {
                    NodeId = node.Id, X = metadata.X, Y = metadata.Y, Collapsed = metadata.Collapsed,
                    DisplayName = metadata.DisplayName, Description = metadata.Description
                });
            }
            for (int i = 0; i < Graph.Edges.Count; i++)
            {
                FlowEdgeData edge = Graph.Edges[i];
                if (edge != null && selected.Contains(edge.FromNodeId) && selected.Contains(edge.ToNodeId))
                    clipboardGraph.Edges.Add(CloneEdge(edge));
            }
            data.RuntimeGraphJson = FlowGraphJson.ToJson(clipboardGraph, false);
            return ClipboardPrefix + JsonUtility.ToJson(data, false);
        }

        public List<string> PasteClipboard(string payload, Vector2 offset)
        {
            var pasted = new List<string>();
            if (string.IsNullOrEmpty(payload) || !payload.StartsWith(ClipboardPrefix, StringComparison.Ordinal))
                return pasted;

            FlowEditorClipboardData data = JsonUtility.FromJson<FlowEditorClipboardData>(
                payload.Substring(ClipboardPrefix.Length));
            if (data == null || data.Version != 2 || string.IsNullOrEmpty(data.RuntimeGraphJson))
                return pasted;

            FlowGraphData sourceGraph = FlowGraphJson.FromJson(data.RuntimeGraphJson);
            var metadataByNode = new Dictionary<string, FlowEditorNodeMetadata>(StringComparer.Ordinal);
            if (data.Positions != null)
            {
                for (int i = 0; i < data.Positions.Count; i++)
                {
                    FlowEditorNodeMetadata metadata = data.Positions[i];
                    if (metadata != null && !string.IsNullOrEmpty(metadata.NodeId))
                        metadataByNode[metadata.NodeId] = metadata;
                }
            }

            var remap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < sourceGraph.Nodes.Count; i++)
            {
                FlowNodeData source = sourceGraph.Nodes[i];
                if (source == null || string.IsNullOrEmpty(source.Id)) continue;
                FlowEditorNodeMetadata sourceMetadata = metadataByNode.TryGetValue(source.Id, out FlowEditorNodeMetadata saved) ? saved : null;
                Vector2 position = sourceMetadata != null ? new Vector2(sourceMetadata.X, sourceMetadata.Y) + offset : offset;
                FlowNodeData clone = AddNode(source.TypeId, position);
                clone.DefinitionVersion = source.DefinitionVersion;
                clone.Parameters = ClonePropertyBag(source.Parameters);
                clone.Condition = CloneCondition(source.Condition);
                clone.Options = CloneOptions(source.Options);
                if (sourceMetadata != null)
                {
                    FlowEditorNodeMetadata cloneMetadata = GetOrCreateMetadata(clone.Id);
                    cloneMetadata.Collapsed = sourceMetadata.Collapsed;
                    cloneMetadata.DisplayName = sourceMetadata.DisplayName;
                    cloneMetadata.Description = sourceMetadata.Description;
                }
                remap[source.Id] = clone.Id;
                pasted.Add(clone.Id);
            }
            for (int i = 0; i < sourceGraph.Edges.Count; i++)
            {
                FlowEdgeData edge = sourceGraph.Edges[i];
                if (edge == null || !remap.TryGetValue(edge.FromNodeId, out string from) ||
                    !remap.TryGetValue(edge.ToNodeId, out string to)) continue;
                AddEdge(from, edge.FromPortId, to, edge.ToPortId);
            }
            return pasted;
        }

        public static bool CanPasteClipboard(string payload) =>
            !string.IsNullOrEmpty(payload) && payload.StartsWith(ClipboardPrefix, StringComparison.Ordinal);

        private static FlowNodeData CloneNode(FlowNodeData source)
        {
            return new FlowNodeData
            {
                Id = source.Id,
                TypeId = source.TypeId,
                DefinitionVersion = source.DefinitionVersion,
                Parameters = ClonePropertyBag(source.Parameters),
                Condition = CloneCondition(source.Condition),
                Options = CloneOptions(source.Options)
            };
        }

        private static FlowCondition CloneCondition(FlowCondition source)
        {
            if (source == null) return null;
            var clone = new FlowCondition
            {
                Kind = source.Kind, Constant = source.Constant, Key = source.Key,
                Operator = source.Operator, Left = CloneCondition(source.Left), Right = CloneCondition(source.Right)
            };
            if (source.Children != null)
                for (int i = 0; i < source.Children.Count; i++) clone.Children.Add(CloneCondition(source.Children[i]));
            return clone;
        }

        private static FlowPropertyBag ClonePropertyBag(FlowPropertyBag source)
        {
            var result = new FlowPropertyBag();
            if (source?.Entries == null) return result;
            for (int i = 0; i < source.Entries.Count; i++) result.Entries.Add(source.Entries[i]);
            return result;
        }

        private static FlowNodeOptions CloneOptions(FlowNodeOptions source)
        {
            return source == null ? new FlowNodeOptions() : new FlowNodeOptions
            {
                Enabled = source.Enabled,
                ResolveParametersOnEnter = source.ResolveParametersOnEnter
            };
        }

        private static FlowEdgeData CloneEdge(FlowEdgeData source)
        {
            return new FlowEdgeData
            {
                FromNodeId = source.FromNodeId, FromPortId = source.FromPortId,
                ToNodeId = source.ToNodeId, ToPortId = source.ToPortId
            };
        }

        public string SerializeGraph() => FlowGraphJson.ToJson(Graph, true);

        public string SerializeMetadata()
        {
            var stable = new FlowEditorMetadataData { Version = Metadata.Version };
            stable.Nodes.AddRange(Metadata.Nodes);
            stable.Nodes.Sort((left, right) => string.CompareOrdinal(left.NodeId ?? string.Empty, right.NodeId ?? string.Empty));
            return JsonUtility.ToJson(stable, true);
        }

        public void RestoreSnapshots(string graphJson, string metadataJson)
        {
            Graph = FlowGraphJson.FromJson(graphJson);
            Metadata = string.IsNullOrEmpty(metadataJson)
                ? new FlowEditorMetadataData()
                : JsonUtility.FromJson<FlowEditorMetadataData>(metadataJson) ?? new FlowEditorMetadataData();
            EnsureMetadata();
            IsDirty = true;
        }
        public void Save()
        {
            if (string.IsNullOrEmpty(RuntimePath)) throw new InvalidOperationException("Runtime path is not set.");
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimePath) ?? string.Empty);
            File.WriteAllText(RuntimePath, SerializeGraph(), Utf8NoBom);
            File.WriteAllText(EditorMetadataPath, SerializeMetadata(), Utf8NoBom);
            AssetDatabase.Refresh();
            IsDirty = false;
        }

        private int FindNodeIndex(string nodeId)
        {
            if (Graph?.Nodes == null || string.IsNullOrEmpty(nodeId)) return -1;
            for (int i = 0; i < Graph.Nodes.Count; i++)
            {
                if (Graph.Nodes[i] != null && string.Equals(Graph.Nodes[i].Id, nodeId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private FlowEditorNodeMetadata GetOrCreateMetadata(string nodeId)
        {
            EnsureMetadata();
            for (int i = 0; i < Metadata.Nodes.Count; i++)
            {
                if (string.Equals(Metadata.Nodes[i].NodeId, nodeId, StringComparison.Ordinal)) return Metadata.Nodes[i];
            }
            var metadata = new FlowEditorNodeMetadata { NodeId = nodeId };
            Metadata.Nodes.Add(metadata);
            return metadata;
        }
        private void EnsureMetadata()
        {
            if (Metadata == null) Metadata = new FlowEditorMetadataData();
            if (Metadata.Version < 2) Metadata.Version = 2;
            if (Metadata.Nodes == null) Metadata.Nodes = new List<FlowEditorNodeMetadata>();
            if (Graph?.Nodes == null) return;
            for (int i = 0; i < Graph.Nodes.Count; i++)
            {
                FlowNodeData node = Graph.Nodes[i];
                if (node != null) GetOrCreateMetadataWithoutRecursion(node.Id);
            }
        }

        private FlowEditorNodeMetadata GetOrCreateMetadataWithoutRecursion(string nodeId)
        {
            for (int i = 0; i < Metadata.Nodes.Count; i++)
            {
                if (string.Equals(Metadata.Nodes[i].NodeId, nodeId, StringComparison.Ordinal)) return Metadata.Nodes[i];
            }
            var metadata = new FlowEditorNodeMetadata { NodeId = nodeId };
            Metadata.Nodes.Add(metadata);
            return metadata;
        }

        private void MarkDirty()
        {
            IsDirty = true;
        }
    }

    internal sealed class FlowEditorUndoState : ScriptableObject
    {
        public string GraphJson;
        public string MetadataJson;
        public string RuntimePath;
    }
}
