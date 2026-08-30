# Runtime Extensions / 源码文档

## 模块职责

`Runtime Extensions` 提供面向 Unity 常用类型的轻量扩展方法和协程辅助能力。

它的目标是：

- 减少重复样板代码
- 提供低风险、小颗粒的帮助方法
- 避免把复杂业务逻辑塞进工具扩展层

## 源码文件

主要文件包括：

- `CollectionExtensions.cs`
- `ColorExtensions.cs`
- `CoroutineExtensions.cs`
- `GameObjectExtensions.cs`
- `LayerExtensions.cs`
- `RectTransformExtensions.cs`
- `StringExtensions.cs`
- `TransformExtensions.cs`
- `VectorExtensions.cs`
- `RenderPipelineCompatibility.cs`

## 总体结构

```text
Runtime Extensions
├─ Collection / String / Vector / Color
├─ Transform / RectTransform / GameObject / Layer
├─ CoroutineExtensions
└─ RenderPipelineCompatibility
```

## 类型详解

## `IDeepCopyable<T>`

### 作用

为集合扩展中的深拷贝约定提供接口。

### 成员

- 通常约定具体实现者提供可深拷贝能力

## `CollectionExtensions`

### 作用

集合辅助扩展。

常见用途：

- 安全遍历
- 空集合判断
- 集合复制
- 查找辅助

## `ColorExtensions`

### 作用

颜色辅助扩展。

常见用途：

- 修改 Alpha
- 颜色格式转换

## `LayerExtensions`

### 作用

Layer 和 `LayerMask` 辅助。

常见用途：

- Layer 判断
- Mask 拼装或匹配

## `StringExtensions`

### 作用

字符串辅助扩展。

常见用途：

- 格式化
- 判空或转换
- 小型字符串处理

## `VectorExtensions`

### 作用

向量辅助扩展。

常见用途：

- `Vector2 / Vector3` 的便捷计算
- 小型插值、偏移、取整等辅助

## `RectTransformExtensions`

### 作用

UI 布局扩展。

常见用途：

- 拉伸填满父节点
- 重置锚点和偏移
- 快速设置位置、尺寸

这是 UIKit 和样例 UI 中经常依赖的一层。

## `TransformExtensions`

### 作用

`Transform` 辅助扩展。

### 内部类型 `TransformStruct`

用于打包：

- `position`
- `rotation`
- `scale`

可用于保存和恢复局部变换状态。

## `GameObjectExtensions`

### 作用

`GameObject` 扩展。

常见用途：

- 组件获取辅助
- 子节点查找
- 批量层级处理
- 激活 / 销毁辅助

### 内部类型 `TransformDepth`

用于某些层级遍历和排序逻辑。

### 内部桥接 `UnityEditorInternalBridge`

用于 Editor 环境下的兼容处理。

## `RenderPipelineCompatibility`

### 作用

处理运行时渲染管线兼容性。

### 类型

- `FrameworkRenderPipelineFamily`
- `RenderPipelineCompatibility`

它用于帮助框架和样例根据当前渲染管线做兼容判断。

## `CoroutineHandle`

### 作用

`CoroutineExtensions` 中的协程句柄包装。

用于：

- 持有协程引用
- 停止协程
- 和取消触发器协作

## `CoroutineCancellationTrigger`

### 作用

协程取消辅助组件。

通常挂在 `GameObject` 上，在生命周期结束时终止相关协程。

## 设计约束

- 扩展方法必须短小、明确、可预测
- 不应在扩展层偷偷引入复杂依赖
- 热路径扩展应尽量避免额外分配
- 修改对象状态的方法要有明确命名和意图

## 常见误用

- 在扩展层塞入复杂业务逻辑
- 在高频调用中隐式分配临时集合

## 测试与验证

- 高频扩展的空值处理
- `RectTransform` 扩展的常见 UI 场景
- 渲染管线兼容判断是否符合预期
