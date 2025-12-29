using System.Text.RegularExpressions;

namespace GenHub.Core.Helpers;

/// <summary>
/// Helper class for version string operations.
/// </summary>
public static class GameVersionHelper
{
    /// <summary>
    /// Extracts a numeric version from a version string like "2025-11-07" or "weekly-2025-11-21".
    /// Extracts all digits and returns them as an integer (e.g., "2025-11-07" -> 20251107).
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The numeric version as an integer, or 0 if parsing fails.</returns>
    public static int ExtractVersionFromVersionString(string? version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return 0;
        }

        // Extract all digits from the version string
        var digits = Regex.Replace(version, @"\D", string.Empty);

        // Take first 8 digits (YYYYMMDD format) to avoid overflow
        if (digits.Length > 8)
        {
            digits = digits.Substring(0, 8);
        }

        return int.TryParse(digits, out var result) ? result : 0;
    }

    /// <summary>
    /// Converts a version string to a normalized integer format.
    /// Examples: "1.04" -> 104, "1.08" -> 108, "20251226" -> 20251226.
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>A normalized integer representation of the version.</returns>
    public static int NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return 0;
        }

        // Handle semantic versions like 1.04
        if (version.Contains('.'))
        {
            var parts = version.Split('.');
            if (parts.Length >= 1 && int.TryParse(parts[0], out int major))
            {
                int minor = 0;
                if (parts.Length >= 2)
                {
                    _ = int.TryParse(parts[1], out minor);
                }

                return (major * 100) + minor;
            }
        }

        // Try to parse as direct integer
        if (int.TryParse(version, out int parsed))
        {
            return parsed;
        }

        // Fallback to extraction for composite strings
        return ExtractVersionFromVersionString(version);
    }
}
