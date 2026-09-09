# FlowKit 示例

`FlowKitSample` 读取 `FlowKitSample.json`，编译并运行 `Entry → Delay(0.25s) → Complete`。示例不依赖 UniTask、Addressables 或 HybridCLR。

将脚本挂到场景对象，将 JSON 拖到 `graphJson` 即可观察 Run 从 Created 到 Completed 的生命周期。更完整的 Graph 检查可使用 `StellarFramework/FlowKit/Graph Validator`。

场景模板位于 `Editor/SampleTemplates/KitSamples/FlowKit_Playable.unity.txt`，运行样例构建器后会重新物化到 `Scenes/FlowKit_Playable.unity`。
