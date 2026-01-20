# StellarFramework 异步编程指南：UniTask 实战手册

**版本**: v1.0  
**适用**: StellarFramework 所有模块

---

## 1. 为什么要用 UniTask？

在 StellarFramework 中，我们全面推荐使用 `UniTask` 替代 Unity 原生的 `Coroutine` (协程)。

*   **零 GC (Zero GC)**：原生协程的 `yield return new WaitForSeconds(...)` 会产生大量垃圾内存，而 UniTask 是基于结构体的，几乎没有内存分配。
*   **线性逻辑**：告别回调地狱，使用 `async/await` 让异步代码像同步代码一样易读。
*   **强类型返回值**：协程无法直接返回值，而 `UniTask<T>` 可以。
*   **生命周期安全**：完美解决“GameObject 销毁后协程还在跑导致报错”的问题。

---

## 2. 基础迁移：从协程到 UniTask

### 2.1 定义方法的区别

**❌ 传统协程 (Coroutine):**
```csharp
// 定义
IEnumerator MyCoroutine() {
    yield return null;
}
// 启动
StartCoroutine(MyCoroutine());
```

**✅ UniTask:**
```csharp
// 定义 (如果是入口方法，用 UniTaskVoid)
async UniTaskVoid MyAsyncMethod() {
    await UniTask.Yield();
}
// 启动
MyAsyncMethod().Forget();
```

### 2.2 常用等待对照表

| 功能 | 协程写法 | UniTask 写法 |
| :--- | :--- | :--- |
| 等待一帧 | `yield return null;` | `await UniTask.Yield();` |
| 等待下一帧(Update后) | `yield return new WaitForEndOfFrame();` | `await UniTask.WaitForEndOfFrame();` |
| 等待时间 | `yield return new WaitForSeconds(1f);` | `await UniTask.Delay(1000);` (毫秒) |
| 等待条件 | `yield return new WaitUntil(() => flag);` | `await UniTask.WaitUntil(() => flag);` |
| 等待物理帧 | `yield return new WaitForFixedUpdate();` | `await UniTask.WaitForFixedUpdate();` |

---

## 3. 进阶：UniTask 与协程的协同工作

在实际开发中，你可能需要使用旧的插件（基于协程）或渐进式重构。UniTask 提供了完美的互操作性。

### 3.1 在 UniTask 中等待协程
如果有一个旧的协程方法必须调用，可以使用 `.ToUniTask()` 将其转换为可等待对象。

```csharp
// 旧的协程
IEnumerator OldSystemInit() {
    yield return new WaitForSeconds(1f);
    Debug.Log("Old Init Done");
}

// 新的 UniTask 方法
async UniTask InitAll() {
    Debug.Log("Start");
    // 核心：将协程转为 UniTask 并等待
    await this.StartCoroutine(OldSystemInit()).ToUniTask(); 
    Debug.Log("End");
}
```

### 3.2 在协程中等待 UniTask
如果你的主流程还是协程，但想调用 StellarFramework 的异步 API。

```csharp
// StellarFramework 的异步方法
async UniTask<int> CalculateAsync() {
    await UniTask.Delay(1000);
    return 100;
}

IEnumerator MyCoroutine() {
    // 核心：使用 .ToCoroutine()
    var task = CalculateAsync().ToCoroutine(result => {
        Debug.Log($"计算结果: {result}");
    });
    yield return task;
}
```

---

## 4. 与框架 Kit 的深度联动

StellarFramework 的核心模块均已原生支持 UniTask。

### 4.1 与 ActionKit 联动 (动画序列)
`ActionKit` 的链式编程可以无缝融入 `await` 流程。

```csharp
async UniTask PlayEntranceAnim()
{
    // 1. 播放入场动画并等待其结束
    await MonoKit.Sequence(gameObject)
        .FadeTo(canvasGroup, 1f, 0.5f)
        .ScaleTo(transform, Vector3.one, 0.5f, Ease.OutBack)
        .Await(); // 注意：使用 .Await() 而不是 .Start()

    // 2. 动画播完后，才执行后续逻辑
    Debug.Log("动画播放完毕，开始加载数据...");
}
```

### 4.2 与 ResKit 联动 (资源加载)
`IResLoader` 提供了 `LoadAsync<T>` 接口。

```csharp
async UniTaskVoid LoadHero()
{
    var loader = ResKit.Allocate<ResourceLoader>();
    
    // 并行加载：同时加载模型和特效，等待两者都完成
    var (prefab, effect) = await UniTask.WhenAll(
        loader.LoadAsync<GameObject>("Hero"),
        loader.LoadAsync<GameObject>("Effect/Fire")
    );

    Instantiate(prefab);
    Instantiate(effect);
    
    // 记得回收 loader
    // ResKit.Recycle(loader); 
}
```

### 4.3 与 UIKit 联动 (界面管理)
`UIKit.OpenPanelAsync` 返回的是 `UniTask<T>`，这意味着你可以等待界面完全初始化并打开（包括播放完 `OnOpen` 里的入场动画）后，再继续逻辑。

```csharp
async void OnClickStartGame()
{
    // 打开 Loading 界面
    var loadingPanel = await UIKit.OpenPanelAsync<LoadingPanel>();
    
    // 模拟耗时操作
    await UniTask.Delay(2000);
    
    // 关闭 Loading，打开主界面
    loadingPanel.Close();
    await UIKit.OpenPanelAsync<MainPanel>();
}
```

---

## 5. 最佳实践与防坑指南

### 5.1 自动取消 (Cancellation)
这是 UniTask 最强大的功能。当 GameObject 销毁时，自动停止异步逻辑，防止空引用报错。

**推荐写法：**
```csharp
// 获取当前物体的 CancellationToken
var token = this.GetCancellationTokenOnDestroy();

// 传给异步方法
await UniTask.Delay(1000, cancellationToken: token);

// 如果在 1秒内物体被销毁，await 会抛出 OperationCanceledException，
// 后面的代码自动不再执行。
transform.position = Vector3.zero; // 安全！不会报错
```

### 5.2 按钮点击事件
Unity 的 Button 事件不支持 `async`，请使用 `UniTaskVoid` 配合 lambda。

```csharp
button.onClick.AddListener(() => OnClickAsync().Forget());

async UniTaskVoid OnClickAsync()
{
    button.interactable = false; // 防止连点
    await NetworkApiKit.PostAsync(...);
    button.interactable = true;
}
```

### 5.3 避免 `async void`
除了 Unity 事件回调（如 Start, Update, ButtonClick），**严禁**使用 `async void`。
*   **请使用**：`async UniTaskVoid` (无等待发后即忘) 或 `async UniTask` (可等待)。
*   **原因**：`async void` 发生的异常无法被 try-catch 捕获，会导致程序崩溃且难以定位。