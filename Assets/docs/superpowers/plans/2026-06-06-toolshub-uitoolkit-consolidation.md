# ToolsHub UI Toolkit Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate the original popup-based ToolsHub tools into the ToolsHub content area, remove standalone popup editor windows from that scope, and keep the tool workflows usable in both Unity 2022 and Unity 6000.

**Architecture:** Keep `StellarFrameworkTools` as the only host window and move the old popup tools to `ToolModule.CreateView()`-based content. For simple tools, build the views directly with UI Toolkit; for complex tools, keep logic in shared state/controller classes and host the interaction inside UI Toolkit-owned views without reopening standalone windows.

**Tech Stack:** Unity Editor C#, UI Toolkit, ToolModule auto-registration, NUnit EditMode policy tests, AssetDatabase/Undo/EditorSceneManager APIs

---

### Task 1: Lock the consolidation contract in tests

**Files:**
- Create: `StellarFramework/Tests/EditMode/FrameworkValidation/ToolsHubUiConsolidationPolicyTests.cs`

- [ ] Add source-policy tests that fail while popup tools still open standalone windows.
- [ ] Assert the ToolsHub module classes for ConfigKit, mesh combine, dictionary serializer, list serializer, folder copy, action engine, and material converter define `CreateView()`.
- [ ] Assert those module sources no longer call `ShowWindow()`, `Open()`, or `GetWindow<...>()` as the primary UI path.
- [ ] Assert the old popup classes no longer derive from `EditorWindow`.

### Task 2: Add shared UI Toolkit support for module migration

**Files:**
- Modify: `StellarFramework/Editor/StellarToolsHub/Core/ToolModule.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Core/StellarFrameworkTools.cs`
- Create: `StellarFramework/Editor/StellarToolsHub/Core/ToolsHubViewFactory.cs`

- [ ] Add a small shared helper layer for common UI Toolkit sections, rows, buttons, help boxes, and scroll hosts.
- [ ] Preserve current ToolsHub grouping and selection behavior.
- [ ] Keep APIs compatible with both Unity 2022 and Unity 6000 by using stable UI Toolkit controls only.

### Task 3: Migrate low-risk popup tools first

**Files:**
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/BuiltinModules.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/CombinedMeshColliderWindow.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/DictionarySerializerWindow.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/ListSerializerWindow.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/FolderContentCopyTool.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/URPMaterialConverterWindow.cs`

- [ ] Move popup-only state and commands into reusable classes or static methods owned by the existing files.
- [ ] Replace each “open window” hub stub with a real `CreateView()` implementation.
- [ ] Remove `EditorWindow` inheritance and standalone open-window entry methods from these tools.

### Task 4: Migrate complex popup tools without changing workflows

**Files:**
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitHubModule.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitWindow.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitWindowInjector.cs`
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/ActionKit/ActionEngineEditorWindow.cs`

- [ ] Refactor ConfigKit into a ToolsHub-owned view while preserving workspace switching, config creation/deletion, and editor behavior.
- [ ] Remove the open-window injector path and bind the ConfigKit editor delegates directly from the in-hub view.
- [ ] Refactor ActionEngine so its UI runs inside ToolsHub ownership instead of a separate popup window.
- [ ] Preserve Undo, playback, graph editing, and asset save behavior.

### Task 5: Verify and update policy/docs if needed

**Files:**
- Modify as needed: `StellarFramework/Tests/EditMode/FrameworkValidation/QuickStartCatalogPolicyTests.cs`
- Modify as needed: `StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md`
- Modify as needed: `StellarFramework/Editor/StellarToolsHub/StellarToolsHub-源码文档-Guide.md`

- [ ] Run the targeted framework validation tests for ToolsHub policy coverage.
- [ ] Verify ToolsHub opens and the migrated modules render without reopening standalone windows.
- [ ] Check that Unity 2022-safe and Unity 6000-safe APIs are still the ones in use.
- [ ] Update docs only where the user-facing entry path changed from popup window to in-hub panel.
