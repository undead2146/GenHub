using System;
using System.Collections.Generic;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents information about a GitHub repository
    /// </summary>
    public class GitHubRepoSettings
    {
        /// <summary>
        /// Unique identifier for the repository, typically "Owner/Name".
        /// </summary>
        public string Id => $"{RepoOwner}/{RepoName}";

        /// <summary>
        /// Owner of the repository (e.g., "TheSuperHackers")
        /// </summary>
        public string RepoOwner { get; set; } = string.Empty;

        /// <summary>
        /// Name of the repository (e.g., "GeneralsGameCode")
        /// </summary>
        public string RepoName { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the repository (e.g., "TheSuperHackers/GeneralsGameCode")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional GitHub token for this specific repository
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Optional path to a specific workflow file (e.g., ".github/workflows/main.yml")
        /// </summary>
        public string? WorkflowFile { get; set; }

        /// <summary>
        /// Optional branch to filter workflows from
        /// </summary>
        public string? Branch { get; set; }

        public override string ToString() => DisplayName ?? $"{RepoOwner}/{RepoName}";
    }
}
