using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>
    /// Stable Graph JSON entry point. A dedicated DTO is used because Unity JsonUtility
    /// cannot reliably serialize the recursive FlowCondition object model.
    /// </summary>
    public static class FlowGraphJson
    {
        private const int MaxConditionNodes = 1024;

        public static FlowGraphData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Flow JSON 不能为空。", nameof(json));
            GraphJsonData data = JsonUtility.FromJson<GraphJsonData>(json);
            if (data == null) throw new InvalidOperationException("Flow JSON 无法解析为 Graph。");
            return ToGraph(data);
        }

        public static string ToJson(FlowGraphData graph, bool prettyPrint = true)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            return JsonUtility.ToJson(FromGraph(graph), prettyPrint);
        }

        public static FlowGraphData FromTextAsset(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return FromJson(asset.text);
        }

        private static GraphJsonData FromGraph(FlowGraphData source)
        {
            var data = new GraphJsonData
            {
                FlowId = source.FlowId,
                SchemaVersion = source.SchemaVersion,
                EntryNodeId = source.EntryNodeId,
                Metadata = source.Metadata == null
                    ? new FlowGraphMetadata()
                    : new FlowGraphMetadata
                    {
                        DisplayName = source.Metadata.DisplayName,
                        Description = source.Metadata.Description
                    }
            };

            if (source.Nodes != null)
            {
                for (int i = 0; i < source.Nodes.Count; i++)
                    data.Nodes.Add(FromNode(source.Nodes[i]));
                data.Nodes.Sort((a, b) => string.CompareOrdinal(a.Id ?? string.Empty, b.Id ?? string.Empty));
            }

            if (source.Edges != null)
            {
                for (int i = 0; i < source.Edges.Count; i++)
                {
                    FlowEdgeData edge = source.Edges[i];
                    if (edge == null) throw new ArgumentException("Graph.Edges 不能包含 null。", nameof(source));
                    data.Edges.Add(new FlowEdgeData
                    {
                        FromNodeId = edge.FromNodeId,
                        FromPortId = edge.FromPortId,
                        ToNodeId = edge.ToNodeId,
                        ToPortId = edge.ToPortId
                    });
                }
                data.Edges.Sort((left, right) => string.CompareOrdinal(
                    EdgeSortKey(left), EdgeSortKey(right)));
            }
            return data;
        }

        private static NodeJsonData FromNode(FlowNodeData node)
        {
            if (node == null) throw new ArgumentException("Graph.Nodes 不能包含 null。", nameof(node));
            var result = new NodeJsonData
            {
                Id = node.Id,
                TypeId = node.TypeId,
                DefinitionVersion = node.DefinitionVersion,
                Options = node.Options == null
                    ? new FlowNodeOptions()
                    : new FlowNodeOptions
                    {
                        Enabled = node.Options.Enabled,
                        ResolveParametersOnEnter = node.Options.ResolveParametersOnEnter
                    },
                Parameters = SerializePropertyBag(node.Parameters),
                Condition = SerializeCondition(node.Condition)
            };
            return result;
        }

        private static FlowPropertyBag ClonePropertyBag(FlowPropertyBag source)
        {
            var result = new FlowPropertyBag();
            if (source?.Entries == null) return result;
            result.Entries.AddRange(source.Entries);
            result.Entries.Sort((left, right) =>
                string.CompareOrdinal(left.Key ?? string.Empty, right.Key ?? string.Empty));
            return result;
        }

        private static FlowGraphData ToGraph(GraphJsonData data)
        {
            var graph = new FlowGraphData
            {
                FlowId = data.FlowId,
                SchemaVersion = data.SchemaVersion,
                EntryNodeId = data.EntryNodeId,
                Metadata = data.Metadata ?? new FlowGraphMetadata()
            };

            if (data.Nodes != null)
            {
                for (int i = 0; i < data.Nodes.Count; i++)
                {
                    NodeJsonData node = data.Nodes[i];
                    if (node == null) throw new InvalidOperationException("Graph JSON Nodes 不能包含 null。");
                    graph.Nodes.Add(new FlowNodeData
                    {
                        Id = node.Id,
                        TypeId = node.TypeId,
                        DefinitionVersion = node.DefinitionVersion,
                        Parameters = DeserializePropertyBag(node.Parameters),
                        Options = node.Options ?? new FlowNodeOptions(),
                        Condition = DeserializeCondition(node.Condition)
                    });
                }
            }

            if (data.Edges != null)
            {
                for (int i = 0; i < data.Edges.Count; i++)
                    graph.Edges.Add(data.Edges[i]);
            }
            return graph;
        }

        private static PropertyBagJsonData SerializePropertyBag(FlowPropertyBag source)
        {
            var result = new PropertyBagJsonData();
            if (source?.Entries == null) return result;
            var entries = new List<FlowPropertyEntry>(source.Entries);
            entries.Sort((a,b) => string.CompareOrdinal(a.Key ?? string.Empty, b.Key ?? string.Empty));
            for (int i = 0; i < entries.Count; i++)
            {
                result.Entries.Add(new PropertyJsonData
                {
                    Key = entries[i].Key,
                    Value = SerializeValue(entries[i].Value)
                });
            }
            return result;
        }

        private static FlowPropertyBag DeserializePropertyBag(PropertyBagJsonData source)
        {
            var result = new FlowPropertyBag();
            if (source?.Entries == null) return result;
            for (int i = 0; i < source.Entries.Count; i++)
            {
                PropertyJsonData entry = source.Entries[i];
                if (entry == null) throw new InvalidOperationException("Property JSON contains null entry.");
                result.Entries.Add(new FlowPropertyEntry { Key = entry.Key, Value = DeserializeValue(entry.Value) });
            }
            return result;
        }

        private static FlowValueJsonData SerializeValue(FlowValue value)
        {
            var data = new FlowValueJsonData
            {
                Kind = (int)value.Kind,
                BoolValue = value.BoolValue,
                IntValue = value.IntValue,
                LongValue = value.LongValue,
                FloatValue = value.FloatValue,
                DoubleValue = value.DoubleValue,
                StringValue = value.StringValue,
                Vector2Value = value.Vector2Value,
                Vector3Value = value.Vector3Value,
                BindingReferenceValue = value.BindingReferenceValue,
                AssetValue = value.AssetValue,
                EnumValue = value.EnumValue
            };
            if (value.Kind == FlowValueKind.Binding)
                throw new InvalidOperationException("Runtime FlowBindingHandle cannot be serialized into Graph JSON.");
            return data;
        }

        private static FlowValue DeserializeValue(FlowValueJsonData data)
        {
            if (data == null) return FlowValue.None;
            if (data.Kind < 0 || data.Kind > (int)FlowValueKind.BindingReference)
                throw new InvalidOperationException("FlowValue JSON contains unknown kind: " + data.Kind);
            FlowValueKind kind = (FlowValueKind)data.Kind;
            switch (kind)
            {
                case FlowValueKind.None: return FlowValue.None;
                case FlowValueKind.Any: return new FlowValue { Kind = FlowValueKind.Any };
                case FlowValueKind.Bool: return FlowValue.FromBool(data.BoolValue);
                case FlowValueKind.Int: return FlowValue.FromInt(data.IntValue);

                case FlowValueKind.Long: return FlowValue.FromLong(data.LongValue);
                case FlowValueKind.Float: return FlowValue.FromFloat(data.FloatValue);
                case FlowValueKind.Double: return FlowValue.FromDouble(data.DoubleValue);
                case FlowValueKind.String: return FlowValue.FromString(data.StringValue);
                case FlowValueKind.Vector2: return FlowValue.FromVector2(data.Vector2Value);
                case FlowValueKind.Vector3: return FlowValue.FromVector3(data.Vector3Value);
                case FlowValueKind.Asset: return FlowValue.FromAsset(data.AssetValue.Id);
                case FlowValueKind.Enum: return FlowValue.FromEnum(data.EnumValue.Id);
                case FlowValueKind.BindingReference: return FlowValue.FromBindingReference(data.BindingReferenceValue.Id);
                case FlowValueKind.Binding:
                    throw new InvalidOperationException("Runtime FlowBindingHandle is not valid in Graph JSON.");
                default:
                    throw new InvalidOperationException("Unsupported FlowValue kind: " + kind);
            }
        }

        private static ConditionJsonData SerializeCondition(FlowCondition condition)
        {
            if (condition == null) return null;
            var data = new ConditionJsonData();
            var stack = new HashSet<FlowCondition>();
            data.Root = AppendConditionNode(condition, data.Nodes, stack, 0);
            return data;
        }

        private static int AppendConditionNode(
            FlowCondition condition,
            List<ConditionNodeJsonData> nodes,
            ISet<FlowCondition> stack,
            int depth)
        {
            if (condition == null) return -1;
            if (depth > 64) throw new InvalidOperationException("Condition nesting depth exceeds 64.");
            if (nodes.Count >= MaxConditionNodes)
                throw new InvalidOperationException($"Condition node count exceeds {MaxConditionNodes}.");
            if (!stack.Add(condition)) throw new InvalidOperationException("Condition graph contains a cycle.");

            int index = nodes.Count;
            var node = new ConditionNodeJsonData
            {
                Kind = condition.Kind,
                Constant = SerializeValue(condition.Constant),
                Key = condition.Key,
                Operator = condition.Operator
            };
            nodes.Add(node);
            node.Left = AppendConditionNode(condition.Left, nodes, stack, depth + 1);
            node.Right = AppendConditionNode(condition.Right, nodes, stack, depth + 1);
            if (condition.Children != null)
            {
                for (int i = 0; i < condition.Children.Count; i++)
                    node.Children.Add(AppendConditionNode(condition.Children[i], nodes, stack, depth + 1));
            }
            stack.Remove(condition);
            return index;
        }

        private static FlowCondition DeserializeCondition(ConditionJsonData data)
        {
            if (data == null || data.Nodes == null || data.Nodes.Count == 0) return null;
            if (data.Root < 0)
                throw new InvalidOperationException("Condition JSON has no root node.");
            if (data.Nodes.Count > MaxConditionNodes)
                throw new InvalidOperationException($"Condition node count exceeds {MaxConditionNodes}.");
            if (data.Root >= data.Nodes.Count)
                throw new InvalidOperationException("Condition root index is out of range.");

            var states = new byte[data.Nodes.Count];
            return BuildCondition(data.Root, data.Nodes, states, 0);
        }

        private static FlowCondition BuildCondition(
            int index,
            IReadOnlyList<ConditionNodeJsonData> nodes,
            byte[] states,
            int depth)
        {
            if (index < 0) return null;
            if (depth > 64) throw new InvalidOperationException("Condition nesting depth exceeds 64.");
            if (index >= nodes.Count) throw new InvalidOperationException("Condition node index is out of range.");
            if (states[index] == 1) throw new InvalidOperationException("Condition JSON contains a cycle.");

            ConditionNodeJsonData source = nodes[index];
            if (source == null) throw new InvalidOperationException("Condition JSON contains a null node.");
            if (!Enum.IsDefined(typeof(FlowConditionKind), source.Kind))
                throw new InvalidOperationException("Condition JSON contains an unknown condition kind.");
            if (!Enum.IsDefined(typeof(FlowComparisonOperator), source.Operator))
                throw new InvalidOperationException("Condition JSON contains an unknown comparison operator.");

            states[index] = 1;
            var result = new FlowCondition
            {
                Kind = source.Kind,
                Constant = DeserializeValue(source.Constant),
                Key = source.Key,
                Operator = source.Operator
            };
            result.Left = BuildCondition(source.Left, nodes, states, depth + 1);
            result.Right = BuildCondition(source.Right, nodes, states, depth + 1);
            if (source.Children != null)
            {
                for (int i = 0; i < source.Children.Count; i++)
                {
                    int childIndex = source.Children[i];
                    if (childIndex < 0)
                        throw new InvalidOperationException("Condition child index cannot be negative.");
                    result.Children.Add(BuildCondition(childIndex, nodes, states, depth + 1));
                }
            }
            states[index] = 0;
            return result;
        }

        private static string EdgeSortKey(FlowEdgeData edge)
        {
            return string.Concat(
                edge.FromNodeId ?? string.Empty, "\u001f",
                edge.FromPortId ?? string.Empty, "\u001f",
                edge.ToNodeId ?? string.Empty, "\u001f",
                edge.ToPortId ?? string.Empty);
        }

        [Serializable]
        private sealed class GraphJsonData
        {
            public string FlowId;
            public int SchemaVersion;
            public string EntryNodeId;
            public List<NodeJsonData> Nodes = new List<NodeJsonData>();
            public List<FlowEdgeData> Edges = new List<FlowEdgeData>();
            public FlowGraphMetadata Metadata = new FlowGraphMetadata();
        }

        [Serializable]
        private sealed class NodeJsonData
        {
            public string Id;
            public string TypeId;
            public int DefinitionVersion = 1;
            public PropertyBagJsonData Parameters = new PropertyBagJsonData();
            public FlowNodeOptions Options = new FlowNodeOptions();
            public ConditionJsonData Condition;
        }

        [Serializable]
        private sealed class PropertyBagJsonData
        {
            public List<PropertyJsonData> Entries = new List<PropertyJsonData>();
        }

        [Serializable]
        private sealed class PropertyJsonData
        {
            public string Key;
            public FlowValueJsonData Value = new FlowValueJsonData();
        }

        [Serializable]
        private sealed class FlowValueJsonData
        {
            public int Kind;
            public bool BoolValue;
            public int IntValue;
            public long LongValue;
            public float FloatValue;
            public double DoubleValue;
            public string StringValue;
            public FlowVector2 Vector2Value;
            public FlowVector3 Vector3Value;
            public FlowBindingReference BindingReferenceValue;
            public FlowAssetReference AssetValue;
            public FlowEnumReference EnumValue;
        }

        [Serializable]
        private sealed class ConditionJsonData
        {
            public int Root = -1;
            public List<ConditionNodeJsonData> Nodes = new List<ConditionNodeJsonData>();
        }

        [Serializable]
        private sealed class ConditionNodeJsonData
        {
            public FlowConditionKind Kind;
            public FlowValueJsonData Constant = new FlowValueJsonData();
            public string Key;
            public FlowComparisonOperator Operator;
            public int Left = -1;
            public int Right = -1;
            public List<int> Children = new List<int>();
        }
    }
}
