using System;
using System.Text.Json.Serialization;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Models;

namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Represents a specific version of the game.
    /// </summary>
    public class GameVersion : IEntityIdentifier<string>
    {
        /// <summary>
        /// Gets or sets the unique identifier for this version.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the version.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the installation type of the game version.
        /// </summary>
        public GameInstallationType InstallationType { get; set; }
        /// <summary>
        /// Gets or sets the description for this version.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the path where this version is installed.
        /// </summary>
        public string InstallPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the game directory within the installation.
        /// </summary>
        public string GamePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the game executable.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date when this version was installed.
        /// </summary>
        public DateTime InstallDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the source type of this installation.
        /// </summary>
        public GameInstallationType SourceType { get; set; } = GameInstallationType.Unknown;

        /// <summary>
        /// Gets or sets the game type (e.g., "ZeroHour", "Generals").
        /// </summary>
        public string GameType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the install name for this version.
        /// </summary>
        public string InstallName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for this version.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the game variant for this version.
        /// </summary>
        public GameVariant GameVariant { get; set; }

        /// <summary>
        /// Holds source-specific metadata (e.g., GitHub details).
        /// This property will be serialized if not null.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BaseSourceMetadata? SourceSpecificMetadata { get; set; }

        /// <summary>
        /// Gets or sets the options used when installing this version.
        /// Not typically serialized with the GameVersion model itself.
        /// </summary>
        [JsonIgnore]
        public ExtractOptions? Options { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this version's installation is considered valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this version is specifically for Zero Hour.
        /// (Consider deriving this from GameType or BuildInfo if possible).
        /// </summary>
        public bool IsZeroHour { get; set; }

        /// <summary>
        /// Gets or sets the size of the installation in bytes.
        /// </summary>
        public long InstallSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the original build date of this version (e.g., artifact creation date).
        /// </summary>
        public DateTime? BuildDate { get; set; } // Can be populated from GitHubMetadata.ArtifactCreationDate

        // Convenience Accessors
        /// <summary>
        /// Gets the GitHub-specific metadata for this game version, if available.
        /// Returns null if SourceSpecificMetadata is not of type GitHubSourceMetadata.
        /// </summary>
        [JsonIgnore]
        public GitHubSourceMetadata? GitHubMetadata
        {
            get => SourceSpecificMetadata as GitHubSourceMetadata;
            set => SourceSpecificMetadata = value;
        }

        [JsonIgnore]
        public bool IsFromGitHub => SourceType == GameInstallationType.GitHubArtifact && GitHubMetadata != null;

        [JsonIgnore]
        public string FormattedSize
        {
            get
            {
            if (InstallSizeBytes <= 0) return "Unknown";
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = InstallSizeBytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
        
        private string? _sourceTypeName;
        [JsonIgnore]
        public string SourceTypeName 
        { 
            get => _sourceTypeName ?? SourceType.ToString();
            set => _sourceTypeName = value;
        }

        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public long SizeInBytes { get; set; }

        public GameVersion()
        {
        }

        public string GetDisplayName()
        {
            if (IsFromGitHub && GitHubMetadata?.AssociatedArtifact != null)
            {
                var artifactName = GitHubMetadata.AssociatedArtifact.Name;
                var buildInfoStr = GitHubMetadata.BuildInfo?.ToString(); // Uses convenience accessor on GitHubSourceMetadata

                if (GitHubMetadata.PullRequestNumber.HasValue) // Uses convenience accessor
                {
                    var prTitle = GitHubMetadata.PullRequestTitle;
                    if (!string.IsNullOrWhiteSpace(prTitle))
                    {
                        return $"PR #{GitHubMetadata.PullRequestNumber}: {prTitle} ({buildInfoStr ?? artifactName})";
                    }
                    return $"PR #{GitHubMetadata.PullRequestNumber} ({buildInfoStr ?? artifactName})";
                }

                if (GitHubMetadata.WorkflowRunNumber.HasValue) // Uses convenience accessor
                {
                     return $"Build #{GitHubMetadata.WorkflowRunNumber} ({buildInfoStr ?? artifactName})";
                }
                
                return !string.IsNullOrEmpty(buildInfoStr) ? buildInfoStr : Name;
            }
            return Name;
        }

        /// <summary>
        /// Creates a deep copy of this GameVersion instance
        /// </summary>
        public GameVersion Clone()
        {
            var cloned = new GameVersion
            {
                Id = this.Id,
                Name = this.Name,
                InstallationType = this.InstallationType,
                Description = this.Description,
                InstallPath = this.InstallPath,
                GamePath = this.GamePath,
                ExecutablePath = this.ExecutablePath,
                InstallDate = this.InstallDate,
                SourceType = this.SourceType,
                GameType = this.GameType,
                InstallName = this.InstallName,
                DisplayName = this.DisplayName,
                GameVariant = this.GameVariant,
                IsValid = this.IsValid,
                IsZeroHour = this.IsZeroHour,
                InstallSizeBytes = this.InstallSizeBytes,
                BuildDate = this.BuildDate,
                Options = this.Options // Note: ExtractOptions might need its own Clone method
            };

            // Handle source-specific metadata cloning
            if (this.SourceSpecificMetadata != null)
            {
                cloned.SourceSpecificMetadata = this.SourceSpecificMetadata switch
                {
                    GitHubSourceMetadata githubMeta => githubMeta.Clone(),
                    _ => (BaseSourceMetadata)this.SourceSpecificMetadata.Clone() // Use base Clone method
                };
            }

            return cloned;
        }
    }
}
