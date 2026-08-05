# StellarFramework UPM 发布与更新指南

本文档说明：
1. **作为框架开发者**，如何发版并自动推送 UPM 包。
2. **作为使用者**，如何通过 Unity Package Manager 安装与更新 `com.stellar.framework`。

---

## 一、原理

```
你的开发仓库 (main)
   │  改代码 → 提交 → 打 tag v1.0.0 → git push --tags
   ▼
GitHub Actions (publish-upm.yml) 自动：
   ├─ 提取版本号 (v1.0.0 → 1.0.0)
   ├─ 镜像 Runtime + Editor → 标准 UPM 结构
   ├─ 写 package.json
   └─ 强推 upm 分支（只含包内容）
   ▼
开发者：
   manifest.json 加 "com.stellar.framework": "git URL#v1.0.0"
   → Package Manager 里看到包 → 装 / 一键更新
```

- **upm 分支**：只含 UPM 包内容（Runtime/Editor/package.json），由 CI 维护，不要手动改。
- **main 分支**：你的完整开发仓库（含样例、工具、文档）。
- 两者解耦：开发照旧在 main，发版自动同步到 upm。

---

## 二、作为框架开发者：发版流程（一条命令）

```bash
# 1. 确保 main 已提交、工作树干净
git status

# 2. 打版本 tag（UPM 版本 = tag 去掉 v 前缀）
git tag v1.0.0

# 3. 推送 tag（触发 publish-upm.yml 自动导出并推 upm 分支）
git push origin main --tags
```

**之后的一切全自动**：CI 导出 UPM 包 → 推 upm 分支 → 开发者可更新。

> 本地也可以手动导出预览（不推远端）：
> Unity 菜单 **StellarFramework → Packages → 导出 UPM 包 (com.stellar.framework)**
> 或 ToolsHub → 框架核心 → **导出 UPM 包**。产物在 `BuildArtifacts/UpmPackage/`。

### 发版清单
- [ ] 改代码并提交到 main
- [ ] （可选）改 `ProjectSettings/ProjectSettings.asset` 的 `bundleVersion`
- [ ] `git tag v<新版本>` + `git push --tags`
- [ ] GitHub Actions 的 Publish UPM Package 跑绿
- [ ] 在 GitHub Releases 写更新说明（建议，配合版本检测）

---

## 三、作为使用者：安装 UPM 包

### 方式 A：git URL（推荐，可一键更新）

编辑 `Packages/manifest.json`，在 `dependencies` 加入：

```json
{
  "dependencies": {
    "com.stellar.framework": "https://github.com/StarrDream/StellarFramework.git#upm"
  }
}
```

保存后 Unity 自动解析安装。之后有新版本（upm 分支更新），
`Window → Package Manager` → 选 com.stellar.framework → **Update** 即可。

> 建议锁定版本：把 `#upm` 改成 `#v1.0.0` 固定某版本；需要最新时改回 `#upm` 或升版本号。
> UPM 的 git 依赖要求**完整 40 位 commit hash / tag / 分支名**。

### 方式 B：固定 tag

```json
"com.stellar.framework": "https://github.com/StarrDream/StellarFramework.git#v1.0.0"
```

### 方式 C：本地包（调试用）

```json
"com.stellar.framework": "file:C:/path/to/com.stellar.framework"
```

---

## 四、作为使用者：更新到新版本

| 安装方式 | 更新做法 |
|---|---|
| git URL `#upm` | Package Manager → Update（自动拉 upm 分支最新） |
| git URL `#v1.0.0` | 手动改成新 tag（如 `#v1.1.0`） |
| 本地包 | 重新指向新目录 |

**依赖自动处理**：package.json 里声明了 Addressables / Newtonsoft / uGUI / TMP 等 registry 依赖，UPM 自动补齐。

> ⚠️ **git 依赖需手动添加**：UPM 的 `dependencies` 只接受 SemVer 版本号，不接受 git URL。
> 因此 **UniTask** 和 **HybridCLR**（git 依赖）不写在包依赖里，开发者需在自己的 `manifest.json` 手动添加：
> ```json
> "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
> "com.code-philosophy.hybridclr": "https://github.com/focus-creative-games/hybridclr_unity.git#4feac30cb2e105992986c737f7f54992b8300e1a"
> ```
> （这两个也是本仓库 `Packages/manifest.json` 中已声明的版本，保持与框架一致。）

---

## 五、常见问题

### Q1: 装了包但脚本用不了（编译报错找不到 StellarFramework）
→ 检查依赖是否装上：UPM 会自动装，但 HybridCLR 等 git 依赖首次可能较慢，等待解析完成或重启编辑器。

### Q2: 更新后我的业务代码要改吗？
→ 包内 API 变更才需要。框架遵循 MSV 契约，一般向后兼容；破坏性变更会在 Release 说明标注。

### Q3: upm 分支和我手动改的冲突吗？
→ upm 分支由 CI **强推**（`--force`），**不要手动往 upm 分支提交**。开发都在 main。

### Q4: 样例（Samples）怎么装？
→ UPM 包默认只含 Runtime + Editor。样例可选：
   - 从 main 分支拷贝 `Assets/StellarFramework/Samples`
   - 或直接 clone 整个仓库用源码方式（含样例/工具/文档）

### Q5: 版本检测？
→ ToolsHub 的版本检测与更新模块会对比本地版本与 GitHub Releases，提示更新（配套使用）。

---

## 六、目录速查

| 项 | 位置 |
|---|---|
| 导出工具（编辑器） | `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkUPMExporter.cs` |
| 导出菜单 | `StellarFramework/Packages/导出 UPM 包 (com.stellar.framework)` |
| CI 发布 workflow | `.github/workflows/publish-upm.yml` |
| 本地导出产物 | `BuildArtifacts/UpmPackage/com.stellar.framework/` |
| 包版本号 | `StellarFrameworkUPMExporter.cs` 的 `PackageVersion` 常量 |
| 工程版本号 | `ProjectSettings/ProjectSettings.asset` 的 `bundleVersion` |
