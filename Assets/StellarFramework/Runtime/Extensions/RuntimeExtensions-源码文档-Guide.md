# Runtime Extensions / 源码文档

Runtime 扩展方法是框架给 Unity 常用类型补的轻量工具层。它们不应该承载业务逻辑，只解决重复、低风险、可复用的小操作。

## 源码位置

- `Runtime/Core/CoroutineRunner.cs`：全局协程 Runner。
- `Runtime/Extensions/CollectionExtensions.cs`
- `Runtime/Extensions/ColorExtensions.cs`
- `Runtime/Extensions/CoroutineExtensions.cs`
- `Runtime/Extensions/GameObjectExtensions.cs`
- `Runtime/Extensions/LayerExtensions.cs`
- `Runtime/Extensions/RectTransformExtensions.cs`
- `Runtime/Extensions/StringExtensions.cs`
- `Runtime/Extensions/TransformExtensions.cs`
- `Runtime/Extensions/VectorExtensions.cs`

## 核心类型

- `CoroutineRunner`：继承 `MonoSingleton<CoroutineRunner>`，给非 MonoBehaviour 代码提供协程承载点。
- `CollectionExtensions`：集合安全遍历、判空、取值辅助。
- `ColorExtensions`：颜色转换和透明度调整。
- `CoroutineExtensions`：协程启动、停止和等待封装。
- `GameObjectExtensions`：GameObject 激活、组件获取、层级查找等常用操作。
- `LayerExtensions`：LayerMask 和 layer 判断辅助。
- `RectTransformExtensions`：UI 坐标、锚点、尺寸和布局辅助。
- `StringExtensions`：字符串判空、格式、转换辅助。
- `TransformExtensions`：Transform 层级查找、位置、子节点操作辅助。
- `VectorExtensions`：Vector2/3 常用计算辅助。

## 关键方法

扩展文件通常没有复杂状态。读源码时按这个顺序判断能否安全使用：

1. 看第一个参数类型，例如 `this Transform`、`this GameObject`。
2. 看 null 处理策略，确认是否会静默返回或抛错。
3. 看是否分配临时集合，频繁调用时注意 GC。
4. 看是否会修改 Transform、GameObject、RectTransform 状态。
5. 看方法是否只适合 Editor/调试，不要误用到热路径。

## 数据流

业务代码调用扩展方法后，扩展方法直接操作 Unity 对象或返回计算结果。扩展层不保存全局状态，除 `CoroutineRunner` 通过 SingletonKit 保留一个全局实例。

## 依赖关系

- 依赖 UnityEngine。
- `CoroutineRunner` 依赖 `SingletonKit`。
- UI 坐标相关扩展依赖 Unity UI 常用类型。
- 不依赖 ToolsHub、ResKit、UIKit 等业务 Kit。

## 扩展点

- 新增扩展文件时按 Unity 类型拆分，不要把所有工具塞进一个类。
- 扩展方法必须短小、可预测，避免隐藏耗时加载或资源释放。
- 会修改对象状态的方法，方法名要体现动作，例如 `Set...`、`Reset...`、`Destroy...`。
- 对热路径方法尽量避免 LINQ 和临时 List。

## 测试入口

- 扩展方法通常通过 Kit 样例间接覆盖。
- 修改集合、Transform、RectTransform 扩展时，应补 EditMode 单测或在对应 Kit 样例中验证。
- 修改 `CoroutineRunner` 时需要确认 `MonoSingleton<T>` 行为没有被破坏。
