using System;
using System.Collections.Generic;
using System.Linq;

namespace StellarFramework
{
    public interface ISaveSection
    {
        SaveSectionId Id { get; }
        int SchemaVersion { get; }
        string SerializerId { get; }
        MissingSectionPolicy MissingPolicy { get; }
        IReadOnlyList<SaveSectionId> RestoreAfter { get; }
        Type DataType { get; }
        object CaptureUntyped(SaveCaptureContext context);
        object CreateDefaultUntyped(SaveRestoreContext context);
        SaveValidationResult ValidateUntyped(object data, SaveValidationContext context);
        void RestoreUntyped(object data, SaveRestoreContext context);
    }

    public interface ISaveSection<TData> : ISaveSection
    {
        TData Capture(SaveCaptureContext context);
        TData CreateDefault(SaveRestoreContext context);
        SaveValidationResult Validate(TData data, SaveValidationContext context);
        void Restore(TData data, SaveRestoreContext context);
    }

    public abstract class SaveSection<TData> : ISaveSection<TData>
    {
        private readonly IReadOnlyList<SaveSectionId> _restoreAfter;

        public abstract SaveSectionId Id { get; }
        public virtual int SchemaVersion => 1;
        public virtual string SerializerId => SaveKit.DefaultSerializerId;
        public virtual MissingSectionPolicy MissingPolicy => MissingSectionPolicy.Fail;
        public virtual IReadOnlyList<SaveSectionId> RestoreAfter => _restoreAfter;
        public Type DataType => typeof(TData);

        protected SaveSection(IEnumerable<SaveSectionId> restoreAfter = null)
        {
            _restoreAfter = restoreAfter == null
                ? Array.Empty<SaveSectionId>()
                : restoreAfter.Where(id => id.IsValid).Distinct().ToArray();
        }

        public abstract TData Capture(SaveCaptureContext context);

        public virtual TData CreateDefault(SaveRestoreContext context)
        {
            return default(TData);
        }

        public virtual SaveValidationResult Validate(TData data, SaveValidationContext context)
        {
            return SaveValidationResult.Valid();
        }

        public abstract void Restore(TData data, SaveRestoreContext context);

        object ISaveSection.CaptureUntyped(SaveCaptureContext context) => Capture(context);
        object ISaveSection.CreateDefaultUntyped(SaveRestoreContext context) => CreateDefault(context);
        SaveValidationResult ISaveSection.ValidateUntyped(object data, SaveValidationContext context)
        {
            if (data == null && typeof(TData).IsValueType)
            {
                return Validate(default(TData), context);
            }

            if (data != null && !(data is TData))
            {
                return SaveValidationResult.Invalid("DataTypeMismatch", $"Section {Id} 收到的数据类型为 {data.GetType().FullName}，预期 {typeof(TData).FullName}。" );
            }

            return Validate((TData)data, context);
        }

        void ISaveSection.RestoreUntyped(object data, SaveRestoreContext context)
        {
            Restore(data == null && typeof(TData).IsValueType ? default(TData) : (TData)data, context);
        }
    }

    public sealed class SaveSectionRegistry
    {
        private readonly Dictionary<string, ISaveSection> _sections =
            new Dictionary<string, ISaveSection>(StringComparer.Ordinal);

        public IReadOnlyCollection<ISaveSection> Sections => _sections.Values;
        public int Count => _sections.Count;

