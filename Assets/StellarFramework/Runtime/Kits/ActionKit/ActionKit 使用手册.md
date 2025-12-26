# ActionKit 核心使用手册

**版本**: v3.3 (Enhanced Control)  
**定位**: 基于 UniTask 的轻量级、高性能、链式动作序列库。

---

## 1. 设计理念 (Design Philosophy)

ActionKit 旨在解决 Unity 开发中异步逻辑碎片化和补间动画 GC 高的问题。

*   **零 GC (Zero GC)**: 运行时无内存分配，底层基于 UniTask 和对象池。
*   **链式编程 (Fluent API)**: `Sequence().Delay().Move().Start()`，逻辑线性化。
*   **安全生命周期**: 自动绑定 GameObject 的 `OnDestroy`，彻底解决 `MissingReferenceException`。
*   **可控性**: 支持手动 `Cancel` 取消执行，支持链式保存引用。

---

## 2. 快速开始 (Quick Start)

### 基础序列
所有动作必须以 `MonoKit.Sequence(gameObject)` 开头，以 `.Start()` 结尾。

```csharp
// 示例：物体生成 -> 变大 -> 停留 -> 消失
MonoKit.Sequence(gameObject)
    .ScaleTo(transform, Vector3.one, 0.5f, Ease.OutBack) // 弹性变大
    .Delay(1.0f)                                         // 停留 1秒
    .ScaleTo(transform, Vector3.zero, 0.2f)              // 缩小
    .Callback(() => Destroy(gameObject))                 // 销毁
    .Start();                                            // [重要] 启动执行
```

---

## 3. MonoKit：流程控制 (Flow Control)

MonoKit 提供了对时间、帧、条件和循环的控制能力。

### 3.1 时间与帧 (Time & Frame)

```csharp
MonoKit.Sequence(this)
    .Delay(1.5f)           // 等待 1.5 秒
    .DelayFrame(1)         // 等待 1 帧 (yield return null)
    .Callback(() => Debug.Log("Done"))
    .Start();
```

### 3.2 条件等待 (Conditions)

用于等待某个特定条件满足后，再执行下一步。

```csharp
// 示例：等待玩家按下空格键
MonoKit.Sequence(this)
    .Until(() => Input.GetKeyDown(KeyCode.Space)) // 阻塞直到返回 true
    .Callback(() => Debug.Log("Space Pressed!"))
    .Start();
```

### 3.3 并行执行 (Parallel)

同时执行多个动作，等待所有动作完成后，继续后续链条。

> **注意**: 在 Parallel 内部必须调用 `.Await()` 而不是 `.Start()`。

```csharp
MonoKit.Sequence(gameObject)
    .Parallel(
        // 任务A：移动
        t => MonoKit.Sequence(gameObject).MoveTo(tf, targetPos, 1f).Await(),
        // 任务B：变色
        t => MonoKit.Sequence(gameObject).ColorTo(img, Color.red, 1f).Await(),
        // 任务C：自定义异步逻辑
        async t => await UniTask.Delay(500, cancellationToken: t)
    )
    .Callback(() => Debug.Log("所有并行任务完成"))
    .Start();
```

---

## 4. TweenKit：动画插值 (Animation)

TweenKit 是 `UniActionChain` 的扩展方法，支持多种组件的插值动画。

### 4.1 常用变换

| 方法名 | 描述 | 参数示例 |
| :--- | :--- | :--- |
| `MoveTo` | 世界坐标移动 | `transform, targetPos, 1f` |
| `LocalMoveTo` | 本地坐标移动 | `transform, targetPos, 1f` |
| `RotateTo` | 欧拉角旋转 | `transform, new Vector3(0,90,0), 0.5f` |
| `ScaleTo` | 缩放 | `transform, Vector3.one * 2, 0.3f` |

### 4.2 UI & 图形 (uGUI / Sprite)

| 方法名 | 支持组件 | 描述 |
| :--- | :--- | :--- |
| `FadeTo` | CanvasGroup, Image, Text, Graphic | 透明度 Alpha 变化 (0~1) |
| `ColorTo` | Image, Text, Graphic | 颜色变化 |

