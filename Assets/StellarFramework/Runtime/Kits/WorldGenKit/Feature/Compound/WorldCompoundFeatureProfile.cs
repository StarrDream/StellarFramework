using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldCompoundFeatureBinding
    {
        public WorldFeatureId FeatureId { get; }
        public WorldCompoundFeatureTemplate Template { get; }

        public WorldCompoundFeatureBinding(WorldFeatureId featureId, WorldCompoundFeatureTemplate template)
        {
            if (!featureId.IsValid) throw new ArgumentException("Feature ID must be valid.", nameof(featureId));
            Template = template ?? throw new ArgumentNullException(nameof(template));
            FeatureId = featureId;
        }
    }

    public sealed class WorldCompoundFeatureProfile
    {
        private readonly WorldCompoundFeatureBinding[] _bindings;

        public WorldCompoundFeatureProfile(ReadOnlySpan<WorldCompoundFeatureBinding> bindings)
        {
            _bindings = bindings.ToArray();
            for (int i = 0; i < _bindings.Length; i++)
            {
                for (int j = i + 1; j < _bindings.Length; j++)
                {
                    if (_bindings[i].FeatureId == _bindings[j].FeatureId)
                        throw new ArgumentException("Duplicate compound feature binding: " + _bindings[i].FeatureId, nameof(bindings));
                }
            }
        }

        public WorldCompiledCompoundFeatureProfile Compile(WorldFeatureCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            WorldCompoundFeatureTemplate[] templates = new WorldCompoundFeatureTemplate[catalog.Count];
            byte[] bound = new byte[catalog.Count];
            for (int i = 0; i < _bindings.Length; i++)
            {
                WorldCompoundFeatureBinding binding = _bindings[i];
                if (!catalog.TryGetIndex(binding.FeatureId, out int featureIndex))
                    throw new InvalidOperationException("Compound feature profile references unknown feature: " + binding.FeatureId);
                WorldFeatureDefinition definition = catalog.GetDefinition(featureIndex);
                if (definition.Kind != WorldFeatureKind.Compound)
                    throw new InvalidOperationException("Compound feature template can only bind to a Compound definition: " + binding.FeatureId);
                templates[featureIndex] = binding.Template;
                bound[featureIndex] = 1;
            }
            return new WorldCompiledCompoundFeatureProfile(templates, bound);
        }
    }

    public sealed class WorldCompiledCompoundFeatureProfile
    {
        private readonly WorldCompoundFeatureTemplate[] _templates;
        private readonly byte[] _bound;
        public int FeatureCount => _bound.Length;

        internal WorldCompiledCompoundFeatureProfile(WorldCompoundFeatureTemplate[] templates, byte[] bound)
        {
            _templates = templates;
            _bound = bound;
        }

        public bool TryGetTemplate(int featureIndex, out WorldCompoundFeatureTemplate template)
        {
            if ((uint)featureIndex >= (uint)_bound.Length) throw new ArgumentOutOfRangeException(nameof(featureIndex));
            if (_bound[featureIndex] == 0)
            {
                template = null;
                return false;
            }
            template = _templates[featureIndex];
            return true;
        }
    }
}
