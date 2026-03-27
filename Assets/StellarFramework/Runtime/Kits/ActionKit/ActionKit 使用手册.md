# ActionKit 使用手册

**适用**: StellarFramework ActionEngine 模块  
**定位**: 基于 UniTask 的轻量级、链式动作序列库。

---

## 1. 设计理念 (Design Philosophy)
ActionKit 旨在优化 Unity 开发中异步逻辑碎片化和传统补间动画容易产生较高 GC 的问题。

*   **低 GC 设计**: 运行时尽量减少内存分配，底层基于 UniTask 和对象池 (`PoolKit`)。
*   **链式编程 (Fluent API)**: 使用 `Sequence().Delay().MoveTo().Start()` 的形式，使逻辑线性化。
*   **生命周期安全**: 自动绑定 GameObject 的 `OnDestroy`，避免对象销毁后继续执行引发的空引用异常。
*   **可控性**: 支持手动 `Cancel` 取消执行，支持链式保存引用。

---

## 2. 快速开始 (Quick Start)

### 基础序列
所有动作建议以 `ActionKit.Sequence(gameObject)` 开头，以 `.Start()` 结尾。

```csharp
// 示例：物体生成 -> 变大 -> 停留 -> 缩小 -> 销毁
ActionKit.Sequence(gameObject)
    .ScaleTo(transform, Vector3.one, 0.5f, Ease.OutBack) // 弹性变大
    .Delay(1.0f)                                         // 停留 1秒
    .ScaleTo(transform, Vector3.zero, 0.2f)              // 缩小
    .Callback(() => Destroy(gameObject))                 // 销毁
    .Start();                                            // [重要] 启动执行
```

---

## 3. 流程控制 (Flow Control)
ActionKit 提供了对时间、帧、条件和并发的控制能力。

### 3.1 时间与帧 (Time & Frame)
```csharp
ActionKit.Sequence(this)
    .Delay(1.5f)           // 等待 1.5 秒
    .DelayFrame(1)         // 等待 1 帧
    .Callback(() => Debug.Log("Done"))
    .Start();
```

### 3.2 条件等待 (Conditions)
用于等待某个特定条件满足后，再执行下一步。
```csharp
// 示例：等待玩家按下空格键
ActionKit.Sequence(this)
    .Until(() => Input.GetKeyDown(KeyCode.Space)) // 阻塞直到返回 true
    .Callback(() => Debug.Log("Space Pressed!"))
    .Start();
```

### 3.3 并行执行 (Parallel)
同时执行多个动作，等待所有动作完成后，继续后续链条。
> **注意**: 在 Parallel 内部必须调用 `.Await()` 而不是 `.Start()`。

```csharp
ActionKit.Sequence(gameObject)
    .Parallel(
        // 任务A：移动
        t => ActionKit.Sequence(gameObject).MoveTo(tf, targetPos, 1f).Await(),
        // 任务B：变色
        t => ActionKit.Sequence(gameObject).ColorTo(img, Color.red, 1f).Await(),
        // 任务C：自定义异步逻辑
        async t => await UniTask.Delay(500, cancellationToken: t)
    )
    .Callback(() => Debug.Log("所有并行任务完成"))
    .Start();
```

---

## 4. 动画插值 (Tween Extensions)
框架提供了针对 `UniActionChain` 的扩展方法，支持多种组件的插值动画。

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
| `FadeTo` | CanvasGroup, Graphic | 透明度 Alpha 变化 (0~1) |
| `ColorTo` | Graphic | 颜色变化 |

```csharp
// UI 进场动画示例
ActionKit.Sequence(gameObject)
    .SetUpdate(true) // 忽略 TimeScale (UI常用)
    .FadeTo(canvasGroup, 1f, 0.5f)
    .LocalMoveTo(rectTransform, Vector3.zero, 0.5f, Ease.OutBack)
    .Start();
```

### 4.3 通用数值驱动 (ValueTo)
通用插值方法，用于驱动自定义属性（如 Shader 参数、音量）。

```csharp
// 示例：音量渐隐
ActionKit.Sequence(gameObject)
    .ValueTo(1.0f, 0.0f, 2.0f, (val) => 
    {
        audioSource.volume = val;
    }, Ease.Linear)
    .Start();
```

---

## 5. 控制与生命周期 (Control & Lifecycle)

### 5.1 手动取消 (Manual Cancel)
`Start()` 方法返回 `UniActionChain` 实例。可以保存该引用，并在需要时调用 `Cancel()`。

```csharp
public class SkillSystem : MonoBehaviour
{
    private UniActionChain _currentAction;

    public void CastSkill()
    {
        // 1. 建议先取消上一次动作，防止逻辑重叠
        _currentAction?.Cancel();

        // 2. 创建新序列并保存引用
        _currentAction = ActionKit.Sequence(gameObject)
            .Delay(0.5f)
            .Callback(() => Debug.Log("Fire!"))
            .Start();
    }

    public void Stop()
    {
        // 3. 手动打断
        _currentAction?.Cancel();
    }
}
```

### 5.2 重新播放规范 (Replay)
**规范**：**不能**对已经 Cancel 或执行完毕的 `UniActionChain` 再次调用 `Start()`。
*   **原因**：ActionKit 基于对象池。动作结束或取消时，对象会被回收。
*   **做法**：如果需要重新播放，必须重新调用 `ActionKit.Sequence(...)` 构建新的链条。

### 5.3 忽略时间缩放 (Unscaled Time)
游戏暂停 (`Time.timeScale = 0`) 时，UI 动画通常需要继续播放。
```csharp
ActionKit.Sequence(gameObject)
    .SetUpdate(true) // true = 使用 UnscaledTime
    .ScaleTo(tf, Vector3.one, 0.5f)
    .Start();
```

---

## 6. 常见问题 (Troubleshooting)

1.  **忘记写 `.Start()`**
    *   现象：代码运行了但画面没反应。
    *   原因：ActionKit 是构建者模式，`Start()` 或 `Await()` 才是触发器。
2.  **Parallel 中使用了 `.Start()`**
    *   现象：并行任务瞬间结束，没有等待动画播完。
    *   解决：在 `Parallel` 内部必须使用 `.Await()` 返回 Task。
3.  **报错 "当前链条已回收"**
    *   原因：尝试复用了一个已经执行完毕或被取消的 `UniActionChain` 变量。
    *   解决：每次播放动画时，请重新调用 `ActionKit.Sequence(...)`。