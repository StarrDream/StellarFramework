using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.Editor.Modules.FlowKit;

namespace StellarFramework.Editor.Modules.FlowKit.Tests
{
    public sealed class FlowKitEditorTests
    {
        [Test]
        public void MovingNodeChangesOnlyEditorMetadata()
        {
            FlowGraphDocument document = FlowGraphDocument.CreateNew(FlowBuiltInNodes.CreateRegistry());
            string nodeId = document.Graph.EntryNodeId;
            string runtimeBefore = document.SerializeGraph();
            string editorBefore = document.SerializeMetadata();

            document.SetNodePosition(nodeId, new Vector2(640f, 360f));

            Assert.That(document.SerializeGraph(), Is.EqualTo(runtimeBefore));
            Assert.That(document.SerializeMetadata(), Is.Not.EqualTo(editorBefore));
        }

        [Test]
        public void NodeAuthoringMetadataChangesOnlyEditorMetadata()
        {
            FlowGraphDocument document = FlowGraphDocument.CreateNew(FlowBuiltInNodes.CreateRegistry());
            string nodeId = document.Graph.EntryNodeId;
            string runtimeBefore = document.SerializeGraph();
            string editorBefore = document.SerializeMetadata();

            document.SetNodeDisplayName(nodeId, "播放开场情景视频");
            document.SetNodeDescription(nodeId, "开场阶段的业务说明");

            Assert.That(document.SerializeGraph(), Is.EqualTo(runtimeBefore));
            Assert.That(document.SerializeMetadata(), Is.Not.EqualTo(editorBefore));
            Assert.That(document.GetNodeDisplayName(nodeId), Is.EqualTo("播放开场情景视频"));
            Assert.That(document.GetNodeDescription(nodeId), Is.EqualTo("开场阶段的业务说明"));
        }

[Test]
        public void LegacyEditorMetadataWithoutNodeNamesLoadsWithEmptyAuthoringFields()
        {
            FlowGraphDocument document = FlowGraphDocument.CreateNew(FlowBuiltInNodes.CreateRegistry());
            string nodeId = document.Graph.EntryNodeId;
            string legacyMetadata = "{\"Version\":1,\"Nodes\":[{\"NodeId\":\"" + nodeId + "\",\"X\":80.0,\"Y\":120.0,\"Collapsed\":false}]}";

            document.RestoreSnapshots(document.SerializeGraph(), legacyMetadata);

            Assert.That(document.GetNodeDisplayName(nodeId), Is.Empty);
            Assert.That(document.GetNodeDescription(nodeId), Is.Empty);
            Assert.That(document.SerializeMetadata(), Does.Contain("\"Version\": 2"));
        }


        [Test]
        public void ClipboardPreservesConditionAndGeneratesNewNodeId()
        {
            try
            {
                FlowGraphDocument document = FlowGraphDocument.CreateNew(FlowBuiltInNodes.CreateRegistry());
                FlowNodeData branch = document.AddNode("flow.branch.condition", new Vector2(300f, 120f));
                document.SetCondition(branch.Id, FlowCondition.Compare(
                    FlowCondition.BlackboardValue("score"),
                    FlowComparisonOperator.GreaterOrEqual,
                    new FlowCondition { Kind = FlowConditionKind.Constant, Constant = FlowValue.FromInt(80) }));
                document.SetNodeDisplayName(branch.Id, "检查训练得分");
                document.SetNodeDescription(branch.Id, "复制后应保留的节点说明");

                string payload = document.CreateClipboard(new List<string> { branch.Id });
                List<string> pasted = document.PasteClipboard(payload, new Vector2(40f, 40f));
                Assert.That(pasted, Has.Count.EqualTo(1));
                FlowNodeData clone = document.FindNode(pasted[0]);
                Assert.That(clone, Is.Not.Null, "Pasted node was not found.");
                Assert.That(clone.Condition, Is.Not.Null, "Pasted condition was lost.");
                Assert.That(clone.Condition.Left, Is.Not.Null, "Pasted condition Left operand was lost.");
                Assert.That(clone.Condition.Left.Key, Is.EqualTo("score"));
                Assert.That(document.GetNodeDisplayName(clone.Id), Is.EqualTo("检查训练得分"));
                Assert.That(document.GetNodeDescription(clone.Id), Is.EqualTo("复制后应保留的节点说明"));
            }
            catch (System.Exception exception)
            {
                Assert.Fail(exception.ToString());
            }
        }

