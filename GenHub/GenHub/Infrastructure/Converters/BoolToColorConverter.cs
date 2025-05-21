using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a boolean value to a color.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        /// <summary>
        /// The color to use when the value is true.
        /// </summary>
        public IBrush TrueColor { get; set; }

        /// <summary>
        /// The color to use when the value is false.
        /// </summary>
        public IBrush FalseColor { get; set; }

        /// <summary>
        /// Converts a boolean to a color.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? TrueColor : FalseColor;
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
