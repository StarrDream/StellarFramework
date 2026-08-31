# SaveKit Sample

场景：`Assets/StellarFramework/Samples/KitSamples/Scenes/SaveKit_Playable.unity`

这个样例把 SaveKit 的使用边界压缩成一个可运行闭环：业务状态留在 `SaveKitExample`，存档内容只通过 `[Serializable]` DTO 进入 Section。SaveKit Core 负责容器、校验、Revision、临时文件、Backup 和文件存储。

## 运行

运行场景后，面板会显示两个 Section：

- `sample.world`：`WorldTick`、`WeatherSeed`，V1。
- `sample.player`：`Money`、`Level`，当前 V2；声明 `RestoreAfter(sample.world)`，所以加载时世界先恢复。

先用 `Level +1`、`Money +100` 或 `WorldTick +10000` 改变内存 DTO，再点击 `Save`。点击 `Load` 会把数据恢复回来，结果区域显示 `Status`、`Revision`、`ErrorCode`、Backup 和耗时诊断。`Delete` 删除当前 Slot。

样例会在 Unity 的 `Application.persistentDataPath` 创建测试存档；停止 Play 不会自动删除，方便重新运行后继续点击 `Load`。需要清理时使用面板中的 `Delete` 或 `Delete Legacy`。

## V1 → V2 迁移

`Create Legacy V1 Save` 会临时注册 V1 玩家 DTO（`Coins`），在 `sf-sample-savekit-migration` 写入一个真实旧版本文件，保存完成后自动切回 V2 配置。

`Load Legacy As V2` 使用注册的 `SaveKitSamplePlayerV1ToV2Migration`：

```text
V1 Coins (int) -> V2 Money (long)
V2 Level = 1
```

加载结果中的 `Migrations` 和 `Restore order` 可以用来确认迁移与恢复顺序。完成后可点击 `Delete Legacy` 清理文件。

## 导出边界

样例 asmdef 只引用 `StellarFramework.SaveKit.Core` 和 `UniTask`。导出 `samples.savekit` 时同时带上 `Example_SaveKit`、`Common/ExampleSceneGuide` 和 `SaveKit_Playable.unity`，不带 TimeKit、ResKit、Addressables、HybridCLR、ToolsHub 或 Newtonsoft.Json。

样例不读取私有存档路径、不解析磁盘文件、不注入损坏数据；这些属于 SaveKit Core 的自动化验证职责。场景销毁时会取消未完成的 UniTask，重复运行不会累积 Section 注册。

核心 API 说明：

- `Assets/StellarFramework/Runtime/Kits/SaveKit/SaveKit-存档系统-说明文档-Guide.md`
- `Assets/StellarFramework/Runtime/Kits/SaveKit/SaveKit-存档系统-源码文档-Guide.md`
