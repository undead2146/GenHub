using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;

namespace GenHub.Core.Models.GitHub
{
    /// <summary>
    /// Represents a GitHub release
    /// </summary>
    public class GitHubRelease
    {
        /// <summary>
        /// The ID of the release
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The name of the release
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The tag name of the release
        /// </summary>
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        /// <summary>
        /// The body/description of the release
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// URL for the release on GitHub
        /// </summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>
        /// Whether this release is a draft
        /// </summary>
        public bool Draft { get; set; }

        /// <summary>
        /// Whether this release is a prerelease
        /// </summary>
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonIgnore]
        public bool IsPrerelease 
        { 
            get => Prerelease;
            set => Prerelease = value;
        }

        [JsonPropertyName("repo")]
        public GitHubRepoSettings Repository { get; set; } = new();

        /// <summary>
        /// When the release was created
        /// </summary>
        [JsonPropertyName("created_at")]        
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the release was published
        /// </summary>
        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Assets/files included in the release
        /// </summary>
        public List<GitHubReleaseAsset> Assets { get; set; } = new();

        /// <summary>
        /// Tarball URL for the source code
        /// </summary>
        [JsonPropertyName("tarball_url")]
        public string TarballUrl { get; set; } = string.Empty;

        /// <summary>
        /// Zipball URL for the source code
        /// </summary>
        [JsonPropertyName("zipball_url")]
        public string ZipballUrl { get; set; } = string.Empty;

        /// <summary>
        /// Repository information for this release
        /// </summary>
        [JsonIgnore]
        public GitHubRepoSettings? RepositoryInfo { get; set; }

        // Private backing field for Version
        private string _version = string.Empty;

        /// <summary>
        /// Semantic version, either provided or derived from TagName
        /// </summary>
        [JsonIgnore]
        public string Version
        {
            get => string.IsNullOrEmpty(_version) ? TagName?.TrimStart('v') ?? string.Empty : _version;
            set => _version = value;
        }

        /// <summary>
        /// Used for displaying release info
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({Version})";
        }
    }
}
