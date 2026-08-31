using System;

namespace StellarFramework.Samples.SaveKit
{
    internal static class SaveKitSampleIds
    {
        public static readonly SaveSectionId World = SaveSectionId.From("sample.world");
        public static readonly SaveSectionId Player = SaveSectionId.From("sample.player");
    }

    /// <summary>存档数据只包含可序列化 DTO，不把 MonoBehaviour 或 Unity 对象放入容器。</summary>
    [Serializable]
    public sealed class SaveKitSampleWorldData
    {
        public long WorldTick;
        public int WeatherSeed;
    }

    [Serializable]
    public sealed class SaveKitSamplePlayerDataV1
    {
        public int Coins;
    }

    [Serializable]
    public sealed class SaveKitSamplePlayerDataV2
    {
        public long Money;
        public int Level;
    }

    /// <summary>世界 Section 先恢复，为玩家 Section 提供已恢复的世界状态。</summary>
    public sealed class SaveKitSampleWorldSection : SaveSection<SaveKitSampleWorldData>
    {
        private readonly SaveKitExample _owner;

        public SaveKitSampleWorldSection(SaveKitExample owner)
        {
            _owner = owner;
        }

        public override SaveSectionId Id => SaveKitSampleIds.World;

        public override SaveKitSampleWorldData Capture(SaveCaptureContext context)
        {
            return new SaveKitSampleWorldData
            {
                WorldTick = _owner.WorldTick,
                WeatherSeed = _owner.WeatherSeed
            };
        }

        public override SaveValidationResult Validate(
            SaveKitSampleWorldData data,
            SaveValidationContext context)
        {
            if (data == null)
            {
                return SaveValidationResult.Invalid("WorldDataMissing", "世界数据不能为空。");
            }

            if (data.WorldTick < 0)
            {
                return SaveValidationResult.Invalid("WorldTickInvalid", "WorldTick 不能为负数。");
            }

            return SaveValidationResult.Valid();
        }

        public override void Restore(SaveKitSampleWorldData data, SaveRestoreContext context)
        {
            _owner.WorldTick = data == null ? 0L : data.WorldTick;
            _owner.WeatherSeed = data == null ? 0 : data.WeatherSeed;
            _owner.RecordRestore("sample.world");
        }
    }

    /// <summary>当前玩家 DTO 为 V2，并声明依赖世界 Section 的恢复顺序。</summary>
    public sealed class SaveKitSamplePlayerSection : SaveSection<SaveKitSamplePlayerDataV2>
    {
        private readonly SaveKitExample _owner;

        public SaveKitSamplePlayerSection(SaveKitExample owner)
            : base(new[] { SaveKitSampleIds.World })
        {
            _owner = owner;
        }

        public override SaveSectionId Id => SaveKitSampleIds.Player;
        public override int SchemaVersion => 2;

        public override SaveKitSamplePlayerDataV2 Capture(SaveCaptureContext context)
        {
            return new SaveKitSamplePlayerDataV2
            {
                Money = _owner.Money,
                Level = _owner.Level
            };
        }

        public override SaveValidationResult Validate(
            SaveKitSamplePlayerDataV2 data,
            SaveValidationContext context)
        {
            if (data == null)
            {
                return SaveValidationResult.Invalid("PlayerDataMissing", "玩家数据不能为空。");
            }

            if (data.Money < 0L)
            {
                return SaveValidationResult.Invalid("MoneyInvalid", "Money 不能为负数。");
            }

            if (data.Level < 1)
            {
                return SaveValidationResult.Invalid("LevelInvalid", "Level 必须从 1 开始。");
            }

            return SaveValidationResult.Valid();
        }

        public override void Restore(SaveKitSamplePlayerDataV2 data, SaveRestoreContext context)
        {
            _owner.Money = data == null ? 0L : data.Money;
            _owner.Level = data == null ? 1 : data.Level;
            _owner.RecordRestore("sample.player");
        }
    }

    /// <summary>仅用于生成可迁移的 V1 存档，生产注册使用 V2 Section。</summary>
    public sealed class SaveKitSampleLegacyPlayerSection : SaveSection<SaveKitSamplePlayerDataV1>
    {
        private readonly SaveKitExample _owner;

        public SaveKitSampleLegacyPlayerSection(SaveKitExample owner)
        {
            _owner = owner;
        }

        public override SaveSectionId Id => SaveKitSampleIds.Player;

        public override SaveKitSamplePlayerDataV1 Capture(SaveCaptureContext context)
        {
            return new SaveKitSamplePlayerDataV1 { Coins = _owner.LegacyCoins };
        }

        public override SaveValidationResult Validate(
            SaveKitSamplePlayerDataV1 data,
            SaveValidationContext context)
        {
            if (data == null)
            {
                return SaveValidationResult.Invalid("LegacyPlayerMissing", "V1 玩家数据不能为空。");
            }

            return data.Coins < 0
                ? SaveValidationResult.Invalid("CoinsInvalid", "V1 Coins 不能为负数。")
                : SaveValidationResult.Valid();
        }

        public override void Restore(SaveKitSamplePlayerDataV1 data, SaveRestoreContext context)
        {
            _owner.LegacyCoins = data == null ? 0 : data.Coins;
            _owner.RecordRestore("sample.player-v1");
        }
    }

    /// <summary>真实的 V1 -> V2 DTO 迁移：Coins 扩展为 long Money，并补齐 Level。</summary>
    public sealed class SaveKitSamplePlayerV1ToV2Migration
        : SaveMigration<SaveKitSamplePlayerDataV1, SaveKitSamplePlayerDataV2>
    {
        public override SaveSectionId SectionId => SaveKitSampleIds.Player;
        public override int FromVersion => 1;
        public override int ToVersion => 2;

        public override SaveKitSamplePlayerDataV2 Migrate(
            SaveKitSamplePlayerDataV1 data,
            SaveMigrationContext context)
        {
            return new SaveKitSamplePlayerDataV2
            {
                Money = data == null ? 0L : data.Coins,
                Level = 1
            };
        }
    }
}
