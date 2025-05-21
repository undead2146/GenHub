using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.Globalization;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Converts a string hex color value to a SolidColorBrush
    /// </summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string colorValue)
                return new SolidColorBrush(Colors.DarkGray);
                
            try
            {
                // Parse the opacity parameter
                double opacity = 1.0;
                if (parameter is string opacityStr)
                {
                    Console.WriteLine($"StringToBrushConverter: Converting '{colorValue}' with parameter '{opacityStr}'");
                    if (double.TryParse(opacityStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedOpacity))
                    {
                        opacity = parsedOpacity;
                        Console.WriteLine($"StringToBrushConverter: Using opacity {opacity}");
                    }
                    else
                    {
                        Console.WriteLine($"StringToBrushConverter: Invalid opacity parameter: {opacityStr}");
                    }
                }
                
                // Parse color - handle both with and without # prefix
                var color = colorValue.StartsWith("#") 
                    ? Color.Parse(colorValue) 
                    : Color.Parse("#" + colorValue);
                
                // Apply opacity if needed
                if (opacity < 1.0)
                {
                    color = new Color((byte)(opacity * 255), color.R, color.G, color.B);
                }
                
                return new SolidColorBrush(color);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StringToBrushConverter: Error {ex.Message}");
                return new SolidColorBrush(Colors.DarkGray);
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Not implemented for this converter
            return null;
        }
    }
}
