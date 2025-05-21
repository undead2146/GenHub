using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that takes a boolean value and converts it to a string based on the parameter
    /// Parameter should be in the format "TrueString|FalseString"
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance of the converter for static XAML reference
        /// </summary>
        public static readonly BoolToStringConverter Instance = new BoolToStringConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string stringParam)
            {
                var parts = stringParam.Split('|');
                if (parts.Length == 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
                return boolValue ? "True" : "False";
            }
            
            return BindingOperations.DoNothing;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && parameter is string stringParam)
            {
                var parts = stringParam.Split('|');
                if (parts.Length == 2)
                {
                    if (stringValue == parts[0]) return true;
                    if (stringValue == parts[1]) return false;
                }
            }
            
            return BindingOperations.DoNothing;
        }
    }
}
