using System;
using System.Collections.Generic;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public sealed class WorldGenerationAuthoringCompileResult
    {
        private readonly string[] _messages;

        public bool Success { get; }
        public WorldGenerationPlan Plan { get; }
        public WorldPlanarSampleLayout Layout { get; }
        public IReadOnlyList<string> Messages => _messages;

        internal WorldGenerationAuthoringCompileResult(
            bool success,
            WorldGenerationPlan plan,
            WorldPlanarSampleLayout layout,
            string[] messages)
        {
            Success = success;
            Plan = plan;
            Layout = layout;
            _messages = messages ?? Array.Empty<string>();
        }
    }

    public static class WorldGenerationAuthoringCompiler
    {
        private sealed class Handles
        {
            internal readonly Dictionary<string, ChannelHandle<float>> Float =
                new Dictionary<string, ChannelHandle<float>>(StringComparer.Ordinal);
            internal readonly Dictionary<string, ChannelHandle<int>> Int =
                new Dictionary<string, ChannelHandle<int>>(StringComparer.Ordinal);
            internal readonly Dictionary<string, ChannelHandle<byte>> Byte =
                new Dictionary<string, ChannelHandle<byte>>(StringComparer.Ordinal);
        }

        public static WorldGenerationAuthoringCompileResult Compile(WorldGenerationAuthoringProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            List<string> messages = new List<string>();

            if (!WorldRuleId.TryCreate(profile.ProfileId, out _, out string profileIdError))
                messages.Add("ProfileId: " + profileIdError);
            if (profile.ProfileVersion <= 0)
                messages.Add("ProfileVersion must be greater than zero.");

            WorldPlanarSampleLayout layout = default(WorldPlanarSampleLayout);
            try
            {
                layout = new WorldPlanarSampleLayout(profile.Width, profile.Height, profile.SampleStep);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                messages.Add("Layout: " + exception.Message);
            }

            if (messages.Count > 0)
                return Failed(layout, messages);

            WorldGenerationPipelineBuilder builder = new WorldGenerationPipelineBuilder();
            Handles handles = new Handles();
            RegisterChannels(profile, builder, handles, messages);
            if (messages.Count > 0)
                return Failed(layout, messages);

            AddTerrainStages(profile.Terrain, layout, builder, handles, messages);
            if (messages.Count > 0)
                return Failed(layout, messages);

            AddBiomeSurfaceAndBuildableStages(
                profile.Terrain,
                profile.BiomeSurface,
                profile.Buildable,
                layout,
                builder,
                handles,
                messages);
            if (messages.Count > 0)
                return Failed(layout, messages);

            WorldGenerationCompileResult compile;
            try
            {
                compile = builder.Compile();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                messages.Add("Pipeline: " + exception.Message);
                return Failed(layout, messages);
            }

            for (int i = 0; i < compile.Diagnostics.Length; i++)
            {
                WorldGenerationDiagnostic diagnostic = compile.Diagnostics[i];
                messages.Add(
                    $"{diagnostic.Severity}: {diagnostic.Code.Value} stage={diagnostic.StageId.Value} channel={diagnostic.ChannelId.Value} — {diagnostic.Message}");
            }

            return new WorldGenerationAuthoringCompileResult(
                compile.Success,
                compile.Plan,
                layout,
                messages.ToArray());
        }

        private static void RegisterChannels(
            WorldGenerationAuthoringProfile profile,
            WorldGenerationPipelineBuilder builder,
            Handles handles,
            List<string> messages)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IList<WorldAuthoringChannelDefinition> channels = profile.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                WorldAuthoringChannelDefinition channel = channels[i];
                if (channel == null)
                {
                    messages.Add($"Channel[{i}] is null.");
                    continue;
                }
                if (!WorldDataChannelId.TryCreate(channel.Id, out WorldDataChannelId id, out string error))
                {
                    messages.Add($"Channel[{i}] ID: {error}");
                    continue;
                }
                if (!ids.Add(id.Value))
                {
                    messages.Add($"Channel[{i}] duplicates ID '{id.Value}'.");
                    continue;
                }

                WorldChannelStorageDescriptor storage;
                try
                {
                    storage = new WorldChannelStorageDescriptor(channel.StorageKind, channel.Scope);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    messages.Add($"Channel[{i}] storage: {exception.Message}");
                    continue;
                }

                try
                {
                    switch (channel.ValueType)
                    {
                        case WorldAuthoringChannelValueType.Float:
                            handles.Float.Add(
                                id.Value,
                                builder.Channels.Register<float>(id, storage, channel.SourceMode));
                            break;
                        case WorldAuthoringChannelValueType.Int:
                            handles.Int.Add(
                                id.Value,
                                builder.Channels.Register<int>(id, storage, channel.SourceMode));
                            break;
                        case WorldAuthoringChannelValueType.Byte:
                            handles.Byte.Add(
                                id.Value,
                                builder.Channels.Register<byte>(id, storage, channel.SourceMode));
                            break;
                        default:
                            messages.Add($"Channel[{i}] has unknown value type '{channel.ValueType}'.");
                            break;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    messages.Add($"Channel[{i}] registration: {exception.Message}");
                }
            }
        }

        private static void AddTerrainStages(
            WorldTerrainPipelineAuthoringSettings terrain,
            WorldPlanarSampleLayout layout,
            WorldGenerationPipelineBuilder builder,
            Handles handles,
            List<string> messages)
        {
            if (terrain == null)
            {
                messages.Add("Terrain pipeline settings are missing.");
                return;
            }

            if (terrain.HeightEnabled)
            {
                if (TryFloat(handles, terrain.HeightChannel, "Height output", messages, out ChannelHandle<float> height) &&
                    TryNoise(terrain.HeightNoise, "Height noise", messages, out WorldFractalNoiseSettings noise))
                {
                    try
                    {
                        builder.AddStage(new WorldHeightStage(
                            height,
                            layout,
                            noise,
                            terrain.MinHeight,
                            terrain.MaxHeight));
                    }
                    catch (ArgumentException exception)
                    {
                        messages.Add("Height stage: " + exception.Message);
                    }
                }
            }

            if (terrain.MoistureEnabled)
            {
                if (TryFloat(handles, terrain.MoistureChannel, "Moisture output", messages, out ChannelHandle<float> moisture) &&
                    TryNoise(terrain.MoistureNoise, "Moisture noise", messages, out WorldFractalNoiseSettings noise))
                {
                    builder.AddStage(new WorldMoistureStage(moisture, layout, noise));
                }
            }

            if (terrain.WaterDepthEnabled)
            {
                bool heightOk = TryFloat(handles, terrain.HeightChannel, "WaterDepth height input", messages, out ChannelHandle<float> height);
                bool waterOk = TryFloat(handles, terrain.WaterDepthChannel, "WaterDepth output", messages, out ChannelHandle<float> water);
                if (heightOk && waterOk)
                {
                    try
                    {
                        builder.AddStage(new WorldWaterDepthStage(height, water, layout, terrain.SeaLevel));
                    }
                    catch (ArgumentOutOfRangeException exception)
                    {
                        messages.Add("WaterDepth stage: " + exception.Message);
                    }
                }
            }

            if (terrain.SlopeEnabled)
            {
                bool heightOk = TryFloat(handles, terrain.HeightChannel, "Slope height input", messages, out ChannelHandle<float> height);
                bool slopeOk = TryFloat(handles, terrain.SlopeChannel, "Slope output", messages, out ChannelHandle<float> slope);
                if (heightOk && slopeOk)
                    builder.AddStage(new WorldSlopeStage(height, slope, layout));
            }
        }

        private static bool TryFloat(
            Handles handles,
            string channelId,
            string label,
            List<string> messages,
            out ChannelHandle<float> handle)
        {
            handle = default(ChannelHandle<float>);
            if (string.IsNullOrWhiteSpace(channelId))
            {
                messages.Add(label + " channel ID is empty.");
                return false;
            }
            if (!handles.Float.TryGetValue(channelId, out handle))
            {
                messages.Add(label + $" requires float Channel '{channelId}'.");
                return false;
            }
            return true;
        }

        private static void AddBiomeSurfaceAndBuildableStages(
            WorldTerrainPipelineAuthoringSettings terrain,
            WorldBiomeSurfaceAuthoringSettings biomeSurface,
            WorldBuildableAuthoringSettings buildable,
            WorldPlanarSampleLayout layout,
            WorldGenerationPipelineBuilder builder,
            Handles handles,
            List<string> messages)
        {
            if (biomeSurface == null)
            {
                messages.Add("Biome/Surface settings are missing.");
                return;
            }
            if (buildable == null)
            {
                messages.Add("Buildable settings are missing.");
                return;
            }

            WorldSurfaceCatalog surfaces = null;
            WorldBiomeCatalog biomes = null;
            if (biomeSurface.BiomeEnabled || biomeSurface.SurfaceEnabled || buildable.BlockedBiomeIds.Count > 0)
            {
                if (!TryBuildSurfaceCatalog(biomeSurface, messages, out surfaces))
                    return;
                if (!TryBuildBiomeCatalog(biomeSurface, surfaces, messages, out biomes))
                    return;
            }

            if (biomeSurface.BiomeEnabled)
            {
                bool heightOk = TryFloat(handles, terrain.HeightChannel, "Biome height input", messages, out ChannelHandle<float> height);
                bool biomeOk = TryInt(handles, biomeSurface.BiomeChannel, "Biome output", messages, out ChannelHandle<int> biome);
                if (heightOk && biomeOk)
                {
                    ChannelHandle<float>? moisture = TryOptionalFloat(handles, terrain.MoistureChannel);
                    ChannelHandle<float>? water = TryOptionalFloat(handles, terrain.WaterDepthChannel);
                    ChannelHandle<float>? slope = TryOptionalFloat(handles, terrain.SlopeChannel);
                    ValidateBiomeCriteriaInputs(biomeSurface, moisture, water, slope, messages);
                    if (messages.Count == 0)
                        builder.AddStage(new WorldBiomeStage(height, biome, layout, biomes, moisture, water, slope));
                }
            }

            if (biomeSurface.SurfaceEnabled)
            {
                bool biomeOk = TryInt(handles, biomeSurface.BiomeChannel, "Surface biome input", messages, out ChannelHandle<int> biome);
                bool surfaceOk = TryInt(handles, biomeSurface.SurfaceChannel, "Surface output", messages, out ChannelHandle<int> surface);
                if (biomeOk && surfaceOk)
                    builder.AddStage(new WorldSurfaceStage(biome, surface, layout, biomes, surfaces));
            }

            if (buildable.Enabled)
            {
                bool slopeOk = TryFloat(handles, terrain.SlopeChannel, "Buildable slope input", messages, out ChannelHandle<float> slope);
                bool waterOk = TryFloat(handles, terrain.WaterDepthChannel, "Buildable water input", messages, out ChannelHandle<float> water);
                bool outputOk = TryByte(handles, buildable.Channel, "Buildable output", messages, out ChannelHandle<byte> output);
                if (slopeOk && waterOk && outputOk)
                {
                    List<string> blockedSource = buildable.BlockedBiomeIds ?? new List<string>();
                    WorldBiomeId[] blocked = new WorldBiomeId[blockedSource.Count];
                    for (int i = 0; i < blocked.Length; i++)
                    {
                        if (!TryBiomeId(blockedSource[i], out blocked[i], out string error))
                        {
                            messages.Add($"BlockedBiome[{i}]: {error}");
                            return;
                        }
                    }

                    ChannelHandle<int>? biomeHandle = null;
                    if (blocked.Length > 0)
                    {
                        if (!TryInt(handles, biomeSurface.BiomeChannel, "Buildable biome input", messages, out ChannelHandle<int> concreteBiome))
                            return;
                        biomeHandle = concreteBiome;
                    }

                    try
                    {
                        WorldBuildableSettings settings = new WorldBuildableSettings(
                            buildable.MaxSlope,
                            buildable.MaxWaterDepth,
                            blocked);
                        builder.AddStage(new WorldBuildableStage(
                            slope,
                            water,
                            output,
                            layout,
                            settings,
                            biomeHandle,
                            blocked.Length > 0 ? biomes : null));
                    }
                    catch (ArgumentException exception)
                    {
                        messages.Add("Buildable stage: " + exception.Message);
                    }
                }
            }
        }

        private static bool TryBuildSurfaceCatalog(
            WorldBiomeSurfaceAuthoringSettings source,
            List<string> messages,
            out WorldSurfaceCatalog catalog)
        {
            catalog = null;
            if (source.SurfaceIds == null || source.SurfaceIds.Count == 0)
            {
                messages.Add("Surface catalog must contain at least one Surface ID.");
                return false;
            }

            WorldSurfaceId[] ids = new WorldSurfaceId[source.SurfaceIds.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                if (!TrySurfaceId(source.SurfaceIds[i], out ids[i], out string error))
                {
                    messages.Add($"Surface[{i}]: {error}");
                    return false;
                }
            }

            try
            {
                catalog = new WorldSurfaceCatalog(ids);
                return true;
            }
            catch (ArgumentException exception)
            {
                messages.Add("Surface catalog: " + exception.Message);
                return false;
            }
        }

        private static bool TryBuildBiomeCatalog(
            WorldBiomeSurfaceAuthoringSettings source,
            WorldSurfaceCatalog surfaces,
            List<string> messages,
            out WorldBiomeCatalog catalog)
        {
            catalog = null;
            if (source.Biomes == null || source.Biomes.Count == 0)
            {
                messages.Add("Biome catalog must contain at least one Biome.");
                return false;
            }
            if (!TryBiomeId(source.FallbackBiomeId, out WorldBiomeId fallback, out string fallbackError))
            {
                messages.Add("FallbackBiomeId: " + fallbackError);
                return false;
            }

            WorldBiomeDefinition[] definitions = new WorldBiomeDefinition[source.Biomes.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                WorldBiomeAuthoringDefinition sourceBiome = source.Biomes[i];
                if (sourceBiome == null)
                {
                    messages.Add($"Biome[{i}] is null.");
                    return false;
                }
                if (!TryBiomeId(sourceBiome.Id, out WorldBiomeId biomeId, out string biomeError))
                {
                    messages.Add($"Biome[{i}] ID: {biomeError}");
                    return false;
                }
                if (!TrySurfaceId(sourceBiome.SurfaceId, out WorldSurfaceId surfaceId, out string surfaceError))
                {
                    messages.Add($"Biome[{i}] SurfaceId: {surfaceError}");
                    return false;
                }
                if (!surfaces.TryGetIndex(surfaceId, out _))
                {
                    messages.Add($"Biome[{i}] references missing Surface '{surfaceId.Value}'.");
                    return false;
                }

                if (!TryRange(sourceBiome.Height, $"Biome[{i}] Height", messages, out WorldRangeRule? height) ||
                    !TryRange(sourceBiome.Moisture, $"Biome[{i}] Moisture", messages, out WorldRangeRule? moisture) ||
                    !TryRange(sourceBiome.WaterDepth, $"Biome[{i}] WaterDepth", messages, out WorldRangeRule? water) ||
                    !TryRange(sourceBiome.Slope, $"Biome[{i}] Slope", messages, out WorldRangeRule? slope))
                    return false;

                definitions[i] = new WorldBiomeDefinition(
                    biomeId,
                    surfaceId,
                    new WorldBiomeCriteria(height, moisture, water, slope),
                    sourceBiome.Priority);
            }

            try
            {
                catalog = new WorldBiomeCatalog(definitions, fallback);
                return true;
            }
            catch (ArgumentException exception)
            {
                messages.Add("Biome catalog: " + exception.Message);
                return false;
            }
        }

        private static bool TryRange(
            WorldRangeAuthoringSettings source,
            string label,
            List<string> messages,
            out WorldRangeRule? rule)
        {
            rule = null;
            if (source == null || !source.Enabled) return true;
            try
            {
                rule = new WorldRangeRule(source.Min, source.Max);
                return true;
            }
            catch (ArgumentException exception)
            {
                messages.Add(label + ": " + exception.Message);
                return false;
            }
        }

        private static void ValidateBiomeCriteriaInputs(
            WorldBiomeSurfaceAuthoringSettings source,
            ChannelHandle<float>? moisture,
            ChannelHandle<float>? water,
            ChannelHandle<float>? slope,
            List<string> messages)
        {
            for (int i = 0; i < source.Biomes.Count; i++)
            {
                WorldBiomeAuthoringDefinition biome = source.Biomes[i];
                if (biome == null) continue;
                if (biome.Moisture != null && biome.Moisture.Enabled && !moisture.HasValue)
                    messages.Add($"Biome[{i}] uses Moisture criteria but the configured moisture Channel is unavailable or not float.");
                if (biome.WaterDepth != null && biome.WaterDepth.Enabled && !water.HasValue)
                    messages.Add($"Biome[{i}] uses WaterDepth criteria but the configured water Channel is unavailable or not float.");
                if (biome.Slope != null && biome.Slope.Enabled && !slope.HasValue)
                    messages.Add($"Biome[{i}] uses Slope criteria but the configured slope Channel is unavailable or not float.");
            }
        }

        private static ChannelHandle<float>? TryOptionalFloat(Handles handles, string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return null;
            return handles.Float.TryGetValue(channelId, out ChannelHandle<float> handle)
                ? handle
                : (ChannelHandle<float>?)null;
        }

        private static bool TryInt(
            Handles handles,
            string channelId,
            string label,
            List<string> messages,
            out ChannelHandle<int> handle)
        {
            handle = default(ChannelHandle<int>);
            if (string.IsNullOrWhiteSpace(channelId) || !handles.Int.TryGetValue(channelId, out handle))
            {
                messages.Add(label + $" requires int Channel '{channelId}'.");
                return false;
            }
            return true;
        }

        private static bool TryByte(
            Handles handles,
            string channelId,
            string label,
            List<string> messages,
            out ChannelHandle<byte> handle)
        {
            handle = default(ChannelHandle<byte>);
            if (string.IsNullOrWhiteSpace(channelId) || !handles.Byte.TryGetValue(channelId, out handle))
            {
                messages.Add(label + $" requires byte Channel '{channelId}'.");
                return false;
            }
            return true;
        }

        private static bool TryBiomeId(string value, out WorldBiomeId id, out string error)
        {
            id = default(WorldBiomeId);
            try
            {
                id = WorldBiomeId.From(value);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TrySurfaceId(string value, out WorldSurfaceId id, out string error)
        {
            id = default(WorldSurfaceId);
            try
            {
                id = WorldSurfaceId.From(value);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryNoise(
            WorldFractalNoiseAuthoringSettings source,
            string label,
            List<string> messages,
            out WorldFractalNoiseSettings settings)
        {
            settings = default(WorldFractalNoiseSettings);
            if (source == null)
            {
                messages.Add(label + " settings are missing.");
                return false;
            }
            if (!WorldRuleId.TryCreate(source.NoiseId, out WorldRuleId id, out string error))
            {
                messages.Add(label + ": " + error);
                return false;
            }

            try
            {
                settings = new WorldFractalNoiseSettings(
                    id,
                    source.BasePeriod,
                    source.Octaves,
                    source.Lacunarity,
                    source.Persistence);
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                messages.Add(label + ": " + exception.Message);
                return false;
            }
        }

        private static WorldGenerationAuthoringCompileResult Failed(
            WorldPlanarSampleLayout layout,
            List<string> messages) =>
            new WorldGenerationAuthoringCompileResult(false, null, layout, messages.ToArray());
    }
}
