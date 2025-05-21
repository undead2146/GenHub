using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that returns Visible for non-null values and Collapsed for null values
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance of the converter for static XAML reference
        /// </summary>
        public static readonly NullToVisibilityConverter Instance = new NullToVisibilityConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Return string values instead of enum references
            return value != null ? "Visible" : "Collapsed";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
