# KitSamples / 模块样例

`Assets/StellarFramework/Samples/KitSamples` 存放各个 Kit 的最小可运行样例。推荐先运行 `Scenes/*.unity`，再回头阅读对应 `Example_*/*.cs` 文件头注释。

## 目录

- `Scenes/`：每个 Kit 的 `*Playable.unity` 场景
- `Example_*/`：示例脚本目录
- `Common/`：多个样例共用的辅助脚本
- `Generated/`：样例构建器生成的资源
- `Editor/`：样例场景构建器与触发器
- `Samples_Index.md`：按验收目标组织的总索引

## 推荐入口

- `../../快速开始.md`：新人先看这个，按主路径跑通框架
- `Samples_Index.md`：先看这个，了解哪些场景可直接跑，哪些依赖 AB/AA/网络/HybridCLR
- `ResKit_Playable.unity`：Resources、AB、AA、RawText 四条加载链路
- `UIKit_Playable.unity`：UIRoot、Open/Push/Pop/Close、运行时快照和压力测试
- `TimeKit_Playable.unity`：World Clock、一次性/周期 Timer、Catch-Up 与 Unity timeScale 解耦
- `SaveKit_Playable.unity`：DTO Section、Save/Load/Delete、RestoreAfter 与 V1→V2 迁移
- `GridKit_Playable.unity`：负坐标、DenseGrid、邻居、Footprint 变换与原子 Occupancy
- `SpatialKit_Playable.unity`：连续二维点、负坐标/小数坐标、动态移动、矩形/圆形查询与最近邻
- `SimulationKit_Playable.unity`：手动 tick、Burst 同刻到期、Staggered 首次派发、预算分批与过期合并
- `HotUpdateKit_Playable.unity`：可选热更新门户与 HybridCLR AA 启动链路示例
- `SettingsKit_Playable.unity`：设置定义、存储、应用策略和示例 UI

## 单个 Kit 说明

TimeKit 与 SaveKit 是需要独立操作说明的正式样例，目录下保留 README；其他样例不重复维护 README。

- 总览、顺序和前置条件看 `Samples_Index.md` 与 `Scenes/README.md`
- TimeKit 的 API 对照和验收步骤看 `Example_TimeKit/README.md`
- SaveKit 的 DTO、迁移和导出边界看 `Example_SaveKit/README.md`
- GridKit 的负坐标、Footprint 与 Occupancy 操作看 `Example_GridKit/README.md`
- SpatialKit 的点索引、查询边界和最近邻操作看 `Example_SpatialKit/README.md`
- SimulationKit 的批量预算、首次延迟和派发边界看 `Example_SimulationKit/README.md`
- 单个样例的按键、操作方式和通过标准写在对应 `Example_*.cs` 文件头注释里
- 场景内还会挂 `ExampleSceneGuide`，运行时可直接在 Game 视图看到核心提示

## 重新生成样例

在 `StellarFramework -> Tools Hub -> Start Here -> Quick Start` 中执行“构建样例”，或在 `样例支持 -> 样例构建` 中单独执行。构建器会补齐：

- `Resources/UIPanel/UIRoot.prefab`
- `Resources/UIPanel/ExamplePanel.prefab`
- ResKit 示例资源与 AB 示例源 Prefab
- 各 Kit 的可播放场景
- 维护者验证区会单独维护 `FrameworkValidation` 场景与发布前检查工具；它不属于用户 Sample

## 验收建议

- 先跑 Editor，再跑目标平台真机
- ResKit 的 AB 物理构建请单独使用本框架 Tools Hub 的 `资源打包 (AssetBundle)`，样例构建器不会自动生成 `StreamingAssets/AssetBundles` 产物
- Addressables 的 Groups、Analyze、Play Mode Script 和 Content Update 使用官方窗口；框架的本地内置 AA、远端热更 AA、Manifest 与发布目录闭环使用 ToolsHub 的 `AA 配置与发布`
- UIKit 在 `UIKit_Playable.unity` 中执行 100 次 Open/Close 压力测试，结束后确认 Snapshot 的 `Loading=0`
- HotUpdateKit 的真实代码热更需要 HybridCLR 产物和 AA 远端资源，本地样例只验证入口和失败诊断。新人建议先跑通基础框架，再进入热更扩展路径。
- 修改示例资源后重新生成样例，避免手动资源和文档步骤不一致


