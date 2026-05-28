# StellarFramework 新人上手路线

这份文档给第一次接触 StellarFramework 的开发者使用。目标不是一次看完所有 Kit，而是在 30 分钟内跑通框架主链路，知道新功能应该从哪里接入。

## 1. 先跑起来

1. 打开 Unity 工程。
2. 打开 `StellarFramework -> Tools Hub -> 样例支持 -> 样例构建`，生成示例资源和场景。
3. 打开 `Assets/StellarFramework/Samples/KitSamples/Scenes/FrameworkValidation_Playable.unity`。
4. 进入 Play Mode，依次点击 ResKit、UIKit、HotUpdateKit 相关按钮。
5. Console 没有 error，报告中没有 Failed，就说明框架主入口可以继续学习。

这个场景是回归和真机前检查入口。新人不要先钻进每个 Kit 的源码，先确认主链路是通的。

## 2. 推荐学习顺序

| 顺序 | 看什么 | 目标 |
| :--- | :--- | :--- |
| 1 | `Samples/ArchitectureDemo/README.md` | 理解 Architecture / Model / Service / View 的协作方式 |
| 2 | `Samples/KitSamples/Samples_Index.md` | 知道每个 Kit 的样例入口和前置条件 |
| 3 | `UIKit_Playable.unity` | 跑通 UI 初始化、打开、关闭、堆栈和压力测试 |
| 4 | `ResKit_Playable.unity` | 跑通 Resources、AB、AA、RawText 加载差异 |
| 5 | `HotUpdateKit_Playable.unity` | 理解资源热更和代码热更入口，不在样例里伪造真实 dll.bytes |
| 6 | 对应 Kit 的 `*-Guide.md` | 接入真实业务时再看细节 |

## 3. 新业务默认怎么选

资源加载：

- 本地小资源：`Resources`，适合配置、默认 UI、开发期快速验证。
- 商业资源热更：优先 `Addressables`，使用官方 Groups、Profiles、Build、Content Update。
- 已有 AB 管线：继续走 `AssetBundle`，构建使用 StellarFramework Tools Hub。
- 第三方资源插件：通过 `ResKit.RegisterCustomLoader` 接入，构建界面使用插件自己的工具。

UI：

- 新代码只从 `UIKit` 进入：`OpenAsync / PushAsync / Pop / Close`。
- `UIStackManager` 是兼容层，不作为新人推荐入口。
- UI 热更时优先用异步接口，AA address 使用完整 `Assets/...` 路径。

热更新：

- `HotUpdateKit` 只负责门户和策略调度。
- 资源更新默认策略是 Addressables。
- 代码热更默认策略是 HybridCLR 启动期加载 `.dll.bytes`，不承诺运行中替换已加载程序集。

## 4. Tools Hub 边界

Tools Hub 做框架自有流程：

- 样例构建。
- AssetBundle 构建和 AssetMap 生成。
- UIKit 工作区、UIRoot、Panel Template、自动绑定代码。
- 运行时诊断类工具。

Tools Hub 不重复做第三方已有构建界面：

- Addressables 模拟加载、构建、内容更新使用 Addressables 官方窗口。
- YooAsset 或其他资源插件使用它们自己的构建窗口。
- HybridCLR dll 生成使用 HybridCLR 官方流程；框架只负责启动期加载、校验和入口调用。

## 5. UIKit 自动绑定规则

1. 在需要绑定的 UI 节点上挂 `UIAutoBind`。
2. Inspector 中选择 Target，默认会自动推断 Button、Text、Toggle、Image 等常用组件。
3. 执行 `Assets/UIKit/生成 UI 绑定代码` 或 `GameObject/UIKit/生成 UI 绑定代码`。
4. 生成器会创建 `{Panel}.Designer.cs`，并在编译后把字段自动赋值到 Prefab。

生成字段保持 `[SerializeField] private`，会显示在 Inspector 中，方便定位和排错。字段带有 Tooltip，提示它们由 UIKit 自动绑定。正常情况下不要手改这些字段；如果自动绑定失败，可以先在 Inspector 里确认字段是否为空，再重新生成绑定。

## 6. 常见排错

- 样例场景打不开：先运行样例构建器。
- 不知道某个样例怎么操作：先看 `Samples_Index.md`，再看对应 `Example_*.cs` 文件头注释。
- UIKit 打不开面板：确认 `UIRoot.prefab`、Panel prefab、Panel 类名和 prefab 名称一致。
- 自动绑定字段为空：确认节点挂了 `UIAutoBind`，Target 不为空，重新执行 Generate & Bind。
- AA 加载失败：检查 Addressables address 是否为完整 `Assets/...`，Play Mode Script 和构建模式是否正确。
- AB 加载失败：检查 Tools Hub 是否已构建当前平台 AB，`StreamingAssets/AssetBundles/<Platform>` 是否存在产物。
- HybridCLR 未执行入口：检查 `HYBRIDCLR_ENABLE`、dll.bytes address、SHA256、AOT metadata keys、入口类和方法。

## 7. 上手完成标准

- `FrameworkValidation_Playable.unity` 能跑完主链路。
- `UIKit_Playable.unity` 100 次 Open/Close 后 `Loading=0`。
- `ResKit_Playable.unity` 至少跑通 Resources，本地需要 AB/AA 时再分别构建。
- 能说清楚：AB 用 Tools Hub，AA/第三方资源插件用插件自己的构建界面。
- 新 UI 能用 `UIAutoBind -> Generate & Bind -> OpenAsync` 跑起来。
