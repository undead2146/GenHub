using System.Text.RegularExpressions;

namespace GenHub.Core.Helpers;

/// <summary>
/// Helper class for version string operations.
/// </summary>
public static class VersionHelper
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
}
