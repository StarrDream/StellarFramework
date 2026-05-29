# ArchitectureDemo / 架构样例

`Assets/StellarFramework/Samples/ArchitectureDemo` 用来展示框架分层、入口脚本和模块协作方式。

当前只保留一个可运行场景，避免多个重复 Demo 场景让新人不知道该打开哪一个。

## 目录

- `Scene/`
  架构样例场景。
- `Runtime/`
  配套运行时代码。
- `Resources/`
  案例依赖资源。

## 场景入口

- `Scene/FrameworkArchitecture_Playable.unity`
  唯一保留的可运行架构样例场景。

## 建议阅读顺序

1. 打开 `Scene/FrameworkArchitecture_Playable.unity`
2. 查看 `Runtime/DemoEntry.cs`
3. 查看 `Runtime/Architecture/`
4. 再回到 `Samples/KitSamples`

如果想先建立整体理解，从这个场景开始即可；更细的 Kit 验收请继续看 `Samples/KitSamples`。
