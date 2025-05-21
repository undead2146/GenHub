using System;
using System.Collections.Generic;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.AppUpdate
{
    /// <summary>
    /// Result of an update check operation
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether an update is available
        /// </summary>
        public bool IsUpdateAvailable { get; set; }
        
        /// <summary>
        /// Gets or sets the error messages (if any)
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the latest release information
        /// </summary>
        public GitHubRelease? LatestRelease { get; set; }
    }
}
