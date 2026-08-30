# Runtime Extensions / 说明文档

`Runtime Extensions` 提供面向 Unity 常用类型的轻量扩展方法。

这部分内容的定位是：

- 减少重复代码
- 提供小而稳定的运行时辅助方法
- 不承载业务逻辑

主要范围：

- 集合辅助
- 字符串辅助
- `Transform / RectTransform / GameObject` 辅助
- 颜色、向量、层级辅助
- 协程扩展和协程句柄

使用约束：

- 扩展方法应当短小、明确、可预测
- 不应在这里塞入复杂流程或业务规则

`CoroutineRunner` 已独立为 `Runtime Tools` 的协程承载工具，不属于扩展方法层。

相关文档：

- [Runtime Extensions 源码文档](RuntimeExtensions-源码文档-Guide.md)
