using System;
using System.Collections.Generic;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldGenKit.Feature;
using StellarFramework.WorldGenKit.Resources;
using StellarFramework.PlacementKit;
using UnityEngine;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public enum WorldAuthoringChannelValueType
    {
        Float = 0,
        Int = 1,
        Byte = 2
    }

    [Serializable]
    public sealed class WorldAuthoringChannelDefinition
    {
        public string Id = string.Empty;
        public WorldAuthoringChannelValueType ValueType = WorldAuthoringChannelValueType.Float;
        public WorldChannelStorageKind StorageKind = WorldChannelStorageKind.Dense;
        public WorldChannelScope Scope = WorldChannelScope.Sample;
        public WorldChannelSourceMode SourceMode = WorldChannelSourceMode.ProducedByStage;

        public WorldAuthoringChannelDefinition()
        {
        }

        public WorldAuthoringChannelDefinition(
            string id,
            WorldAuthoringChannelValueType valueType,
            WorldChannelStorageKind storageKind = WorldChannelStorageKind.Dense,
            WorldChannelScope scope = WorldChannelScope.Sample,
            WorldChannelSourceMode sourceMode = WorldChannelSourceMode.ProducedByStage)
        {
            Id = id ?? string.Empty;
            ValueType = valueType;
            StorageKind = storageKind;
            Scope = scope;
            SourceMode = sourceMode;
        }
    }

    [Serializable]
    public sealed class WorldFractalNoiseAuthoringSettings
    {
        public string NoiseId = "noise.world";
        public long BasePeriod = 64L;
        [Range(1, 16)] public int Octaves = 4;
        [Min(2)] public int Lacunarity = 2;
        [Range(0.0001f, 1f)] public float Persistence = 0.5f;
    }

    [Serializable]
    public sealed class WorldTerrainPipelineAuthoringSettings
    {
        public bool HeightEnabled = true;
        public string HeightChannel = "terrain.height";
        public WorldFractalNoiseAuthoringSettings HeightNoise = new WorldFractalNoiseAuthoringSettings
        {
            NoiseId = "noise.terrain.height",
            BasePeriod = 128L
        };
        public float MinHeight = -16f;
        public float MaxHeight = 32f;

        public bool MoistureEnabled = true;
        public string MoistureChannel = "terrain.moisture";
        public WorldFractalNoiseAuthoringSettings MoistureNoise = new WorldFractalNoiseAuthoringSettings
        {
            NoiseId = "noise.terrain.moisture",
            BasePeriod = 96L
        };

        public bool WaterDepthEnabled = true;
        public string WaterDepthChannel = "terrain.water_depth";
        public float SeaLevel = 0f;

        public bool SlopeEnabled = true;
        public string SlopeChannel = "terrain.slope";
    }

    [Serializable]
    public sealed class WorldRangeAuthoringSettings
    {
        public bool Enabled;
        public float Min;
        public float Max = 1f;
    }

    [Serializable]
    public sealed class WorldBiomeAuthoringDefinition
    {
        public string Id = "biome.default";
        public string SurfaceId = "surface.default";
        public int Priority;
        public WorldRangeAuthoringSettings Height = new WorldRangeAuthoringSettings();
        public WorldRangeAuthoringSettings Moisture = new WorldRangeAuthoringSettings();
        public WorldRangeAuthoringSettings WaterDepth = new WorldRangeAuthoringSettings();
        public WorldRangeAuthoringSettings Slope = new WorldRangeAuthoringSettings();
    }

    [Serializable]
    public sealed class WorldBiomeSurfaceAuthoringSettings
    {
        public bool BiomeEnabled = true;
        public string BiomeChannel = "terrain.biome";
        public string FallbackBiomeId = "biome.default";
        public List<string> SurfaceIds = new List<string> { "surface.default" };
        public List<WorldBiomeAuthoringDefinition> Biomes = new List<WorldBiomeAuthoringDefinition>
        {
            new WorldBiomeAuthoringDefinition()
        };
        public bool SurfaceEnabled = true;
        public string SurfaceChannel = "terrain.surface";
    }

    [Serializable]
    public sealed class WorldBuildableAuthoringSettings
    {
        public bool Enabled = true;
        public string Channel = "terrain.buildable";
        [Min(0f)] public float MaxSlope = 1f;
        [Min(0f)] public float MaxWaterDepth = 0f;
        public List<string> BlockedBiomeIds = new List<string>();
    }

    [Serializable]
    public sealed class WorldResourceAuthoringDefinition
    {
        public string Id = "resource.tree";
        public string CategoryId = "resource.vegetation";
        public WorldResourceDistributionMode Mode = WorldResourceDistributionMode.Density;
        [Range(0f, 1f)] public float Occurrence = 0.1f;
        [Min(1)] public int ClusterSize = 1;
        [Min(0f)] public float Richness = 1f;
        [Min(0f)] public float MinSpacing;
        public int Priority;
        public List<string> Occupies = new List<string> { "occupancy.vegetation" };
        public List<string> Excludes = new List<string> { "occupancy.vegetation" };
    }

    [Serializable]
    public sealed class WorldResourceAuthoringSettings
    {
        public List<string> OccupancyTypeIds = new List<string>
        {
            "occupancy.vegetation",
            "occupancy.mineral",
            "occupancy.building"
        };
        public List<WorldResourceAuthoringDefinition> Resources = new List<WorldResourceAuthoringDefinition>
        {
            new WorldResourceAuthoringDefinition()
        };
    }

    [Serializable]
    public sealed class WorldFeatureAuthoringDefinition
    {
        public string Id = "feature.landmark";
        public string CategoryId = "feature.landmark";
        public WorldFeatureKind Kind = WorldFeatureKind.Landmark;
        public WorldFeatureFootprintKind FootprintKind = WorldFeatureFootprintKind.Rectangle;
        [Min(0.0001f)] public float SizeX = 2f;
        [Min(0.0001f)] public float SizeY = 2f;
        public int MaxPerWorld = -1;
        public int MaxPerRegion = -1;
        public int Priority;
    }

    [Serializable]
    public sealed class WorldFeatureAuthoringSettings
    {
        public List<WorldFeatureAuthoringDefinition> Features = new List<WorldFeatureAuthoringDefinition>
        {
            new WorldFeatureAuthoringDefinition()
        };
    }

    [Serializable]
    public sealed class WorldPlacementProbeSettings
    {
        public string TypeId = "placement.default";
        public PlacementFootprintKind FootprintKind = PlacementFootprintKind.Rectangle;
        [Min(0.0001f)] public float SizeX = 1f;
        [Min(0.0001f)] public float SizeY = 1f;
        public double X;
        public double Y;
        public double RotationDegrees;
        [Min(0f)] public double SiteMaxSlopeDegrees;
        public double SiteMinWaterDepth;
        public double SiteMaxWaterDepth;
        public ulong SiteZoneMask;
        public ulong SiteConflictMask;
        public ulong SiteConnectionMask;
        public double BaseSuitability;
        public bool UseSlopeRule = true;
        [Min(0f)] public double MaxAllowedSlopeDegrees = 45d;
        public bool UseWaterRule = true;
        public double MinAllowedWaterDepth;
        public double MaxAllowedWaterDepth;
        public ulong RequiredZoneMask;
        public bool RequireAllZones = true;
        public ulong ForbiddenConflictMask;
        public ulong RequiredConnectionMask;
        public bool RequireAllConnections = true;
        public bool UseBaseSuitability = true;
    }

    [CreateAssetMenu(
        fileName = "WorldGenerationProfile",
        menuName = "StellarFramework/World Framework/Generation Profile")]
    public sealed class WorldGenerationAuthoringProfile : ScriptableObject
    {
        [SerializeField] private string _profileId = "world.profile.default";
        [SerializeField, Min(1)] private int _profileVersion = 1;
        [SerializeField, Min(1)] private int _width = 128;
        [SerializeField, Min(1)] private int _height = 128;
        [SerializeField, Min(1)] private long _sampleStep = 1L;
        [SerializeField] private List<WorldAuthoringChannelDefinition> _channels =
            new List<WorldAuthoringChannelDefinition>();
        [SerializeField] private WorldTerrainPipelineAuthoringSettings _terrain =
            new WorldTerrainPipelineAuthoringSettings();
        [SerializeField] private WorldBiomeSurfaceAuthoringSettings _biomeSurface =
            new WorldBiomeSurfaceAuthoringSettings();
        [SerializeField] private WorldBuildableAuthoringSettings _buildable =
            new WorldBuildableAuthoringSettings();
        [SerializeField] private WorldResourceAuthoringSettings _resources =
            new WorldResourceAuthoringSettings();
        [SerializeField] private WorldFeatureAuthoringSettings _features =
            new WorldFeatureAuthoringSettings();
        [SerializeField] private WorldPlacementProbeSettings _placementProbe =
            new WorldPlacementProbeSettings();

        public string ProfileId => _profileId;
        public int ProfileVersion => _profileVersion;
        public int Width => _width;
        public int Height => _height;
        public long SampleStep => _sampleStep;
        public IList<WorldAuthoringChannelDefinition> Channels => _channels;
        public WorldTerrainPipelineAuthoringSettings Terrain => _terrain;
        public WorldBiomeSurfaceAuthoringSettings BiomeSurface => _biomeSurface;
        public WorldBuildableAuthoringSettings Buildable => _buildable;
        public WorldResourceAuthoringSettings Resources => _resources;
        public WorldFeatureAuthoringSettings Features => _features;
        public WorldPlacementProbeSettings PlacementProbe => _placementProbe;

        public void ResetToDefaults()
        {
            _profileId = "world.profile.default";
            _profileVersion = 1;
            _width = 128;
            _height = 128;
            _sampleStep = 1L;
            _terrain = new WorldTerrainPipelineAuthoringSettings();
            _biomeSurface = new WorldBiomeSurfaceAuthoringSettings();
            _buildable = new WorldBuildableAuthoringSettings();
            _resources = new WorldResourceAuthoringSettings();
            _features = new WorldFeatureAuthoringSettings();
            _placementProbe = new WorldPlacementProbeSettings();
            _channels = new List<WorldAuthoringChannelDefinition>
            {
                new WorldAuthoringChannelDefinition("terrain.height", WorldAuthoringChannelValueType.Float),
                new WorldAuthoringChannelDefinition("terrain.moisture", WorldAuthoringChannelValueType.Float),
                new WorldAuthoringChannelDefinition("terrain.water_depth", WorldAuthoringChannelValueType.Float),
                new WorldAuthoringChannelDefinition("terrain.slope", WorldAuthoringChannelValueType.Float),
                new WorldAuthoringChannelDefinition("terrain.biome", WorldAuthoringChannelValueType.Int),
                new WorldAuthoringChannelDefinition("terrain.surface", WorldAuthoringChannelValueType.Int),
                new WorldAuthoringChannelDefinition("terrain.buildable", WorldAuthoringChannelValueType.Byte)
            };
        }

        public void SetIdentityForTests(string profileId, int profileVersion)
        {
            _profileId = profileId;
            _profileVersion = profileVersion;
        }

        public void SetLayoutForTests(int width, int height, long sampleStep)
        {
            _width = width;
            _height = height;
            _sampleStep = sampleStep;
        }
    }
}
