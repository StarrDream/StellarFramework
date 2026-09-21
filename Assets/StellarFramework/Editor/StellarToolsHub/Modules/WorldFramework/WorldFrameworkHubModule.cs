using System;
using StellarFramework.PlacementKit;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldKit;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    [StellarTool(
        "World Framework",
        "框架核心",
        21,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.WorldKit.Core",
            "StellarFramework.WorldGenKit.Core"
        })]
    public sealed class WorldFrameworkHubModule : ToolModule
    {
        private enum Tab
        {
            Basic,
            Advanced,
            Diagnostics
        }

        private Tab _tab;
        private Vector2 _scroll;
        private int _sourceIndex;
        private WorldGenerationAuthoringProfile _authoringProfile;
        private WorldGenerationAuthoringCompileResult _authoringCompile;
        private WorldSemanticAuthoringCompileResult _semanticCompile;
        private WorldAuthoringValidationReport _validationReport;
        private WorldCandidateHeatmapSnapshot _heatmap;
        private Texture2D _heatmapTexture;
        private WorldPlacementProbeResult? _placementProbeResult;
        private WorldFrameworkDetailSnapshot _detailSnapshot;
        private string _detailError = string.Empty;
        private int _previewResourceIndex;
        private int _previewWidth = 64;
        private int _previewHeight = 64;
        private long _previewSeed = 1L;
        private string _authoringMessage = string.Empty;

        public override string Icon => "d_Grid.PaintTool";
        public override string Description =>
            "WorldKit / WorldGenKit 的 Editor-only 诊断与生产 Authoring 入口；Runtime 不依赖 ToolsHub。";

        public override void OnDisable()
        {
            DestroyHeatmapTexture();
        }

        public override void OnGUI()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Basic", "Advanced", "Diagnostics" }, EditorStyles.toolbarButton);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawAuthoringProfile();
            DrawSourceSelector();
            IWorldFrameworkDiagnosticsSource source = GetSelectedSource();

            switch (_tab)
            {
                case Tab.Basic:
                    DrawBasic(source);
                    break;
                case Tab.Advanced:
                    DrawAdvanced(source == null ? null : source.GenerationPlan);
                    break;
                case Tab.Diagnostics:
                    DrawDiagnostics(source);
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAuthoringProfile()
        {
            Section("Authoring Profile");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _authoringProfile = (WorldGenerationAuthoringProfile)EditorGUILayout.ObjectField(
                    "Profile",
                    _authoringProfile,
                    typeof(WorldGenerationAuthoringProfile),
                    false);

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新建 Profile", GUILayout.Width(110)))
                        CreateProfileAsset();
                    using (new EditorGUI.DisabledScope(_authoringProfile == null))
                    {
                        if (GUILayout.Button("Validate / Compile", GUILayout.Width(130)))
                            CompileAuthoringProfile();
                    }
                }

                if (_authoringProfile != null)
                {
                    SerializedObject serialized = new SerializedObject(_authoringProfile);
                    serialized.Update();
                    EditorGUILayout.PropertyField(serialized.FindProperty("_profileId"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_profileVersion"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_width"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_height"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_sampleStep"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_channels"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_terrain"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_biomeSurface"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_buildable"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_resources"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_features"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("_placementProbe"), true);
                    if (serialized.ApplyModifiedProperties())
                    {
                        _authoringCompile = null;
                        _semanticCompile = null;
                        _validationReport = null;
                        _heatmap = null;
                        DestroyHeatmapTexture();
                        _placementProbeResult = null;
                        _authoringMessage = "Profile changed; validate/compile again.";
                    }
                }

                if (!string.IsNullOrEmpty(_authoringMessage))
                    EditorGUILayout.HelpBox(
                        _authoringMessage,
                        _authoringCompile != null && _authoringCompile.Success
                            ? MessageType.Info
                            : MessageType.Warning);
            }
        }

        private void DrawSourceSelector()
        {
            Section("数据源");
            int count = WorldFrameworkDiagnosticsRegistry.Count;
            if (count == 0) return;

            if (_sourceIndex >= count) _sourceIndex = count - 1;
            string[] names = new string[count];
            for (int i = 0; i < count; i++) names[i] = WorldFrameworkDiagnosticsRegistry.GetAt(i).DisplayName;
            EditorGUI.BeginChangeCheck();
            _sourceIndex = EditorGUILayout.Popup("World Source", _sourceIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                _detailSnapshot = null;
                _detailError = string.Empty;
            }
        }

        private IWorldFrameworkDiagnosticsSource GetSelectedSource()
        {
            int count = WorldFrameworkDiagnosticsRegistry.Count;
            if (count == 0) return null;
            if (_sourceIndex < 0 || _sourceIndex >= count) _sourceIndex = 0;
            return WorldFrameworkDiagnosticsRegistry.GetAt(_sourceIndex);
        }

        private void DrawBasic(IWorldFrameworkDiagnosticsSource source)
        {
            if (_authoringCompile != null && _authoringCompile.Success)
            {
                Section("Authoring Compile");
                EditorGUILayout.LabelField("Profile", $"{_authoringProfile.ProfileId} v{_authoringProfile.ProfileVersion}");
                EditorGUILayout.LabelField("Layout", $"{_authoringCompile.Layout.Width} × {_authoringCompile.Layout.Height} / step {_authoringCompile.Layout.SampleStep}");
                EditorGUILayout.LabelField("Plan Hash", _authoringCompile.Plan.PlanHash.ToString("X16"));
                EditorGUILayout.LabelField("Channels / Stages", $"{_authoringCompile.Plan.Channels.Count} / {_authoringCompile.Plan.StageCount}");
            }
            if (_semanticCompile != null && _semanticCompile.Success)
            {
                Section("Semantic Catalogs");
                EditorGUILayout.LabelField("Occupancy Types", _semanticCompile.OccupancyRegistry.Count.ToString());
                EditorGUILayout.LabelField("Resources", _semanticCompile.ResourceCatalog.Count.ToString());
                EditorGUILayout.LabelField("Features", _semanticCompile.FeatureCatalog.Count.ToString());
            }
            if (_validationReport != null)
            {
                Section("Memory Report");
                WorldAuthoringMemoryReport memory = _validationReport.Memory;
                EditorGUILayout.LabelField("Samples", memory.SampleCount.ToString("N0"));
                EditorGUILayout.LabelField(
                    "Known Fixed Channel Bytes",
                    EditorUtility.FormatBytes(memory.KnownFixedChannelBytes));
                EditorGUILayout.LabelField(
                    "Dense / Constant / Variable",
                    $"{memory.DenseChannelCount} / {memory.ConstantChannelCount} / {memory.VariableStorageChannelCount}");
                if (memory.VariableStorageChannelCount > 0)
                    EditorGUILayout.HelpBox(
                        "Sparse / Chunked / Computed / External storage is variable and intentionally excluded from the fixed-byte estimate.",
                        MessageType.Info);
            }

            if (source == null)
            {
                Section("Runtime Source");
                EditorGUILayout.HelpBox(
                    "没有已注册的 World diagnostics source。项目 Editor 代码可调用 WorldFrameworkDiagnosticsRegistry.Register(...) 显式接入自己的 World Service；Runtime 无需全局单例。",
                    MessageType.Info);
                return;
            }

            Section("World 概览");
            if (!source.TryCaptureRuntimeSnapshot(out WorldFrameworkRuntimeSnapshot snapshot, out string error))
            {
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(error) ? "World snapshot capture failed." : error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("World", snapshot.WorldId.Value);
            EditorGUILayout.LabelField("Extent", snapshot.Extent.ToString());
            EditorGUILayout.LabelField("Registered Chunks", snapshot.RegisteredChunks.ToString());
            EditorGUILayout.LabelField("Lifecycle", $"Unloaded {snapshot.UnloadedChunks} / Metadata {snapshot.MetadataChunks} / DataReady {snapshot.DataReadyChunks} / Active {snapshot.ActiveChunks}");
            EditorGUILayout.LabelField("Streaming", $"Metadata {snapshot.StreamingMetadataChunks} / Data {snapshot.StreamingDataChunks} / Simulation {snapshot.StreamingSimulationChunks} / Presentation {snapshot.StreamingPresentationChunks}");
            EditorGUILayout.LabelField("Dirty / Delta", $"{snapshot.DirtyChunks} / {snapshot.DeltaCount}");
            EditorGUILayout.LabelField("Approx Managed", EditorUtility.FormatBytes(snapshot.ApproximateManagedBytes));
            DrawChunkDetails(source);

            WorldGenerationPlan plan = source.GenerationPlan;
            Section("Generation");
            if (plan == null)
            {
                EditorGUILayout.HelpBox("当前 source 未暴露已编译 WorldGenerationPlan。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Plan Hash", plan.PlanHash.ToString("X16"));
                EditorGUILayout.LabelField("Channels / Stages", $"{plan.Channels.Count} / {plan.StageCount}");
            }
        }

        private void DrawAdvanced(WorldGenerationPlan plan)
        {
            Section("Compiled Pipeline");
            if (_authoringCompile != null && _authoringCompile.Success)
            {
                EditorGUILayout.LabelField("Authoring Preview", EditorStyles.boldLabel);
                DrawPlan(_authoringCompile.Plan);
                if (plan != null) Section("Runtime Source Pipeline");
            }

            if (plan == null)
            {
                if (_authoringCompile == null || !_authoringCompile.Success)
                    EditorGUILayout.HelpBox("当前没有已编译的 Authoring Profile 或 Runtime WorldGenerationPlan。", MessageType.Info);
            }
            else
            {
                DrawPlan(plan);
            }

            DrawResourceHeatmap();
        }

        private void DrawPlan(WorldGenerationPlan plan)
        {
            WorldGenerationChannelInspection[] channels = new WorldGenerationChannelInspection[plan.Channels.Count];
            WorldGenerationInspectorModel.WriteChannels(plan, channels);
            EditorGUILayout.LabelField("Channels", channels.Length.ToString(), EditorStyles.boldLabel);
            for (int i = 0; i < channels.Length; i++)
            {
                WorldGenerationChannelInspection channel = channels[i];
                EditorGUILayout.LabelField($"[{channel.Index}] {channel.Id.Value}", $"{channel.Storage.Kind} / {channel.Storage.Scope} / {channel.SourceMode}");
            }

            WorldGenerationStageInspection[] stages = new WorldGenerationStageInspection[plan.StageCount];
            WorldGenerationInspectorModel.WriteStages(plan, stages);
            Section("Stages");
            for (int i = 0; i < stages.Length; i++)
            {
                WorldGenerationStageInspection stage = stages[i];
                EditorGUILayout.LabelField($"[{stage.Index}] {stage.StageId.Value}", stage.SeedScope.ToString());
                EditorGUILayout.LabelField("  I/O", $"Required {stage.RequiredCount}, Optional {stage.OptionalCount}, Produced {stage.ProducedCount}, Mutated {stage.MutatedCount}");
            }
        }

        private void DrawDiagnostics(IWorldFrameworkDiagnosticsSource source)
        {
            if (_authoringCompile != null && _authoringCompile.Messages.Count > 0)
            {
                Section("Authoring Validation");
                for (int i = 0; i < _authoringCompile.Messages.Count; i++)
                    EditorGUILayout.HelpBox(_authoringCompile.Messages[i], _authoringCompile.Success ? MessageType.Info : MessageType.Warning);
            }
            if (_semanticCompile != null && _semanticCompile.Messages.Count > 0)
            {
                Section("Semantic Validation");
                for (int i = 0; i < _semanticCompile.Messages.Count; i++)
                    EditorGUILayout.HelpBox(_semanticCompile.Messages[i], MessageType.Warning);
            }
            if (_validationReport != null)
            {
                Section("Consolidated Validator");
                EditorGUILayout.LabelField(
                    "Status",
                    _validationReport.Success
                        ? $"PASS / {_validationReport.WarningCount} warnings"
                        : $"FAIL / {_validationReport.ErrorCount} errors / {_validationReport.WarningCount} warnings");
                for (int i = 0; i < _validationReport.Issues.Count; i++)
                {
                    WorldAuthoringValidationIssue issue = _validationReport.Issues[i];
                    MessageType type = issue.Severity == WorldAuthoringValidationSeverity.Error
                        ? MessageType.Error
                        : issue.Severity == WorldAuthoringValidationSeverity.Warning
                            ? MessageType.Warning
                            : MessageType.Info;
                    EditorGUILayout.HelpBox($"{issue.Code}: {issue.Message}", type);
                }
            }

            if (_authoringProfile != null)
            {
                Section("Placement Probe");
                if (GUILayout.Button("Run Placement Probe", GUILayout.Width(160)))
                {
                    try
                    {
                        _placementProbeResult =
                            WorldSemanticAuthoringCompiler.EvaluatePlacement(
                                _authoringProfile.PlacementProbe);
                    }
                    catch (Exception exception)
                    {
                        _placementProbeResult = null;
                        _authoringMessage = "Placement Probe: " + exception.Message;
                    }
                }

                if (_placementProbeResult.HasValue)
                {
                    WorldPlacementProbeResult probe = _placementProbeResult.Value;
                    EditorGUILayout.LabelField(
                        "Result",
                        probe.Evaluation.Allowed
                            ? $"Allowed / Score {probe.Evaluation.Score:0.###}"
                            : $"Rejected / {probe.Evaluation.FailureCount} failures");
                    for (int i = 0; i < probe.Failures.Count; i++)
                    {
                        PlacementFailureRecord failure = probe.Failures[i];
                        EditorGUILayout.LabelField(
                            failure.RuleId.Value,
                            failure.FailureId.Value);
                    }
                }
            }

            Section("Last Generation Report");
            if (source == null)
            {
                EditorGUILayout.HelpBox("当前没有 Runtime diagnostics source。", MessageType.Info);
                return;
            }
            DrawDataLayerAndDeltaDetails(source);

            WorldGenerationReport report = source.LastGenerationReport;
            if (report == null)
            {
                EditorGUILayout.HelpBox("当前 source 没有 GenerationReport。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Status", report.Status.ToString());
            EditorGUILayout.LabelField("Plan Hash", report.PlanHash.ToString("X16"));
            EditorGUILayout.LabelField("Run Key", $"({report.RunKey.X}, {report.RunKey.Y}) / {report.RunKey.LocalKey}");
            EditorGUILayout.LabelField("Terminal", report.TerminalStageIndex < 0 ? "None" : $"Stage {report.TerminalStageIndex}: {report.TerminalCode.Value}");

            WorldGenerationStageExecutionRecord[] records = new WorldGenerationStageExecutionRecord[report.StageCount];
            WorldGenerationInspectorModel.WriteReportStages(report, records);
            for (int i = 0; i < records.Length; i++)
            {
                WorldGenerationStageExecutionRecord record = records[i];
                EditorGUILayout.LabelField($"[{i}] {record.StageId.Value}", $"{record.Result.Status} / {record.Result.Code.Value}");
            }
        }

        private void DrawChunkDetails(IWorldFrameworkDiagnosticsSource source)
        {
            Section("Region / Chunk Viewer");
            if (!(source is IWorldFrameworkDetailDiagnosticsSource detailSource))
            {
                EditorGUILayout.HelpBox(
                    "当前 source 只提供汇总诊断。若项目需要 Chunk / DataLayer / Delta 明细，可在 Editor bridge 额外实现 IWorldFrameworkDetailDiagnosticsSource。",
                    MessageType.Info);
                return;
            }

            DrawDetailRefresh(detailSource);
            if (_detailSnapshot == null) return;

            EditorGUILayout.LabelField("Chunks", _detailSnapshot.ChunkCount.ToString());
            const int maxRows = 256;
            int visible = Math.Min(_detailSnapshot.ChunkCount, maxRows);
            for (int i = 0; i < visible; i++)
            {
                WorldChunkDiagnosticSnapshot chunk = _detailSnapshot.GetChunk(i);
                EditorGUILayout.LabelField(
                    $"[{chunk.Coord.X}, {chunk.Coord.Y}]  Region [{chunk.Region.X}, {chunk.Region.Y}]",
                    $"{chunk.LifecycleState} / {chunk.StreamingState} / Dirty={chunk.Dirty} / {EditorUtility.FormatBytes(chunk.ApproximateManagedBytes)}");
            }
            if (_detailSnapshot.ChunkCount > maxRows)
            {
                EditorGUILayout.HelpBox(
                    $"Showing first {maxRows} of {_detailSnapshot.ChunkCount} chunks. Filter/capture a smaller diagnostic snapshot at the project bridge for large worlds.",
                    MessageType.Info);
            }
        }

        private void DrawDataLayerAndDeltaDetails(IWorldFrameworkDiagnosticsSource source)
        {
            if (!(source is IWorldFrameworkDetailDiagnosticsSource detailSource)) return;
            DrawDetailRefresh(detailSource);
            if (_detailSnapshot == null) return;

            Section("Data Layer Inspector");
            for (int i = 0; i < _detailSnapshot.DataLayerCount; i++)
            {
                WorldDataLayerDiagnosticSnapshot layer = _detailSnapshot.GetDataLayer(i);
                EditorGUILayout.LabelField(
                    $"{layer.LayerId.Value} / {layer.Scope}",
                    $"{layer.Owner} / {layer.ValueType} / {EditorUtility.FormatBytes(layer.ApproximateManagedBytes)}");
            }
            if (_detailSnapshot.DataLayerCount == 0)
                EditorGUILayout.HelpBox("No data-layer diagnostics in this snapshot.", MessageType.Info);

            Section("Delta Inspector");
            for (int i = 0; i < _detailSnapshot.DeltaCount; i++)
            {
                WorldDeltaDiagnosticSnapshot delta = _detailSnapshot.GetDelta(i);
                EditorGUILayout.LabelField(
                    $"#{delta.Sequence} {delta.TypeId.Value} v{delta.Version.Value}",
                    $"{FormatTarget(delta.Target)} / {EditorUtility.FormatBytes(delta.ApproximateManagedBytes)}");
            }
            if (_detailSnapshot.DeltaCount == 0)
                EditorGUILayout.HelpBox("No deltas in this snapshot.", MessageType.Info);
        }

        private void DrawDetailRefresh(IWorldFrameworkDetailDiagnosticsSource source)
        {
            if (_detailSnapshot == null && string.IsNullOrEmpty(_detailError))
                CaptureDetails(source);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Details", GUILayout.Width(110)))
                    CaptureDetails(source);
                if (!string.IsNullOrEmpty(_detailError))
                    GUILayout.Label(_detailError, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void CaptureDetails(IWorldFrameworkDetailDiagnosticsSource source)
        {
            if (source.TryCaptureDetailSnapshot(
                    out WorldFrameworkDetailSnapshot snapshot,
                    out string error))
            {
                _detailSnapshot = snapshot;
                _detailError = string.Empty;
                return;
            }

            _detailSnapshot = null;
            _detailError = string.IsNullOrWhiteSpace(error)
                ? "Detail snapshot capture failed."
                : error;
        }

        private static string FormatTarget(WorldDeltaTarget target)
        {
            switch (target.Kind)
            {
                case WorldDeltaTargetKind.World:
                    return "World";
                case WorldDeltaTargetKind.Region:
                    return $"Region [{target.Region.X}, {target.Region.Y}]";
                case WorldDeltaTargetKind.Chunk:
                    return $"Chunk [{target.Chunk.X}, {target.Chunk.Y}]";
                default:
                    return "Invalid";
            }
        }

        private void CreateProfileAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create World Generation Profile",
                "WorldGenerationProfile",
                "asset",
                "Choose where to save the authoring profile.");
            if (string.IsNullOrEmpty(path)) return;

            WorldGenerationAuthoringProfile profile = ScriptableObject.CreateInstance<WorldGenerationAuthoringProfile>();
            profile.ResetToDefaults();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            _authoringProfile = profile;
            _authoringCompile = null;
            _semanticCompile = null;
            _validationReport = null;
            _heatmap = null;
            DestroyHeatmapTexture();
            _placementProbeResult = null;
            _authoringMessage = "Profile created.";
        }

        private void CompileAuthoringProfile()
        {
            _validationReport = WorldAuthoringDiagnosticsModel.Validate(_authoringProfile);
            _authoringCompile = _validationReport.Generation;
            _semanticCompile = _validationReport.Semantics;
            _heatmap = null;
            DestroyHeatmapTexture();
            _placementProbeResult = null;
            if (_validationReport.Success)
            {
                _authoringMessage =
                    $"PASS — plan {_authoringCompile.Plan.PlanHash:X16}, {_authoringCompile.Plan.Channels.Count} channels, {_authoringCompile.Plan.StageCount} stages, {_semanticCompile.ResourceCatalog.Count} resources, {_semanticCompile.FeatureCatalog.Count} features, {_validationReport.WarningCount} warnings.";
                return;
            }

            string pipelineMessages = string.Join("\n", _authoringCompile.Messages);
            string semanticMessages = string.Join("\n", _semanticCompile.Messages);
            _authoringMessage = string.IsNullOrEmpty(pipelineMessages) && string.IsNullOrEmpty(semanticMessages)
                ? "Validation failed without diagnostics."
                : string.Join("\n", new[] { pipelineMessages, semanticMessages });
        }

        private void DrawResourceHeatmap()
        {
            Section("Resource Candidate Heatmap");
            if (_authoringProfile == null || _semanticCompile == null || !_semanticCompile.Success)
            {
                EditorGUILayout.HelpBox(
                    "Validate / Compile a Profile before building a Resource heatmap.",
                    MessageType.Info);
                return;
            }

            int resourceCount = _semanticCompile.ResourceCatalog.Count;
            if (_previewResourceIndex >= resourceCount) _previewResourceIndex = resourceCount - 1;
            _previewResourceIndex = EditorGUILayout.IntSlider(
                "Resource Index",
                _previewResourceIndex,
                0,
                Math.Max(0, resourceCount - 1));
            _previewWidth = Math.Max(1, EditorGUILayout.IntField("Preview Width", _previewWidth));
            _previewHeight = Math.Max(1, EditorGUILayout.IntField("Preview Height", _previewHeight));
            _previewSeed = EditorGUILayout.LongField("Preview Seed", _previewSeed);

            if (GUILayout.Button("Build Heatmap", GUILayout.Width(130)))
            {
                try
                {
                    _heatmap = WorldAuthoringDiagnosticsModel.BuildResourceHeatmap(
                        _authoringProfile,
                        _semanticCompile,
                        _previewResourceIndex,
                        _previewWidth,
                        _previewHeight,
                        unchecked((ulong)_previewSeed));
                    RebuildHeatmapTexture(_heatmap);
                    _authoringMessage =
                        $"Heatmap: {_heatmap.GeneratedCandidates} candidates / {_heatmap.AcceptedCandidates} accepted.";
                }
                catch (Exception exception)
                {
                    _heatmap = null;
                    DestroyHeatmapTexture();
                    _authoringMessage = "Heatmap: " + exception.Message;
                }
            }

            if (_heatmap == null) return;
            EditorGUILayout.LabelField(
                "Generated / Accepted",
                $"{_heatmap.GeneratedCandidates} / {_heatmap.AcceptedCandidates}");
            EditorGUILayout.LabelField(
                "Rejected",
                $"Occupancy {_heatmap.RejectedOccupancy} / Budget {_heatmap.RejectedBudget} / Spacing {_heatmap.RejectedSpacing}");
            EditorGUILayout.HelpBox(
                "Heatmap colors: green = accepted density, red = rejected candidate density.",
                MessageType.Info);
            if (_heatmapTexture != null)
                GUILayout.Label(_heatmapTexture, GUILayout.Width(256), GUILayout.Height(256));
        }

        private void RebuildHeatmapTexture(WorldCandidateHeatmapSnapshot snapshot)
        {
            DestroyHeatmapTexture();
            Texture2D texture = new Texture2D(
                snapshot.Width,
                snapshot.Height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "WorldFramework_ResourceHeatmap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[snapshot.Width * snapshot.Height];
            int maxCandidate = Math.Max(1, snapshot.MaxCandidateCount);
            int maxAccepted = Math.Max(1, snapshot.MaxAcceptedCount);
            for (int y = 0; y < snapshot.Height; y++)
            {
                for (int x = 0; x < snapshot.Width; x++)
                {
                    int candidate = snapshot.GetCandidateCount(x, y);
                    int accepted = snapshot.GetAcceptedCount(x, y);
                    int rejected = Math.Max(0, candidate - accepted);
                    byte red = (byte)Math.Min(255, (rejected * 255) / maxCandidate);
                    byte green = (byte)Math.Min(255, (accepted * 255) / maxAccepted);
                    pixels[(y * snapshot.Width) + x] = new Color32(red, green, 0, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _heatmapTexture = texture;
        }

        private void DestroyHeatmapTexture()
        {
            if (_heatmapTexture == null) return;
            UnityEngine.Object.DestroyImmediate(_heatmapTexture);
            _heatmapTexture = null;
        }
    }
}
