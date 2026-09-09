using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>Graph 的文本入口。JSON 只保存稳定定义，不保存运行态对象或 CLR 类型名。</summary>
    public static class FlowGraphJson
    {
        public static FlowGraphData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Flow JSON 不能为空。", nameof(json));
            FlowGraphData graph = JsonUtility.FromJson<FlowGraphData>(json);
            if (graph == null) throw new InvalidOperationException("Flow JSON 无法解析为 Graph。");
            return graph;
        }

        public static string ToJson(FlowGraphData graph, bool prettyPrint = true)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            return JsonUtility.ToJson(CreateStableCopy(graph), prettyPrint);
        }

        public static FlowGraphData FromTextAsset(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return FromJson(asset.text);
        }

        private static FlowGraphData CreateStableCopy(FlowGraphData source)
        {
            var copy = new FlowGraphData
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
                {
                    FlowNodeData node = source.Nodes[i];
                    if (node == null) throw new ArgumentException("Graph.Nodes 不能包含 null。", nameof(source));
                    var nodeCopy = new FlowNodeData
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
                        Parameters = new FlowPropertyBag()
                    };
                    if (node.Parameters != null && node.Parameters.Entries != null)
                    {
                        nodeCopy.Parameters.Entries.AddRange(node.Parameters.Entries);
                        nodeCopy.Parameters.Entries.Sort((left, right) =>
                            string.CompareOrdinal(left.Key ?? string.Empty, right.Key ?? string.Empty));
                    }

                    copy.Nodes.Add(nodeCopy);
                }

                copy.Nodes.Sort((left, right) => string.CompareOrdinal(left.Id ?? string.Empty, right.Id ?? string.Empty));
            }

            if (source.Edges != null)
            {
                for (int i = 0; i < source.Edges.Count; i++)
                {
                    FlowEdgeData edge = source.Edges[i];
                    if (edge == null) throw new ArgumentException("Graph.Edges 不能包含 null。", nameof(source));
                    copy.Edges.Add(new FlowEdgeData
                    {
                        FromNodeId = edge.FromNodeId,
                        FromPortId = edge.FromPortId,
                        ToNodeId = edge.ToNodeId,
                        ToPortId = edge.ToPortId
                    });
                }

                copy.Edges.Sort((left, right) => string.CompareOrdinal(
                    $"{left.FromNodeId}\u001f{left.FromPortId}\u001f{left.ToNodeId}\u001f{left.ToPortId}",
                    $"{right.FromNodeId}\u001f{right.FromPortId}\u001f{right.ToNodeId}\u001f{right.ToPortId}"));
            }

            return copy;
        }
    }
}
