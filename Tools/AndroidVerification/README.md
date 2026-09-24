# StellarFramework Android Verification

Dedicated Android emulator automation for StellarFramework release validation.

## Local environment

- SDK root: `C:\Android\Sdk`
- AVD: `StellarFramework_API35`
- Image: Android 15 / API 35 / Google APIs / x86_64
- Acceleration: Windows Hypervisor Platform (WHPX)
- ADB/Platform Tools: installed in the dedicated SDK, separate from Unity Hub's embedded SDK
- JDK for Android CLI: Microsoft OpenJDK 17
- Unity 2022.3 keeps using its own embedded JDK/SDK unless its External Tools settings are changed manually.

The scripts intentionally identify the target emulator by **AVD name**, not by “first ADB device”, so connected PICO/phones/other emulators are not targeted accidentally.

## Validate environment

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Test-StellarAndroidEnvironment.ps1
```

## Start the dedicated emulator

Headless:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Start-StellarAndroid.ps1
```

Windowed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Start-StellarAndroid.ps1 -Windowed
```

The launcher uses a free emulator console port and later resolves the device by AVD name.

## Stop

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Stop-StellarAndroid.ps1
```

## APK smoke test

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Invoke-StellarApkSmoke.ps1 `
  -ApkPath .\Builds\MyValidation.apk `
  -RuntimeSeconds 20
```

`PackageName` and the launch activity are read from the APK with the official Android `aapt` tool by default. They can still be overridden explicitly for diagnosis.

The smoke runner:

1. finds/starts only `StellarFramework_API35`;
2. waits for `sys.boot_completed=1`;
3. disables Android animation scales for deterministic automation;
4. installs/replaces the APK and clears app data;
5. cold starts the launchable Activity and asserts the process stays alive;
6. records full/app-scoped logcat, package/activity dumps, screenshot, and `result.json`;
7. fails on configured Unity Error / FATAL EXCEPTION / ANR patterns;
8. force-stops and restarts the app, then repeats process/log validation.

## Full Release verification pipeline

Run from a shell while no other Unity process has the same project open:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Invoke-StellarAndroidReleaseVerification.ps1
```

This performs:

`Unity Release Build -> Start/Wait Emulator -> Install -> Clear Data -> Cold Start -> Smoke -> logcat -> Screenshot -> Stop/Restart App -> PASS/FAIL`.

The Unity build entry point is `StellarFrameworkAndroidReleaseVerificationBuild.BuildRelease`. It temporarily selects IL2CPP + x86_64 + non-development APK output and restores the previous Unity Android build settings in `finally`. It does not modify Unity Hub's embedded SDK/JDK configuration.

Unity build coordination state is written to `Library/StellarFramework/AndroidVerification/android-build-state.json`, intentionally separate from the APK output directory so Unity/Gradle output cleanup cannot delete the pipeline handshake file.

For emulator/ADB-only validation of an already built APK:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Invoke-StellarAndroidReleaseVerification.ps1 `
  -SkipBuild `
  -ApkPath .\Builds\AndroidEmulator\StellarFramework-ArchitectureDemo-x86_64.apk
```

## Android Release IL2CPP HotUpdate profile

Run the full target-platform HotUpdate gate from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Invoke-StellarAndroidReleaseVerification.ps1 `
  -HotUpdate
```

This reuses the Android Release pipeline and smoke runner. It requires the Unity Active Build Target to be Android, runs HybridCLR `Generate/All` under temporary IL2CPP + x86_64 settings, exports freshly generated Android AOT metadata / `HotUpdate.dll` / Manifest SHA, and rebuilds the verification YooAsset package. The settings are restored by the Editor methods in `finally`.

The pipeline serves the package from a local Python standard-library HTTP server bound only to `127.0.0.1`, then uses `adb reverse` to connect the emulator to it. It builds a non-Development IL2CPP APK and passes CDN/package/version values through Android launch Intent extras. The app must prove a cold download and a force-stop/restart cache hit. During each HotUpdate launch, the smoke runner waits for the Player's `BootstrapEntered` marker; it uses UIAutomator to find Android's accessible startup prompt buttons and selects **Wait** for `android:id/aerr_wait` when a system app is unresponsive. It records the number of handled system prompts and fails if the Player bootstrap does not appear within the bounded startup window. Each run emits the complete structured result as ordered, 512-character Base64 log chunks because Unity's Android logger truncates long messages; the smoke runner requires every chunk exactly once, reconstructs and parses the JSON, then asserts Manifest target Android, ResKit loads, SHA match, successful AOT metadata load, loaded `HotUpdate` Assembly, entry execution marker, and 0 redownloads after restart.

The HotUpdate profile starts the existing `StellarFramework_API35` AVD with 4096 MB RAM because the Android Release IL2CPP player and API 35 Google APIs image exceeded the prior 2048 MB test configuration. A manually running AVD is reused only when Android reports at least 3584 MB in `/proc/meminfo`; otherwise the pipeline stops before install and asks for that same AVD to be restarted through the existing helper. The ordinary smoke profile keeps its 2048 MB default.

Evidence is written under `Tools/AndroidVerification/Results/<run-id>/`, including both app logcats, full logcats, screenshot, `result.json`, `pipeline-result.json`, build state references, and CDN request JSONL. Android runtime logs include `[StellarHotUpdateVerificationStage]` milestones so a stalled updater can be located without treating a startup message as PASS; only the final structured result satisfies the smoke gate. Use `-CdnPort` or `-PythonExe` when the local default is unavailable. `-SkipBuild` is intentionally unsupported with `-HotUpdate` because the gate must regenerate target artifacts and build a fresh Release APK.

If the UnitySkills gateway returns HTTP 504 after its request timeout, the pipeline waits for the Editor-written build state and continues only when that state is `PASS`, the Android build result is `Succeeded`, and the recorded APK exists. `pipeline-result.json` records the recovered transport warning; a missing, failed, or incomplete build artifact remains a hard failure.

The PlayMode Range interruption/resume Gate remains a separate complementary check; Android runs here do not inject a download interruption.

Headless startup uses Google SwiftShader instead of the host GPU renderer. This is slower than direct GPU rendering but materially more deterministic for unattended CI and avoids host-driver instability observed with `-gpu auto`.

This is a **smoke gate**, not a replacement for real-device GPU, vendor-ROM, ARM64/native-plugin, XR, PICO or hardware-sensor testing.
