using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Determines whether white or black text is more readable on a given background color
    /// </summary>
    public class ContrastTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string colorValue)
                return new SolidColorBrush(Colors.White);
                
            try
            {
                // Default opacity is 1.0 unless specified
                double opacity = 1.0;
                if (parameter is string opacityStr)
                {
                    if (double.TryParse(opacityStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedOpacity))
                    {
                        opacity = parsedOpacity;
                    }
                }
                
                // Parse color - handle both with and without # prefix
                var color = colorValue.StartsWith("#") 
                    ? Color.Parse(colorValue) 
                    : Color.Parse("#" + colorValue);
                
                // Calculate relative luminance: https://www.w3.org/TR/WCAG20/#relativeluminancedef
                // Simplified approach to determine if white or black is better contrast
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                
                // Luminance threshold for determining text color (0.5 is middle point)
                // Use black text on light backgrounds, white text on dark backgrounds
                return luminance > 0.6 
                    ? new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0)) // Black with opacity
                    : new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 255, 255, 255)); // White with opacity
            }
            catch
            {
                // Default to white text if something goes wrong
                return new SolidColorBrush(Colors.White);
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Not implemented for this converter
            return null;
        }
    }
}
