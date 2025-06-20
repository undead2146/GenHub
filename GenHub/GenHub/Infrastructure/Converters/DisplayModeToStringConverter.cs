using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GenHub.Core.Models.Enums;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts DisplayMode enum values to user-friendly display names
    /// </summary>
    public class DisplayModeToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DisplayMode mode)
                return value?.ToString() ?? "Unknown";

            return mode switch
            {
                DisplayMode.All => "All Items",
                DisplayMode.Workflows => "Workflows",
                DisplayMode.Releases => "Releases",
                _ => mode.ToString()
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue)
                return null;

            return stringValue switch
            {
                "All Items" => DisplayMode.All,
                "Workflows" => DisplayMode.Workflows,
                "Releases" => DisplayMode.Releases,
                _ => Enum.TryParse<DisplayMode>(stringValue, out var result) ? result : null
            };
        }
    }
}
