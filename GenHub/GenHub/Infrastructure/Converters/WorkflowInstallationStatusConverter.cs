using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using GenHub.Core.Models;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Converter to determine if any artifact in a workflow is being installed
    /// </summary>
    public class WorkflowInstallationStatusConverter : IMultiValueConverter
    {
        // Singleton instance
        public static readonly WorkflowInstallationStatusConverter Instance = new WorkflowInstallationStatusConverter();
        
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Check if currentlyInstallingArtifact exists
            if (values.Count >= 2 && values[0] is GitHubArtifact
 installingArtifact && installingArtifact != null)
            {
                // Check if artifacts collection contains the currently installing artifact
                if (values[1] is IEnumerable<GitHubArtifact
> artifacts && artifacts != null)
                {
                    string paramStr = parameter as string ?? "Any";
                    
                    if (paramStr == "Any")
                    {
                        // Return true if any artifact in workflow is being installed
                        return artifacts.Any(a => a.Id == installingArtifact.Id);
                    }
                }
            }
            
            return false;
        }
    }
}
