using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using StellarFramework.FlowKit;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal sealed class FlowNodeView : Node
    {
        private readonly Dictionary<string, Port> _inputs = new Dictionary<string, Port>(StringComparer.Ordinal);
        private readonly Dictionary<string, Port> _outputs = new Dictionary<string, Port>(StringComparer.Ordinal);
        private readonly Action<string> _selected;
        private readonly Label _typeLabel;
        private readonly Label _summaryLabel;

        public string NodeId { get; }
        public FlowNodeDescriptor Descriptor { get; }

        public FlowNodeView(string nodeId, FlowNodeDescriptor descriptor, string displayName, string description, string summary, Action<string> selected)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _selected = selected;
            _typeLabel = new Label();
            _typeLabel.style.fontSize = 10f;
            _typeLabel.style.color = FlowKitEditorTheme.TextSecondary;
            _typeLabel.style.marginLeft = 6f;
            _typeLabel.style.marginRight = 6f;
            _typeLabel.style.marginTop = 2f;
            _summaryLabel = new Label();
            _summaryLabel.style.fontSize = 10f;
            _summaryLabel.style.color = FlowKitEditorTheme.TextSecondary;
            _summaryLabel.style.marginLeft = 6f;
            _summaryLabel.style.marginRight = 6f;
            _summaryLabel.style.marginTop = 1f;
            _summaryLabel.style.marginBottom = 4f;
            extensionContainer.Add(_typeLabel);
            extensionContainer.Add(_summaryLabel);
            RefreshExpandedState();
            viewDataKey = nodeId;
            style.backgroundColor = FlowKitEditorTheme.Card;
            Color border = string.Equals(descriptor.TypeId.Value, "flow.fail", StringComparison.Ordinal)
                ? FlowKitEditorTheme.Error
                : FlowKitEditorTheme.Border;
            float borderWidth = string.Equals(descriptor.TypeId.Value, "flow.fail", StringComparison.Ordinal) ? 2f : 1f;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftWidth = borderWidth;
            style.borderRightWidth = borderWidth;
            style.borderTopWidth = borderWidth;
            style.borderBottomWidth = borderWidth;
            userData = nodeId;
            BuildPorts();
            SetPresentation(displayName, description, summary);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            _selected?.Invoke(NodeId);
        }

        public void SetPresentation(string displayName, string description, string summary)
        {
            string typeName = FlowKitEditorLocalization.NodeName(Descriptor);
            string businessName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            title = string.IsNullOrEmpty(businessName) ? typeName : businessName;
            _typeLabel.text = typeName;
            _typeLabel.style.display = string.IsNullOrEmpty(businessName) ? DisplayStyle.None : DisplayStyle.Flex;
            string summaryText = string.IsNullOrWhiteSpace(summary) ? string.Empty : summary.Trim();
            _summaryLabel.text = summaryText;
            _summaryLabel.style.display = string.IsNullOrEmpty(summaryText) ? DisplayStyle.None : DisplayStyle.Flex;
            string details = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim() + "\n\n";
            tooltip = details + "Node ID: " + NodeId + "\nType: " + typeName;
        }

        public void SetRuntimeActive(bool active)
        {
            Color color = active ? FlowKitEditorTheme.Success : FlowKitEditorTheme.Border;
            float width = active ? 3f : 1f;
            style.borderLeftColor = color;
            style.borderRightColor = color;
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
        }

        public bool TryGetInput(string portId, out Port port) => _inputs.TryGetValue(portId, out port);
        public bool TryGetOutput(string portId, out Port port) => _outputs.TryGetValue(portId, out port);

        private void BuildPorts()
        {
            for (int i = 0; i < Descriptor.Ports.Count; i++)
            {
                FlowPortDescriptor descriptor = Descriptor.Ports[i];
                Direction direction = descriptor.Direction == FlowPortDirection.Input ? Direction.Input : Direction.Output;
                Port port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(bool));
                port.portName = FlowKitEditorLocalization.PortName(descriptor.Id);
                port.userData = descriptor.Id;
                port.portColor = FlowKitEditorTheme.PortColor(descriptor.Semantic);
                if (direction == Direction.Input)
                {
                    inputContainer.Add(port);
                    _inputs.Add(descriptor.Id, port);
                }
                else
                {
                    outputContainer.Add(port);
                    _outputs.Add(descriptor.Id, port);
                }
            }
            RefreshPorts();
            RefreshExpandedState();
        }
    }

    internal sealed class FlowGraphCanvas : GraphView
    {
        private readonly Dictionary<string, FlowNodeView> _nodes = new Dictionary<string, FlowNodeView>(StringComparer.Ordinal);
        private bool _loading;

        public event Action<string> NodeSelected;
        public event Action<string, Vector2> NodeMoved;
        public event Action<IReadOnlyList<string>> NodesDeleted;
        public event Action<FlowEdgeData> EdgeCreated;
        public event Action<FlowEdgeData> EdgeRemoved;
        public event Action<Vector2> NodeCreationRequested;
        public Func<IReadOnlyList<string>, string> SerializeNodes { get; set; }
        public Func<string, bool> CanPasteNodes { get; set; }
        public Action<string> PasteNodesRequested { get; set; }
        public FlowGraphCanvas()
        {
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            style.backgroundColor = FlowKitEditorTheme.Panel;
            graphViewChanged = OnGraphViewChanged;
            nodeCreationRequest = context =>
            {
                Vector2 guiPoint = GUIUtility.ScreenToGUIPoint(context.screenMousePosition);
                Vector2 local = contentViewContainer.WorldToLocal(guiPoint);
                NodeCreationRequested?.Invoke(local);
            };
            serializeGraphElements = elements =>
            {
                var ids = elements.OfType<FlowNodeView>().Select(node => node.NodeId).Distinct().ToList();
                return SerializeNodes?.Invoke(ids) ?? string.Empty;
            };
            canPasteSerializedData = data => CanPasteNodes?.Invoke(data) ?? false;
            unserializeAndPaste = (_, data) => PasteNodesRequested?.Invoke(data);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort || port.node == startPort.node || port.direction == startPort.direction) return;
                result.Add(port);
            });
            return result;
        }

        public void LoadDocument(FlowGraphDocument document, FlowNodeRegistry registry)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            _loading = true;
            DeleteElements(graphElements.ToList());
            _nodes.Clear();

            for (int i = 0; i < document.Graph.Nodes.Count; i++)
            {
                FlowNodeData node = document.Graph.Nodes[i];
                if (node == null || !registry.TryGetDescriptor(node.TypeId, out FlowNodeDescriptor descriptor)) continue;
                AddNodeView(node, descriptor, document.GetNodePosition(node.Id, i),
                    document.GetNodeDisplayName(node.Id), document.GetNodeDescription(node.Id));
            }
            for (int i = 0; i < document.Graph.Edges.Count; i++)
            {
                FlowEdgeData data = document.Graph.Edges[i];
                if (data == null || !_nodes.TryGetValue(data.FromNodeId, out FlowNodeView fromNode) ||
                    !_nodes.TryGetValue(data.ToNodeId, out FlowNodeView toNode) ||
                    !fromNode.TryGetOutput(data.FromPortId, out Port output) ||
                    !toNode.TryGetInput(data.ToPortId, out Port input)) continue;

                Edge edge = output.ConnectTo(input);
                edge.userData = CloneEdge(data);
                ApplyEdgeSemantic(edge);
                AddElement(edge);
            }
            _loading = false;
        }

        public FlowNodeView AddNodeView(FlowNodeData node, FlowNodeDescriptor descriptor, Vector2 position,
            string displayName = null, string description = null)
        {
            var view = new FlowNodeView(node.Id, descriptor, displayName, description, BuildNodeSummary(node),
                id => NodeSelected?.Invoke(id));
            view.SetPosition(new Rect(position, new Vector2(240f, 140f)));
            _nodes[node.Id] = view;
            AddElement(view);
            return view;
        }

        public bool TryGetNodeView(string nodeId, out FlowNodeView view) => _nodes.TryGetValue(nodeId, out view);

        public void RefreshNodePresentation(FlowGraphDocument document, string nodeId)
        {
            if (document == null || string.IsNullOrEmpty(nodeId) || !_nodes.TryGetValue(nodeId, out FlowNodeView view)) return;
            FlowNodeData node = document.FindNode(nodeId);
            if (node == null) return;
            view.SetPresentation(document.GetNodeDisplayName(nodeId), document.GetNodeDescription(nodeId), BuildNodeSummary(node));
        }

        private static string BuildNodeSummary(FlowNodeData node)
        {
            if (node == null) return string.Empty;
            switch (node.TypeId)
            {
                case "flow.operation": return ParameterSummary(node, "operation", "operation");
                case "flow.wait.signal":
                case "flow.emit.signal": return ParameterSummary(node, "signal", "signal");
                case "flow.wait.state": return ParameterSummary(node, "state", "state");
                case "flow.stable.for":
                {
                    string state = ParameterSummary(node, "state", "state");
                    string seconds = ParameterSummary(node, "seconds", "seconds");
                    if (string.IsNullOrEmpty(state)) return seconds;
                    return string.IsNullOrEmpty(seconds) ? state : state + " | " + seconds;
                }
                case "flow.delay": return ParameterSummary(node, "seconds", "seconds");
                case "flow.wait.blackboard":
                case "flow.set.blackboard":
                case "flow.increment.blackboard": return ParameterSummary(node, "key", "key");
                case "flow.fail": return ParameterSummary(node, "message", "message");
                case "flow.branch.bool":
                {
                    string key = ParameterSummary(node, "blackboardKey", "blackboard");
                    return string.IsNullOrEmpty(key) ? ParameterSummary(node, "value", "value") : key;
                }
                default: return string.Empty;
            }
        }

        private static string ParameterSummary(FlowNodeData node, string key, string label)
        {
            if (node.Parameters == null || !node.Parameters.TryGet(key, out FlowValue value)) return string.Empty;
            string valueText = SummaryValue(value);
            return string.IsNullOrEmpty(valueText) ? string.Empty : label + ": " + valueText;
        }

        private static string SummaryValue(FlowValue value)
        {
            switch (value.Kind)
            {
                case FlowValueKind.String: return value.StringValue ?? string.Empty;
                case FlowValueKind.Bool: return value.BoolValue ? "true" : "false";
                case FlowValueKind.Int: return value.IntValue.ToString();
                case FlowValueKind.Long: return value.LongValue.ToString();
                case FlowValueKind.Float: return value.FloatValue.ToString("0.###");
                case FlowValueKind.Double: return value.DoubleValue.ToString("0.###");
                default: return string.Empty;
            }
        }

        public void SelectNodes(IReadOnlyList<string> nodeIds)
        {
            ClearSelection();
            if (nodeIds == null) return;
            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (_nodes.TryGetValue(nodeIds[i], out FlowNodeView node)) AddToSelection(node);
            }
        }

        public int GetSelectedNodeIds(List<string> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int before = destination.Count;
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i] is FlowNodeView node) destination.Add(node.NodeId);
            }
            return destination.Count - before;
        }

        public void SetRuntimeActiveNodes(ISet<string> activeNodeIds)
        {
            foreach (KeyValuePair<string, FlowNodeView> pair in _nodes)
            {
                pair.Value.SetRuntimeActive(activeNodeIds != null && activeNodeIds.Contains(pair.Key));
            }
        }

        public void FocusNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out FlowNodeView node)) return;
            ClearSelection();
            AddToSelection(node);
            FrameSelection();
            NodeSelected?.Invoke(nodeId);
        }
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_loading) return change;

            if (change.edgesToCreate != null)
            {
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    Edge edge = change.edgesToCreate[i];
                    if (!(edge.output?.node is FlowNodeView fromNode) || !(edge.input?.node is FlowNodeView toNode)) continue;
                    var data = new FlowEdgeData
                    {
                        FromNodeId = fromNode.NodeId,
                        FromPortId = edge.output.userData as string,
                        ToNodeId = toNode.NodeId,
                        ToPortId = edge.input.userData as string
                    };
                    edge.userData = data;
                    ApplyEdgeSemantic(edge);
                    EdgeCreated?.Invoke(CloneEdge(data));
                }
            }

            if (change.movedElements != null)
            {
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    if (change.movedElements[i] is FlowNodeView node)
                        NodeMoved?.Invoke(node.NodeId, node.GetPosition().position);
                }
            }

            HandleRemovals(change.elementsToRemove);
            return change;
        }
        private void HandleRemovals(List<GraphElement> elements)
        {
            if (elements == null || elements.Count == 0) return;
            var deletedNodes = new List<string>();
            for (int i = 0; i < elements.Count; i++)
            {
                GraphElement element = elements[i];
                if (element is Edge edge && edge.userData is FlowEdgeData edgeData)
                {
                    EdgeRemoved?.Invoke(CloneEdge(edgeData));
                }
                else if (element is FlowNodeView node)
                {
                    deletedNodes.Add(node.NodeId);
                    _nodes.Remove(node.NodeId);
                }
            }
            if (deletedNodes.Count > 0) NodesDeleted?.Invoke(deletedNodes);
        }

        private static void ApplyEdgeSemantic(Edge edge)
        {
            if (edge?.edgeControl == null || edge.output == null) return;
            Color color = edge.output.portColor;
            edge.edgeControl.inputColor = color;
            edge.edgeControl.outputColor = color;
        }

        private static FlowEdgeData CloneEdge(FlowEdgeData source)
        {
            return new FlowEdgeData
            {
                FromNodeId = source.FromNodeId,
                FromPortId = source.FromPortId,
                ToNodeId = source.ToNodeId,
                ToPortId = source.ToPortId
            };
        }
    }
}
