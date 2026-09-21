namespace StellarFramework.WorldGenKit.Builtins
{
    public static class WorldGenBuiltinDiagnosticIds
    {
        public static readonly WorldGenerationDiagnosticId DenseStorageRequired =
            WorldGenerationDiagnosticId.From("builtins.dense_storage_required");

        public static readonly WorldGenerationDiagnosticId StorageLengthMismatch =
            WorldGenerationDiagnosticId.From("builtins.storage_length_mismatch");

        public static readonly WorldGenerationDiagnosticId InvalidBiomeIndex =
            WorldGenerationDiagnosticId.From("builtins.invalid_biome_index");
    }
}
