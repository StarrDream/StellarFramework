# Tank Arena

Tank Arena 是 StellarFramework 的可运行框架案例。玩家需要操控坦克抵御持续来袭的敌军，在波次间收集修理核心，并在战斗结束后查看本局结果、继续出击。玩法逻辑由 `HotUpdate` 程序集承载；战场、坦克、HUD、弹体、维修核心和战斗特效均以 Sample 内的 Prefab 交付，运行时从 `Resources` 加载。

案例的玩法流程、Kit 协作方式和资源修改入口见 [Tank Arena 案例说明](CaseStudy.md)。

## 在 Unity 中运行

1. 打开 `Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity`。
2. 等待 Unity 完成首次导入与脚本编译，点击 Play。
3. 左侧摇杆控制车体移动；右侧摇杆独立控制炮塔。切换到 **MANUAL** 后按住右侧摇杆开火；**AUTO** 会自动瞄准并射击。
4. 击毁敌人可提升分数与波次；修理核心恢复装甲。暂停后可继续、进入 **SYSTEMS** 面板或重新开始；装甲归零时结算本局并保存记录。
5. 桌面可用 WASD 移动车体，鼠标拖动右侧瞄准区，按住 Space 开火。

编辑器预览会从本地编译的 `HotUpdate` 程序集调用 `HotUpdateMain.Main()`。它用于快速开发，不会下载远端包，也不构成热更发布验证。

## 游戏中的 Kit 协作

| Kit | Demo 中的实际职责 |
| --- | --- |
| Architecture / BindableKit | Model 保存分数、装甲、波次、击毁数和阶段；Service 更新规则，HUD 订阅 Bindable 属性。 |
| FSMKit | 驱动 Active、Paused、Game Over 战斗状态。 |
| ActionKit | 管理按钮动效等可取消的短时序列。 |
| EventKit | 发布击毁、波次推进、维修和受伤事件；系统面板订阅并展示最近事件。 |
| PoolKit | 管理弹体与命中特效的数据记录；画面对象从 Sample Prefab 实例化，池化数据和视觉资源保持分工。 |
| ConfigKit.Core | 从 `Assets/StreamingAssets/TankArena/demo-config.json` 读取速度、射击间隔、敌人数量与波次参数；同相对路径的 `persistentDataPath` 文件可覆盖默认值。 |
| SaveKit | 将最高分、累计击毁数和出击次数保存到本机，并在下一次启动时恢复。 |
| SettingsKit | 将屏幕震动偏好写入 PlayerPrefs 设置后端。 |
| LocalizationKit | 战斗中即时切换简体中文与英文。 |
| UIAdaptationKit | 为运行时 UGUI HUD 配置安全区根节点和方向适配。 |
| LogKit | 记录配置加载、战局结算与存档结果。 |
| ResKit / YooAsset / HybridCLR | 仅在 Android / Windows 发布门禁中验证包下载、Manifest/程序集读取、AOT metadata 加载与热更入口调用。 |

Systems 面板展示的是这局中正在调用的 Kits。HUD 使用 Sample 内的 UGUI Prefab，UIAdaptationKit 读取 Sample 内的适配 Profile；Demo 不把 UIKit 或其他未实际调用的 Kit 算作覆盖。各 Kit 的完整 API、边界和项目接入方式见 `Assets/StellarFramework/FrameworkDoc/02-Kits`。

## 配置与存档

- 默认配置：`Assets/StreamingAssets/TankArena/demo-config.json`
- 配置覆盖位置：`Application.persistentDataPath/TankArena/demo-config.json`
- 存档槽：`tank-arena-profile`
- 存档内容：最高分、累计击毁数和出击次数

删除设备上的配置覆盖文件或存档后，下一次启动会回到默认配置或新档状态。

## 热更验证

使用 `Tools > StellarFramework > Verification` 下的 Android 或 Windows HotUpdate Release Gate。门禁会构建匹配的平台 Player 与 YooAsset 热更包，检查下载、ResKit 读取、HybridCLR metadata 加载及 `HotUpdateMain.Main()` 调用。请以 `Assets/StellarFramework/FrameworkDoc/02-Kits/HybridCLRKit` 和 `Assets/StellarFrameworkVerification` 的门禁说明为准。

## 目录

- `Scene/FrameworkDemo.unity`：可直接打开的干净启动场景。
- `Runtime/`：`HotUpdate.asmdef`、入口、坦克玩法、架构模型、FSM、事件和本地化服务。
- `Editor/`：仅限编辑器的本地预览启动器。
- `Resources/TankArena/Prefabs/`：游戏主机、战场、HUD、坦克、弹体、维修核心和特效 Prefab。
- `Resources/TankArena/Generated/`：HUD 圆角与圆形 Sprite、材质和 UIAdaptationProfile 资产。
- `Resources/Fonts/`：中文 UI 字体、来源与许可证。
- `Assets/StreamingAssets/TankArena/demo-config.json`：游戏默认平衡配置。
