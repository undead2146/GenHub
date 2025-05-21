using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Converts a boolean value to one of two possible values.
    /// </summary>
    public class BoolToValueConverter : IValueConverter
    {
        /// <summary>
        /// The value to return when the boolean input is true
        /// </summary>
        public object TrueValue { get; set; }

        /// <summary>
        /// The value to return when the boolean input is false
        /// </summary>
        public object FalseValue { get; set; }

        /// <summary>
        /// Converts a boolean to the configured true/false value.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? TrueValue : FalseValue;
        }

        /// <summary>
        /// Converts back from a value to a boolean.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.Equals(TrueValue);
        }
    }

    public class BoolToThicknessConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool installed && installed)
                return 1;
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
