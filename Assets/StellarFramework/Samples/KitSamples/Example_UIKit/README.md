# Example UIKit / UI 样例

这个样例用于验收 UIKit 的生产基础能力：异步初始化、统一入口、堆栈导航、静态/动态 Canvas、运行时快照和 Open/Close 压力测试。

## 前置条件

- 存在 `Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`。
- 存在 `Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab`。
- 默认使用 `Resources` 后端；如切到 AA/AB，请先完成对应资源构建和 `UIKitSettings` 配置。

可通过 KitSamples 的场景构建器自动补齐上述资源。

## 运行方式

打开：

`Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity`

按键：

- `O`：异步打开面板。
- `P`：Push 到 UI 栈。
- `Backspace`：Pop。
- `C`：Close。
- `S`：100 次 Open/Close 压力测试。
- `D`：打印 `UIKitRuntimeSnapshot`。

## 通过标准

- 启动后自动出现 `ExamplePanel`。
- 按钮可点击，点击确认后面板关闭。
- `O/P/C/Backspace` 不报错，生命周期日志顺序清晰。
- `S` 压力测试结束后 Snapshot 中 `Loading=0`。
- 真机上返回前后台、切换分辨率或横竖屏后 UI 仍可点击。

## AA/AB UI 加载

- AA：把 `UIRoot.prefab` 和 Panel prefab 加入 Addressables，address 使用完整 `Assets/...`，在 `UIKitSettings` 中选择 `Addressables` 后端，并使用 `OpenAsync/PushAsync`。
- AB：通过 ToolHub 的 AssetBundle 模块构建 UI 资源，启动期先初始化 `AssetBundleManager`，再使用 UIKit。
- 自定义：注册 ResKit custom loader 后，在 `UIKitSettings` 中填写 `Custom Loader Key`。
