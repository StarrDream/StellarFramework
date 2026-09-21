using System;

namespace StellarFramework.WorldGenKit.Builtins
{
    public static class WorldFractalValueNoise
    {
        public static double Sample01(
            WorldGenerationSeed seed,
            long absoluteX,
            long absoluteY,
            in WorldFractalNoiseSettings settings)
        {
            double weighted = 0d;
            double weightSum = 0d;
            double amplitude = 1d;
            long period = settings.BasePeriod;

            for (int octave = 0; octave < settings.Octaves; octave++)
            {
                double sample = SampleSmooth01(
                    seed,
                    absoluteX,
                    absoluteY,
                    period,
                    settings.NoiseKey,
                    (ulong)octave);

                weighted += sample * amplitude;
                weightSum += amplitude;
                amplitude *= settings.Persistence;

                if (period > 1L)
                {
                    long next = period / settings.Lacunarity;
                    period = next < 1L ? 1L : next;
                }
            }

            return weighted / weightSum;
        }

        private static double SampleSmooth01(
            WorldGenerationSeed seed,
            long x,
            long y,
            long period,
            WorldNoiseKey noiseKey,
            ulong octaveKey)
        {
            long cellX = FloorDiv(x, period);
            long cellY = FloorDiv(y, period);
            long nextX = checked(cellX + 1L);
            long nextY = checked(cellY + 1L);

            long remainderX = FloorMod(x, period);
            long remainderY = FloorMod(y, period);
            double tx = Smooth((double)remainderX / period);
            double ty = Smooth((double)remainderY / period);

            double v00 = WorldNoiseRule.Sample01(seed, cellX, cellY, noiseKey, octaveKey);
            double v10 = WorldNoiseRule.Sample01(seed, nextX, cellY, noiseKey, octaveKey);
            double v01 = WorldNoiseRule.Sample01(seed, cellX, nextY, noiseKey, octaveKey);
            double v11 = WorldNoiseRule.Sample01(seed, nextX, nextY, noiseKey, octaveKey);

            double bottom = Lerp(v00, v10, tx);
            double top = Lerp(v01, v11, tx);
            return Lerp(bottom, top, ty);
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder < 0L ? quotient - 1L : quotient;
        }

        private static long FloorMod(long value, long divisor)
        {
            long remainder = value % divisor;
            return remainder < 0L ? remainder + divisor : remainder;
        }

        private static double Smooth(double t) => t * t * (3d - (2d * t));
        private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
    }
}
