# SaveKit 示例

该示例演示一个纯 DTO Section 的注册、保存和加载。业务对象只在 Capture/Restore 中读写字段，存档容器、校验、临时文件和 Backup 由 SaveKit Core 处理。

示例不依赖 TimeKit、ResKit、Addressables 或 HybridCLR。将 SaveKit 导入业务项目后，可在任意 MonoBehaviour 中调用 SaveSampleAsync 和 LoadSampleAsync。
