# StellarFramework Single-Package Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a single public `StellarFramework.unitypackage` that installs dependencies and then imports the full framework payload from an embedded asset.

**Architecture:** Keep the dependency-safe bootstrap package as the first imported layer, but change it from a selector UI into a one-click installer backed by an embedded full-framework payload. Update the package exporter to build that payload, embed it under `Assets/StellarFrameworkBootstrap/Payloads`, and export one public package plus Chinese distribution docs.

**Tech Stack:** Unity Editor C#, AssetDatabase export/import APIs, Unity Package Manager, NUnit EditMode tests, Markdown docs

---

### Task 1: Lock the new public contract in tests

**Files:**
- Modify: `StellarFramework/Tests/EditMode/FrameworkValidation/PackagePublisherPolicyTests.cs`

- [ ] **Step 1: Write failing test expectations for the single-package naming and Chinese UI text**

Add assertions for:
- `StellarFramework.unitypackage`
- `StellarFramework/单包安装器`
- `一键安装 StellarFramework`
- absence of the old base/full chooser labels

- [ ] **Step 2: Run the targeted test to verify it fails**

Run with Unity Test Framework for `PackagePublisherPolicyTests`.
Expected: FAIL because current source still contains the old split-package strings and chooser flow.

- [ ] **Step 3: Extend the test to cover embedded payload and Chinese README text**

Add assertions that the installer source references:
- a payload directory or payload file lookup
- `ImportPackage`
- no `OpenFilePanel`

Add README assertions for:
- `只需导入一个包`
- `一键安装`

- [ ] **Step 4: Run the targeted test again**

Expected: FAIL for the new payload and README assertions.

### Task 2: Implement the single-package exporter

**Files:**
- Modify: `StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs`

- [ ] **Step 1: Implement the minimal exporter changes**

Change the exporter to:
- define the public package name as `StellarFramework.unitypackage`
- export the full framework payload first
- write the payload into `Assets/StellarFrameworkBootstrap/Payloads`
- export the public package from `Assets/StellarFrameworkBootstrap`

- [ ] **Step 2: Write Chinese distribution guide text**

Replace the old dependency guide with Chinese text that explains:
- external users only import one package
- installer window will finish dependency installation and framework import
- required UPM packages are installed automatically

- [ ] **Step 3: Keep internal helper boundaries small**

Use focused helpers for:
- collecting full asset paths
- building payload output paths
- refreshing generated bootstrap payload assets

- [ ] **Step 4: Re-run the targeted test**

Expected: still FAIL because the bootstrap installer and README are not updated yet.

### Task 3: Replace the chooser bootstrap with a one-click installer

**Files:**
- Modify: `StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapInstaller.cs`
- Modify: `StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapPackageUtility.cs`
- Modify: `StellarFrameworkBootstrap/Editor/StellarFrameworkBootstrapWindow.cs`

- [ ] **Step 1: Add failing expectations mentally aligned with current tests**

Use the existing failing policy test as the red state for this task. Do not change production code until the red state is confirmed.

- [ ] **Step 2: Implement payload import support**

Add utility and installer logic to:
- locate the embedded payload asset
- copy it to a temp `.unitypackage`
- import it after dependencies finish

- [ ] **Step 3: Simplify the window into one Chinese action**

Replace:
- base/full buttons
- `OpenFilePanel`

With:
- one menu entry: `StellarFramework/单包安装器`
- one button: `一键安装 StellarFramework`
- Chinese status and error labels

- [ ] **Step 4: Re-run the targeted test**

Expected: the targeted policy test passes.

### Task 4: Update public Chinese docs

**Files:**
- Modify: `StellarFrameworkBootstrap/README.md`

- [ ] **Step 1: Replace the English README with Chinese install steps**

Document:
- 只需导入一个包
- 打开单包安装器
- 点击一键安装
- 自动安装依赖并导入完整框架

- [ ] **Step 2: Re-run the targeted test**

Expected: PASS for README expectations.

### Task 5: Verify the whole slice

**Files:**
- No code changes required unless verification fails

- [ ] **Step 1: Run the focused test suite**

Run the policy tests that cover package publishing and bootstrap docs.

- [ ] **Step 2: Refresh Unity and check the console**

Verify there are no compilation errors caused by the packaging changes.

- [ ] **Step 3: Summarize generated artifacts and residual risk**

Call out that payload generation happens during export, so runtime verification of the final `.unitypackage` still depends on executing the exporter inside Unity.
