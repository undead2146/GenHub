using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents a GitHub workflow artifact
    /// </summary>
    public class GitHubArtifact
    {
        /// <summary>
        /// Unique identifier for the artifact
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Name of the artifact
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Size of the artifact in bytes
        /// </summary>
        [JsonPropertyName("size_in_bytes")]
        public long SizeInBytes { get; set; }

        /// <summary>
        /// URL for downloading the artifact archive
        /// </summary>
        [JsonPropertyName("archive_download_url")]
        public string? ArchiveDownloadUrl { get; set; }

        /// <summary>
        /// When the artifact was created
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the artifact expires
        /// </summary>
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether the artifact has expired
        /// </summary>
        [JsonPropertyName("expired")]
        public bool Expired { get; set; }

        /// <summary>
        /// The workflow run ID this artifact belongs to
        /// </summary>
        public long RunId { get; set; }

        /// <summary>
        /// The workflow ID this artifact belongs to
        /// </summary>
        public long WorkflowId { get; set; }

        /// <summary>
        /// The workflow run number
        /// </summary>
        public int WorkflowNumber { get; set; }

        /// <summary>
        /// Pull request number if this artifact is from a PR
        /// </summary>
        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// Pull request title if this artifact is from a PR
        /// </summary>
        public string? PullRequestTitle { get; set; }

        /// <summary>
        /// Commit SHA this artifact was built from
        /// </summary>
        public string? CommitSha { get; set; }

        /// <summary>
        /// Commit message this artifact was built from
        /// </summary>
        public string? CommitMessage { get; set; }

        /// <summary>
        /// Repository information
        /// </summary>
        public GitHubRepository? RepositoryInfo { get; set; }

        /// <summary>
        /// Build information parsed from the artifact name
        /// </summary>
        public GitHubBuild? BuildInfo { get; set; }

        /// <summary>
        /// Whether this artifact represents a release
        /// </summary>
        public bool IsRelease { get; set; }

        /// <summary>
        /// Whether this artifact is installed locally
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Custom download URL if different from archive URL
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// The event type that triggered the workflow
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Build preset used for this artifact
        /// </summary>
        public string? BuildPreset { get; set; }

        /// <summary>
        /// Whether this artifact is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether this artifact is currently being installed
        /// </summary>
        public bool IsInstalling { get; set; }

        /// <summary>
        /// Gets a display name for the artifact
        /// </summary>
        public string GetDisplayName()
        {
            if (BuildInfo != null)
            {
                return $"{Name} ({BuildInfo.GameVariant})";
            }
            return Name;
        }

        /// <summary>
        /// Gets a formatted description
        /// </summary>
        public string GetDescription()
        {
            var parts = new List<string>();
            
            if (SizeInBytes > 0)
            {
                parts.Add(FormatFileSize(SizeInBytes));
            }
            
            if (WorkflowNumber > 0)
            {
                parts.Add($"Run #{WorkflowNumber}");
            }
            
            if (PullRequestNumber.HasValue)
            {
                parts.Add($"PR #{PullRequestNumber}");
            }
            
            return string.Join(" - ", parts);
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