        public bool TryRegister(ISaveSection section, out string error)
        {
            error = null;
            if (section == null)
            {
                error = "Section 不能为空。";
                return false;
            }

            if (!section.Id.IsValid)
            {
                error = "Section ID 非法。";
                return false;
            }

            if (section.SchemaVersion < 1)
            {
                error = $"Section {section.Id} 的 SchemaVersion 必须大于 0。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(section.SerializerId) || section.SerializerId.Length > 128)
            {
                error = $"Section {section.Id} 的 SerializerId 非法。";
                return false;
            }

            if (_sections.ContainsKey(section.Id.Value))
            {
                error = $"Section {section.Id} 已注册。";
                return false;
            }

            _sections.Add(section.Id.Value, section);
            if (!TryGetRestoreOrder(out IReadOnlyList<ISaveSection> ignored, out error))
            {
                _sections.Remove(section.Id.Value);
                return false;
            }

            return true;
        }

        public bool Unregister(SaveSectionId id) => id.IsValid && _sections.Remove(id.Value);

        public bool TryGet(SaveSectionId id, out ISaveSection section)
        {
            return _sections.TryGetValue(id.Value ?? string.Empty, out section);
        }

        public bool TryGetRestoreOrder(out IReadOnlyList<ISaveSection> order, out string error)
        {
            error = null;
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (ISaveSection section in _sections.Values)
            {
                indegree[section.Id.Value] = 0;
                outgoing[section.Id.Value] = new List<string>();
            }

            foreach (ISaveSection section in _sections.Values)
            {
                IReadOnlyList<SaveSectionId> dependencies = section.RestoreAfter ?? Array.Empty<SaveSectionId>();
                foreach (SaveSectionId dependency in dependencies)
                {
                    if (!dependency.IsValid || !_sections.ContainsKey(dependency.Value))
                    {
                        continue;
                    }

                    outgoing[dependency.Value].Add(section.Id.Value);
                    indegree[section.Id.Value]++;
                }
            }

            var queue = new Queue<string>(_sections.Values
                .Where(section => indegree[section.Id.Value] == 0)
                .OrderBy(section => section.Id.Value, StringComparer.Ordinal)
                .Select(section => section.Id.Value));
            var sorted = new List<ISaveSection>(_sections.Count);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                sorted.Add(_sections[id]);
                foreach (string next in outgoing[id].OrderBy(value => value, StringComparer.Ordinal))
                {
                    indegree[next]--;
                    if (indegree[next] == 0)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            if (sorted.Count != _sections.Count)
            {
                order = Array.Empty<ISaveSection>();
                error = "RestoreAfter 存在循环依赖。";
                return false;
            }

            order = sorted;
            return true;
        }
    }

    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        object Migrate(object data, SaveMigrationContext context);
    }

    public abstract class SaveMigration<TFrom, TTo> : ISaveMigration
    {
        public abstract int FromVersion { get; }
        public abstract int ToVersion { get; }
        public abstract TTo Migrate(TFrom data, SaveMigrationContext context);

        object ISaveMigration.Migrate(object data, SaveMigrationContext context)
        {
            if (data != null && !(data is TFrom))
            {
                throw new InvalidCastException($"Migration 需要 {typeof(TFrom).FullName}，实际收到 {data.GetType().FullName}。" );
            }

            return Migrate(data == null ? default(TFrom) : (TFrom)data, context);
        }
    }

    public sealed class SaveMigrationRegistry
    {
        private readonly Dictionary<string, List<ISaveMigration>> _migrations =
            new Dictionary<string, List<ISaveMigration>>(StringComparer.Ordinal);

        public bool TryRegister(SaveSectionId sectionId, ISaveMigration migration, out string error)
        {
            error = null;
            if (!sectionId.IsValid || migration == null || migration.FromVersion < 1 || migration.ToVersion <= migration.FromVersion)
            {
                error = "Migration 或 Section ID 非法，且迁移版本必须递增。";
                return false;
            }

            List<ISaveMigration> list;
            if (!_migrations.TryGetValue(sectionId.Value, out list))
            {
                list = new List<ISaveMigration>();
                _migrations.Add(sectionId.Value, list);
            }

            if (list.Any(existing => existing.FromVersion == migration.FromVersion))
            {
                error = $"Section {sectionId} 的 Migration {migration.FromVersion} 已存在。";
                return false;
            }

            list.Add(migration);
            return true;
        }

        public bool TryBuildChain(SaveSectionId sectionId, int fromVersion, int toVersion,
            out IReadOnlyList<ISaveMigration> chain, out string error)
        {
            chain = Array.Empty<ISaveMigration>();
            error = null;
            if (fromVersion == toVersion)
            {
                return true;
            }

            List<ISaveMigration> list;
            if (!_migrations.TryGetValue(sectionId.Value, out list))
            {
                error = $"Section {sectionId} 缺少 {fromVersion} -> {toVersion} 的 Migration。";
                return false;
            }

            var result = new List<ISaveMigration>();
            int current = fromVersion;
            var visited = new HashSet<int>();
            while (current < toVersion)
            {
                if (!visited.Add(current))
                {
                    error = $"Section {sectionId} 的 Migration 链存在循环。";
                    return false;
                }

                ISaveMigration next = list.FirstOrDefault(migration => migration.FromVersion == current);
                if (next == null || next.ToVersion > toVersion)
                {
                    error = $"Section {sectionId} 缺少 {current} -> {toVersion} 的连续 Migration。";
                    return false;
                }

                result.Add(next);
                current = next.ToVersion;
            }

            if (current != toVersion)
            {
                error = $"Section {sectionId} 的 Migration 未到达目标版本 {toVersion}。";
                return false;
            }

            chain = result;
            return true;
        }
    }
}
