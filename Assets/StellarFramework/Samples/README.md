# Samples / Demo

## 中文

StellarFramework 的可运行框架示例统一为 **Tank Arena**。它不是逐个 Kit 的 API 手册，而是一局可以从开始打到结束、并能查看 Kits 如何协作的完整小型游戏。玩法说明与资源修改入口见 [Tank Arena 案例说明](TankArena/CaseStudy.md)。

### 运行

1. 在 Unity Project 窗口打开 `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`。
2. 点击 Play。首次导入或编译完成后，编辑器会从本地 `HotUpdate` 程序集启动游戏。
3. 左侧摇杆移动坦克，右侧摇杆独立瞄准并开火；右上角可切换语言、自动/手动开火和查看热更包版本。
4. 暂停或点击 **SYSTEMS / 系统**，查看本局的 Kits 状态与事件；坚持战斗、收集修理核心，直到战斗结束，再开始下一局。

这是本地开发预览入口。真正的热更验证仍要运行 Android 或 Windows HotUpdate Release Gate，由 YooAsset 加载包、ResKit 提供资源，HybridCLR 进入热更程序集。

### 游戏中展示的能力

- **Architecture / BindableKit / FSMKit**：Model 持有分数、装甲、波次和阶段；Service 处理规则，FSM 驱动战斗、暂停和结束状态，HUD 订阅 Bindable 更新。
- **ActionKit / EventKit / PoolKit**：按钮反馈由 ActionKit 播放；EventKit 将击毁、升级、维修和受伤事件送到系统面板；PoolKit 管理弹体与命中特效关联的数据记录。
- **ConfigKit / SaveKit / SettingsKit**：StreamingAssets 配置控制战斗参数；SaveKit 保存最高分与出击记录；设置面板可开关并持久化屏幕震动。
- **LocalizationKit / UIAdaptationKit**：运行时切换中英文；HUD 根据安全区和设备方向调整布局。
- **LogKit / ResKit / YooAsset / HybridCLR**：日志记录战局和加载状态；发布门禁通过 YooAsset 与 ResKit 分发热更内容，并由 HybridCLR 调用 `HotUpdateMain.Main()`。

战场、坦克、HUD、维修核心、弹体与特效均作为 Prefab 保存在 `Samples/TankArena/Resources/TankArena/Prefabs`；图形、材质与 UIAdaptationProfile 也保存在 Sample 的 Resources 目录。运行时代码负责加载资产、绑定交互和更新战局，不临时搭建画面。UIKit 等未在这局游戏中实际调用的 Kit，请以 `FrameworkDoc/02-Kits` 的对应指南与各 Kit 独立验证为准；面板上展示“Kit 名称”不等于该 Kit 已由 Demo 覆盖。

## English

**Tank Arena** is StellarFramework's single playable framework demo. It is a compact game loop with a live Systems panel, not a per-Kit API catalogue.

### Run

1. Open `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity` in the Unity Project window.
2. Press Play. The Editor bootstrap starts the game from the locally compiled `HotUpdate` assembly.
3. Use the left pad to move and the right pad to aim and fire independently. The upper-right controls switch language and fire mode and show the hot-update package version.
4. Pause or open **SYSTEMS** to inspect the Kits active in the match. Fight through the waves, collect repair cores, finish the match, and deploy again.

The Editor preview is for local iteration. It does not verify remote delivery. Run the Android or Windows HotUpdate Release Gate to verify the YooAsset package, ResKit loading path, and HybridCLR entry point.

### Kits used by the game

- **Architecture, BindableKit, FSMKit** manage the match model, rules, observable HUD state, and Active / Paused / Game Over transitions.
- **ActionKit, EventKit, PoolKit** drive button feedback, publish combat events, and reuse projectile/effect data records.
- **ConfigKit, SaveKit, SettingsKit** load balance values, persist match records, and save the screen-shake preference.
- **LocalizationKit, UIAdaptationKit** provide runtime language switching and safe-area/orientation-aware HUD layout.
- **LogKit, ResKit, YooAsset, HybridCLR** record runtime activity and provide the release-gate hot-update delivery path.

The arena, tanks, HUD, repair core, projectiles, and effects are Prefabs stored under `Samples/TankArena/Resources/TankArena/Prefabs`. Their sprites, materials, and UIAdaptationProfile are also sample assets. Runtime code loads these assets, binds input, and updates the match; it does not construct the presentation. Kits not called by the game, such as UIKit, are documented and validated separately; their names are not counted as Demo coverage.
