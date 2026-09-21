using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;
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
        public void BuildValidatorAcceptsFrameworkRepositoryWithoutBusinessFlows()
        {
            FlowBuildValidationResult result = FlowKitBuildValidator.ValidateProject();
            Assert.That(result.Succeeded, Is.True, result.CreateSummary());
            Assert.That(result.GraphCount, Is.EqualTo(0),
                "Framework source repository should not require a bundled business/sample Flow graph.");
        }

        [Test]
        public void AuthoringIdRulesRequireStableLowerSnakeCaseSegments()
        {
            Assert.That(FlowAuthoringIdRules.IsValidContractId("school.assembly.all_ready"), Is.True);
            Assert.That(FlowAuthoringIdRules.IsValidContractId("school.assembly_zone", 2), Is.True);
            Assert.That(FlowAuthoringIdRules.IsValidContractId("School.Assembly.Ready"), Is.False);
            Assert.That(FlowAuthoringIdRules.IsValidContractId("school..ready"), Is.False);
            Assert.That(FlowAuthoringIdRules.IsValidContractId("ready"), Is.False);
            Assert.That(FlowAuthoringIdRules.IsValidArgumentKey("playerId"), Is.True);
            Assert.That(FlowAuthoringIdRules.IsValidArgumentKey("2player"), Is.False);
            Assert.That(FlowAuthoringIdRules.IsValidArgumentKey("player-id"), Is.False);
        }

        [Test]
        public void ContractValidatorRejectsUnknownOperationWhenContractsAreActive()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.StrictScopes.Add(new FlowStrictContractScope(null));
            contracts.Operations.Add(
                "school.video.play",
                CreateAuthoringContract("school.video.play", FlowValueKind.Any));

            var graph = new FlowGraphData { FlowId = "tests.contract.unknown_operation", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("school.audio.play"));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("unknown Operation contract 'school.audio.play'"));
        }

        [Test]
        public void ContractValidatorAllowsUnknownOperationForNonStrictPartialCatalog()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.Operations.Add(
                "school.video.play",
                CreateAuthoringContract("school.video.play", FlowValueKind.Any));

            var graph = new FlowGraphData { FlowId = "tests.contract.partial_catalog", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("rainforest.video.play"));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void ContractValidatorStrictScopeOnlyAppliesToConfiguredFlowIds()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.StrictScopes.Add(new FlowStrictContractScope(
                new[] { "school.main.flow" }));
            contracts.Operations.Add(
                "school.video.play",
                CreateAuthoringContract("school.video.play", FlowValueKind.Any));

            var graph = new FlowGraphData { FlowId = "rainforest.main.flow", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("rainforest.video.play"));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void StrictScopeWithOnlyInvalidFlowIdsDoesNotSilentlyBecomeGlobal()
        {
            var scope = new FlowStrictContractScope(new[] { string.Empty, "   " });

            Assert.That(scope.AppliesTo("school.main.flow"), Is.False);
        }

        [Test]
        public void ContractValidatorRejectsMissingRequiredOperationArgument()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.Operations.Add(
                "school.voice.play",
                CreateOperationContract(
                    "school.voice.play",
                    CreateArgumentContract("voiceId", FlowValueKind.String, true)));

            var graph = new FlowGraphData { FlowId = "school.main.flow", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("school.voice.play"));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("missing required argument 'voiceId'"));
        }

        [Test]
        public void ContractValidatorRejectsOperationArgumentTypeMismatch()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.Operations.Add(
                "school.voice.play",
                CreateOperationContract(
                    "school.voice.play",
                    CreateArgumentContract("voiceId", FlowValueKind.String, true)));

            var graph = new FlowGraphData { FlowId = "school.main.flow", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("school.voice.play"));
            node.Parameters.Set("voiceId", FlowValue.FromInt(7));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("expects String but graph contains Int"));
        }

        [Test]
        public void ContractValidatorRejectsStateAndBlackboardTypeMismatch()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.States.Add(
                "school.assembly.all_ready",
                CreateAuthoringContract("school.assembly.all_ready", FlowValueKind.Bool));
            contracts.BlackboardKeys.Add(
                "school.route.retry_count",
                CreateAuthoringContract("school.route.retry_count", FlowValueKind.Int));

            var graph = new FlowGraphData { FlowId = "school.main.flow", EntryNodeId = "state" };
            var state = new FlowNodeData { Id = "state", TypeId = "flow.wait.state" };
            state.Parameters.Set("state", FlowValue.FromString("school.assembly.all_ready"));
            state.Parameters.Set("expected", FlowValue.FromInt(1));
            graph.Nodes.Add(state);

            var blackboard = new FlowNodeData { Id = "bb", TypeId = "flow.set.blackboard" };
            blackboard.Parameters.Set("key", FlowValue.FromString("school.route.retry_count"));
            blackboard.Parameters.Set("value", FlowValue.FromString("one"));
            graph.Nodes.Add(blackboard);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("State 'school.assembly.all_ready' expected value expects Bool but graph contains Int"));
            Assert.That(result.Errors, Has.Some.Contains("Blackboard 'school.route.retry_count' value expects Int but graph contains String"));
        }

        [Test]
        public void ContractValidatorRejectsUnknownBindingOnlyInStrictScope()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.StrictScopes.Add(new FlowStrictContractScope(
                new[] { "school.main.flow" }));

            var graph = new FlowGraphData { FlowId = "school.main.flow", EntryNodeId = "op" };
            var node = new FlowNodeData { Id = "op", TypeId = "flow.operation" };
            node.Parameters.Set("operation", FlowValue.FromString("school.navigation.start"));
            node.Parameters.Set("binding", FlowValue.FromBindingReference("school.assembly_zone"));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("unknown Binding contract 'school.assembly_zone'"));
        }

        [Test]
        public void ContractValidatorRejectsSignalPayloadTypeMismatch()
        {
            var contracts = new FlowContractCatalogSnapshot();
            contracts.Signals.Add(
                "school.video.completed",
                CreateAuthoringContract("school.video.completed", FlowValueKind.Bool));

            var graph = new FlowGraphData { FlowId = "tests.contract.signal_type", EntryNodeId = "emit" };
            var node = new FlowNodeData { Id = "emit", TypeId = "flow.emit.signal" };
            node.Parameters.Set("signal", FlowValue.FromString("school.video.completed"));
            node.Parameters.Set("payload", FlowValue.FromInt(1));
            graph.Nodes.Add(node);

            var result = new FlowBuildValidationResult();
            FlowKitContractValidator.ValidateGraph("Assets/Test.flow.json", graph, contracts, result);

            Assert.That(result.Errors, Has.Some.Contains("expects Bool but graph contains Int"));
        }

        [Test]
        public void ProjectScaffolderCreatesCompilableBoundaryFileSetWithoutOverwriting()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "StellarFramework_FlowKitScaffold_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string module = FlowKitProjectScaffolder.BuildModuleFiles(
                    "school.main.flow",
                    root,
                    "School");

                Assert.That(module, Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(module, "Contracts", "SchoolFlowContracts.cs")), Is.True);
                Assert.That(File.Exists(Path.Combine(module, "Bootstrap", "SchoolFlowConfigurator.cs")), Is.True);
                Assert.That(File.Exists(Path.Combine(module, "Facts", "SchoolFlowFactsBridge.cs")), Is.True);
                string adapter = Path.Combine(module, "Operations", "SchoolOperationAdapterExample.cs");
                Assert.That(File.Exists(adapter), Is.True);
                Assert.That(File.ReadAllText(adapter), Does.Contain("Operation adapter template is not implemented"));

                string second = FlowKitProjectScaffolder.BuildModuleFiles(
                    "school.main.flow",
                    root,
                    "School");
                Assert.That(second, Is.Empty, "Scaffolder must never overwrite an existing module.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void FlowBindingUsesExplicitTargetAndPreservesSelfFallback()
        {
            var gameObject = new GameObject("FlowBindingContractTest");
            try
            {
                FlowBinding binding = gameObject.AddComponent<FlowBinding>();
                Assert.That(binding.BoundValue, Is.SameAs(binding));

                SetPrivateField(binding, "target", gameObject.transform);
                Assert.That(binding.Target, Is.SameAs(gameObject.transform));
                Assert.That(binding.BoundValue, Is.SameAs(gameObject.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TwoWayIntegrationGraphCompletesThroughSignalStateAndOperation()
        {
            FlowGraphData graph = CreateTwoWayIntegrationGraph();
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
        public void TwoWayIntegrationGraphRoutesOperationFailureToBusinessFailure()
        {
            FlowGraphData graph = CreateTwoWayIntegrationGraph();
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

        private static FlowGraphData CreateTwoWayIntegrationGraph()
        {
            var graph = new FlowGraphData
            {
                FlowId = "tests.editor.two_way_integration",
                EntryNodeId = "entry"
            };

            graph.Nodes.Add(new FlowNodeData { Id = "entry", TypeId = "flow.entry" });

            var ready = new FlowNodeData { Id = "ready", TypeId = "flow.wait.signal" };
            ready.Parameters.Set("signal", FlowValue.FromString("fire_drill.player1.ready"));
            ready.Parameters.Set("scope", FlowValue.FromString("Host"));
            graph.Nodes.Add(ready);

            var task = new FlowNodeData { Id = "task", TypeId = "flow.operation" };
            task.Parameters.Set("operation", FlowValue.FromString("fire_drill.extinguisher_a.start"));
            graph.Nodes.Add(task);

            var taskDone = new FlowNodeData { Id = "task-done", TypeId = "flow.wait.state" };
            taskDone.Parameters.Set("state", FlowValue.FromString("fire_drill.extinguisher_a.completed"));
            taskDone.Parameters.Set("expected", FlowValue.FromBool(true));
            graph.Nodes.Add(taskDone);

            var safety = new FlowNodeData { Id = "safety", TypeId = "flow.operation" };
            safety.Parameters.Set("operation", FlowValue.FromString("fire_drill.safety.check"));
            graph.Nodes.Add(safety);

            var safetyPassed = new FlowNodeData { Id = "safety-passed", TypeId = "flow.wait.state" };
            safetyPassed.Parameters.Set("state", FlowValue.FromString("fire_drill.safety.passed"));
            safetyPassed.Parameters.Set("expected", FlowValue.FromBool(true));
            graph.Nodes.Add(safetyPassed);

            var delay = new FlowNodeData { Id = "delay", TypeId = "flow.delay" };
            delay.Parameters.Set("seconds", FlowValue.FromDouble(5d));
            graph.Nodes.Add(delay);
            graph.Nodes.Add(new FlowNodeData { Id = "complete", TypeId = "flow.complete" });

            var fail = new FlowNodeData { Id = "fail", TypeId = "flow.fail" };
            fail.Parameters.Set("message", FlowValue.FromString("Operation failed."));
            graph.Nodes.Add(fail);

            AddEdge(graph, "entry", "next", "ready");
            AddEdge(graph, "ready", "received", "task");
            AddEdge(graph, "task", "succeeded", "task-done");
            AddEdge(graph, "task", "failed", "fail");
            AddEdge(graph, "task", "cancelled", "fail");
            AddEdge(graph, "task-done", "changed", "safety");
            AddEdge(graph, "safety", "succeeded", "safety-passed");
            AddEdge(graph, "safety", "failed", "fail");
            AddEdge(graph, "safety", "cancelled", "fail");
            AddEdge(graph, "safety-passed", "changed", "delay");
            AddEdge(graph, "delay", "completed", "complete");
            return graph;
        }

        private static void AddEdge(FlowGraphData graph, string fromNode, string fromPort, string toNode)
        {
            graph.Edges.Add(new FlowEdgeData
            {
                FromNodeId = fromNode,
                FromPortId = fromPort,
                ToNodeId = toNode,
                ToPortId = "in"
            });
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

        private static FlowAuthoringContractEntry CreateAuthoringContract(string id, FlowValueKind valueKind)
        {
            var entry = new FlowAuthoringContractEntry();
            SetPrivateField(entry, "id", id);
            SetPrivateField(entry, "valueKind", valueKind);
            return entry;
        }

        private static FlowAuthoringContractEntry CreateOperationContract(
            string id,
            params FlowAuthoringArgumentContract[] arguments)
        {
            var entry = CreateAuthoringContract(id, FlowValueKind.Any);
            SetPrivateField(
                entry,
                "arguments",
                new List<FlowAuthoringArgumentContract>(arguments ?? Array.Empty<FlowAuthoringArgumentContract>()));
            SetPrivateField(entry, "allowAdditionalArguments", false);
            return entry;
        }

        private static FlowAuthoringArgumentContract CreateArgumentContract(
            string key,
            FlowValueKind kind,
            bool required)
        {
            var argument = new FlowAuthoringArgumentContract();
            SetPrivateField(argument, "key", key);
            SetPrivateField(argument, "valueKind", kind);
            SetPrivateField(argument, "required", required);
            return argument;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
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
