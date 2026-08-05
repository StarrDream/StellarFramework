# BuildScript — CLI 构建入口使用说明

`BuildScript` 提供 Unity batchmode 命令行构建入口，供本地脚本、CI/CD 自动化出包使用。
无需打开编辑器点 ToolsHub，一条命令即可构建指定平台并输出到指定目录。

> ✅ 已实测验证：StandaloneWindows64 构建成功（约 83 秒，产物 926.9MB）。
> 下方"注意事项"中的坑均为实测踩坑记录。

## 位置

`Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/BuildScript.cs`

## 命令行用法

```bash
# Windows (PowerShell) — 推荐用 cmd 批处理（见注意事项 2）
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "C:\GitProjects\StellarFramework" `
  -executeMethod StellarFramework.Build.BuildScript.PerformBuild `
  -buildTarget StandaloneWindows64 `
  -output BuildArtifacts/StandaloneWindows64 `
  -version 1.2.3 `
  -logFile build.log

# Android
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "C:\GitProjects\StellarFramework" `
  -executeMethod StellarFramework.Build.BuildScript.PerformBuild `
  -buildTarget Android `
  -output BuildArtifacts/Android `
  -version 1.2.3 `
  -logFile build.log
```

> ⚠️ **重要**：实测发现 PowerShell `Start-Process -ArgumentList` 数组传递会**吞掉 `-executeMethod` 之后的自定义参数**，
> 导致 executeMethod 不执行（1 秒退出、无构建）。可靠做法见下方"注意事项 2"。已实测可行的方式：
> 用环境变量传自定义参数（`UNITY_BUILD_TARGET` / `UNITY_OUTPUT_DIR`），命令行只保留 Unity 标准参数。
> 或在 cmd 批处理中确保所有 Unity 参数在自定义参数之前、且执行方式为直接调用。

## 参数说明

| 参数 | 说明 | 默认值 |
|---|---|---|
| `-buildTarget` | 目标平台：`StandaloneWindows64` / `Android` / `iOS` / `WebGL` 等（`BuildTarget` 枚举名） | `EditorUserBuildSettings.activeBuildTarget` |
| `-output` | 输出目录（相对项目根） | `BuildArtifacts/<Target>` |
| `-version` | 设置 `PlayerSettings.bundleVersion` | 不修改 |
| `-clean` | 构建前删除输出目录 | 不清理 |

支持环境变量 `UNITY_BUILD_TARGET` / `UNITY_OUTPUT_DIR`（CI 常用），优先级：命令行参数 > 环境变量 > 默认值。

## 构建的场景

取 `File > Build Settings` 中**启用**的场景（`EditorBuildSettings.scenes`）。构建前请确认 Build Settings 已配置要打包的场景。

## 退出码

- `0`：构建成功
- `1`：构建失败（CI 据此判断）

## 输出文件名

| 平台 | 文件名 |
|---|---|
| Windows | `StellarFramework.exe` |
| Android | `StellarFramework.apk` |
| iOS / WebGL / macOS / Linux | `StellarFramework`（目录） |

## CI/CD（GitHub Actions）

项目自带 `.github/workflows/build.yml`，使用 `game-ci/unity-builder@v4` 在 Windows runner 上构建 `StandaloneWindows64` + `Android`。

前置条件（仓库 Settings > Secrets and variables > Actions）：
- `UNITY_LICENSE`：Unity 个人版激活文件 `.ulf` 内容（Unity 2022.3 及以下需要）
- 版本号：自动取 git tag（`versioning: Semantic`）

> 说明：`unity-builder` 会输出到 `build/<target>` 目录，与本地 `-output` 行为不同（由 action 管理）。

## 本地构建（无 Unity License）

个人版 Unity 在 Windows 桌面可直接用上面的命令行（无需 `.ulf`）。CI 的 License 仅用于云端 runner 激活。

## 注意事项

以下均为实测踩坑记录：

1. **`-nographics` 在 Windows 上会导致 batchmode 只输出版本号退出**。Windows 构建**不要加** `-nographics`（它主要给 Linux CI 用）。
2. **参数传递**：实测发现以下方式会导致 `-executeMethod` 之后的自定义参数被吞、构建不执行（1 秒退出）：
   - PowerShell `Start-Process -ArgumentList <数组>`（数组拼接时吞参数）
   - 自定义参数（`-buildTarget` 等）与 Unity 参数混排时
   
   可靠做法（已实测）：
   - **方式 A（推荐）**：命令行只保留 Unity 标准参数，自定义参数用环境变量：
     ```bash
     $env:UNITY_BUILD_TARGET = "StandaloneWindows64"
     $env:UNITY_OUTPUT_DIR = "BuildArtifacts/StandaloneWindows64"
     & "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
       -batchmode -quit -projectPath "C:\GitProjects\StellarFramework" `
       -executeMethod StellarFramework.Build.BuildScript.PerformBuild `
       -logFile build.log
     ```
   - **方式 B**：cmd 批处理，Unity 参数在前、自定义参数在后，直接调用（实测可行）：
     ```cmd
     @echo off
     cd /d C:\GitProjects\StellarFramework
     "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -quit ^
       -projectPath "C:\GitProjects\StellarFramework" ^
       -executeMethod StellarFramework.Build.BuildScript.PerformBuild ^
       -buildTarget StandaloneWindows64 ^
       -output C:\GitProjects\StellarFramework\BuildArtifacts\StandaloneWindows64 ^
       -version 1.0.0 ^
       -logFile C:\GitProjects\StellarFramework\Logs\cli-build.log
     ```
3. **构建前需关闭已打开该项目的 Unity 编辑器**（batchmode 与编辑器争用项目锁），并删除残留的 `Temp/UnityLockfile`（强制杀进程后可能残留，导致 batchmode 拒绝打开项目）。
4. Android 构建需要 Android SDK/NDK 已配置（Unity Hub 安装时勾选）。
5. 构建前建议先运行一次样例构建器（ToolsHub → Quick Start）确保场景与资源完整。
- 热更新产物（`HotUpdate.dll.bytes` / AA bundles）需另行通过 ToolsHub 的 `AA 配置与发布` 生成，`BuildScript` 不负责热更产物。
