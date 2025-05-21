using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Data.Converters;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converter for debugging binding issues - logs the binding path and value
    /// </summary>
    public class DebugBindingConverter : IValueConverter
    {
        private static readonly ILogger? _logger;

        static DebugBindingConverter()
        {
            // Don't try to access services, just use debug output
            _logger = null;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string bindingPath = parameter as string ?? "unknown";
            string valueType = value?.GetType().Name ?? "null";
            string valueContent = value?.ToString() ?? "null";
            
            // Log to debug output
            string debugMsg = $"BINDING DEBUG - Path: {bindingPath}, Value Type: {valueType}, Value: {valueContent}";
            Console.WriteLine(debugMsg);
            _logger?.LogDebug(debugMsg);
            
            return value; // Pass through the original value unchanged
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }

        // Singleton instance for easy XAML access
        public static readonly DebugBindingConverter Instance = new DebugBindingConverter();
    }
}
