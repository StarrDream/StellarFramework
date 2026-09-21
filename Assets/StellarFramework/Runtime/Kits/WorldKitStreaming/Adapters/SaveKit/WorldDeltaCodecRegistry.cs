using System;
using System.Collections.Generic;
using StellarFramework.WorldKit;

namespace StellarFramework.WorldKit.Streaming.SaveKitAdapter
{
    public interface IWorldDeltaCodec
    {
        WorldDeltaTypeId TypeId { get; }
        bool TryEncode(IWorldDelta delta, out string payload, out string error);
        bool TryDecode(
            WorldDeltaTarget target,
            WorldDeltaVersion version,
            string payload,
            out IWorldDelta delta,
            out string error);
    }

    public sealed class WorldDeltaCodecRegistry
    {
        private readonly Dictionary<WorldDeltaTypeId, IWorldDeltaCodec> _codecs =
            new Dictionary<WorldDeltaTypeId, IWorldDeltaCodec>();

        public int Count => _codecs.Count;

        public bool TryRegister(IWorldDeltaCodec codec, out string error)
        {
            error = null;
            if (codec == null)
            {
                error = "World delta codec cannot be null.";
                return false;
            }
            if (!codec.TypeId.IsValid)
            {
                error = "World delta codec TypeId must be valid.";
                return false;
            }
            if (_codecs.ContainsKey(codec.TypeId))
            {
                error = "Duplicate world delta codec TypeId: " + codec.TypeId + ".";
                return false;
            }

            _codecs.Add(codec.TypeId, codec);
            return true;
        }

        public bool TryGet(WorldDeltaTypeId typeId, out IWorldDeltaCodec codec)
        {
            codec = null;
            return typeId.IsValid && _codecs.TryGetValue(typeId, out codec);
        }
    }
}
