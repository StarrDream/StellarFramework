using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    /// <summary>
    /// Creates a production-oriented FlowKit integration skeleton.
    /// It deliberately generates boundaries and TODO composition points, not gameplay logic.
    /// </summary>
    internal static class FlowKitProjectScaffolder
    {
        internal static bool CreateWithDialog(string flowId)
        {
            if (string.IsNullOrWhiteSpace(flowId))
            {
                EditorUtility.DisplayDialog(
                    "FlowKit 业务骨架",
                    "请先打开或创建一个具有稳定 FlowId 的流程，再生成业务骨架。",
                    "确定");
                return false;
            }

            string parent = EditorUtility.OpenFolderPanel(
                "选择 FlowKit 业务模块父目录（必须位于 Assets 下）",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrEmpty(parent)) return false;

            string normalizedParent = parent.Replace('\\', '/').TrimEnd('/');
            string normalizedAssets = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (!normalizedParent.StartsWith(normalizedAssets, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "FlowKit 业务骨架",
                    "目标目录必须位于当前 Unity 工程的 Assets 目录下。",
                    "确定");
                return false;
            }

            string moduleName = ToPascalCase(flowId);
            if (string.IsNullOrEmpty(moduleName)) moduleName = "Workflow";
            string moduleDirectory = BuildModuleFiles(flowId, parent, moduleName);
            if (string.IsNullOrEmpty(moduleDirectory))
            {
                EditorUtility.DisplayDialog(
                    "FlowKit 业务骨架",
                    $"目录已存在，不会覆盖：\n{Path.Combine(parent, moduleName + "Flow")}",
                    "确定");
                return false;
            }

            AssetDatabase.Refresh();
            string assetPath = "Assets" + moduleDirectory.Replace('\\', '/')
                .Substring(normalizedAssets.Length);
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }

            Debug.Log(
                $"FlowKit 业务骨架已创建：{assetPath}\n" +
                "请按 MSV 规则把业务放入 Service/Model；生成目录只负责 FlowKit 边界与组装。");
            return true;
        }

        internal static string BuildModuleFiles(string flowId, string parentDirectory, string moduleName = null)
        {
            if (string.IsNullOrWhiteSpace(flowId))
                throw new ArgumentException("FlowId is required.", nameof(flowId));
            if (string.IsNullOrWhiteSpace(parentDirectory))
                throw new ArgumentException("Parent directory is required.", nameof(parentDirectory));

            moduleName = string.IsNullOrEmpty(moduleName) ? ToPascalCase(flowId) : moduleName;
            if (string.IsNullOrEmpty(moduleName)) moduleName = "Workflow";

            string moduleDirectory = Path.Combine(parentDirectory, moduleName + "Flow");
            if (Directory.Exists(moduleDirectory)) return string.Empty;

            Directory.CreateDirectory(moduleDirectory);
            string contracts = CreateDirectory(moduleDirectory, "Contracts");
            string bootstrap = CreateDirectory(moduleDirectory, "Bootstrap");
            string operations = CreateDirectory(moduleDirectory, "Operations");
            string facts = CreateDirectory(moduleDirectory, "Facts");
            CreateDirectory(moduleDirectory, "Bindings");
            CreateDirectory(moduleDirectory, "Graphs");
            CreateDirectory(moduleDirectory, "Tests");

            Write(Path.Combine(moduleDirectory, "README.md"), BuildReadme(flowId, moduleName));
            Write(Path.Combine(contracts, moduleName + "FlowContracts.cs"), BuildContracts(moduleName));
            Write(Path.Combine(bootstrap, moduleName + "FlowConfigurator.cs"), BuildConfigurator(moduleName));
            Write(Path.Combine(operations, moduleName + "OperationAdapterExample.cs"), BuildOperationAdapterExample(moduleName));
            Write(Path.Combine(facts, moduleName + "FlowFactsBridge.cs"), BuildFactsBridge(moduleName));
            Write(Path.Combine(operations, "README.md"), OperationsReadme);
            Write(Path.Combine(moduleDirectory, "Bindings", "README.md"), BindingsReadme);
            Write(Path.Combine(moduleDirectory, "Graphs", "README.md"), GraphsReadme);
            Write(Path.Combine(moduleDirectory, "Tests", "README.md"), TestsReadme);
            return moduleDirectory;
        }

        private static string CreateDirectory(string parent, string name)
        {
            string path = Path.Combine(parent, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Write(string path, string content)
        {
            File.WriteAllText(path, content.Replace("\n", Environment.NewLine), new UTF8Encoding(false));
        }

        private static string ToPascalCase(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool upper = true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c))
                {
                    upper = true;
                    continue;
                }

                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return builder.ToString();
        }

        private static string BuildReadme(string flowId, string moduleName) =>
$@"# {moduleName} Flow Integration

FlowId: `{flowId}`

This folder follows the StellarFramework FlowKit Coding Contract.

```text
Flow Graph
    -> Operation Adapter
    -> Domain Service
    -> Model
    -> View / Domain Events
    -> Flow Facts Bridge
    -> Signal / State
    -> FlowKit
```

Rules:

- Model owns business state.
- Service owns business rules and mutates Model.
- View only presents state and forwards user intent.
- Operation Adapter translates FlowKit calls into Service calls; it is not a Service.
- Facts Bridge projects only workflow-relevant facts back to FlowKit.
- Blackboard stores Flow-local context, never the project Model.
- Scene objects are reached through Binding/Adapter boundaries, never GameObject.Find/runtime scans.
- Configurator is the composition root only.
";

        private static string BuildContracts(string moduleName) =>
