using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that compares a string value with a parameter to determine equality
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance of the converter for XAML static reference
        /// </summary>
        public static readonly StringEqualsConverter Instance = new StringEqualsConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
                return true;
                
            if (value == null || parameter == null)
                return false;

            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
