# KitSamples Scenes / 场景索引

`Assets/StellarFramework/Samples/KitSamples/Scenes` 存放各个模块对应的可运行场景。

更完整的学习和验收顺序见上级目录的 `Samples_Index.md`。单个样例的操作说明写在对应 `Example_*.cs` 文件头注释里。

## 场景列表

| 场景 | 说明 | 备注 |
| :--- | :--- | :--- |
| `ActionKit_Playable.unity` | 动作链、延迟、并行和取消 | 可直接运行 |
| `AudioKit_Playable.unity` | BGM、2D/3D 音效、音量控制 | 可直接运行 |
| `BindableKit_Playable.unity` | 属性、列表、字典绑定 | 可直接运行 |
| `ConfigKit_Playable.unity` | 配置加载、覆盖与保存 | 可直接运行 |
| `SettingsKit_Playable.unity` | 默认设置页、自定义扩展页、即时应用与保存 | 可直接运行 |
| `TimeKit_Playable.unity` | World Clock、Timer、Catch-Up、TimeKit Pause 与 Unity timeScale 对比 | 可直接运行 |
| `SaveKit_Playable.unity` | DTO Section、异步存档、RestoreAfter、V1→V2 迁移 | 可直接运行 |
| `EventKit_Playable.unity` | 枚举事件和结构体事件 | 可直接运行 |
| `FSMKit_Playable.unity` | 轻量状态机与动画联动 | 可直接运行 |
| `HotUpdateKit_Playable.unity` | 热更入口接线 | 场景可运行，完整热更仍需额外 DLL |
| `HttpKit_Playable.unity` | 登录、请求、图片加载 | 可直接运行，联网时信息更完整 |
| `LogKit_Playable.unity` | 日志与性能输出 | 可直接运行 |
| `PoolKit_Playable.unity` | 对象池与工厂对象池 | 可直接运行 |
| `ResKit_Playable.unity` | Resources、AB、AA、RawText | `Resources / AB / RawText` 可直接验证，`AA` 依赖 Addressables |
| `SingletonKit_Playable.unity` | 全局单例与场景单例 | 可直接运行 |
| `UIKit_Playable.unity` | UIRoot、面板打开、堆栈与压力测试 | 可直接运行 |

## 建议顺序

1. `UIKit_Playable.unity`
2. `ResKit_Playable.unity`
3. `HotUpdateKit_Playable.unity`
4. `TimeKit_Playable.unity`
5. `SaveKit_Playable.unity`
6. `ActionKit_Playable.unity`
7. `BindableKit_Playable.unity`
8. `EventKit_Playable.unity`
9. `LogKit_Playable.unity`
10. `SingletonKit_Playable.unity`
11. `AudioKit_Playable.unity`
12. `ConfigKit_Playable.unity`
13. `SettingsKit_Playable.unity`
14. `FSMKit_Playable.unity`
15. `PoolKit_Playable.unity`
16. `HttpKit_Playable.unity`

## 已补齐的公共资源

- `Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab`
- `Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`
- `Assets/StellarFramework/Resources/Audio/BGM/MainTheme.wav`
- `Assets/StellarFramework/Resources/Audio/BGM/BattleTheme.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/UI_Click.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/Explosion.wav`
- `Assets/StellarFramework/Resources/Audio/SFX/Footstep.wav`
- `Assets/StellarFramework/Samples/KitSamples/Generated/Animations/Example_FSM.controller`
- `Assets/StellarFramework/Samples/KitSamples/Generated/Prefabs/ExampleBullet.prefab`
- `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab`
- `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab`
- `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab`
- `Assets/StreamingAssets/Configs/Normal/TestGameConfig.json`
- `Assets/StreamingAssets/Configs/Net/TestApiConfig.json`
- `Assets/StreamingAssets/StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt`

## 说明

- `SettingsKit_Playable.unity` 会自动安装默认设置页，并附带一个 `Example Extensions` 扩展页
- `ResKit_Playable.unity` 的 Addressables 部分依赖本地安装和构建结果；AA 模拟与构建请使用 Addressables 官方界面
- `HotUpdateKit_Playable.unity` 只验证入口，不包含完整热更产物
- `TimeKit_Playable.unity` 不需要 SaveKit；`SaveKit_Playable.unity` 不需要 TimeKit，两个样例可以分别导出
- `SaveKit_Playable.unity` 的 Legacy 按钮会生成一个真实 V1 文件，再由当前 V2 Section 和 Migration 加载
- `FrameworkValidation` 已迁入框架外验证区，适合框架开发者做发布前回归，不再作为用户样例的一部分
- `HttpKit_Playable.unity` 离线也能看到本地逻辑，联网时信息更完整
