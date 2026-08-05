# StellarFramework CI 构建使用说明

本文档说明如何使用 `.github/workflows/build.yml` 在 GitHub Actions 上自动构建 StellarFramework。

## 一、CI 能做什么

推送代码 / 打 tag / 手动触发时，自动在 Windows 云端机器上：

1. 安装 Unity 2022.3.62f3c1
2. 以 batchmode 运行 `StellarFramework.Build.BuildScript.PerformBuild`
3. 构建 **StandaloneWindows64** 和 **Android** 两个平台
4. 上传构建产物为 GitHub Artifact，可直接下载

构建失败（脚本返回非零退出码）→ CI 任务标红，可配置通知。

## 二、前置条件（一次性配置）

### 1. Unity 个人版 License（必须）

GitHub 云端 runner 是无激活状态的 Unity，需要提供激活文件：

- 打开 Unity → Help → Manage License → 激活你的个人版（Free / Personal）
- 激活文件位置（Windows）：`C:\ProgramData\Unity\Unity_lic.ulf`
- 打开内容，**复制全部文本**

### 2. 配置 secret

仓库页面 → Settings → Secrets and variables → Actions → New repository secret：

| Name | Value |
|---|---|
| `UNITY_LICENSE` | 上面复制的 `.ulf` 文件**完整内容** |

> ⚠️ Unity 2022.3（及以下版本）需要 `.ulf` 激活文件；Unity 6 改用 Personal License，`game-ci/unity-builder@v4` 会自动处理，无需此 secret。

## 三、触发方式

| 方式 | 触发条件 |
|---|---|
| 自动 | push 到 `main` 分支 |
| 自动 | push `v*` 标签（如 `v1.0.0`，用于发版） |
| 手动 | Actions 页面 → 选该 workflow → Run workflow |

手动触发时无需改代码即可重新构建。

## 四、查看与下载产物

1. 仓库 Actions 页 → 点击本次运行
2. 看到 `Build (StandaloneWindows64)` 和 `Build (Android)` 两个任务
3. 点进任一任务 → 底部 **Artifacts** 区域 → 下载 `build-<平台>`

## 五、版本号

- push 到 `main`：版本号来自 `ProjectSettings/ProjectSettings.asset` 的 `bundleVersion`
- push `v*` 标签：自动取 tag 名作为版本号（`versioning: Semantic`）
- 手动触发：同上（取当前 bundleVersion）

## 六、本地等价命令（无 CI 时手动出包）

CI 内部执行的命令等价于：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -quit -nographics -projectPath . `
  -executeMethod StellarFramework.Build.BuildScript.PerformBuild `
  -buildTarget StandaloneWindows64 `
  -version "1.2.3" `
  -logFile build.log
```

详细参数见 `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/BuildScript-使用说明-Guide.md`。

> 本地个人版无需 `.ulf`（桌面直接可用）；仅 CI 云端需要。

## 七、常见问题（FAQ）

### Q1: 构建报 "No valid Unity license found"
→ `UNITY_LICENSE` secret 未配置或内容不完整。重新复制 `Unity_lic.ulf` 全文并更新 secret。

### Q2: Android 构建报 SDK/NDK 缺失
→ 云端 runner 由 `game-ci/unity-builder` 自动装 Android SDK/NDK，一般不会缺。若报错，检查 workflow 的 `targetPlatform` 是否拼写正确（`Android`）。

### Q3: 构建产物在 `build/` 目录而非我指定的 `-output`
→ `unity-builder` 有自己的输出约定（`build/<platform>`），与本地 `-output` 参数不同。这是 action 设计，不影响产物质量。

### Q4: 想只构建一个平台
→ 修改 `build.yml` 的 `matrix.target`，去掉不想构建的平台即可：
```yaml
matrix:
  target: [ StandaloneWindows64 ]
```

### Q5: 如何让 CI 失败时收到通知
→ 仓库 Settings → Notifications，或接 Slack/飞书 webhook（`actions/slack` / `actions/github-script` 等）。

### Q6: 热更产物（DLL / AA bundle）会被 CI 打包吗？
→ 不会。`BuildScript` 只构建 Player，热更产物（`HotUpdate.dll.bytes`、Addressables bundles）需另行用 ToolsHub 的 `AA 配置与发布` 生成并发布到远端/StreamingAssets。

## 八、workflow 文件结构

```
.github/workflows/build.yml
├── on: push(main, v*) + workflow_dispatch     # 触发条件
├── jobs.build                                 # 单个 job
│   ├── strategy.matrix.target                 # [StandaloneWindows64, Android]
│   ├── game-ci/unity-builder@v4               # 装 Unity + 构建
│   │   ├── UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
│   │   ├── targetPlatform: ${{ matrix.target }}
│   │   └── buildMethod: StellarFramework.Build.BuildScript.PerformBuild
│   └── actions/upload-artifact@v4             # 上传 build/<target>
```