$@"namespace ProjectFlow.{moduleName}
{{
    /// <summary>
    /// Stable workflow IDs. Keep IDs centralized and register the same contracts
    /// in a FlowAuthoringCatalog so the editor/build validator can type-check graphs.
    /// </summary>
    public static class {moduleName}FlowContracts
    {{
        public static class Operations {{ }}
        public static class Signals {{ }}
        public static class States {{ }}
        public static class Blackboard {{ }}
        public static class Bindings {{ }}
        public static class Capabilities {{ }}
    }}
}}
";

        private static string BuildConfigurator(string moduleName) =>
$@"using System;
using StellarFramework.FlowKit.Unity;
using UnityEngine;

namespace ProjectFlow.{moduleName}
{{
    /// <summary>
    /// Composition root only. Register adapters/capabilities here.
    /// Never implement gameplay rules in this class.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class {moduleName}FlowConfigurator : MonoBehaviour, IFlowHostConfigurator
    {{
        public void Configure(FlowHostBuilder builder)
        {{
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            RegisterCapabilities(builder);
            RegisterOperations(builder);
        }}

        private void RegisterCapabilities(FlowHostBuilder builder)
        {{
            // TODO: builder.AddCapability(...);
        }}

        private void RegisterOperations(FlowHostBuilder builder)
        {{
            // TODO: one focused IFlowOperationAdapter per external capability/command.
            // builder.RegisterOperation({moduleName}FlowContracts.Operations.Xxx, new XxxOperationAdapter(service));
        }}
    }}
}}
";

        private static string BuildOperationAdapterExample(string moduleName) =>
$@"using System;
using StellarFramework.FlowKit;

namespace ProjectFlow.{moduleName}
{{
    /// <summary>
    /// Example only. Rename this class and inject the real Domain Service.
    /// Adapter translates FlowKit -> Service and owns only the external call lifecycle.
    /// </summary>
    public sealed class {moduleName}OperationAdapterExample : IFlowOperationAdapter
    {{
        public void Start(
            in FlowOperationContext context,
            in FlowOperationRequest request,
            FlowOperationHandle handle,
            Action<FlowOperationResult> complete)
        {{
            if (complete == null) throw new ArgumentNullException(nameof(complete));

            // TODO:
            // 1. validate/translate request arguments;
            // 2. invoke one focused Domain Service capability;
            // 3. complete exactly once with Success / Failure / Cancelled.
            complete(FlowOperationResult.Failure(
                ""Operation adapter template is not implemented. Replace or delete this example before registration.""));
        }}

        public void Cancel(in FlowOperationContext context, FlowOperationHandle handle)
        {{
            // TODO: cancel only the external async handle started by this adapter.
        }}
    }}
}}
";

        private static string BuildFactsBridge(string moduleName) =>
$@"using System;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;
using UnityEngine;

namespace ProjectFlow.{moduleName}
{{
    /// <summary>
    /// Adapter boundary from domain facts to FlowKit.
    /// Subscribe to Service/Model/domain events; do not move gameplay rules here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class {moduleName}FlowFactsBridge : MonoBehaviour
    {{
        [SerializeField] private FlowHost host;

        public void PublishHostSignal(FlowSignalId id, FlowValue payload = default)
        {{
            EnsureHost();
            host.Services.Signals.Publish(id, FlowSignalScope.Host, payload: payload);
        }}

        public void SetExternalState(FlowStateId id, FlowValue value, string sourceKey = null)
        {{
            EnsureHost();
            host.Services.States.Set(
                new FlowStateKey(id, sourceKey),
                value,
                FlowStateLifetime.External);
        }}

        private void EnsureHost()
        {{
            if (host == null) throw new InvalidOperationException(""FlowFactsBridge requires FlowHost."");
            if (!host.IsInitialized) throw new InvalidOperationException(""FlowHost is not initialized."");
        }}
    }}
}}
";

        private const string OperationsReadme =
@"# Operations

Each production adapter should have one focused responsibility.

The generated *OperationAdapterExample.cs compiles but intentionally returns Failure.
Rename/replace it and inject a Domain Service before registering it.

Adapter responsibilities:
- translate FlowOperationRequest into a Domain Service call;
- map completion/failure/cancellation back to FlowOperationResult;
- own/cancel the external async handle it started.

Forbidden:
- gameplay rules;
- direct Model mutation that bypasses Service;
- giant operationId switch routers;
- GameObject.Find / runtime assembly scans;
- swallowed exceptions or fake-success fallbacks.
";

        private const string BindingsReadme =
@"# Bindings

Bindings are stable scene-object boundaries. Graphs keep stable IDs; adapters resolve the runtime object.
Do not store scene objects in Blackboard or static globals.
";

        private const string GraphsReadme =
@"# Graphs

Keep .flow.json and .flow.editor.json here (or in the project's chosen graph folder).
Graph expresses orchestration, branching, timeout and composition; gameplay stays in Services.
";

        private const string TestsReadme =
@"# Tests

Minimum production coverage:
- graph compile/contract validation;
- happy-path business closure;
- Operation failure and cancellation;
- timeout branch;
- missing/invalid Binding;
- Facts Bridge Signal/State projection;
- regression tests for fixed workflow bugs.
";
    }
}
