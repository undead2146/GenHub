using System.Collections.Generic;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.AppUpdate
{
    /// <summary>
    /// Result of checking for application updates
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>
        /// Gets or sets whether an update is available
        /// </summary>
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// Gets or sets the current application version
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the latest available version
        /// </summary>
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL to download the update
        /// </summary>
        public string? UpdateUrl { get; set; }

        /// <summary>
        /// Gets or sets the release notes for the update
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred during the check
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the latest release information
        /// </summary>
        public GitHubRelease? LatestRelease { get; set; }

        /// <summary>
        /// Gets or sets error messages that occurred during the check
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }
}