        [Test]
        public void RuntimeSerializationIsStableAcrossRepeatedWrites()
        {
            FlowGraphDocument document = FlowGraphDocument.CreateNew(FlowBuiltInNodes.CreateRegistry());
            FlowNodeData pass = document.AddNode("flow.pass", new Vector2(300f, 120f));
            FlowNodeData complete = document.AddNode("flow.complete", new Vector2(560f, 120f));
            document.AddEdge(document.Graph.EntryNodeId, "next", pass.Id, "in");
            document.AddEdge(pass.Id, "next", complete.Id, "in");

            string first = document.SerializeGraph();
            string second = document.SerializeGraph();
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EditorRegistryAlwaysContainsBuiltInConditionNode()
        {
            FlowNodeRegistry registry = FlowKitEditorRegistry.Create(out IReadOnlyList<string> issues);
            Assert.That(issues, Is.Empty);
            Assert.That(registry.TryGetDescriptor(new FlowNodeTypeId("flow.branch.condition"), out _), Is.True);
        }

        [Test]
        public void FlowKitEditorIsExposedThroughToolsHubWithoutStandaloneMenu()
        {
            bool hasToolsHubRegistration = false;
            IList<CustomAttributeData> typeAttributes = typeof(FlowKitHubModule).GetCustomAttributesData();
            for (int i = 0; i < typeAttributes.Count; i++)
            {
                CustomAttributeData attribute = typeAttributes[i];
                if (!string.Equals(attribute.AttributeType.FullName,
                        "StellarFramework.Editor.StellarToolAttribute", StringComparison.Ordinal)) continue;
                hasToolsHubRegistration = attribute.ConstructorArguments.Count > 0 &&
                    string.Equals(attribute.ConstructorArguments[0].Value as string,
                        "FlowKit 流程编辑器", StringComparison.Ordinal);
                break;
            }
            Assert.That(hasToolsHubRegistration, Is.True, "FlowKit must register as a ToolsHub module.");

            Type[] types = typeof(FlowKitHubModule).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo[] methods = types[i].GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int j = 0; j < methods.Length; j++)
                {
                    object[] attributes = methods[j].GetCustomAttributes(typeof(MenuItem), false);
                    for (int k = 0; k < attributes.Length; k++)
                    {
                        var menuItem = (MenuItem)attributes[k];
                        Assert.That(menuItem.menuItem, Does.Not.StartWith("StellarFramework/FlowKit/"),
                            $"FlowKit must be hosted inside ToolsHub, but standalone menu remains on {types[i].FullName}.{methods[j].Name}.");
                    }
                }
            }
        }

