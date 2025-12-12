using System.Globalization;

namespace GenHub.Core.Helpers;

/// <summary>
/// Provides consistent file size formatting across the application.
/// </summary>
public static class FileSizeFormatter
{
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 MB").
    /// </summary>
    /// <param name="bytes">The number of bytes to format.</param>
    /// <returns>A formatted string representation of the file size.</returns>
    /// <remarks>
    /// Uses binary units (1 KB = 1024 bytes) and formats with one decimal place.
    /// Uses InvariantCulture for consistent formatting across different system locales.
    /// </remarks>
    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            unitIndex++;
            size /= 1024.0;
        }

        // Use 1 decimal place for all values for consistent formatting
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", size, SizeUnits[unitIndex]);
    }

    /// <summary>
    /// Formats a nullable byte count into a human-readable string.
    /// </summary>
    /// <param name="bytes">The number of bytes to format, or null.</param>
    /// <returns>A formatted string representation, or null if input is null.</returns>
    public static string? FormatNullable(long? bytes)
    {
        return bytes.HasValue ? Format(bytes.Value) : null;
    }
}
