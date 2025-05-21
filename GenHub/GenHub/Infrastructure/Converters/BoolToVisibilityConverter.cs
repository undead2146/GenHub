using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that returns Visible for true and Collapsed for false
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance of the converter for static XAML reference
        /// </summary>
        public static readonly BoolToVisibilityConverter Instance = new BoolToVisibilityConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Return string values instead of enum references
            return value is bool bValue && bValue ? "Visible" : "Collapsed";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return strValue == "Visible";
            }
            
            return BindingOperations.DoNothing;
        }
    }
}
