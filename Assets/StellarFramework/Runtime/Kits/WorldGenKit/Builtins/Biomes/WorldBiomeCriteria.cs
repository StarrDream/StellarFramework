namespace StellarFramework.WorldGenKit.Builtins
{
    public readonly struct WorldBiomeCriteria
    {
        private readonly WorldRangeRule _height;
        private readonly WorldRangeRule _moisture;
        private readonly WorldRangeRule _waterDepth;
        private readonly WorldRangeRule _slope;

        public bool UsesHeight { get; }
        public bool UsesMoisture { get; }
        public bool UsesWaterDepth { get; }
        public bool UsesSlope { get; }
        public bool IsUnconditional => !UsesHeight && !UsesMoisture && !UsesWaterDepth && !UsesSlope;

        public WorldBiomeCriteria(
            WorldRangeRule? height = null,
            WorldRangeRule? moisture = null,
            WorldRangeRule? waterDepth = null,
            WorldRangeRule? slope = null)
        {
            UsesHeight = height.HasValue;
            UsesMoisture = moisture.HasValue;
            UsesWaterDepth = waterDepth.HasValue;
            UsesSlope = slope.HasValue;
            _height = height.GetValueOrDefault();
            _moisture = moisture.GetValueOrDefault();
            _waterDepth = waterDepth.GetValueOrDefault();
            _slope = slope.GetValueOrDefault();
        }

        public bool Matches(
            float height,
            bool hasMoisture,
            float moisture,
            bool hasWaterDepth,
            float waterDepth,
            bool hasSlope,
            float slope)
        {
            if (UsesHeight && !_height.Contains(height)) return false;
            if (UsesMoisture && (!hasMoisture || !_moisture.Contains(moisture))) return false;
            if (UsesWaterDepth && (!hasWaterDepth || !_waterDepth.Contains(waterDepth))) return false;
            if (UsesSlope && (!hasSlope || !_slope.Contains(slope))) return false;
            return true;
        }
    }
}
