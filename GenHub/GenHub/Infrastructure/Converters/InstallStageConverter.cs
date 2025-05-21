using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GenHub.Core.Models;
using System;
using System.Globalization;
using System.Diagnostics;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Converter to determine installation status for UI display
    /// </summary>
    public class InstallStageConverter : IValueConverter
    {
        // Singleton instance
        public static readonly InstallStageConverter Instance = new InstallStageConverter();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // For matching current installing artifact with selected artifact
            if (parameter is GitHubArtifact selectedArtifact && value is GitHubArtifact installingArtifact)
            {
                // Return true if it's the same artifact
                return installingArtifact == selectedArtifact;
            }
            
            return false;
        }
        
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
