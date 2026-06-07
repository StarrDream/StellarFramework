# Generated / 说明文档

`Generated` 目录存放框架工具或构建流程自动生成的 C# 代码。

当前主要生成内容：

- `AssetMap`
- `SingletonRegister`

使用规则：

- 可以阅读
- 不建议手动修改
- 真正要改逻辑时，应修改生成器，而不是直接修改生成结果

来源：

- AssetBundle 构建工具生成 `AssetMap`
- Singleton 生成器生成 `SingletonRegister`

相关文档：

- [Generated 源码文档](Generated-源码文档-Guide.md)
