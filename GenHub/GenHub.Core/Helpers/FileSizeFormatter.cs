using System;
using System.Globalization;

namespace GenHub.Core.Helpers;

/// <summary>
/// Provides consistent file size formatting and parsing across the application.
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

    /// <summary>
    /// Parses a file size string (e.g., "2.5 MB", "1.2 KB") into bytes.
    /// </summary>
    /// <param name="sizeText">The size text to parse.</param>
    /// <returns>The file size in bytes, or 0 if parsing fails.</returns>
    public static long ParseToBytes(string? sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
        {
            return 0;
        }

        // Split by common separators and try to find numeric and unit parts
        var parts = sizeText.Trim().Split(new[] { ' ', ',', '.', }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return 0;
        }

        // Try to find numeric part and unit
        double size = 0;
        string? unit = null;

        foreach (var part in parts)
        {
            if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            {
                size = s;
            }
            else if (part.Length <= 3) // Likely a unit (KB, MB, GB)
            {
                unit = part.ToUpperInvariant();
            }
        }

        if (size == 0 || unit == null)
        {
            return 0;
        }

        return ConvertToBytes(size, unit);
    }

    /// <summary>
    /// Converts a size value with a unit to bytes.
    /// </summary>
    /// <param name="size">The numeric size value.</param>
    /// <param name="unit">The unit (KB, MB, GB, K, M, G).</param>
    /// <returns>The size in bytes.</returns>
    private static long ConvertToBytes(double size, string unit)
    {
        return unit.ToUpperInvariant() switch
        {
            "KB" or "K" => (long)(size * 1024),
            "MB" or "M" => (long)(size * 1024 * 1024),
            "GB" or "G" => (long)(size * 1024 * 1024 * 1024),
            _ => (long)size,
        };
    }
}