```csharp
// UI 进场动画示例
MonoKit.Sequence(gameObject)
    .SetUpdate(true) // 忽略 TimeScale (UI常用)
    .FadeTo(canvasGroup, 1f, 0.5f)
    .LocalMoveTo(rectTransform, Vector3.zero, 0.5f, Ease.OutBack)
    .Start();
```

### 4.3 通用数值驱动 (ValueTo)

万能插值方法，用于驱动任何自定义属性（如金币跳动、Shader参数、音量）。

```csharp
// 示例：音量渐隐
MonoKit.Sequence(gameObject)
    .ValueTo(1.0f, 0.0f, 2.0f, (val) => 
    {
        audioSource.volume = val;
    }, Ease.Linear)
    .Start();
```

---

## 5. 控制与生命周期 (Control & Lifecycle)

### 5.1 手动取消 (Manual Cancel)

`Start()` 方法现在返回 `UniActionChain` 实例本身。你可以保存这个引用，并在需要时调用 `Cancel()`。

**标准写法：**

```csharp
public class SkillSystem : MonoBehaviour
{
    private UniActionChain _currentAction;

    public void CastSkill()
    {
        // 1. 务必先取消上一次动作（防止快速点击导致逻辑重叠）
        _currentAction?.Cancel();

        // 2. 创建新序列并保存引用
        _currentAction = MonoKit.Sequence(gameObject)
            .Delay(0.5f)
            .Callback(() => Debug.Log("Fire!"))
            .Start(); // Start 返回实例
    }

    public void Stop()
    {
        // 3. 手动打断
        _currentAction?.Cancel();
    }
}
```

### 5.2 重新播放 (Replay)

**重要原则**：**不能**对已经 Cancel 或执行完毕的 `UniActionChain` 再次调用 `Start()`。

*   **原因**：ActionKit 基于对象池。当动作结束或取消时，该对象会被立即回收并重置。
*   **做法**：如果需要重新播放，必须**重新构建**序列（参考 5.1 的写法，每次都调用 `MonoKit.Sequence`）。

### 5.3 忽略时间缩放 (Unscaled Time)

游戏暂停 (`Time.timeScale = 0`) 时，UI 动画通常需要继续播放。

```csharp
MonoKit.Sequence(gameObject)
    .SetUpdate(true) // true = 使用 UnscaledTime
    .ScaleTo(tf, Vector3.one, 0.5f)
    .Start();
```

### 5.4 自动销毁绑定

`MonoKit.Sequence(gameObject)` 传入的 `gameObject` 是绑定的生命周期对象。
*   如果该 GameObject 被销毁，动画序列会自动停止并回收。
*   不需要在 `OnDestroy` 中手动写 `Cancel`。

---

## 6. 常见问题 (Troubleshooting)

1.  **忘记写 `.Start()`**
    *   现象：代码运行了但画面没反应。
    *   原因：ActionKit 是构建者模式，`Start()` 才是触发器。

2.  **Parallel 中使用了 `.Start()`**
    *   现象：并行任务瞬间结束，没有等待动画播完。
    *   解决：在 `Parallel` 内部必须使用 `.Await()` 返回 Task。

3.  **报错 "Object is already recycled" 或空引用**
    *   原因：你尝试复用了一个已经执行完毕或被取消的 `UniActionChain` 变量。
    *   解决：每次播放动画时，请重新调用 `MonoKit.Sequence(...)` 创建新的链条。

---

## 7. 扩展指南 (Extension)

如果需要支持 TextMeshPro 或自定义 Shader 属性，可以编写扩展方法：

```csharp
public static class ActionKitExtensions
{
    // 扩展：打字机效果
    public static UniActionChain Typewriter(this UniActionChain chain, Text textComp, string content, float duration)
    {
        chain.AppendTask(async (token) => 
        {
            await TweenKit.To(0, content.Length, duration, (val) => 
            {
                int count = Mathf.FloorToInt(val);
                textComp.text = content.Substring(0, count);
            }, Ease.Linear, token, chain.IsIgnoreTimeScale);
        });
        return chain;
    }
}
```