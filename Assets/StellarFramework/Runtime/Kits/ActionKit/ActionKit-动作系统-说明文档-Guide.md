# ActionKit / 动作系统说明文档

ActionKit 提供两类能力：代码侧的异步动作链，以及配置化的 ActionEngine。前者适合临时代码动画，后者适合把移动、旋转、缩放、淡入淡出等步骤做成可复用资产。

## 入口 API

- `ActionKit.Sequence(GameObject target)`：创建绑定目标的动作链。
- `ActionKit.Sequence(Component component)`：从组件创建动作链。
- `ActionKit.Delay(seconds, token)`：等待一段时间。
- `TweenKit.To(...)`：插值 float、Vector3、Color、Quaternion。
- `TweenExtensions`：给 `UniActionChain` 添加 `MoveTo`、`ScaleTo`、`FadeTo` 等扩展。
- `ActionPlayer`：在场景中播放 `ActionEngineAsset`。
- `ActionEngineRunner.Play(...)`：代码触发配置化动作。

## 使用模板

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.ActionKit;
using UnityEngine;

public sealed class OpenAnimation : MonoBehaviour
{
    private async UniTaskVoid OnEnable()
    {
        await ActionKit.Sequence(gameObject)
            .FadeTo(GetComponent<CanvasGroup>(), 1f, 0.2f)
            .ScaleTo(transform, Vector3.one, 0.25f)
            .Play(destroyCancellationToken);
    }
}
```

## ToolsHub 关联

- `样例构建` 会生成 ActionKit 相关示例资源。
- `文档中心 (Docs)` 可以直接阅读动作系统说明和源码文档。

## 样例与测试

- 样例脚本位于 `Samples/KitSamples/Scripts`。
- 用户样例入口优先走具体 ActionKit Playable 场景；框架开发者可额外使用外置验证区的 FrameworkValidation 场景。

## 常见问题

- 动作对象销毁后仍执行：传入 `destroyCancellationToken`。
- 配置化动作没有恢复初始状态：使用 `ActionEngineRunner.InitSnapshot` 和 `RestoreSnapshot`。
- UI 淡入无效：确认目标挂了 `CanvasGroup` 或使用 Graphic 颜色淡入扩展。
