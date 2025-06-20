using System;
using System.Globalization;
using Avalonia.Data.Converters;
using GenHub.Core.Models.Enums;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts GitHubSearchCriteria enum values to user-friendly display names
    /// </summary>
    public class GitHubSearchCriteriaConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not GitHubSearchCriteria criteria)
                return value?.ToString() ?? "Unknown";

            return criteria switch
            {
                GitHubSearchCriteria.All => "All Fields",
                GitHubSearchCriteria.WorkflowNumber => "Workflow Number",
                GitHubSearchCriteria.CommitMessage => "Commit Message",
                GitHubSearchCriteria.PullRequestNumber => "Pull Request Number",
                _ => criteria.ToString()
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string stringValue)
                return null;

            return stringValue switch
            {
                "All Fields" => GitHubSearchCriteria.All,
                "Workflow Number" => GitHubSearchCriteria.WorkflowNumber,
                "Commit Message" => GitHubSearchCriteria.CommitMessage,
                "Pull Request Number" => GitHubSearchCriteria.PullRequestNumber,
                _ => Enum.TryParse<GitHubSearchCriteria>(stringValue, out var result) ? result : null
            };
        }
    }
}
