using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a string value to a boolean by checking if it's not null or empty.
    /// </summary>
    public class NotNullOrEmptyConverter : IValueConverter
    {
        /// <summary>
        /// Returns true if the string value is not null or empty.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return !string.IsNullOrEmpty(strValue);
            }
            return value != null;
        }

        /// <summary>
        /// Not implemented for one-way binding.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
