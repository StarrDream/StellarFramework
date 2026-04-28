# KitSamples / 模块样例

`Assets/StellarFramework/Samples/KitSamples` 用来存放各个模块的最小可运行样例。

## 目录

- `Scenes/`
  每个模块对应一个 `*Playable.unity` 场景。
- `Example_*/`
  示例脚本目录。
- `Common/`
  多个样例共用的辅助脚本。
- `Generated/`
  样例构建器生成的资源。
- `Editor/`
  样例场景构建器与触发器。

## 建议查看方式

1. 先看 `Scenes/README.md`
2. 打开对应的 `*Playable.unity`
3. 对照 `Example_*/*.cs`
4. 回看对应模块目录下的 `English-中文-Guide.md`

## 推荐入口

- `ActionKit_Playable.unity`
  看最轻量的调用链。
- `AudioKit_Playable.unity`
  看资源、Mixer 和运行时音量控制。
- `SettingsKit_Playable.unity`
  看设置定义、存储、扩展页和策略解耦。
- `UIKit_Playable.unity`
  看面板加载和界面入口。

## 说明

- `Example_*` 目录主要是示例脚本，不等于完整项目模板。
- 优先从 `Scenes/*.unity` 进入案例，再回头看脚本。
- 如需补齐或重建样例场景，请在 `StellarFramework -> Tools Hub -> 样例支持 -> 样例构建` 中执行。
- `Example_ResKit/README.md` 集中说明 `ResKit` 的脚本、测试资源和运行方式。
