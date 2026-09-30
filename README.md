# StellarFramework

给 Unity 项目使用的模块化开发框架。这里是 **General 通用能力的用户发布仓**，适合直接打开体验，也可作为挑选和导出 Kit 的工作台。

发布版本：1.0.0
源码：StellarFramework.Dev@3999c2d4366da655096766fb294f52898076a70b

## 先从这里开始

1. 通过 Git 克隆本仓库，或在 GitHub 选择 **Code → Download ZIP** 并解压。安装 Unity Hub，再用 Unity **2022.3.62f3c1** 打开解压/克隆后的仓库根目录。这里是完整 Unity 工程，不是需要手动复制的源码压缩包。
2. 等待 Unity 导入资源并完成 Package Manager 依赖解析。若提示导入 TMP Essentials，按 Unity 提示导入一次即可。
3. 在菜单 **StellarFramework → Tools Hub** 打开工具中心。
4. 进入 **Start Here → Quick Start**，然后打开入门场景：

   `Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`

5. 点击 Unity Play。Demo 可以运行一轮循环任务、关闭并重新打开界面，也可以在运行时切换中文和 English。

第一次只想“看框架怎么用”，做到这一步就够了。无需先读完所有架构文档。

## 把框架用到自己的项目

建议先在本仓库中选择 Kit，再把导出的包导入目标工程：

1. 在 Tools Hub 中打开 **StellarFramework → Export**。
2. 选一个具体 Kit Profile；想从最小组合开始，就只选一个 Kit。也可以选择一个 Recommended Profile 获得一组相关功能。
3. 导出器会根据 Kit 依赖自动加入必需的 StellarFramework Profile，避免手动猜依赖。
4. 在自己的 Unity 工程中使用 **Assets → Import Package → Custom Package…** 导入生成的 `.unitypackage`，保留包内 `.meta` 文件。
5. 按导出器摘要和该 Kit 使用文档安装所需 UPM 包。不要把未使用的可选后端一并装入项目。

**导出包包含所选功能及必需的框架依赖；Unity Package Manager 依赖仍由你的项目管理。** 每个 Kit 的能力、依赖和示例见 [Kit 使用文档](Assets/StellarFramework/FrameworkDoc/02-Kits)。

### 新手常见选择

| 你要做什么 | 从哪里开始 |
| --- | --- |
| 状态驱动界面、服务分层 | ArchitectureDemo，然后看 Architecture、BindableKit、UIKit |
| 加载 Prefab、图片等资源 | ResKit.Core；需要时再选 Addressables、AssetBundle 或 YooAsset Adapter |
| 运行时切换中英文 | LocalizationKit.Core；按需增加 UGUI / TMP Adapter 和编辑工具 |
| 适配刘海屏、安全区和不同屏幕比例 | UIAdaptationKit.Core；它不要求安装 UIKit |
| 运行时代码热更新 | 先阅读 HotUpdate 与 ResKit 文档；通常需要 YooAsset、HybridCLR、UniTask 和目标平台构建环境 |
| 快速获得一组相关能力 | 查看 Tools Hub 中的 Recommended Profiles，例如 ResKit Complete、Localization Complete、UIAdaptationKit Complete |

Recommended Profile 会按声明补齐框架依赖。确切的 Adapter 和 UPM 版本以导出界面与 Kit 文档为准。

## 这里有哪些 Kit

- **基础与流程**：Architecture、BindableKit、EventKit、PoolKit、SingletonKit、TimeKit、FSMKit、ActionKit。
- **数据与服务**：ConfigKit、SettingsKit、SaveKit、LogKit、HttpKit。
- **表现与内容**：UIKit、UIAdaptationKit、LocalizationKit、AudioKit、ResKit。
- **资源后端**：Resources 默认能力，以及可选的 AssetBundle、Addressables、YooAsset Adapter。
- **编辑器工具**：统一 Tools Hub；具体 Kit 的 Editor 工具可单独选择，不会为了编辑器功能把所有 Runtime Kit 装入目标项目。

**Algorithms、World、Flow 和 HybridCLR HotUpdate 属于扩展能力**，发布在 [StellarFramework.Extensions](https://github.com/StarrDream/StellarFramework.Extensions)。要挑选并导出扩展 Kit，请按该仓 README 的组合步骤操作。

## 文档和示例怎么找

- 第一次上手：Tools Hub 的 Quick Start 和 ArchitectureDemo。
- 单个 Kit：`Assets/StellarFramework/FrameworkDoc/02-Kits` 下对应目录中的“说明文档”。
- 编辑器工具：`Assets/StellarFramework/FrameworkDoc/04-ToolsHub`。
- Demo 说明：`Assets/StellarFramework/Samples/README.md`。
- 需要修改框架源码：转到 [StellarFramework.Dev](https://github.com/StarrDream/StellarFramework.Dev)，不要直接在发布仓维护功能分叉。

## 常见问题

**打开后仍在编译或下载包**
先等 Unity Console 和 Package Manager 完成工作。首次导入会比之后重新打开慢。

**编译报找不到某个外部命名空间**
确认 Package Manager 已解析本仓依赖。若这是导出的单 Kit 工程，按该 Kit 文档和导出摘要补齐 UPM 包。

**导入单个 Kit 后想增加另一个功能**
回到本仓导出新增 Kit，或选包含所需 Profile 的 Recommended Profile；导入时保留 `.meta`，避免资源 GUID 改变。

**TMP 文字没有自动本地化**
TMP 能力是可选 Adapter / Tools。确认已导入 TMP Essentials，并在导出时选了 TMP 对应 Profile。

**UIAdaptationKit 与 UIKit 是什么关系**
UIAdaptationKit 可单独运行；UIKit 需要屏幕适配时再组合适配 Adapter。

## 发布信息

本仓由 Dev 工程生成。`RELEASE-MANIFEST.json` 记录版本、源码提交、包含的 Profile、UPM 需求和验证状态。发现问题或要增加功能，请在 Dev 工程修改、验证后再发布。
