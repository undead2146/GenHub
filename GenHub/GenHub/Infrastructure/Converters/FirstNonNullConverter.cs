using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Infrastructure.Converters

{
    /// <summary>
    /// Converter that returns the first non-null value from the binding sources
    /// </summary>
    public class FirstNonNullConverter : IMultiValueConverter
    {
        public static readonly FirstNonNullConverter Instance = new FirstNonNullConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Return the first non-null value
            foreach (var value in values)
            {
                if (value != null && !Equals(value, AvaloniaProperty.UnsetValue))
                {
                    return value;
                }
            }

            // Handle the special case when we get a workflow but no artifact
            if (values.Count >= 2 && values[1] is GitHubWorkflowDisplayItemViewModel workflow)
            {
                // If we have a workflow but no artifact, use the FirstArtifact property
                if (workflow.FirstArtifact != null)
                {
                    return workflow.FirstArtifact;
                }
            }

            return null;
        }
    }
}
