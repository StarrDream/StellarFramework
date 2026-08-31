# SaveKit / 存档系统

SaveKit 是按需导出的存档基础设施。它只理解 Slot、Snapshot 和 Section，不理解玩家、作物、世界或物品的业务语义。

## 导入

从框架开发工程导出 SaveKit.Core 会自动带上 LogKit 与 com.cysharp.unitask。Core 不依赖 TimeKit、ResKit、Addressables、HybridCLR 或 Newtonsoft。

需要 JSON 时再导出 SaveKit.NewtonsoftJson，需要编辑器诊断时再导出 SaveKit.Tools。导出器会合并依赖并生成导入说明。

## 最小用法

    using StellarFramework;

    public sealed class PlayerSaveSection : SaveSection<PlayerData>
    {
        public override SaveSectionId Id => SaveSectionId.From("player");
        public override int SchemaVersion => 1;

        public override PlayerData Capture(SaveCaptureContext context)
        {
            return new PlayerData { Level = PlayerModel.Level };
        }

        public override void Restore(PlayerData data, SaveRestoreContext context)
        {
            PlayerModel.Level = data.Level;
        }
    }

    SaveKit.Initialize();
    SaveKit.Register(new PlayerSaveSection());
    SaveResult result = await SaveKit.SaveAsync("slot-01");
    SaveResult loaded = await SaveKit.LoadAsync("slot-01");

普通业务不需要处理 Container Offset、临时文件或 Backup。

## Section 设计

一个 Section 是一个数据领域，而不是一个对象。大型模拟项目应使用紧凑 DTO、数组和稳定的整数 ID：

    public struct CropSaveRecord
    {
        public int CropTypeId;
        public int CellX;
        public int CellY;
        public long PlantTick;
        public byte Stage;
    }

不要直接持久化 GameObject、Transform、MonoBehaviour、UnityEngine.Object、Delegate、Task、Stream 或 TimerHandle。资源引用保存稳定 ID，读档后由业务重新解析。

## Serializer

Core 自带 UnityJsonSaveSerializer 和 RawBytesSaveSerializer。大型或高性能数据可注册自定义 ISaveSerializer，每个 Section 通过 SerializerId 独立选择格式。

Newtonsoft Adapter 使用固定的 TypeNameHandling.None，不会根据外部 $type 创建任意 CLR 类型：

    SaveKit.Initialize(builder => builder
        .UseSerializer(new StellarFramework.SaveKitAdapters.NewtonsoftJson.NewtonsoftJsonSaveSerializer())
        .SetDefaultSerializer("newtonsoft-json"));

## 事务、Backup 与 Recovery

保存流程是 Capture -> Serialize -> .tmp -> Flush -> Verify -> .sav。已有 current 会先进入 .bak，Commit 失败不会替换上一份成功存档。Load 会先验证 current，损坏时尝试健康的 Backup，并按选项自动恢复。

取消只在 Commit 开始前生效；进入 Commit 后会完成可恢复提交。V1 默认保留一个 Backup。

## Migration 与 Restore

Section 的 SchemaVersion 独立于 ContainerVersion 和游戏版本。注册连续的 1 -> 2 -> 3 Migration，SaveKit 会在 Load 的 Prepare 阶段执行：

    Read -> Checksum -> Deserialize -> Migration -> Validate
                                          |
                                   全部成功后 Restore

RestoreAfter 用 DAG 计算顺序，循环会在注册时被拒绝。Missing Section 可选择 Fail、UseDefault 或 Ignore；未知 Section 默认 Preserve，重新保存时原始 Payload 不会被反序列化或丢弃。

## 存档位置与安全

默认目录为 Application.persistentDataPath/Saves/，文件名为 <slot>.sav、<slot>.bak、<slot>.tmp。Slot ID、Section ID、字符串、数量、长度、Offset、Payload 和 ContainerVersion 都经过边界检查。外部文件必须视为不可信输入。

## ToolsHub

导入 SaveKit.Tools 后，Tools Hub 中出现“SaveKit 存档中心”，提供：

- Slot、Revision、大小、健康度和 Backup 状态
- 本地或外部存档只读 Inspector
- Section Schema、Serializer、Payload 长度和 Checksum
- 当前注册版本对照、Profiler 和 Diagnostics
- 外部存档 Import Copy、Backup 恢复和 Slot 删除

Open External Save 不会复制或修改原文件；Import Copy 默认不覆盖已有 Slot。

## 性能边界

SaveKit 空闲时无 Update、无常驻 Driver、无 Slot 扫描。Capture 在主线程完成，之后才进入序列化、Checksum、IO。ToolsHub 对外部文件受大小和预览限制；大型数据应使用流式自定义 Serializer 和紧凑 DTO。

## 常见误用

- 用一个全局 GameSaveData 包含所有业务。
- 为每条作物记录创建一个 Section。
- 把 TimerHandle、Unity 对象或服务实例写入存档。
- 在 Restore 中发网络请求或加载远端资源。
- 将 Checksum 当作加密或防作弊。
- 在没有 Migration 时强读旧 Schema。
