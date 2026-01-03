using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters;

/// <summary>
/// Converts a value to a boolean based on whether it equals the parameter.
/// </summary>
public sealed class EqualsToConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return parameter == null;
        }

        return value.Equals(parameter);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null && value.Equals(true) ? parameter : AvaloniaProperty.UnsetValue;
    }
}
