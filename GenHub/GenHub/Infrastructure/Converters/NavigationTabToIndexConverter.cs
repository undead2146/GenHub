using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GenHub.Common.Models;

namespace GenHub.Infrastructure.Converters;

/// <summary>
/// Converts NavigationTab enum values to their integer representation.
/// </summary>
public class NavigationTabToIndexConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NavigationTabToIndexConverter Instance = new NavigationTabToIndexConverter();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is NavigationTab tab)
        {
            return (int)tab;
        }

        return 0;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is int index && Enum.IsDefined(typeof(NavigationTab), index))
        {
            return (NavigationTab)index;
        }

        return NavigationTab.GameProfiles;
    }
}
