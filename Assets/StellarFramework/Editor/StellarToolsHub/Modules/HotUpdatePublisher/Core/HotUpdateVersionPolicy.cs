using System;
using System.Collections.Generic;
using System.Globalization;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Creates a YooAsset PackageVersion independently from the immutable Publisher ReleaseId.</summary>
    public interface IHotUpdateVersionPolicy
    {
        string CreateNextVersion(DateTime utcNow, IEnumerable<string> existingPackageVersions);
    }

    /// <summary>Default PackageVersion policy: UTC YYYY.MM.DD.NNN, starting from 001 each day.</summary>
    public sealed class DailyHotUpdateVersionPolicy : IHotUpdateVersionPolicy
    {
        private const int MaximumDailySequence = 999;
        private const string DateFormat = "yyyy.MM.dd";
        public string CreateNextVersion(DateTime utcNow, IEnumerable<string> existingPackageVersions)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Version policy requires an explicitly UTC timestamp.", nameof(utcNow));
            if (existingPackageVersions == null) throw new ArgumentNullException(nameof(existingPackageVersions));

            string datePrefix = utcNow.ToString(DateFormat, CultureInfo.InvariantCulture) + ".";
            int maxSequence = 0;
            foreach (string existingVersion in existingPackageVersions)
            {
                if (TryReadSequence(existingVersion, datePrefix, out int sequence) && sequence > maxSequence)
                    maxSequence = sequence;
            }

            if (maxSequence >= MaximumDailySequence)
                throw new InvalidOperationException($"PackageVersion sequence for {datePrefix.TrimEnd('.')} is exhausted at {MaximumDailySequence:000}.");
            return datePrefix + (maxSequence + 1).ToString("000", CultureInfo.InvariantCulture);
        }

        private static bool TryReadSequence(string version, string datePrefix, out int sequence)
        {
            sequence = 0;
            if (string.IsNullOrWhiteSpace(version) || !version.StartsWith(datePrefix, StringComparison.Ordinal)) return false;
            string suffix = version.Substring(datePrefix.Length);
            if (suffix.Length != 3 || !int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
                return false;
            return sequence >= 1 && sequence <= MaximumDailySequence;
        }
    }
}
