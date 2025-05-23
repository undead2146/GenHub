using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter that checks if the value equals the parameter
    /// Used for tab selection states and button active states
    /// </summary>
    public class EqualityConverter : IValueConverter
    {
        public static readonly EqualityConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
                return true;
                
            if (value == null || parameter == null)
                return false;

            // Handle numeric conversions for tab indices
            if (value is int intValue && parameter is string strParam && int.TryParse(strParam, out int paramInt))
            {
                return intValue == paramInt;
            }

            // Handle string comparisons
            if (value is string strValue && parameter is string paramStr)
            {
                return string.Equals(strValue, paramStr, StringComparison.OrdinalIgnoreCase);
            }

            return value.Equals(parameter);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EqualityConverter does not support ConvertBack");
        }
    }
}
