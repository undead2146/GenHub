using System;
using System.Collections.Generic;

namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Centralized theme colors and appearance settings for consistent use across the application.
    /// </summary>
    public static class ProfileThemeColor 
    {
        // Game-specific colors
        public const string GeneralsColor = "#BD5A0F"; // Dark orange/yellow
        public const string ZeroHourColor = "#2D4963"; // Dark teal/blue

        // Default colors for card backgrounds
        private static readonly Random _random = new Random();
        private static readonly string[] _cardColors = new[]
        {
            "#1E88E5", // Blue
            "#8E24AA", // Purple
            "#43A047", // Green
            "#FB8C00", // Orange
            "#00ACC1", // Teal
            "#7CB342", // Light Green
            "#F44336", // Red
            "#6D4C41", // Brown
            "#009688", // Cyan
            "#3F51B5"  // Indigo
        };

        // Mapping from build preset names to colors
        private static readonly Dictionary<string, string> _presetColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Debug", "#3949AB" },      // Blue
            { "Release", "#2E7D32" },    // Green
            { "Final", "#6D4C41" },      // Brown
            { "WB", "#00695C" },         // Dark Teal
            { "Test", "#C62828" },       // Red
            { "WSkeleton", "#880E4F" },  // Pink
            { "ReleasePGEA", "#F57C00" } // Orange
        };

        /// <summary>
        /// Gets a random card color
        /// </summary>
        public static string GetRandomColor()
        {
            lock (_random)
            {
                return _cardColors[_random.Next(_cardColors.Length)];
            }
        }

        /// <summary>
        /// Gets the color for a specific game type
        /// </summary>
        public static string? GetColorForGameType(string gameType)
        {
            if (string.IsNullOrWhiteSpace(gameType))
                return null;

            // Make comparison case-insensitive
            if (gameType.Equals("Generals", StringComparison.OrdinalIgnoreCase))
                return GeneralsColor;

            if (gameType.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) || 
                gameType.Contains("Zero", StringComparison.OrdinalIgnoreCase))
                return ZeroHourColor;

            return null;
        }
    /// <summary>
    /// Gets the list of default theme colors for UI
    /// </summary>
    public static IEnumerable<(string Name, string HexValue)> GetDefaultColors()
    {
        yield return ("Generals", GeneralsColor);
        yield return ("Zero Hour", ZeroHourColor);
        
        foreach (var color in _cardColors)
        {
            yield return ($"Color {Array.IndexOf(_cardColors, color) + 1}", color);
        }
        
        foreach (var kvp in _presetColors)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }
        /// <summary>
        /// Gets the color for a build preset
        /// </summary>
        public static string GetColorForBuildPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return _cardColors[0]; // Default to first color

            // Use the dictionary to lookup a color
            if (_presetColors.TryGetValue(preset, out string? color))
                return color;

            // Fallback to a random color
            return GetRandomColor();
        }
    }
}
