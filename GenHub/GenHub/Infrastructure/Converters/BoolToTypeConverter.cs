using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GenHub.Core.Constants;

namespace GenHub.Infrastructure.Converters;

/// <summary>
/// Converts a boolean value to a map type string ("Map Package" for true, "Map File" for false).
/// </summary>
public class BoolToTypeConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDirectory)
        {
            return isDirectory ? MapManagerConstants.MapPackageDisplayName : MapManagerConstants.MapFileDisplayName;
        }

        return MapManagerConstants.MapFileDisplayName;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
