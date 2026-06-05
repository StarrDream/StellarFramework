# Tools Hub / HotUpdate Work Handoff

Date: 2026-06-05

## Scope

This checkpoint bundles a coherent slice of recent work around:

- Tools Hub group ordering and Quick Start portal cleanup
- Addressables-based AA workflow tooling migration into a dedicated module area
- HotUpdate / HybridCLR onboarding assets and sample scene stabilization
- Policy tests and docs updates that describe the new onboarding path

This is a temporary handoff commit for environment switching, not a claim that the whole branch is fully production-verified.

## Main outcomes

### 1. Tools Hub grouping and Quick Start docs/tests were tightened

- Explicit group ordering is now documented and tested.
- Quick Start welcome portal copy, entry button, and return path are covered by policy tests.
- User guide now states the intended sidebar order and the fixed resource-management subgroup order.

Key files:

- `Assets/StellarFramework/Editor/StellarToolsHub/StellarToolsHub-使用手册-Guide.md`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/QuickStartCatalogPolicyTests.cs`

### 2. AA workflow tooling was moved under a dedicated Addressables module folder

Older flat files were replaced by files under:

- `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Addressables/`

Important pieces there include:

- `AAHotUpdatePublishToolModule.cs`
- `AAWorkflowWorkspaceInitializer.cs`
- `AAWorkflowConfigSet.cs`
- `AAHotUpdatePublishToolTests.cs`
- `AddressablesHotUpdateRuntimeTests.cs`

### 3. Fixed the HotUpdate sample scene’s missing-script root cause

Console investigation showed that the visible IMGUI `EndLayoutGroup` errors were not the first failure. The deeper failure was:

- `Example_HotUpdateKit_Runner` in `HotUpdateKit_Playable.unity` had a missing script during HybridCLR prebuild / workspace initialization.

The root cause was that `Example_HybridCLRAAStartup` lived as an additional `MonoBehaviour` type in the same file as `Example_HotUpdateKit`, which made the scene reference unstable during the current code / assembly arrangement.

Fix applied:

- `Example_HybridCLRAAStartup` was split into its own file:
  - `Assets/StellarFramework/Samples/KitSamples/Example_HotUpdateKit/Example_HybridCLRAAStartup.cs`
- `Example_HotUpdateKit.cs` now contains only `Example_HotUpdateKit`
- `HotUpdateKit_Playable.unity` was cleaned so the runner references the real standalone script asset instead of the stale scene-side entry

Key files:

- `Assets/StellarFramework/Samples/KitSamples/Example_HotUpdateKit/Example_HotUpdateKit.cs`
- `Assets/StellarFramework/Samples/KitSamples/Example_HotUpdateKit/Example_HybridCLRAAStartup.cs`
- `Assets/StellarFramework/Samples/KitSamples/Scenes/HotUpdateKit_Playable.unity`
- `Assets/StellarFramework/Samples/KitSamples/Editor/ExamplePlayableSceneBuilder.cs`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/HotUpdateKitPlayableScenePolicyTests.cs`

### 4. AA workspace initialization no longer runs directly inside the IMGUI button path

Another root cause behind the layout-stack errors was that the “initialize hot update workspace” button executed heavy HybridCLR / BuildPlayer-class work directly during `OnGUI`.

Fix applied:

- initialization is queued through `EditorApplication.delayCall`
- module state tracks queued/running work
- UI disables repeat initialization clicks while the task is queued/running

Key file:

- `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs`

## Supporting asmdef/layout changes

This checkpoint also includes the assembly-definition reshaping needed for the split:

- `Assets/StellarFramework/Editor/StellarToolsHub/StellarFramework.Editor.asmdef`
- `Assets/StellarFramework/Runtime/Kits/HotUpdateKit/StellarFramework.HotUpdateKit.asmdef`
- `Assets/StellarFramework/Runtime/Kits/Reskit/StellarFramework.ResKit.asmdef`
- `Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader/StellarFramework.ResKit.Addressables.asmdef`
- `Assets/StellarFramework/Samples/KitSamples/Editor/StellarFramework.Samples.Editor.asmdef`
- `Assets/StellarFramework/Samples/StellarFramework.Samples.Runtime.asmdef`
- `Assets/StellarFramework/Samples/KitSamples/Example_HotUpdateKit/StellarFramework.Samples.HotUpdate.Runtime.asmdef`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/StellarFramework.FrameworkValidation.Tests.asmdef`
- `Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Addressables/StellarFramework.FrameworkValidation.Addressables.Tests.asmdef`

These are important: the move to the dedicated Addressables module/test folders and the split HotUpdate sample assembly are not self-contained without the asmdef updates.

## Verification status

What was verified in-session:

- `git diff --check` on the scoped file set reported no whitespace errors, only CRLF warnings.
- Earlier focused policy tests for Quick Start / onboarding were observed passing before Unity session instability.
- Fresh EditMode invocation on 2026-06-05 returned a nominal `Passed` state, but the payload reported `total: 0`, so that result should not be treated as strong proof.

What was not fully closed with high confidence:

- A final stable Unity/MCP verification pass across all related Addressables/HotUpdate tests was blocked by repeated Unity plugin session disconnects after script reloads.
- `read_console` later showed unrelated editor/environment noise as well:
  - serialization-depth errors on `Training.children`
  - warning/error about Unity running with Administrator privileges

## Recommended next steps

1. Reopen Unity in a stable session if possible.
2. Re-run the focused tests:
   - `StellarFramework.Tests.FrameworkValidation.QuickStartCatalogPolicyTests`
   - `StellarFramework.Tests.FrameworkValidation.HotUpdateKitPlayableScenePolicyTests`
   - `StellarFramework.Tests.FrameworkValidation.Addressables.AAHotUpdatePublishToolTests`
   - `StellarFramework.Tests.FrameworkValidation.Addressables.AddressablesHotUpdateRuntimeTests`
3. Manually open:
   - `Tools Hub -> Start Here -> Quick Start`
   - `Tools Hub -> 资源管理 -> AA 配置与发布`
4. In the AA workflow screen, verify that clicking the initialization button no longer produces `EndLayoutGroup` / IMGUI layout-stack errors.
5. Re-check `HotUpdateKit_Playable.unity` in Inspector to confirm `Example_HotUpdateKit_Runner` has no missing components.

## Important boundaries

- This checkpoint intentionally avoids claiming the entire branch is complete.
- The surrounding working tree contains many other unrelated or broader-scope modifications; this handoff focuses only on the current Tools Hub / Addressables / HotUpdate onboarding chain.
