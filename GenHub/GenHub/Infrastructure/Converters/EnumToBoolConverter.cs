using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters;

/// <summary>
/// Converts an enum value to a boolean based on whether it matches the parameter.
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        return value?.Equals(parameter) == true || value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            if (Enum.TryParse(targetType, parameter.ToString(), out var result))
            {
                return result;
            }
        }

        return AvaloniaProperty.UnsetValue;
    }
}