        [Test]
        public void ConditionGraphJsonRoundTripPreservesNestedAst()
        {
            var graph = new FlowGraphData { FlowId = "tests.editor.condition.roundtrip", EntryNodeId = "branch" };
            graph.Nodes.Add(new FlowNodeData
            {
                Id = "branch",
                TypeId = "flow.branch.condition",
                Condition = new FlowCondition
                {
                    Kind = FlowConditionKind.All,
                    Children = new List<FlowCondition>
                    {
                        FlowCondition.Compare(FlowCondition.BlackboardValue("score"), FlowComparisonOperator.GreaterOrEqual,
                            new FlowCondition { Kind = FlowConditionKind.Constant, Constant = FlowValue.FromInt(80) }),
                        FlowCondition.Compare(FlowCondition.StateValue("training.completed"), FlowComparisonOperator.Equal,
                            FlowCondition.FromBool(true))
                    }
                }
            });

            string json = StellarFramework.FlowKit.Unity.FlowGraphJson.ToJson(graph, false);
            FlowGraphData clone = StellarFramework.FlowKit.Unity.FlowGraphJson.FromJson(json);
            FlowCondition condition = clone.Nodes[0].Condition;
            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.Kind, Is.EqualTo(FlowConditionKind.All));
            Assert.That(condition.Children, Has.Count.EqualTo(2));
            Assert.That(condition.Children[0].Left.Key, Is.EqualTo("score"));
            Assert.That(condition.Children[1].Left.Key, Is.EqualTo("training.completed"));
        }

        [Test]
        public void BuildValidatorAcceptsAllCurrentProjectFlows()
        {
            FlowBuildValidationResult result = FlowKitBuildValidator.ValidateProject();
            Assert.That(result.Succeeded, Is.True, result.CreateSummary());
            Assert.That(result.GraphCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void FireDrillSampleGraphCompletesThroughTwoWayCommunication()
        {
            FlowGraphData graph = LoadFireDrillSampleGraph();
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True, JoinIssues(compile));

            FlowRuntimeServices services = CreateFireDrillServices(graph, null);
            var runner = new FlowRunner(services);
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            PublishAllReady(services);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            CompleteAllRoleTasks(services);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            services.States.Set(new FlowStateKey("fire_drill.safety.passed"), FlowValue.FromBool(true), FlowStateLifetime.External);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            runner.Tick(new FlowTimeSnapshot(5.1d, 5.1d, 5.1d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Completed), run.LastError?.ToString());
        }

        [Test]
        public void FireDrillSampleGraphRoutesOperationFailureToBusinessFailure()
        {
            FlowGraphData graph = LoadFireDrillSampleGraph();
            FlowCompileResult compile = FlowCompiler.Compile(graph, FlowBuiltInNodes.CreateRegistry());
            Assert.That(compile.Succeeded, Is.True, JoinIssues(compile));

            FlowRuntimeServices services = CreateFireDrillServices(graph, "fire_drill.extinguisher_a.start");
            var runner = new FlowRunner(services);
            FlowRun run = runner.Start(compile.Plan);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));
            PublishAllReady(services);
            runner.Tick(new FlowTimeSnapshot(0d, 0d, 0d));

            Assert.That(run.Status, Is.EqualTo(FlowRunStatus.Failed));
            Assert.That(run.LastError, Is.Not.Null);
            Assert.That(run.LastError.Code, Is.EqualTo(FlowRuntimeErrorCode.BusinessFailure));
        }

        [Test]
        public void ParameterGraphJsonRoundTripPreservesFlowValues()
        {
            var graph = new FlowGraphData { FlowId = "tests.editor.parameters", EntryNodeId = "delay" };
            var delay = new FlowNodeData { Id = "delay", TypeId = "flow.delay" };
            delay.Parameters.Set("seconds", FlowValue.FromDouble(10d));
            delay.Parameters.Set("label", FlowValue.FromString("hello"));
            delay.Parameters.Set("binding", FlowValue.FromBindingReference("AssemblyPoint"));
            graph.Nodes.Add(delay);

            string json = StellarFramework.FlowKit.Unity.FlowGraphJson.ToJson(graph, false);
            FlowGraphData clone = StellarFramework.FlowKit.Unity.FlowGraphJson.FromJson(json);
            Assert.That(clone.Nodes[0].Parameters.TryGet("seconds", out FlowValue seconds), Is.True);
            Assert.That(seconds.Kind, Is.EqualTo(FlowValueKind.Double));
            Assert.That(seconds.DoubleValue, Is.EqualTo(10d));
            Assert.That(clone.Nodes[0].Parameters.TryGet("label", out FlowValue label), Is.True);
            Assert.That(label.StringValue, Is.EqualTo("hello"));
            Assert.That(clone.Nodes[0].Parameters.TryGet("binding", out FlowValue binding), Is.True);
            Assert.That(binding.BindingReferenceValue.Id, Is.EqualTo("AssemblyPoint"));
        }

        private static FlowGraphData LoadFireDrillSampleGraph()
        {
            const string path = "Assets/StellarFramework/Samples/KitSamples/Example_FlowKit/FireDrillWorkflow4P.flow.json";
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.That(asset, Is.Not.Null, "FireDrillWorkflow4P.flow.json is missing.");
            return StellarFramework.FlowKit.Unity.FlowGraphJson.FromTextAsset(asset);
        }

        private static FlowRuntimeServices CreateFireDrillServices(FlowGraphData graph, string failedOperation)
        {
            var operations = new FlowOperationRegistry();
            var adapter = new FireDrillTestOperationAdapter(failedOperation);
            var registered = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FlowNodeData node = graph.Nodes[i];
                if (node.TypeId != "flow.operation" || !node.Parameters.TryGet("operation", out FlowValue value)) continue;
                if (value.Kind == FlowValueKind.String && registered.Add(value.StringValue))
                    operations.Register(value.StringValue, adapter);
            }
            return new FlowRuntimeServices(operations: operations);
        }

        private static void PublishAllReady(FlowRuntimeServices services)
        {
            string[] signals =
            {
                "fire_drill.player1.ready", "fire_drill.player2.ready",
                "fire_drill.player3.ready", "fire_drill.player4.ready"
            };
            for (int i = 0; i < signals.Length; i++)
                services.Signals.Publish(new FlowSignalId(signals[i]), FlowSignalScope.Host);
        }

        private static void CompleteAllRoleTasks(FlowRuntimeServices services)
        {
            string[] states =
            {
                "fire_drill.commander.reported", "fire_drill.extinguisher_a.completed",
                "fire_drill.extinguisher_b.completed", "fire_drill.evacuation.completed"
            };
            for (int i = 0; i < states.Length; i++)
                services.States.Set(new FlowStateKey(states[i]), FlowValue.FromBool(true), FlowStateLifetime.External);
        }

        private static string JoinIssues(FlowCompileResult result)
        {
            if (result == null || result.Issues.Count == 0) return string.Empty;
            var lines = new List<string>(result.Issues.Count);
            for (int i = 0; i < result.Issues.Count; i++) lines.Add(result.Issues[i].ToString());
            return string.Join("\n", lines);
        }

        private sealed class FireDrillTestOperationAdapter : IFlowOperationAdapter
        {
            private readonly string _failedOperation;

            internal FireDrillTestOperationAdapter(string failedOperation)
            {
                _failedOperation = failedOperation;
            }

            public void Start(in FlowOperationContext context, in FlowOperationRequest request,
                FlowOperationHandle handle, Action<FlowOperationResult> complete)
            {
                complete(string.Equals(request.OperationId, _failedOperation, StringComparison.Ordinal)
                    ? FlowOperationResult.Failure("sample failure")
                    : FlowOperationResult.Success());
            }

            public void Cancel(in FlowOperationContext context, FlowOperationHandle handle) { }
        }
    }
}
