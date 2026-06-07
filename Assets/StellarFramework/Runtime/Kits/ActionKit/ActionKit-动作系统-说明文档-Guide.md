# ActionKit / 动作系统说明文档

## 模块定位

`ActionKit` 用来组织短流程动画和可配置动作序列。

它同时支持两种使用方式：

- 代码式动作链
- 配置式动作资产

前者适合临时流程动画和 UI 出入场，后者适合希望复用、可编辑、可快进和可恢复的动作逻辑。

## 模块组成

- `ActionKit`
  动作链静态入口
- `UniActionChain`
  代码式动作链实例
- `TweenKit / TweenExtensions`
  缓动与插值层
- `ActionEngineAsset`
  配置式动作资产
- `ActionEngineRunner`
  配置式动作执行器
- `ActionPlayer`
  场景中的动作播放组件

## 典型场景

适合：

- UI 面板淡入淡出
- 开场 / 转场动画
- 物体位移、缩放、旋转
- 可复用的配置式动作序列

不适合：

- 复杂技能时间轴编辑器
- 大规模关卡脚本系统

## 代码式动作链

示例：

```csharp
await ActionKit.Sequence(gameObject)
    .FadeTo(canvasGroup, 1f, 0.2f)
    .ScaleTo(transform, Vector3.one, 0.25f)
    .Play(destroyCancellationToken);
```

这条链路适合：

- 少量步骤
- 写在业务代码里即可表达清楚
- 不需要做成资产给策划或美术复用

## 配置式动作资产

示例使用方式：

1. 创建 `ActionEngineAsset`
2. 配置动作节点
3. 在场景中挂 `ActionPlayer`
4. 调用 `PlayForward()` 或 `PlayReverse()`

适合：

- 多对象复用同一套动作
- 需要倒放或恢复初始状态
- 希望动作结构从代码中抽离

## 运行规则

- 代码式动作链依赖 `UniTask`
- 推荐始终传入取消令牌
- 配置式动作的目标对象必须满足策略要求的组件条件
- 需要恢复初始状态时，要使用快照链路

## 常见 API

- `ActionKit.Sequence(...)`
- `ActionKit.Delay(...)`
- `MoveTo(...)`
- `ScaleTo(...)`
- `RotateTo(...)`
- `FadeTo(...)`
- `ColorTo(...)`
- `ActionPlayer.PlayForward()`
- `ActionPlayer.PlayReverse()`
- `ActionPlayer.ResetToStart()`

## ToolsHub 关联

- 样例构建会补齐 ActionKit 相关样例资源
- 文档中心可直接查看说明文档和源码文档

## 常见问题

- 对象销毁后动作还在执行
  传入 `destroyCancellationToken`。
- 配置动作播放完后没恢复初始状态
  使用快照初始化和恢复流程。
- UI 淡入无效
  检查目标是否具备 `CanvasGroup` 或对应图形组件。

## 相关文档

- [ActionKit 源码文档](ActionKit-动作系统-源码文档-Guide.md)
