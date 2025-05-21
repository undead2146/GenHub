using Avalonia.Data.Converters;
using GenHub.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// MultiValueConverter for determining installation stage visibility by comparing artifacts
    /// </summary>
    public class InstallStageMultiConverter : IMultiValueConverter
    {
        public static readonly InstallStageMultiConverter Instance = new InstallStageMultiConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // First value should be the currently installing artifact
            // Second value should be the artifact to check
            
            if (values.Count < 2)
                return false;

            var installingArtifact = values[0] as GitHubArtifact
;
            var selectedArtifact = values[1] as GitHubArtifact
;
            
            if (installingArtifact == null || selectedArtifact == null)
                return false;
                
            // Compare the artifacts to determine if this is the one being installed
            return installingArtifact.Id == selectedArtifact.Id;
        }
    }
}
