# Framework Validation Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable validation pack that exercises ResKit, UIKit, HotUpdateKit, and nearby Kit sample readiness from one scene.

**Architecture:** Add a sample-only validation runner under KitSamples, backed by a small report model that can be EditMode-tested. Extend the existing `ExamplePlayableSceneBuilder` so the validation scene is generated alongside the other playable scenes.

**Tech Stack:** Unity 2022.3, UniTask, StellarFramework ResKit/UIKit/HotUpdateKit, Unity Test Framework, existing KitSamples scene builder.

---

### Task 1: Report Model Tests

**Files:**
- Create: `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/StellarFramework.FrameworkValidation.Tests.asmdef`
- Create: `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/FrameworkValidationReportTests.cs`
- Create: `Assets/StellarFramework/Samples/KitSamples/Example_FrameworkValidation/FrameworkValidationRunner.cs`

- [ ] Write tests for report entry counting, failure detection, and summary text.
- [ ] Implement the minimal report model in `FrameworkValidationRunner.cs`.
- [ ] Run EditMode tests after Unity refresh.

### Task 2: Runtime Validation Runner

**Files:**
- Modify: `Assets/StellarFramework/Samples/KitSamples/Example_FrameworkValidation/FrameworkValidationRunner.cs`

- [ ] Add OnGUI controls for ResKit, UIKit, HotUpdateKit, and report export.
- [ ] Implement safe dry-run validation for settings, AA status, HybridCLR settings, and UI snapshots.
- [ ] Keep real asset loading behind explicit buttons.

### Task 3: Scene Builder Integration

**Files:**
- Modify: `Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs`

- [ ] Add `BuildFrameworkValidationScene`.
- [ ] Include it in `BuildScenes`.
- [ ] Reuse existing support assets and UIRoot generation.

### Task 4: Docs And Shortboard Fixes

**Files:**
- Modify: `Assets/StellarFramework/Samples/KitSamples/README.md`
- Modify: `Assets/StellarFramework/Samples/KitSamples/Scenes/README.md`
- Modify: `Assets/StellarFramework/Samples/KitSamples/Samples_Index.md`
- Modify: `Assets/StellarFramework/Samples/KitSamples/Example_FrameworkValidation/FrameworkValidationRunner.cs`

- [ ] Document Editor, PlayMode, AA, AB, UIKit, and HybridCLR validation steps in the shared sample index and script header comments.
- [ ] Mention known shortboards that remain manual, especially true remote AA and real HybridCLR dll.bytes.

### Task 5: Verification

**Commands:**
- `dotnet build .\StellarFramework.Samples.Runtime.csproj -v:minimal -clp:ErrorsOnly`
- `dotnet build .\StellarFramework.Samples.Editor.csproj -v:minimal -clp:ErrorsOnly`
- `dotnet build .\StellarFramework.UIKit.csproj -v:minimal -clp:ErrorsOnly`
- `dotnet build .\StellarFramework.sln -v:minimal -clp:ErrorsOnly`

- [ ] Refresh Unity if needed so generated csproj includes new sample and test files.
- [ ] Use `unityMCP` only after confirming `projectRoot` is `C:/UnityProjects/MyProjects/StellarFramework`.
