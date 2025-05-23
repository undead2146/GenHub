using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that shows/hides content based on tab index comparison
    /// Used for MVVM-compliant tab switching without violating separation of concerns
    /// </summary>
    public class TabIndexToVisibilityConverter : IValueConverter
    {
        public static readonly TabIndexToVisibilityConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int currentIndex && parameter is string targetIndexStr && int.TryParse(targetIndexStr, out int targetIndex))
            {
                return currentIndex == targetIndex;
            }
            
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("TabIndexToVisibilityConverter does not support ConvertBack");
        }
    }
}
