# Samples / 样例总览

`Assets/StellarFramework/Samples` 是样例入口，分为完整业务示范和单模块最小样例两部分。

## 目录

- `ArchitectureDemo/`
  完整架构案例，用来演示 `Architecture / Model / Service / View / UI` 的协作链路。
- `KitSamples/`
  单个模块的最小可运行案例，用来验证接线、资源和调用方式。

## 建议顺序

1. `../快速开始.md`
2. `KitSamples/Scenes/UIKit_Playable.unity`
3. `KitSamples/Scenes/ResKit_Playable.unity`
4. `KitSamples/README.md`
5. `KitSamples/Scenes/README.md`
6. `ArchitectureDemo/README.md`
7. `ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`
8. 对应模块目录下的 `English-中文-Guide.md`

## 说明

- `KitSamples` 适合按模块查 API、看资源组织方式和验证最小闭环。
- `ArchitectureDemo` 适合在基础样例跑通后，再理解 `Architecture / Model / Service / View / UI` 的协作链路。
- `KitSamples/Editor` 里的构建器会补齐样例场景、测试配置和依赖资源。
- `SettingsKit_Playable.unity` 已加入 `KitSamples`，可直接验证设置系统的默认页、扩展页、存储和即时应用。
- `FrameworkValidation` 已迁入框架外的验证区，仅供框架开发者做发布前自检与冒烟验证。

