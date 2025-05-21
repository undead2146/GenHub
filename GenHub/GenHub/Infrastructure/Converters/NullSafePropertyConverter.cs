using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a binding path with nested properties safely by checking for null values
    /// </summary>
    public class NullSafePropertyConverter : IValueConverter
    {
        public static readonly NullSafePropertyConverter Instance = new NullSafePropertyConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // If the value is null, return null or BindingOperations.DoNothing depending on target type
            if (value == null)
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null 
                    ? BindingOperations.DoNothing 
                    : null;
            }

            // Extract property path from parameter
            string? propertyPath = parameter as string;
            if (string.IsNullOrEmpty(propertyPath))
            {
                return value; // Return the value itself if no property path specified
            }

            // Navigate the property path
            object? currentObject = value;
            string[] properties = propertyPath.Split('.');

            foreach (string property in properties)
            {
                if (currentObject == null) return null;

                Type currentType = currentObject.GetType();
                var propertyInfo = currentType.GetProperty(property);

                if (propertyInfo == null)
                {
                    return null; // Property doesn't exist
                }

                currentObject = propertyInfo.GetValue(currentObject);
            }

            return currentObject;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Not implemented for two-way binding
            return BindingOperations.DoNothing;
        }
    }
}
