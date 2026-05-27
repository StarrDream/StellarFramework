# KitSamples / 模块样例

`Assets/StellarFramework/Samples/KitSamples` 存放各 Kit 的最小可运行样例。推荐先运行 `Scenes/*.unity`，再回头看对应的 `Example_*/*.cs`。

## 目录

- `Scenes/`：每个 Kit 的 `*Playable.unity` 场景。
- `Example_*/`：示例脚本目录。
- `Common/`：多个样例共用的辅助脚本。
- `Generated/`：样例构建器生成的资源。
- `Editor/`：样例场景构建器与触发器。

## 推荐入口

- `ResKit_Playable.unity`：Resources、AB、AA、RawText 四条加载链路。
- `UIKit_Playable.unity`：UIRoot、Open/Push/Pop/Close、运行时快照和压力测试。
- `HotUpdateKit_Playable.unity`：热更新门户与 HybridCLR AA 启动链路示例。
- `SettingsKit_Playable.unity`：设置定义、存储、应用策略和示例 UI。

## 重新生成样例

在 `StellarFramework -> Tools Hub -> 样例支持 -> 样例构建` 中执行生成。构建器会补齐：

- `Resources/UIPanel/UIRoot.prefab`
- `Resources/UIPanel/ExamplePanel.prefab`
- ResKit 示例资源和 AB 示例产物
- 各 Kit 的可播放场景

## 验收建议

- 先跑 Editor，再跑目标平台真机。
- ResKit AA 的构建/模拟使用 Addressables 官方窗口。
- UIKit 在 `UIKit_Playable.unity` 中按 `S` 执行 100 次 Open/Close 压力测试，结束后确认 Snapshot 的 `Loading=0`。
- 修改示例资源后重新生成样例，避免手动资源和文档步骤不一致。
