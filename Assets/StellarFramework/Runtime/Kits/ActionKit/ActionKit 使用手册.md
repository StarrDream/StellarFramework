# ActionKit 核心使用手册

**版本**: v2.0 (UniTask Powered)  
**定位**: 基于 UniTask 的轻量级、高性能、链式动作序列库。

---

## 1. 设计理念 (Design Philosophy)

ActionKit 旨在解决 Unity 开发中异步逻辑碎片化和补间动画 GC 高的问题。

*   **零 GC (Zero GC)**: 运行时无内存分配，底层基于 UniTask 和对象池。
*   **链式编程 (Fluent API)**: `Sequence().Delay().Move().Start()`，逻辑线性化。
*   **安全生命周期**: 自动绑定 GameObject 的 `OnDestroy`，彻底解决 `MissingReferenceException`。
*   **模块化**:
    *   **MonoKit**: 负责流程控制 (延时、循环、条件、并行)。
    *   **TweenKit**: 负责数值插值 (移动、旋转、缩放、渐变)。

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
    .DelayFrame(5)         // 等待 5 帧
    .Callback(() => Debug.Log("Done"))
    .Start();
```

### 3.2 条件等待 (Conditions)

用于等待某个特定条件满足后，再执行下一步。

```csharp
// 示例：等待玩家按下空格键，或者敌人死亡
MonoKit.Sequence(this)
    .Until(() => Input.GetKeyDown(KeyCode.Space)) // 阻塞直到返回 true
    .Callback(() => Debug.Log("Space Pressed!"))
    .Start();

// 示例：While 循环 (当条件满足时一直执行)
MonoKit.Sequence(this)
    .While(() => player.IsAlive, (chain) => 
    {
        // 只要玩家活着，每秒回血一次
        chain.Delay(1.0f)
             .Callback(() => player.Heal(5));
    })
    .Start();
```

### 3.3 循环与重复 (Loop & Repeat)

```csharp
// 重复固定次数
MonoKit.Sequence(this)
    .Repeat(3, (chain) => 
    {
        // 这里的逻辑会执行 3 次
        chain.ScaleTo(tf, Vector3.one * 1.2f, 0.2f)
             .ScaleTo(tf, Vector3.one, 0.2f);
    })
    .Start();

// 无限循环 (Forever)
MonoKit.Sequence(this)
    .Forever((chain) => 
    {
        // 自身旋转，永不停歇，直到 GameObject 销毁
        chain.RotateTo(tf, new Vector3(0, 0, 360), 2.0f)
             .Callback(() => tf.rotation = Quaternion.identity);
    })
    .Start();
```

### 3.4 并行执行 (Parallel)

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

### 4.1 Transform 变换

| 方法名 | 描述 | 参数示例 |
| :--- | :--- | :--- |
| `MoveTo` | 世界坐标移动 | `transform, new Vector3(10,0,0), 1f` |
| `LocalMoveTo` | 本地坐标移动 | `transform, new Vector3(10,0,0), 1f` |
| `RotateTo` | 欧拉角旋转 | `transform, new Vector3(0,90,0), 0.5f` |
| `LocalRotateTo` | 本地欧拉角旋转 | `transform, new Vector3(0,90,0), 0.5f` |
| `ScaleTo` | 缩放 | `transform, Vector3.one * 2, 0.3f` |
| `LookAt` | 旋转朝向目标 | `transform, targetPosition, 0.5f` |

### 4.2 UI & 图形 (uGUI / Sprite)

| 方法名 | 支持组件 | 描述 |
| :--- | :--- | :--- |
| `FadeTo` | CanvasGroup, Image, Text, SpriteRenderer | 透明度 Alpha 变化 (0~1) |
| `ColorTo` | Image, Text, SpriteRenderer, Graphic | 颜色变化 |
| `FillAmountTo` | Image | 填充进度变化 (0~1) |

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

### 4.4 缓动函数 (Ease)

所有 `To` 方法的最后一个参数通常是 `Ease` 类型。
*   `Ease.Linear`: 线性（匀速）
*   `Ease.InSine / OutSine / InOutSine`: 平滑正弦
*   `Ease.InQuad / OutQuad`: 二次加速/减速
*   `Ease.OutBack`: 超过终点再弹回（弹性效果，UI 常用）
*   `Ease.OutBounce`: 像球落地一样弹跳

---

## 5. 高级功能 (Advanced)

### 5.1 手动取消 (Cancellation)

虽然 ActionKit 会自动随 GameObject 销毁而取消，但有时你需要手动打断动画（例如：玩家在攻击前摇时被打断）。

```csharp
// 1. 保存引用
var action = MonoKit.Sequence(gameObject)
    .Delay(5.0f)
    .Callback(() => Debug.Log("Attack!"));
    
action.Start();

// 2. 在需要的时候取消
// action.Cancel(); // 取消当前链条，不再执行后续步骤
```

### 5.2 忽略时间缩放 (Unscaled Time)

游戏暂停 (`Time.timeScale = 0`) 时，UI 动画通常需要继续播放。

```csharp
MonoKit.Sequence(gameObject)
    .SetUpdate(true) // true = 使用 UnscaledTime
    .ScaleTo(tf, Vector3.one, 0.5f)
    .Start();
```

---

## 6. 常见问题 (Troubleshooting)

1.  **忘记写 `.Start()`**
    *   现象：代码运行了但画面没反应。
    *   原因：ActionKit 是构建者模式，`Start()` 才是触发器。

2.  **Parallel 中使用了 `.Start()`**
    *   现象：并行任务瞬间结束，没有等待动画播完。
    *   解决：在 `Parallel` 内部必须使用 `.Await()` 返回 Task。

3.  **空引用异常**
    *   虽然 ActionKit 处理了自身的生命周期，但在 `.Callback(() => obj.name)` 中，如果 `obj` 是外部变量且已被销毁，C# 依然会报错。请在 Callback 中做好判空保护。

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