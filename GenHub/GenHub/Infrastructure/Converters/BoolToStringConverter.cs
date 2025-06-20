using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts boolean values to custom string values
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        /// <summary>
        /// String to return when value is true
        /// </summary>
        public string TrueValue { get; set; } = "True";

        /// <summary>
        /// String to return when value is false
        /// </summary>
        public string FalseValue { get; set; } = "False";

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }

            return FalseValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (stringValue == TrueValue)
                    return true;
                if (stringValue == FalseValue)
                    return false;
            }

            return false;
        }
    }
}
