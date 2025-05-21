using System;
using System.Text.Json.Serialization;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.SourceMetadata;


namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Model for a game profile including launch settings and metadata.
    /// </summary>
    public class GameProfile : IGameProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string DataPath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;
        public string ColorValue { get; set; } = "#FF6A00"; // Default color
        public string CommandLineArguments { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty; // ID of the associated GameVersion
        public bool IsDefaultProfile { get; set; }
        public bool IsCustomProfile { get; set; } = true;
        public bool IsInstalled { get; set; } = true; // Default for new profiles linked to installed versions
        public bool RunAsAdmin { get; set; }
        public GameInstallationType SourceType { get; set; } = GameInstallationType.Unknown;
        
        [JsonPropertyName("display_order")]
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Holds source-specific metadata (e.g., GitHub details).
        /// This property will be serialized if not null.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BaseSourceMetadata? SourceSpecificMetadata { get; set; }

        // Convenience Accessor
        /// <summary>
        /// Gets the GitHub-specific metadata for this game profile, if available.
        /// Returns null if SourceSpecificMetadata is not of type GitHubSourceMetadata.
        /// </summary>
        [JsonIgnore]
        public GitHubSourceMetadata? GitHubMetadata => SourceSpecificMetadata as GitHubSourceMetadata;
        
        [JsonIgnore]
        public bool IsFromGitHub => SourceType == GameInstallationType.GitHubArtifact && GitHubMetadata != null;


        public GameProfile()
        {
        }

        public GameProfile(IGameProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            Id = profile.Id;
            Name = profile.Name;
            Description = profile.Description;
            ExecutablePath = profile.ExecutablePath;
            DataPath = profile.DataPath;
            IconPath = profile.IconPath;
            CoverImagePath = profile.CoverImagePath;
            ColorValue = profile.ColorValue;
            CommandLineArguments = profile.CommandLineArguments;
            VersionId = profile.VersionId;
            IsDefaultProfile = profile.IsDefaultProfile;
            IsCustomProfile = profile.IsCustomProfile;
            IsInstalled = profile.IsInstalled;
            RunAsAdmin = profile.RunAsAdmin;
            SourceType = profile.SourceType;
            DisplayOrder = profile.DisplayOrder;

            // Deep copy or assign SourceSpecificMetadata.
            // If SourceSpecificMetadata contains complex objects that might be shared and modified,
            // consider a deep copy mechanism. For now, direct assignment.
            if (profile.SourceSpecificMetadata != null)
            {
                // If it's GitHubSourceMetadata, you might want to ensure AssociatedArtifact is also handled correctly (cloned if necessary)
                // For simplicity here, direct assignment.
                SourceSpecificMetadata = profile.SourceSpecificMetadata; 
            }
        }

        /// <summary>
        /// Creates a deep copy of this GameProfile
        /// </summary>
        public GameProfile Clone()
        {
            var clone = new GameProfile
            {
                Id = Id,
                Name = Name,
                Description = Description,
                ExecutablePath = ExecutablePath,
                DataPath = DataPath,
                CommandLineArguments = CommandLineArguments,
                IconPath = IconPath,
                CoverImagePath = CoverImagePath,
                ColorValue = ColorValue,
                RunAsAdmin = RunAsAdmin,
                VersionId = VersionId,
                SourceType = SourceType,
                IsDefaultProfile = IsDefaultProfile,
                IsCustomProfile = IsCustomProfile
            };

            // Copy source-specific metadata if present
            if (SourceSpecificMetadata != null)
            {
                clone.SourceSpecificMetadata = SourceSpecificMetadata switch
                {
                    GitHubSourceMetadata github => github.Clone(),
                    FileSystemSourceMetadata fs => fs.Clone(),
                    CustomSourceMetadata custom => custom.Clone(),
                    _ => SourceSpecificMetadata // Default shallow copy for unknown types
                };
            }

            return clone;
        }
    }
}
