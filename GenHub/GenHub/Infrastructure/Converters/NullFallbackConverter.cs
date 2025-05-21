using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Provides a fallback value when the binding value is null.
    /// </summary>
    public class NullFallbackConverter : IValueConverter
    {
        /// <summary>
        /// The fallback value to use when the binding value is null.
        /// </summary>
        public object FallbackValue { get; set; }

        /// <summary>
        /// Returns the original value if not null, or the fallback value if null.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value ?? FallbackValue;
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
