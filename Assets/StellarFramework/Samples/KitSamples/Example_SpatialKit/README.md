# SpatialKit Sample

场景：`Assets/StellarFramework/Samples/KitSamples/Scenes/SpatialKit_Playable.unity`

该样例只使用 `StellarFramework.SpatialKit.Core`。运行后会注册 64 个连续二维点，数据包含正坐标、负坐标和小数坐标；ID 1/2/3 用于演示圆形边界，ID 4 位于半径 5 圆的外接方框角落。按钮可以执行 Reset、Insert、Move Selected、Remove Selected、Query Rect、Query Circle、Nearest，以及排除选中点的最近邻查询。

面板只显示公开状态：BucketSize、Count、Selected ID/Position、最后一次查询的 WrittenCount/MatchCount/Truncated 和最近邻 ID。QueryMatched 点直接来自 SpatialKit 查询缓冲区，黄色圆线使用同一 Center/Radius；写入结果被截断时只高亮实际写入的 ID。SpatialId 由样例自行分配，索引 Core 不生成 ID，也不持有 GameObject、Transform 或 Unity 生命周期。

场景由 `Editor/SampleTemplates/KitSamples/SpatialKit_Playable.unity.txt` 生成。SpatialKit Core 不需要 GridKit、ResKit、Addressables、HybridCLR、UniTask 或其他 UPM 包。
