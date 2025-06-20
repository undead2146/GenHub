using System;
using System.Collections.Generic;
using System.Linq;
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
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// The name of the release
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The tag name of the release
        /// </summary>
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        /// <summary>
        /// The body/description of the release
        /// </summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>
        /// URL for the release on GitHub
        /// </summary>
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        /// <summary>
        /// Whether this release is a draft
        /// </summary>
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        /// <summary>
        /// Whether this release is a prerelease
        /// </summary>
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        /// <summary>
        /// When the release was created
        /// </summary>
        [JsonPropertyName("created_at")]        
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the release was published
        /// </summary>
        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        /// <summary>
        /// The commit SHA this release is based on
        /// </summary>
        [JsonPropertyName("target_commitish")]
        public string? TargetCommitish { get; set; }

        /// <summary>
        /// Assets/files included in the release
        /// </summary>
        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }

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
        public GitHubRepository? RepositoryInfo { get; set; }

        /// <summary>
        /// Whether this release is a prerelease (computed property for compatibility)
        /// </summary>
        [JsonIgnore]
        public bool IsPrerelease 
        { 
            get => Prerelease;
            set => Prerelease = value;
        }

        /// <summary>
        /// Repository settings (alias for RepositoryInfo for compatibility)
        /// </summary>
        [JsonIgnore]
        public GitHubRepository? Repository 
        { 
            get => RepositoryInfo;
            set => RepositoryInfo = value;
        }

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
        /// Gets the display name for this release
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(Name))
                return Name;
            
            return TagName;
        }

        /// <summary>
        /// Gets a formatted description
        /// </summary>
        public string GetDescription()
        {
            var parts = new List<string>();
            
            if (PublishedAt.HasValue)
                parts.Add($"Published {PublishedAt.Value:yyyy-MM-dd}");
            
            if (Prerelease)
                parts.Add("Pre-release");
            
            if (Draft)
                parts.Add("Draft");
            
            if (Assets?.Count > 0)
                parts.Add($"{Assets.Count} assets");
            
            return string.Join(" - ", parts);
        }

        /// <summary>
        /// Gets the total download count for all assets
        /// </summary>
        public int GetTotalDownloadCount()
        {
            return Assets?.Sum(a => a.DownloadCount) ?? 0;
        }

        /// <summary>
        /// Gets assets that are likely executables
        /// </summary>
        public IEnumerable<GitHubReleaseAsset> GetExecutableAssets()
        {
            return Assets?.Where(a => a.IsExecutable()) ?? Enumerable.Empty<GitHubReleaseAsset>();
        }

        /// <summary>
        /// Gets assets that are likely archives
        /// </summary>
        public IEnumerable<GitHubReleaseAsset> GetArchiveAssets()
        {
            return Assets?.Where(a => a.IsArchive()) ?? Enumerable.Empty<GitHubReleaseAsset>();
        }

        /// <summary>
        /// Used for displaying release info
        /// </summary>
        public override string ToString()
        {
            return $"{GetDisplayName()} ({TagName})";
        }
    }
}
