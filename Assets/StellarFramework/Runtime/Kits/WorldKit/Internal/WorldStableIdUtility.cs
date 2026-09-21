using System;

namespace StellarFramework.WorldKit
{
    internal static class WorldStableIdUtility
    {
        internal static bool TryValidate(
            string value,
            int maxLength,
            string displayName,
            out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(value))
            {
                error = displayName + " cannot be null or empty.";
                return false;
            }

            if (value.Length > maxLength)
            {
                error = string.Format("{0} cannot exceed {1} characters.", displayName, maxLength);
                return false;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = displayName + " cannot contain leading or trailing whitespace.";
                return false;
            }

            bool segmentHasCharacter = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '.')
                {
                    if (!segmentHasCharacter)
                    {
                        error = displayName + " cannot contain empty dot-separated segments.";
                        return false;
                    }

                    segmentHasCharacter = false;
                    continue;
                }

                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_';
                if (!valid)
                {
                    error = displayName + " segments may only contain lower-case letters, digits and underscore.";
                    return false;
                }

                segmentHasCharacter = true;
            }

            if (!segmentHasCharacter)
            {
                error = displayName + " cannot end with a dot.";
                return false;
            }

            return true;
        }
    }
}
