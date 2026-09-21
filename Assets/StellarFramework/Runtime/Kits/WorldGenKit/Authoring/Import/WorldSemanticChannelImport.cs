using System;
using StellarFramework.WorldGenKit.Builtins;

namespace StellarFramework.WorldGenKit.Authoring
{
    public enum WorldSemanticImportError
    {
        None = 0,
        LengthMismatch = 1,
        UnknownStableId = 2
    }

    public static class WorldSemanticChannelImport
    {
        public static bool TryImportBiomeIds(
            ReadOnlySpan<WorldBiomeId> source,
            WorldBiomeCatalog catalog,
            DenseChannelStorage<int> destination,
            out int errorIndex,
            out WorldSemanticImportError error)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source.Length != destination.Length)
            {
                errorIndex = -1;
                error = WorldSemanticImportError.LengthMismatch;
                return false;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (!catalog.TryGetIndex(source[i], out _))
                {
                    errorIndex = i;
                    error = WorldSemanticImportError.UnknownStableId;
                    return false;
                }
            }

            Span<int> output = destination.AsSpan();
            for (int i = 0; i < source.Length; i++)
            {
                catalog.TryGetIndex(source[i], out int index);
                output[i] = index;
            }

            errorIndex = -1;
            error = WorldSemanticImportError.None;
            return true;
        }

        public static bool TryImportSurfaceIds(
            ReadOnlySpan<WorldSurfaceId> source,
            WorldSurfaceCatalog catalog,
            DenseChannelStorage<int> destination,
            out int errorIndex,
            out WorldSemanticImportError error)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source.Length != destination.Length)
            {
                errorIndex = -1;
                error = WorldSemanticImportError.LengthMismatch;
                return false;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (!catalog.TryGetIndex(source[i], out _))
                {
                    errorIndex = i;
                    error = WorldSemanticImportError.UnknownStableId;
                    return false;
                }
            }

            Span<int> output = destination.AsSpan();
            for (int i = 0; i < source.Length; i++)
            {
                catalog.TryGetIndex(source[i], out int index);
                output[i] = index;
            }

            errorIndex = -1;
            error = WorldSemanticImportError.None;
            return true;
        }
    }
}
