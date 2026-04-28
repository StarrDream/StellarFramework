# KitSamples Scenes / 场景索引

`Assets/StellarFramework/Samples/KitSamples/Scenes` 存放各个模块对应的可运行场景。

## 场景列表

| 场景 | 说明 | 备注 |
| :--- | :--- | :--- |
| `ActionKit_Playable.unity` | 动作链、延迟、并行和取消 | 可直接运行 |
| `AudioKit_Playable.unity` | BGM、2D/3D 音效、音量控制 | 可直接运行 |
| `BindableKit_Playable.unity` | 属性、列表、字典绑定 | 可直接运行 |
| `ConfigKit_Playable.unity` | 配置加载、覆盖与保存 | 可直接运行 |
| `SettingsKit_Playable.unity` | 默认设置页、自定义扩展页、即时应用与保存 | 可直接运行 |
| `EventKit_Playable.unity` | 枚举事件和结构体事件 | 可直接运行 |
| `FSMKit_Playable.unity` | 轻量状态机与动画联动 | 可直接运行 |
| `HotUpdateKit_Playable.unity` | 热更入口接线 | 场景可运行，完整热更仍需额外 DLL |
| `HttpKit_Playable.unity` | 登录、请求、图片加载 | 可直接运行，联网时信息更完整 |
| `LogKit_Playable.unity` | 日志与性能输出 | 可直接运行 |
| `PoolKit_Playable.unity` | 对象池与工厂对象池 | 可直接运行 |
| `ResKit_Playable.unity` | Resources、AB、AA、RawText | `Resources / AB / RawText` 可直接验证，`AA` 需 Addressables |
| `SingletonKit_Playable.unity` | 全局单例与场景单例 | 可直接运行 |
| `UIKit_Playable.unity` | UIRoot 与面板打开流程 | 可直接运行 |

## 建议顺序

1. `ActionKit_Playable.unity`
2. `BindableKit_Playable.unity`
3. `EventKit_Playable.unity`
4. `LogKit_Playable.unity`
5. `SingletonKit_Playable.unity`
6. `AudioKit_Playable.unity`
7. `ConfigKit_Playable.unity`
8. `SettingsKit_Playable.unity`
9. `FSMKit_Playable.unity`
10. `PoolKit_Playable.unity`
11. `UIKit_Playable.unity`
12. `ResKit_Playable.unity`
13. `HttpKit_Playable.unity`
14. `HotUpdateKit_Playable.unity`

## 已补齐的公共资源

- `Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab`
- `Assets/StellarFramework/Resources/Audio/BGM/MainTheme.wav`
- `Assets/StellarFramework/Resources/Audio/BGM/BattleTheme.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/UI_Click.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/Explosion.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/Footstep.wav`
- `Assets/StellarFramework/Samples/KitSamples/Generated/Animations/Example_FSM.controller`
- `Assets/StellarFramework/Samples/KitSamples/Generated/Prefabs/ExampleBullet.prefab`
- `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab`
- `Assets/StreamingAssets/Configs/Normal/TestGameConfig.json`
- `Assets/StreamingAssets/Configs/Net/TestApiConfig.json`
- `Assets/StreamingAssets/StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt`

## 说明

- `SettingsKit_Playable.unity` 会自动安装默认设置页，并附带一个 `Example Extensions` 扩展页。
- `ResKit_Playable.unity` 的 `Addressables` 部分依赖本地安装和构建结果。
- `HotUpdateKit_Playable.unity` 只验证入口，不包含完整热更产物。
- `HttpKit_Playable.unity` 离线也能看到本地逻辑，联网时信息更完整。
