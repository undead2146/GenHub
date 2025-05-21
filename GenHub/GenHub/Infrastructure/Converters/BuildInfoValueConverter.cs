using Avalonia.Data.Converters;
using GenHub.Core.Models;
using System;
using System.Globalization;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Safely gets a property value from a potentially null BuildInfo object
    /// </summary>
    public class BuildInfoValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value is GitHubBuild
 buildInfo && parameter is string propertyName)
            {
                return propertyName switch
                {
                    "Compiler" => buildInfo.Compiler,
                    "Configuration" => buildInfo.Configuration,
                    "GameVariant" => buildInfo.GameVariant,
                    "HasTFlag" => buildInfo.HasTFlag,
                    "HasEFlag" => buildInfo.HasEFlag,
                    _ => null
                };
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
