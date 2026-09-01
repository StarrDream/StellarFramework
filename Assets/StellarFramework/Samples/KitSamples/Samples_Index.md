# KitSamples 验收索引

这个索引用来替代“继续增加大量测试脚本”的做法。每个 Kit 只保留一个核心 Sample，更多边界行为写在验收清单里。

## 分层定位

- `Example_*`：教学入口，展示这个 Kit 的推荐用法；具体操作写在主 `Example_*.cs` 文件头注释中
- `Scenes/*_Playable.unity`：手动验收入口，适合 Editor 和真机冒烟
- EditMode/PlayMode Test：只保护核心逻辑和容易回归的边界，不追求覆盖每个教学步骤

## 快速路径

| 目标 | 推荐入口 | 前置条件 |
| :--- | :--- | :--- |
| 第一次了解框架 | `Start Here -> Quick Start` + `Scenes/UIKit_Playable.unity` + `Scenes/ResKit_Playable.unity` | 先运行样例构建器，优先跑 UI 与资源两条主链路 |
| 基础 Kit 学习 | `ActionKit / BindableKit / EventKit / LogKit / SingletonKit` | 无 |
| 世界时间与 Timer | `TimeKit_Playable.unity` | 无；不依赖 SaveKit 或 Unity `Time.timeScale` |
| 存档与迁移 | `SaveKit_Playable.unity` | 仅 SaveKit.Core + UniTask；不依赖 TimeKit/ResKit/热更 |
| 整数网格基础 | `GridKit_Playable.unity` | 仅 GridKit.Core；无 UPM、Addressables、HybridCLR |
| 连续空间基础 | `SpatialKit_Playable.unity` | 仅 SpatialKit.Core；无 GridKit、UPM、Addressables、HybridCLR |
| 资源与 UI 验收 | `ResKit_Playable.unity`、`UIKit_Playable.unity` | AB/AA 按需构建 |
| 设置系统验收 | `SettingsKit_Playable.unity` | 样例构建器生成资源 |
| 热更链路验收 | `HotUpdateKit_Playable.unity` | 可选扩展路径；完整热更需 HybridCLR 与 AA 产物 |
| 网络链路验收 | `HttpKit_Playable.unity` | 联网时结果更完整 |

## 运行顺序建议

1. 打开 `StellarFramework -> Tools Hub -> Start Here -> Quick Start`
2. 点击“构建样例”
3. 先跑 `TimeKit_Playable.unity` 和 `SaveKit_Playable.unity`，确认两个不带资源/热更前置的基础 Kit 闭环
4. 跑 `GridKit_Playable.unity`，确认负坐标、Footprint 与 Occupancy 原子性
5. 跑 `SpatialKit_Playable.unity`，确认连续负/小数坐标、Rect/Circle 查询和最近邻
6. 再跑 `UIKit_Playable.unity` 和 `ResKit_Playable.unity`，确认 UI 与资源主链路无 error
7. 再按 `Scenes/README.md` 的顺序跑单个 Kit 场景
8. 涉及 AB 的场景先用 ToolHub 构建 AB
9. 涉及 AA 的场景使用 Addressables 官方 `Groups / Profiles / Build` 或 Play Mode Script
10. 涉及 HybridCLR 的场景只做入口检查，真实 dll.bytes 走 HybridCLR 官方流程；建议在基础框架稳定后再接入

## 不继续堆文档/脚本的规则

- 只有当某个 Kit 没有可运行闭环时，才新增 Sample 脚本
- 能用总索引、场景提示或脚本头注释说清楚的验收步骤，不新增 MonoBehaviour
- TimeKit 与 SaveKit 是正式的教学/验收样例，保留各自目录下的 README；其他样例以总索引、场景提示和脚本头注释为准
- SpatialKit 另有 README，说明连续点索引、查询边界、依赖和导出闭包
- 面向框架开发者的集中冒烟已迁入外置验证区，不再混入用户样例主路径
- 自动化测试只补纯逻辑和高风险边界，例如事件注销、状态切换、对象池回收、资源引用计数
