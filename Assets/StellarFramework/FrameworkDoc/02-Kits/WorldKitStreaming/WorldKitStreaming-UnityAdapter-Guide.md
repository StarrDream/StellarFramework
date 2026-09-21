# WorldKit.Streaming.UnityAdapter

Unity Adapter 只负责把高精度逻辑世界坐标映射到当前 floating origin 附近的小范围 `Vector3`。

## 边界

- 真值位置仍是 `WorldPoint2D(double,double)`。
- Adapter 不修改 Transform，不持有场景对象。
- `TryComputeRecenter` 返回新的 logical origin 与应应用到 presentation roots 的 `sceneDelta`。
- Core/Streaming assembly 不引用 UnityEngine。

## Recenter

`WorldFloatingOriginSettings` 明确指定 threshold 和 snap size。超过 threshold 后，origin 按 snap size 对齐；同一逻辑对象“旧 Unity 坐标 + sceneDelta”必须等于“新 origin 重新映射后的 Unity 坐标”。

验证覆盖到约 `1e12` 的逻辑坐标，并连续多次 recenter，Unity local 坐标仍保持在小范围内。
