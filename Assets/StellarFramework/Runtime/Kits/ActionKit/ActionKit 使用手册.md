# ActionKit 使用手册

## 1. 设计理念 (Why)
在商业游戏开发中，异步逻辑（Asynchronous Logic）和 补间动画（Tweening）是两大核心需求。
*   **传统痛点**：
    *   `Coroutine`：写法嵌套过深，难以维护，且开启协程 (`StartCoroutine`) 会产生 GC。
    *   `DOTween`：功能强大但体积庞大，闭源导致难以深度定制，且泛型扩展不够灵活。
    *   `UniTask`：虽然解决了异步问题，但缺乏一套直观的、类似 DOTween 的“链式动作序列”封装。

**ActionKit 的特性：**
*   **链式编程 (Fluent API)**：`Sequence().Delay().MoveTo().Callback()`，逻辑一目了然，代码即文档。
*   **轻量高性能**：底层完全基于 `UniTask` 驱动，**0 GC Update**。
*   **生命周期安全**：核心类 `UniActionChain` 自动绑定 GameObject 的 `CancellationToken`。当物体销毁时，所有未完成的延时、动画会自动取消，**彻底根绝空引用异常 (MissingReferenceException)**。
*   **对象池化**：链条对象实现了 `IPoolable`，使用完自动回收。在弹幕游戏等高频场景下，性能表现优异。

---

## 2. 核心架构 (Under the hood)

### 2.1 模块划分
*   **MonoKit**：负责流程控制。提供 `Sequence` (序列), `Delay` (延时), `Parallel` (并行), `Until` (条件等待) 等逻辑节点。
*   **TweenKit**：负责数值插值。提供 `MoveTo`, `ScaleTo`, `FadeTo`, `ValueTo` 等动画节点。本质上是 `UniActionChain` 的扩展方法。

### 2.2 运行机制
1.  **申请**：调用 `MonoKit.Sequence(target)` 时，从 `PoolKit` 申请一个 `UniActionChain` 实例。
2.  **构建**：调用 `.Delay()`, `.MoveTo()` 等方法，实际上是向链条内部的 `List<Func<CancellationToken, UniTask>>` 添加任务委托。
3.  **执行**：调用 `.Start()` 后，链条开始遍历任务列表，使用 `await` 逐个执行。
4.  **回收**：当所有任务完成、或发生异常、或被取消时，链条自动重置数据并回收到对象池。

---

## 3. 使用指南 (How)

### 3.1 基础序列 (Sequence)
最常用的功能，按顺序执行一系列动作。

```csharp
// 场景：怪物死亡流程 -> 变红 -> 震动 -> 缩小 -> 销毁
MonoKit.Sequence(gameObject) // 1. 传入 gameObject 绑定生命周期 (必填)
    .ColorTo(renderer, Color.red, 0.2f)                // 2. 变红
    .RotateTo(transform, new Vector3(0, 360, 0), 0.5f) // 3. 旋转一圈
    .ScaleTo(transform, Vector3.zero, 0.3f)            // 4. 缩小消失
    .Callback(() => {                                  // 5. 回调逻辑
        AudioKit.PlaySound("MonsterDie");
        EffectManager.Play("Explosion", transform.position);
    })
    .Callback(() => Destroy(gameObject))               // 6. 销毁物体
    .Start();                                          // 7. 启动！(切记)
```

### 3.2 并行执行 (Parallel)
同时播放多个动画或逻辑，等待所有任务完成后，继续执行后续步骤。

```csharp
MonoKit.Sequence(gameObject)
    .Parallel(
        // 任务A：移动 (注意：必须调用 .Await() 将动作转为 UniTask)
        (token) => MonoKit.Sequence(gameObject).MoveTo(tf, targetPos, 1f).Await(),
        
        // 任务B：UI 渐变
        (token) => MonoKit.Sequence(gameObject).FadeTo(canvasGroup, 0f, 1f).Await(),
        
        // 任务C：纯逻辑 (直接返回 UniTask)
        (token) => { 
            Debug.Log("开始并行计算..."); 
            return UniTask.Delay(500, cancellationToken: token); 
        }
    )
    .Callback(() => Debug.Log("所有并行任务已完成，继续后续逻辑"))
    .Start();
```

### 3.3 UI 动画 (忽略 TimeScale)
做弹窗、暂停菜单动画时，必须忽略 `Time.timeScale`，否则游戏暂停时动画也会卡住。

```csharp
MonoKit.Sequence(gameObject)
    .SetUpdate(true) // <--- 开启忽略 TimeScale (使用 unscaledTime)
    .FadeTo(canvasGroup, 1f, 0.3f)
    .ScaleTo(panelRect, Vector3.one, 0.3f, Ease.OutBack) // 使用弹性曲线
    .Start();
```

### 3.4 数值驱动 (ValueTo)
常用于金币增长、血条变化、经验条滚动等效果。

```csharp
// 场景：金币数字从 0 滚动到 1000
MonoKit.Sequence(gameObject)
    .ValueTo(0, 1000, 1.5f, (val) => 
    {
        // 每帧回调，val 是当前插值
        coinText.text = $"Coins: {(int)val}";
    }, Ease.OutExpo)
    .Start();
```

---

## 4. 常见坑点与解决方案 (Troubleshooting)

### Q1: 动画代码写了，但是没反应？
*   **原因 A**：忘记调用 `.Start()`。ActionKit 的设计是“构建-执行”分离的，不调用 Start 只是构建了数据。
*   **原因 B**：`gameObject` 在动画开始前就被销毁或隐藏了。
*   **原因 C**：`Time.timeScale` 为 0，且没有调用 `.SetUpdate(true)`。

### Q2: `Parallel` 里的动画瞬间完成了，没有等待？
*   **错误写法**：
    ```csharp
    .Parallel((t) => MonoKit.Sequence(go).MoveTo(...).Start()) // ❌ 错误
    ```
    `.Start()` 是 "Fire-and-Forget"（发后即忘）的方法，它返回 `void`。`Parallel` 收到 `void` 后认为任务立即完成了。
*   **正确写法**：
    ```csharp
    .Parallel((t) => MonoKit.Sequence(go).MoveTo(...).Await()) // ✅ 正确
    ```
    `.Await()` 返回 `UniTask`，`Parallel` 会等待这个 Task 完成。

### Q3: 报错 `MissingReferenceException`？
*   虽然 ActionKit 会自动取消任务，但如果你在 `.Callback(() => ...)` 中引用了**其他**已经销毁的物体，依然会报错。
*   **建议**：在 Callback 中操作外部对象时，最好判空 `if (otherObj != null)`。

---

## 5. 扩展指南 (Extension)
如果你需要支持新的组件（比如 `TextMeshPro` 的颜色渐变），可以编写扩展方法：

```csharp
public static class MyTweenExtensions
{
    public static UniActionChain ColorTo(this UniActionChain chain, TextMeshProUGUI target, Color endColor, float duration)
    {
        // 向链条添加一个自定义任务
        chain.AppendTask(async (token) => 
        {
            if (target == null) return;
            // 复用 TweenKit 核心插值器
            await TweenKit.To(target.color, endColor, duration, (c) => 
            {
                if (target != null) target.color = c;
            }, Ease.Linear, token, chain.IsIgnoreTimeScale);
        });
        return chain;
    }
}
```