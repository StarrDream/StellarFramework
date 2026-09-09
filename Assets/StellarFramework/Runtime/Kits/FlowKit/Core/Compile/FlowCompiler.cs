using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    public interface IFlowNodeHandler
    {
        void Start(in FlowNodeExecutionContext context, in FlowCompiledNode node, FlowNodeHandle handle);
        void Cancel(in FlowNodeExecutionContext context, in FlowCompiledNode node);
    }

    public interface IFlowNodeRegistry
    {
        bool TryGetDescriptor(FlowNodeTypeId typeId, out FlowNodeDescriptor descriptor);
        bool TryGetHandler(FlowNodeTypeId typeId, out IFlowNodeHandler handler);
    }

    /// <summary>编译后输出端口的稳定路由；保留目标输入端口，避免 Edge 映射在运行时丢失。</summary>
    public readonly struct FlowOutputRoute
    {
        public int NodeIndex { get; }
        public string InputPort { get; }

        public FlowOutputRoute(int nodeIndex, string inputPort)
        {
            if (nodeIndex < 0) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            if (string.IsNullOrEmpty(inputPort)) throw new ArgumentException("目标输入端口不能为空。", nameof(inputPort));
            NodeIndex = nodeIndex;
            InputPort = inputPort;
        }
    }

    /// <summary>
    /// 显式注册表。Editor 可以生成 Register 调用，Runtime 不扫描 Assembly、不使用反射。
    /// </summary>
    public sealed class FlowNodeRegistry : IFlowNodeRegistry
    {
        private readonly Dictionary<FlowNodeTypeId, FlowNodeDescriptor> _descriptors =
            new Dictionary<FlowNodeTypeId, FlowNodeDescriptor>();
        private readonly Dictionary<FlowNodeTypeId, IFlowNodeHandler> _handlers =
            new Dictionary<FlowNodeTypeId, IFlowNodeHandler>();
        private readonly List<FlowNodeDescriptor> _descriptorList = new List<FlowNodeDescriptor>();
        private readonly IReadOnlyList<FlowNodeDescriptor> _descriptorListView;

        public FlowNodeRegistry()
        {
            _descriptorListView = _descriptorList.AsReadOnly();
        }

        public int Count => _descriptors.Count;
        public IReadOnlyList<FlowNodeDescriptor> Descriptors => _descriptorListView;

        public FlowNodeRegistry Register(FlowNodeDescriptor descriptor, IFlowNodeHandler handler)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_descriptors.ContainsKey(descriptor.TypeId))
            {
                throw new InvalidOperationException($"Flow 节点 TypeId 已注册: {descriptor.TypeId}");
            }

            _descriptors.Add(descriptor.TypeId, descriptor);
            _handlers.Add(descriptor.TypeId, handler);
            _descriptorList.Add(descriptor);
            return this;
        }

        public bool TryGetDescriptor(FlowNodeTypeId typeId, out FlowNodeDescriptor descriptor) =>
            _descriptors.TryGetValue(typeId, out descriptor);

        public bool TryGetHandler(FlowNodeTypeId typeId, out IFlowNodeHandler handler) =>
            _handlers.TryGetValue(typeId, out handler);
    }

    public sealed class FlowCompiledNode
    {
        private readonly Dictionary<string, IReadOnlyList<int>> _outputTargets;
        private readonly Dictionary<string, IReadOnlyList<FlowOutputRoute>> _outputRoutes;

        public int Index { get; }
        public string NodeId { get; }
        public FlowNodeTypeId TypeId { get; }
        public int DefinitionVersion { get; }
        public FlowPropertyBagSnapshot Parameters { get; }
        public FlowCompiledCondition Condition { get; }
        public FlowNodeDescriptor Descriptor { get; }
        public IFlowNodeHandler Handler { get; }

        internal FlowCompiledNode(
            int index,
            string nodeId,
            FlowNodeTypeId typeId,
            int definitionVersion,
            FlowPropertyBagSnapshot parameters,
            FlowCompiledCondition condition,
            FlowNodeDescriptor descriptor,
            IFlowNodeHandler handler,
            Dictionary<string, FlowOutputRoute[]> outputRoutes)
        {
            Index = index;
            NodeId = nodeId;
            TypeId = typeId;
            DefinitionVersion = definitionVersion;
            Parameters = parameters;
            Condition = condition;
            Descriptor = descriptor;
            Handler = handler;
            _outputTargets = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            _outputRoutes = new Dictionary<string, IReadOnlyList<FlowOutputRoute>>(StringComparer.Ordinal);
            if (outputRoutes == null) return;
            foreach (KeyValuePair<string, FlowOutputRoute[]> route in outputRoutes)
            {
                FlowOutputRoute[] routes = route.Value ?? Array.Empty<FlowOutputRoute>();
                FlowOutputRoute[] routeCopy = (FlowOutputRoute[])routes.Clone();
                _outputRoutes.Add(route.Key, Array.AsReadOnly(routeCopy));
                int[] targetCopy = new int[routeCopy.Length];
                for (int i = 0; i < routeCopy.Length; i++) targetCopy[i] = routeCopy[i].NodeIndex;
                _outputTargets.Add(route.Key, Array.AsReadOnly(targetCopy));
            }
        }

        public IReadOnlyList<int> GetOutputTargets(string portId)
        {
            if (portId != null && _outputTargets.TryGetValue(portId, out IReadOnlyList<int> targets))
            {
                return targets;
            }

            return Array.Empty<int>();
        }

        public IReadOnlyList<FlowOutputRoute> GetOutputRoutes(string portId)
        {
            if (portId != null && _outputRoutes.TryGetValue(portId, out IReadOnlyList<FlowOutputRoute> routes))
            {
                return routes;
            }

            return Array.Empty<FlowOutputRoute>();
        }
    }

    public sealed class FlowCompiledPlan
    {
        private readonly FlowCompiledNode[] _nodes;
        private readonly FlowCapabilityId[] _requiredCapabilities;
        private readonly Dictionary<string, int> _nodeIndices;
        private readonly IReadOnlyList<FlowCompiledNode> _nodesView;
        private readonly IReadOnlyList<FlowCapabilityId> _requiredCapabilitiesView;

        public string FlowId { get; }
        public int SchemaVersion { get; }
        public int EntryNodeIndex { get; }
        public ulong PlanHash { get; }
        public IReadOnlyList<FlowCompiledNode> Nodes => _nodesView;
        public IReadOnlyList<FlowCapabilityId> RequiredCapabilities => _requiredCapabilitiesView;
        public int NodeCount => _nodes.Length;

        internal FlowCompiledPlan(
            string flowId,
            int schemaVersion,
            int entryNodeIndex,
            ulong planHash,
            FlowCompiledNode[] nodes,
            FlowCapabilityId[] requiredCapabilities,
            Dictionary<string, int> nodeIndices)
        {
            FlowId = flowId;
            SchemaVersion = schemaVersion;
            EntryNodeIndex = entryNodeIndex;
            PlanHash = planHash;
            _nodes = nodes ?? Array.Empty<FlowCompiledNode>();
            _requiredCapabilities = requiredCapabilities ?? Array.Empty<FlowCapabilityId>();
            _nodeIndices = nodeIndices ?? new Dictionary<string, int>(StringComparer.Ordinal);
            _nodesView = Array.AsReadOnly(_nodes);
            _requiredCapabilitiesView = Array.AsReadOnly(_requiredCapabilities);
        }

        public FlowCompiledNode GetNode(int index) => _nodes[index];

        public bool TryGetNodeIndex(string nodeId, out int index)
        {
            if (!string.IsNullOrEmpty(nodeId) && _nodeIndices.TryGetValue(nodeId, out index)) return true;
            index = -1;
            return false;
        }
    }

    public interface IFlowGraphMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        FlowMigrationResult Migrate(FlowGraphData graph);
    }

    /// <summary>显式版本迁移链。不会自动把 Graph 升级到“最新版本”。</summary>
    public sealed class FlowGraphMigrationPipeline
    {
        private readonly List<IFlowGraphMigrator> _migrators = new List<IFlowGraphMigrator>();

        public FlowGraphMigrationPipeline Register(IFlowGraphMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));
            if (migrator.ToVersion <= migrator.FromVersion)
            {
                throw new ArgumentException("Flow migrator 的 ToVersion 必须大于 FromVersion。", nameof(migrator));
            }

            for (int i = 0; i < _migrators.Count; i++)
            {
                if (_migrators[i].FromVersion == migrator.FromVersion)
                {
                    throw new InvalidOperationException($"Flow schema {migrator.FromVersion} 已存在迁移器。");
                }
            }

            _migrators.Add(migrator);
            return this;
        }

        public FlowMigrationResult Migrate(FlowGraphData graph, int targetVersion)
        {
            if (graph == null)
            {
                return new FlowMigrationResult(null, new List<FlowValidationIssue>
                {
                    new FlowValidationIssue(FlowValidationSeverity.Error, FlowValidationErrorCode.MigrationFailed, "Graph 不能为空。")
                }, false);
            }

            if (targetVersion <= 0 || graph.SchemaVersion > targetVersion)
            {
                return new FlowMigrationResult(null, new List<FlowValidationIssue>
                {
                    new FlowValidationIssue(FlowValidationSeverity.Error, FlowValidationErrorCode.MigrationFailed,
                        $"无法从 schema {graph.SchemaVersion} 迁移到 {targetVersion}。")
                }, false);
            }

            var issues = new List<FlowValidationIssue>();
            int guard = 0;
            while (graph.SchemaVersion < targetVersion)
            {
                if (++guard > _migrators.Count + 1)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MigrationFailed, "检测到迁移链循环。"));
                    return new FlowMigrationResult(null, issues, false);
                }

                IFlowGraphMigrator migrator = null;
                for (int i = 0; i < _migrators.Count; i++)
                {
                    if (_migrators[i].FromVersion == graph.SchemaVersion)
                    {
                        migrator = _migrators[i];
                        break;
                    }
                }

                if (migrator == null)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MissingMigrator,
                        $"缺少 schema {graph.SchemaVersion} 到下一版本的迁移器。"));
                    return new FlowMigrationResult(null, issues, false);
                }

                FlowMigrationResult result = migrator.Migrate(graph);
                if (result == null || !result.Succeeded || result.Graph == null)
                {
                    if (result != null && result.Issues != null)
                    {
                        for (int i = 0; i < result.Issues.Count; i++) issues.Add(result.Issues[i]);
                    }

                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MigrationFailed,
                        $"schema {graph.SchemaVersion} 的迁移失败。"));
                    return new FlowMigrationResult(null, issues, false);
                }

                if (result.Graph.SchemaVersion != migrator.ToVersion || result.Graph.SchemaVersion > targetVersion)
                {
                    issues.Add(new FlowValidationIssue(
                        FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MigrationFailed,
                        $"迁移器声明目标版本 {migrator.ToVersion}，实际返回 {result.Graph.SchemaVersion}，或超过目标版本 {targetVersion}。"));
                    return new FlowMigrationResult(null, issues, false);
                }

                graph = result.Graph;
                for (int i = 0; i < result.Issues.Count; i++) issues.Add(result.Issues[i]);
            }

            return new FlowMigrationResult(graph, issues, true);
        }
    }

    public static class FlowCompiler
    {
        public static FlowCompileResult Compile(FlowGraphData graph, IFlowNodeRegistry registry)
        {
            var issues = new List<FlowValidationIssue>();
            if (graph == null)
            {
                issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                    FlowValidationErrorCode.MissingFlowId, "Graph 不能为空。"));
                return new FlowCompileResult(null, issues);
            }

            if (string.IsNullOrEmpty(graph.FlowId))
            {
                issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                    FlowValidationErrorCode.MissingFlowId, "FlowId 不能为空。"));
            }

            if (graph.SchemaVersion <= 0)
            {
                issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                    FlowValidationErrorCode.InvalidSchemaVersion, "SchemaVersion 必须大于 0。"));
            }

            if (registry == null)
            {
                issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                    FlowValidationErrorCode.UnknownTypeId, "节点 Registry 不能为空。"));
                return new FlowCompileResult(null, issues);
            }

            var sortedNodes = new List<FlowNodeData>();
            if (graph.Nodes != null)
            {
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    if (graph.Nodes[i] != null) sortedNodes.Add(graph.Nodes[i]);
                    else issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidNodeId, "Nodes 不能包含 null。"));
                }
            }

            sortedNodes.Sort((left, right) => string.CompareOrdinal(left.Id ?? string.Empty, right.Id ?? string.Empty));
            var nodeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var compiledNodes = new List<FlowCompiledNode>();
            var routeLists = new List<Dictionary<string, List<FlowOutputRoute>>>();
            var requiredCapabilities = new HashSet<FlowCapabilityId>();

            for (int i = 0; i < sortedNodes.Count; i++)
            {
                FlowNodeData node = sortedNodes[i];
                if (string.IsNullOrEmpty(node.Id))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidNodeId, "NodeId 不能为空。"));
                    continue;
                }

                if (nodeIndices.ContainsKey(node.Id))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.DuplicateNodeId, "NodeId 必须唯一。", node.Id));
                    continue;
                }

                FlowNodeTypeId typeId = new FlowNodeTypeId(node.TypeId);
                if (!typeId.IsValid || !registry.TryGetDescriptor(typeId, out FlowNodeDescriptor descriptor))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.UnknownTypeId, $"未注册的 TypeId: {node.TypeId}", node.Id));
                    continue;
                }

                if (node.DefinitionVersion <= 0 || node.DefinitionVersion != descriptor.Version)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.UnsupportedDefinitionVersion,
                        $"节点版本 {node.DefinitionVersion} 与已注册版本 {descriptor.Version} 不匹配。", node.Id));
                }

                if (!registry.TryGetHandler(typeId, out IFlowNodeHandler handler))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MissingHandler, $"TypeId 没有 Handler: {typeId}", node.Id));
                    continue;
                }

                FlowPropertyBag parameters = node.Parameters ?? new FlowPropertyBag();
                if (parameters.HasDuplicateKeys(out string duplicateKey))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.DuplicatePropertyKey,
                        $"参数 key 重复: {duplicateKey}", node.Id));
                }

                ValidateProperties(node, parameters, descriptor, issues);
                for (int c = 0; c < descriptor.RequiredCapabilities.Count; c++)
                {
                    if (descriptor.RequiredCapabilities[c].IsValid) requiredCapabilities.Add(descriptor.RequiredCapabilities[c]);
                }

                FlowCompiledCondition compiledCondition = null;
                if (descriptor.RequiresCondition && node.Condition == null)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidCondition, "Node requires a condition AST.", node.Id));
                }
                else if (node.Condition != null)
                {
                    try { compiledCondition = FlowCompiledCondition.Compile(node.Condition); }
                    catch (Exception exception)
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.InvalidCondition, exception.Message, node.Id));
                    }
                }

                int index = compiledNodes.Count;
                nodeIndices.Add(node.Id, index);
                compiledNodes.Add(new FlowCompiledNode(
                    index,
                    node.Id,
                    typeId,
                    node.DefinitionVersion,
                    parameters.CreateSnapshot(),
                    compiledCondition,
                    descriptor,
                    handler,
                    new Dictionary<string, FlowOutputRoute[]>(StringComparer.Ordinal)));
                routeLists.Add(new Dictionary<string, List<FlowOutputRoute>>(StringComparer.Ordinal));
            }

            int entryIndex;
            if (string.IsNullOrEmpty(graph.EntryNodeId) || !nodeIndices.TryGetValue(graph.EntryNodeId, out entryIndex))
            {
                issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                    FlowValidationErrorCode.MissingEntry,
                    $"EntryNodeId 不存在: {graph.EntryNodeId}"));
                entryIndex = -1;
            }

            var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
            if (graph.Edges != null)
            {
                for (int i = 0; i < graph.Edges.Count; i++)
                {
                    FlowEdgeData edge = graph.Edges[i];
                    if (edge == null)
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.InvalidEdgeNode, "Edges 不能包含 null.", edgeIndex: i.ToString()));
                        continue;
                    }

                    string edgeKey = $"{edge.FromNodeId}\u001f{edge.FromPortId}\u001f{edge.ToNodeId}\u001f{edge.ToPortId}";
                    if (!edgeKeys.Add(edgeKey))
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.DuplicateEdge, "Edge 不能重复。", edgeIndex: i.ToString()));
                        continue;
                    }

                    if (!nodeIndices.TryGetValue(edge.FromNodeId, out int fromIndex) ||
                        !nodeIndices.TryGetValue(edge.ToNodeId, out int toIndex))
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.InvalidEdgeNode, "Edge 引用的 Node 不存在。", edgeIndex: i.ToString()));
                        continue;
                    }

                    FlowCompiledNode fromNode = compiledNodes[fromIndex];
                    FlowCompiledNode toNode = compiledNodes[toIndex];
                    if (!fromNode.Descriptor.TryGetPort(edge.FromPortId, FlowPortDirection.Output, out _))
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.UnknownPort, $"输出端口不存在: {edge.FromPortId}", fromNode.NodeId, i.ToString()));
                        continue;
                    }

                    if (!toNode.Descriptor.TryGetPort(edge.ToPortId, FlowPortDirection.Input, out _))
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.UnknownPort, $"输入端口不存在: {edge.ToPortId}", toNode.NodeId, i.ToString()));
                        continue;
                    }

                    if (!routeLists[fromIndex].TryGetValue(edge.FromPortId, out List<FlowOutputRoute> targets))
                    {
                        targets = new List<FlowOutputRoute>();
                        routeLists[fromIndex].Add(edge.FromPortId, targets);
                    }

                    targets.Add(new FlowOutputRoute(toIndex, edge.ToPortId));
                }
            }

            for (int i = 0; i < compiledNodes.Count; i++)
            {
                FlowCompiledNode node = compiledNodes[i];
                for (int p = 0; p < node.Descriptor.Ports.Count; p++)
                {
                    FlowPortDescriptor port = node.Descriptor.Ports[p];
                    if (port.Direction != FlowPortDirection.Output || !port.RecommendedRoute) continue;
                    if (routeLists[i].TryGetValue(port.Id, out List<FlowOutputRoute> recommended) && recommended.Count > 0) continue;
                    issues.Add(new FlowValidationIssue(
                        FlowValidationSeverity.Warning,
                        FlowValidationErrorCode.UnroutedRecommendedOutput,
                        $"建议显式处理输出端口: {port.Id}",
                        node.NodeId));
                }
            }

            for (int i = 0; i < compiledNodes.Count; i++)
            {
                Dictionary<string, FlowOutputRoute[]> routes = new Dictionary<string, FlowOutputRoute[]>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<FlowOutputRoute>> route in routeLists[i])
                {
                    route.Value.Sort((left, right) =>
                    {
                        int target = left.NodeIndex.CompareTo(right.NodeIndex);
                        return target != 0 ? target : string.CompareOrdinal(left.InputPort, right.InputPort);
                    });
                    routes.Add(route.Key, route.Value.ToArray());
                }

                FlowCompiledNode current = compiledNodes[i];
                compiledNodes[i] = new FlowCompiledNode(
                    current.Index,
                    current.NodeId,
                    current.TypeId,
                    current.DefinitionVersion,
                    current.Parameters,
                    current.Condition,
                    current.Descriptor,
                    current.Handler,
                    routes);
            }

            DetectImmediateCycles(compiledNodes, routeLists, issues);

            bool hasErrors = false;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].IsError)
                {
                    hasErrors = true;
                    break;
                }
            }

            if (hasErrors || entryIndex < 0)
            {
                return new FlowCompileResult(null, issues);
            }

            FlowCapabilityId[] capabilities = new FlowCapabilityId[requiredCapabilities.Count];
            requiredCapabilities.CopyTo(capabilities);
            Array.Sort(capabilities, (left, right) => string.CompareOrdinal(left.Value, right.Value));
            FlowCompiledNode[] nodes = compiledNodes.ToArray();
            ulong planHash = CalculatePlanHash(graph, sortedNodes, graph.Edges);
            var plan = new FlowCompiledPlan(graph.FlowId, graph.SchemaVersion, entryIndex, planHash,
                nodes, capabilities, nodeIndices);
            return new FlowCompileResult(plan, issues);
        }

        private static void ValidateProperties(
            FlowNodeData node,
            FlowPropertyBag parameters,
            FlowNodeDescriptor descriptor,
            List<FlowValidationIssue> issues)
        {
            for (int i = 0; i < descriptor.Properties.Count; i++)
            {
                FlowPropertyDescriptor property = descriptor.Properties[i];
                if (!parameters.TryGet(property.Key, out FlowValue value))
                {
                    if (property.Required)
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.MissingRequiredProperty,
                            $"缺少必需参数: {property.Key}", node.Id));
                    }

                    continue;
                }

                if (property.ValueKind != FlowValueKind.Any && value.Kind != property.ValueKind)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidPropertyType,
                        $"参数 {property.Key} 类型为 {value.Kind}，需要 {property.ValueKind}。", node.Id));
                }
                else if (value.Kind == FlowValueKind.String && property.AllowedStringValues.Count > 0)
                {
                    bool allowed = false;
                    for (int a = 0; a < property.AllowedStringValues.Count; a++)
                    {
                        if (string.Equals(value.StringValue, property.AllowedStringValues[a], StringComparison.Ordinal))
                        {
                            allowed = true;
                            break;
                        }
                    }
                    if (!allowed)
                    {
                        issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                            FlowValidationErrorCode.InvalidPropertyType,
                            $"参数 {property.Key} 包含未声明值: {value.StringValue}", node.Id));
                    }
                }
                else if (value.Kind == FlowValueKind.BindingReference && !value.BindingReferenceValue.IsValid)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MissingBinding,
                        $"BindingReference property is empty: {property.Key}", node.Id));
                }
                else if (value.Kind == FlowValueKind.Asset && !value.AssetValue.IsValid)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.MissingAsset,
                        $"参数 {property.Key} 的 AssetReference 不能为空。", node.Id));
                }
                else if (value.Kind == FlowValueKind.Enum && !value.EnumValue.IsValid)
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidPropertyType,
                        $"参数 {property.Key} 的 EnumReference 不能为空。", node.Id));
                }
            }

            if (parameters.Entries == null)
            {
                return;
            }

            if (descriptor.AllowAdditionalProperties)
            {
                return;
            }

            for (int i = 0; i < parameters.Entries.Count; i++)
            {
                if (!descriptor.TryGetProperty(parameters.Entries[i].Key, out _))
                {
                    issues.Add(new FlowValidationIssue(FlowValidationSeverity.Error,
                        FlowValidationErrorCode.InvalidPropertyType,
                        $"未声明的参数: {parameters.Entries[i].Key}", node.Id));
                }
            }
        }

        private static ulong CalculatePlanHash(FlowGraphData graph, List<FlowNodeData> sortedNodes, List<FlowEdgeData> edges)
        {
            var hash = new Fnv64();
            hash.Add(graph.FlowId);
            hash.Add(graph.SchemaVersion);
            hash.Add(graph.EntryNodeId);
            for (int i = 0; i < sortedNodes.Count; i++)
            {
                FlowNodeData node = sortedNodes[i];
                hash.Add(node.Id);
                hash.Add(node.TypeId);
                hash.Add(node.DefinitionVersion);
                FlowPropertyBag parameters = node.Parameters ?? new FlowPropertyBag();
                var entries = parameters.Entries == null
                    ? new List<FlowPropertyEntry>()
                    : new List<FlowPropertyEntry>(parameters.Entries);
                entries.Sort((left, right) => string.CompareOrdinal(left.Key ?? string.Empty, right.Key ?? string.Empty));
                for (int p = 0; p < entries.Count; p++)
                {
                    hash.Add(entries[p].Key);
                    hash.Add(entries[p].Value.Kind);
                    hash.Add(GetStableValueText(entries[p].Value));
                }
                AddConditionHash(ref hash, node.Condition);
            }

            if (edges != null)
            {
                var sortedEdges = new List<FlowEdgeData>();
                for (int i = 0; i < edges.Count; i++) if (edges[i] != null) sortedEdges.Add(edges[i]);
                sortedEdges.Sort((left, right) => string.CompareOrdinal(
                    $"{left.FromNodeId}\u001f{left.FromPortId}\u001f{left.ToNodeId}\u001f{left.ToPortId}",
                    $"{right.FromNodeId}\u001f{right.FromPortId}\u001f{right.ToNodeId}\u001f{right.ToPortId}"));
                for (int i = 0; i < sortedEdges.Count; i++)
                {
                    hash.Add(sortedEdges[i].FromNodeId);
                    hash.Add(sortedEdges[i].FromPortId);
                    hash.Add(sortedEdges[i].ToNodeId);
                    hash.Add(sortedEdges[i].ToPortId);
                }
            }

            return hash.Value;
        }

        private static void AddConditionHash(ref Fnv64 hash, FlowCondition condition)
        {
            if (condition == null) { hash.Add("condition:null"); return; }
            hash.Add((int)condition.Kind);
            hash.Add(condition.Key);
            hash.Add((int)condition.Operator);
            hash.Add(condition.Constant.Kind);
            hash.Add(GetStableValueText(condition.Constant));
            AddConditionHash(ref hash, condition.Left);
            AddConditionHash(ref hash, condition.Right);
            int count = condition.Children == null ? 0 : condition.Children.Count;
            hash.Add(count);
            for (int i = 0; i < count; i++) AddConditionHash(ref hash, condition.Children[i]);
        }

        private static string GetStableValueText(FlowValue value)
        {
            switch (value.Kind)
            {
                case FlowValueKind.Bool:
                    return value.BoolValue ? "true" : "false";
                case FlowValueKind.Int:
                    return value.IntValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Long:
                    return value.LongValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Float:
                    return value.FloatValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Double:
                    return value.DoubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.String:
                    return value.StringValue ?? string.Empty;
                case FlowValueKind.Vector2:
                    return value.Vector2Value.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        value.Vector2Value.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Vector3:
                    return value.Vector3Value.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        value.Vector3Value.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        value.Vector3Value.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.Binding:
                    return value.BindingValue.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                        value.BindingValue.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FlowValueKind.BindingReference:
                    return value.BindingReferenceValue.Id ?? string.Empty;
                case FlowValueKind.Asset:
                    return value.AssetValue.Id ?? string.Empty;
                case FlowValueKind.Enum:
                    return value.EnumValue.Id ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static void DetectImmediateCycles(
            List<FlowCompiledNode> nodes,
            List<Dictionary<string, List<FlowOutputRoute>>> routes,
            List<FlowValidationIssue> issues)
        {
            var colors = new int[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                if (colors[i] != 0 || nodes[i].Descriptor.ExecutionMode != FlowNodeExecutionMode.Immediate) continue;
                if (VisitImmediate(i, nodes, routes, colors, issues)) return;
            }
        }

        private static bool VisitImmediate(
            int index,
            List<FlowCompiledNode> nodes,
            List<Dictionary<string, List<FlowOutputRoute>>> routes,
            int[] colors,
            List<FlowValidationIssue> issues)
        {
            colors[index] = 1;
            foreach (KeyValuePair<string, List<FlowOutputRoute>> route in routes[index])
            {
                for (int i = 0; i < route.Value.Count; i++)
                {
                    int target = route.Value[i].NodeIndex;
                    if (nodes[target].Descriptor.ExecutionMode != FlowNodeExecutionMode.Immediate) continue;
                    if (colors[target] == 1)
                    {
                        issues.Add(new FlowValidationIssue(
                            FlowValidationSeverity.Error,
                            FlowValidationErrorCode.ImmediateCycle,
                            "Immediate 节点之间存在无界循环，必须插入 Completion/Operation 节点。",
                            nodes[target].NodeId));
                        return true;
                    }

                    if (colors[target] == 0 && VisitImmediate(target, nodes, routes, colors, issues)) return true;
                }
            }

            colors[index] = 2;
            return false;
        }

        private struct Fnv64
        {
            private ulong _value;
            public ulong Value => _value;

            public Fnv64(bool initialize = true)
            {
                _value = 14695981039346656037UL;
            }

            public void Add(string value)
            {
                if (value == null) value = string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    _value ^= value[i];
                    _value *= 1099511628211UL;
                }

                _value ^= 0xff;
                _value *= 1099511628211UL;
            }

            public void Add(int value) => Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            public void Add(FlowValueKind value) => Add((int)value);
        }
    }
}
