# Tank Arena 案例说明

Tank Arena 是一场短局制生存战：玩家移动坦克、瞄准并击退来袭敌人；击毁数推动波次和分数，受到攻击会损失装甲，装甲归零后结算战绩。战场里会出现维修核心，拾取后恢复装甲。暂停面板可继续游戏、重新出击或查看正在运行的框架模块。

这个案例把可运行玩法、框架服务和可修改资源放在同一个 Sample 中。它适合用来检查一个 Kit 如何进入真实游戏流程，也可以作为拆分到自己项目中的起点。

## 打开案例

1. 在 Unity Project 窗口打开 `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`。
2. 等待资源导入和脚本编译完成后点击 Play。
3. 手机上使用左侧摇杆移动、右侧摇杆瞄准。自动模式会锁定并攻击最近的敌人；切换为手动模式后，按住右侧摇杆开火。
4. 桌面使用 WASD 移动，鼠标拖动右侧瞄准区域；空格开火。
5. 顶部可切换中英文、自动/手动模式或打开系统面板。右上角显示当前热更包版本。

编辑器预览从本地编译的 `HotUpdate` 程序集启动；它不下载远端 DLL，也不证明发生了代码热更。正式启动链先由 YooAsset 准备内容，再由 HybridCLRKit 读取热更 DLL 与 AOT metadata 并调用入口。框架自身的 Android / Windows 门禁保留在 Dev 维护工具中。

## 一局游戏中发生什么

1. `HotUpdateMain.Main()` 进入 `TankArenaGame.Launch()`，从 `Resources` 实例化 `GameHost.prefab`。
2. 游戏根节点初始化 SettingsKit、SaveKit 和 Tank Arena 架构，从 ConfigKit 读取默认参数或本机覆盖文件。
3. `BuildWorld()` 实例化 `Arena.prefab`；`BuildInterface()` 实例化 `HUD.prefab`，连接按钮、摇杆和 UIAdaptationKit。
4. Model 保存分数、波次、装甲、击毁数和战斗阶段。Service 执行伤害、修理、计分、波次推进和状态切换。
5. 敌人、维修核心、炮弹和命中特效分别从 Sample Prefab 实例化。对象外观由资产决定，代码只更新位置、朝向、颜色参数和动画进度。
6. 战斗事件经 EventKit 发布；Bindables 更新 HUD；PoolKit 管理弹体和特效对应的数据记录；结束战斗后 SaveKit 保存战绩。

打开暂停面板中的 **SYSTEMS**，可以看到战斗状态、配置来源、最近事件、复用计数、本地化和包版本。这些值来自当前战局，不是静态展示标签。

## Kit 在案例中的用途

| Kit | 案例中的用法 | 可从哪里继续读 |
| --- | --- | --- |
| Architecture / BindableKit | `TankArenaModel` 保存比分与战斗状态；HUD 订阅属性并刷新显示。 | `Runtime/TankArenaArchitecture.cs`、`Runtime/TankArenaGame.cs` |
| FSMKit | `TankArenaService` 在 Active、Paused、Game Over 间切换。 | `Runtime/TankArenaArchitecture.cs` |
| ActionKit | 控制短时 UI 动画序列。 | `Runtime/TankArenaGame.cs` 中按钮与面板处理方法 |
| EventKit | 广播击毁、波次、受伤和维修事件；Systems 面板显示最近事件。 | `Runtime/TankArenaArchitecture.cs`、`Runtime/TankArenaGame.cs` |
| ConfigKit | 读取敌人刷新、速度、弹速和波次参数。 | `Runtime/TankArenaGame.cs`、`../../../StreamingAssets/TankArena/demo-config.json` |
| SaveKit | 保存最高分、累计击毁数和出击次数。 | `Runtime/TankArenaGame.cs` 中 `ProfileSection` 和存档方法 |
| SettingsKit | 保存屏幕震动偏好。 | `Runtime/TankArenaGame.cs` 中 `InitializeSettings` 和屏幕反馈按钮 |
| LocalizationKit | 在中文与英文间切换游戏文本；结果文本使用具名格式参数。 | `Runtime/TankArenaLocalizationService.cs` |
| UIAdaptationKit | 读取安全区、分辨率与方向断点，调整 HUD 根节点和缩放。 | `Resources/TankArena/Generated/Profiles/UIAdaptationProfile.asset` |
| PoolKit | 管理弹体和特效关联的数据对象生命周期。 | `Runtime/TankArenaGame.cs` 中 `Projectile` 与 `ArenaEffect` |
| LogKit | 记录配置、存档、战斗结果和适配状态。 | `Runtime/TankArenaGame.cs` |
| ResKit.YooAsset / HybridCLRKit | 资源包准备完成后，HybridCLRKit 通过 ResKit 读取并校验程序集、AOT metadata，再调用热更入口。 | `FrameworkDoc/02-Kits/HybridCLRKit` |

Systems 面板只列出这个案例实际调用的 Kit。项目内其他可用 Kit 并未因此自动被视为本案例覆盖。

## 修改游戏资源

游戏资源都随 Sample 保存，运行时代码不会通过 `CreatePrimitive`、`new Material`、`Sprite.Create` 或逐个创建 UI 节点的方式搭建画面。

| 资源 | 位置 | 修改方式 |
| --- | --- | --- |
| 游戏主机 | `Resources/TankArena/Prefabs/GameHost.prefab` | 玩法入口组件所在 Prefab。 |
| 战场与玩家坦克 | `Resources/TankArena/Prefabs/Arena.prefab` | 在 Prefab Mode 调整场地、镜头、灯光、掩体和玩家模型。 |
| HUD 与按钮布局 | `Resources/TankArena/Prefabs/HUD.prefab` | 调整锚点、字号、颜色、按钮和面板层级；节点名称需与 `BuildInterface()` 查找名称一致。 |
| 敌方坦克 | `Resources/TankArena/Prefabs/EnemyLight.prefab`、`EnemyHeavy.prefab` | 调整敌人外观；逻辑根据敌人强度选择对应 Prefab。 |
| 维修核心、炮弹、冲击环 | `Resources/TankArena/Prefabs/RepairCore.prefab`、`Projectile.prefab`、`ShockRing.prefab` | 修改动态对象的模型、材质、组件和基础效果。 |
| UI 图形与材质 | `Resources/TankArena/Generated/Sprites/`、`Generated/Materials/` | 编辑现有资源并保存在 Sample 内。 |
| 适配规则 | `Resources/TankArena/Generated/Profiles/UIAdaptationProfile.asset` | 调整参考分辨率、安全区策略和方向断点。 |
| 中文字体 | `Resources/Fonts/` | 查看字体来源和许可证文件后再替换或扩展。 |

`TankArenaGame.BuildInterface()` 会按层级名称绑定已有 UI 节点。移动、删除或重命名按钮、HUD 文本和摇杆节点时，同时更新这段绑定代码；不要在运行时补建缺失的画面节点。

## 配置、存档与发布

- 默认配置：`Assets/StreamingAssets/TankArena/demo-config.json`
- 本机覆盖：`Application.persistentDataPath/TankArena/demo-config.json`
- 存档槽：`tank-arena-profile`
- 存档内容：最高分、累计击毁数、出击次数
- 热更运行时与发布：General 主仓包含 HybridCLRKit、HybridCLRKit.Tools 和 HotUpdate Publisher；按 HybridCLRKit Guide 配置项目。

Dev 中的 Android / Windows Release Gate 用于框架维护验证，不随 General 发布。实际发布前仍应以目标 Unity 版本、设备和服务端配置完成项目自己的平台验证。
