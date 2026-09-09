using System;
using System.Collections.Generic;

namespace StellarFramework.FlowKit
{
    /// <summary>Graph JSON 的运行数据部分。该模型不包含任何运行态引用。</summary>
    [Serializable]
    public sealed class FlowGraphData
    {
        public string FlowId;
        public int SchemaVersion = 1;
        public string EntryNodeId;
        public List<FlowNodeData> Nodes = new List<FlowNodeData>();
        public List<FlowEdgeData> Edges = new List<FlowEdgeData>();
        public FlowGraphMetadata Metadata = new FlowGraphMetadata();
    }

    [Serializable]
    public sealed class FlowGraphMetadata
    {
        public string DisplayName;
        public string Description;
    }

    [Serializable]
    public sealed class FlowNodeData
    {
        public string Id;
        public string TypeId;
        public int DefinitionVersion = 1;
        public FlowPropertyBag Parameters = new FlowPropertyBag();
        public FlowCondition Condition;
        public FlowNodeOptions Options = new FlowNodeOptions();
    }

    [Serializable]
    public sealed class FlowNodeOptions
    {
        public bool Enabled = true;
        public bool ResolveParametersOnEnter = true;
    }

    [Serializable]
    public sealed class FlowEdgeData
    {
        public string FromNodeId;
        public string FromPortId;
        public string ToNodeId;
        public string ToPortId;
    }

    /// <summary>
    /// Graph 参数容器。它用于低频配置/编辑路径，运行时编译后使用不可变快照。
    /// </summary>
    [Serializable]
    public sealed class FlowPropertyBag
    {
        public List<FlowPropertyEntry> Entries = new List<FlowPropertyEntry>();

        public bool TryGet(string key, out FlowValue value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (Entries == null)
                {
                    value = FlowValue.None;
                    return false;
                }

                for (int i = 0; i < Entries.Count; i++)
                {
                    if (string.Equals(Entries[i].Key, key, StringComparison.Ordinal))
                    {
                        value = Entries[i].Value;
                        return true;
                    }
                }
            }

            value = FlowValue.None;
            return false;
        }

        public void Set(string key, FlowValue value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("FlowPropertyBag 的 key 不能为空。", nameof(key));
            }

            if (Entries == null) Entries = new List<FlowPropertyEntry>();
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].Key, key, StringComparison.Ordinal))
                {
                    FlowPropertyEntry entry = Entries[i];
                    entry.Value = value;
                    Entries[i] = entry;
                    return;
                }
            }

            Entries.Add(new FlowPropertyEntry { Key = key, Value = value });
        }

        public bool HasDuplicateKeys(out string duplicateKey)
        {
            if (Entries == null)
            {
                duplicateKey = null;
                return false;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                string key = Entries[i].Key;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                for (int j = i + 1; j < Entries.Count; j++)
                {
                    if (string.Equals(key, Entries[j].Key, StringComparison.Ordinal))
                    {
                        duplicateKey = key;
                        return true;
                    }
                }
            }

            duplicateKey = null;
            return false;
        }

        public FlowPropertyBagSnapshot CreateSnapshot()
        {
            int count = Entries == null ? 0 : Entries.Count;
            var values = new FlowPropertyEntry[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = Entries[i];
            }

            return new FlowPropertyBagSnapshot(values);
        }
    }

    [Serializable]
    public struct FlowPropertyEntry
    {
        public string Key;
        public FlowValue Value;
    }

    /// <summary>编译后节点参数的只读快照。</summary>
    public sealed class FlowPropertyBagSnapshot
    {
        private readonly FlowPropertyEntry[] _entries;

        internal FlowPropertyBagSnapshot(FlowPropertyEntry[] entries)
        {
            _entries = entries ?? Array.Empty<FlowPropertyEntry>();
        }

        public int Count => _entries.Length;

        public bool TryGet(string key, out FlowValue value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (string.Equals(_entries[i].Key, key, StringComparison.Ordinal))
                    {
                        value = _entries[i].Value;
                        return true;
                    }
                }
            }

            value = FlowValue.None;
            return false;
        }

        public FlowPropertyEntry GetAt(int index) => _entries[index];
    }

    public enum FlowPortDirection
    {
        Input,
        Output
    }

    public enum FlowNodeExecutionMode
    {
        Immediate,
        Completion,
        Operation
    }

    public enum FlowPortSemantic
    {
        Normal,
        Success,
        Failure,
        Cancelled,
        ConditionTrue,
        ConditionFalse,
        Timeout
    }

    [Serializable]
    public sealed class FlowPortDescriptor
    {
        public string Id { get; }
        public string DisplayName { get; }
        public FlowPortDirection Direction { get; }
        public FlowPortSemantic Semantic { get; }
        public bool RecommendedRoute { get; }

        public FlowPortDescriptor(string id, string displayName, FlowPortDirection direction,
            FlowPortSemantic semantic = FlowPortSemantic.Normal, bool recommendedRoute = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("PortId 不能为空。", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            Direction = direction;
            Semantic = semantic;
            RecommendedRoute = recommendedRoute;
        }
    }

    [Serializable]
    public sealed class FlowPropertyDescriptor
    {
        private readonly string[] _allowedStringValues;
        private readonly IReadOnlyList<string> _allowedStringValuesView;

        public string Key { get; }
        public string DisplayName { get; }
        public FlowValueKind ValueKind { get; }
        public bool Required { get; }
        public IReadOnlyList<string> AllowedStringValues => _allowedStringValuesView;

        public FlowPropertyDescriptor(
            string key,
            string displayName,
            FlowValueKind valueKind,
            bool required,
            string[] allowedStringValues = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Property key 不能为空。", nameof(key));
            }

            Key = key;
            DisplayName = string.IsNullOrEmpty(displayName) ? key : displayName;
            ValueKind = valueKind;
            Required = required;
            _allowedStringValues = allowedStringValues == null ? Array.Empty<string>() : (string[])allowedStringValues.Clone();
            for (int i = 0; i < _allowedStringValues.Length; i++)
            {
                if (string.IsNullOrEmpty(_allowedStringValues[i]))
                    throw new ArgumentException("Allowed string values cannot contain empty entries.", nameof(allowedStringValues));
            }
            _allowedStringValuesView = Array.AsReadOnly(_allowedStringValues);
        }
    }

    /// <summary>Runtime 注册表使用的节点描述，不包含 Running/Timer/Subscription 等运行态。</summary>
    public sealed class FlowNodeDescriptor
    {
        private readonly FlowPortDescriptor[] _ports;
        private readonly FlowPropertyDescriptor[] _properties;
        private readonly FlowCapabilityId[] _requiredCapabilities;
        private readonly IReadOnlyList<FlowPortDescriptor> _portsView;
        private readonly IReadOnlyList<FlowPropertyDescriptor> _propertiesView;
        private readonly IReadOnlyList<FlowCapabilityId> _requiredCapabilitiesView;

        public FlowNodeTypeId TypeId { get; }
        public int Version { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public FlowNodeExecutionMode ExecutionMode { get; }
        public FlowEffectSemantics EffectSemantics { get; }
        public bool CompletesFlow { get; }
        public bool AllowAdditionalProperties { get; }
        public bool RequiresCondition { get; }
        public IReadOnlyList<FlowPortDescriptor> Ports => _portsView;
        public IReadOnlyList<FlowPropertyDescriptor> Properties => _propertiesView;
        public IReadOnlyList<FlowCapabilityId> RequiredCapabilities => _requiredCapabilitiesView;

        public FlowNodeDescriptor(
            FlowNodeTypeId typeId,
            int version,
            string displayName,
            string category,
            FlowNodeExecutionMode executionMode,
            FlowPortDescriptor[] ports,
            FlowPropertyDescriptor[] properties = null,
            FlowCapabilityId[] requiredCapabilities = null,
            FlowEffectSemantics effectSemantics = FlowEffectSemantics.None,
            bool completesFlow = false,
            bool allowAdditionalProperties = false,
            bool requiresCondition = false)
        {
            if (!typeId.IsValid)
            {
                throw new ArgumentException("节点 TypeId 不能为空。", nameof(typeId));
            }

            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            TypeId = typeId;
            Version = version;
            DisplayName = string.IsNullOrEmpty(displayName) ? typeId.Value : displayName;
            Category = category ?? string.Empty;
            ExecutionMode = executionMode;
            EffectSemantics = effectSemantics;
            CompletesFlow = completesFlow;
            AllowAdditionalProperties = allowAdditionalProperties;
            RequiresCondition = requiresCondition;
            _ports = ports == null ? Array.Empty<FlowPortDescriptor>() : (FlowPortDescriptor[])ports.Clone();
            _properties = properties == null ? Array.Empty<FlowPropertyDescriptor>() : (FlowPropertyDescriptor[])properties.Clone();
            _requiredCapabilities = requiredCapabilities == null ? Array.Empty<FlowCapabilityId>() : (FlowCapabilityId[])requiredCapabilities.Clone();
            var portIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _ports.Length; i++)
            {
                if (_ports[i] == null) throw new ArgumentException("Port descriptor 不能为 null。", nameof(ports));
                if (!portIds.Add(_ports[i].Id + "\u001f" + (int)_ports[i].Direction))
                    throw new ArgumentException("同一方向的 PortId 不能重复。", nameof(ports));
            }

            var propertyKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _properties.Length; i++)
            {
                if (_properties[i] == null) throw new ArgumentException("Property descriptor 不能为 null。", nameof(properties));
                if (!propertyKeys.Add(_properties[i].Key))
                    throw new ArgumentException("Property key 不能重复。", nameof(properties));
            }

            for (int i = 0; i < _requiredCapabilities.Length; i++)
            {
                if (!_requiredCapabilities[i].IsValid)
                    throw new ArgumentException("Required CapabilityId 不能为空。", nameof(requiredCapabilities));
            }
            _portsView = Array.AsReadOnly(_ports);
            _propertiesView = Array.AsReadOnly(_properties);
            _requiredCapabilitiesView = Array.AsReadOnly(_requiredCapabilities);
        }

        public bool TryGetPort(string portId, FlowPortDirection direction, out FlowPortDescriptor descriptor)
        {
            for (int i = 0; i < _ports.Length; i++)
            {
                if (_ports[i].Direction == direction && string.Equals(_ports[i].Id, portId, StringComparison.Ordinal))
                {
                    descriptor = _ports[i];
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        public bool TryGetProperty(string key, out FlowPropertyDescriptor descriptor)
        {
            for (int i = 0; i < _properties.Length; i++)
            {
                if (string.Equals(_properties[i].Key, key, StringComparison.Ordinal))
                {
                    descriptor = _properties[i];
                    return true;
                }
            }

            descriptor = null;
            return false;
        }
    }
}
