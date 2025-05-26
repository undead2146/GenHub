using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Models;


namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Model for a game profile including launch settings and metadata following MVVM patterns
    /// </summary>
    public class GameProfile : IGameProfile, IEntityIdentifier<string>
    {
        /// <summary>
        /// Gets or sets the unique identifier for this profile
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the profile
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the profile
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the path to the game executable
        /// </summary>
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the game data directory
        /// </summary>
        public string? DataPath { get; set; }

        /// <summary>
        /// Gets or sets the game variant for this profile
        /// </summary>
        public GameVariant GameVariant { get; set; } = GameVariant.Unknown;


        /// <summary>
        /// Gets or sets the path to the profile icon
        /// </summary>
        public string? IconPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the cover image
        /// </summary>
        public string? CoverImagePath { get; set; }

        /// <summary>
        /// Gets or sets the theme color for the profile
        /// </summary>
        public string? ThemeColor { get; set; }

        /// <summary>
        /// Gets or sets the theme color value for the profile (interface implementation)
        /// </summary>
        public string? ColorValue 
        { 
            get => ThemeColor ?? string.Empty;
            set => ThemeColor = value;
        }

        /// <summary>
        /// Gets or sets the command line arguments
        /// </summary>
        public string? CommandLineArguments 
        { 
            get => LaunchArguments ?? string.Empty;
            set => LaunchArguments = value;
        }

        /// <summary>
        /// Gets or sets the game version identifier
        /// </summary>
        public string? VersionId 
        { 
            get => GameVersion?.Id;
            set 
            { 
                if (GameVersion != null) 
                    GameVersion.Id = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets or sets whether this profile is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets whether this is a default profile
        /// </summary>
        public bool IsDefaultProfile { get; set; }

        /// <summary>
        /// Gets or sets whether this is a custom profile
        /// </summary>
        public bool IsCustomProfile { get; set; }

        /// <summary>
        /// Gets or sets whether the game is installed
        /// </summary>
        public bool IsInstalled 
        { 
            get => GameVersion?.IsValid ?? false;
            set 
            { 
                if (GameVersion != null) 
                    GameVersion.IsValid = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to run as administrator
        /// </summary>
        public bool RunAsAdmin { get; set; }

        /// <summary>
        /// Gets or sets when this profile was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this profile was last modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the source type
        /// </summary>
        public GameInstallationType SourceType 
        { 
            get => GameVersion?.SourceType ?? GameInstallationType.Unknown;
            set 
            { 
                if (GameVersion != null) 
                    GameVersion.SourceType = value;
            }
        }

        /// <summary>
        /// Gets or sets the display order
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Gets or sets the launch arguments for the game
        /// </summary>
        public string? LaunchArguments { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the game
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the game version this profile is associated with
        /// </summary>
        [JsonIgnore]
        public GameVersion? GameVersion { get; set; }

        /// <summary>
        /// Gets GitHub metadata if this profile is from GitHub
        /// </summary>
        [JsonIgnore]
        public GitHubSourceMetadata? GitHubMetadata 
        { 
            get => SourceSpecificMetadata as GitHubSourceMetadata;
        }

        /// <summary>
        /// Gets whether this profile is from GitHub
        /// </summary>
        [JsonIgnore]
        public bool IsFromGitHub => 
            SourceType == GameInstallationType.GitHubArtifact && 
            GitHubMetadata != null;

        /// <summary>
        /// Gets or sets source-specific metadata
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BaseSourceMetadata? SourceSpecificMetadata { get; set; }

        /// <summary>
        /// Creates a deep copy of this GameProfile
        /// </summary>
        public GameProfile Clone()
        {
            var cloned = new GameProfile
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                GameVariant = this.GameVariant,
                GameVersion = this.GameVersion?.Clone(),
                IconPath = this.IconPath,
                CoverImagePath = this.CoverImagePath,
                ThemeColor = this.ThemeColor,
                IsActive = this.IsActive,
                CreatedAt = this.CreatedAt,
                LastModified = this.LastModified,
                LaunchArguments = this.LaunchArguments,
                WorkingDirectory = this.WorkingDirectory,
            };

            // Handle source-specific metadata cloning
            if (this.SourceSpecificMetadata != null)
            {
                cloned.SourceSpecificMetadata = this.SourceSpecificMetadata switch
                {
                    GitHubSourceMetadata githubMeta => githubMeta.Clone(),
                    _ => this.SourceSpecificMetadata
                };
            }

            return cloned;
        }

        public override string ToString()
        {
            return Name;
        }

        public GameProfile()
        {
        }

        /// <summary>
        /// Creates a new GameProfile from an IGameProfile interface
        /// </summary>
        public GameProfile(IGameProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            Id = profile.Id;
            Name = profile.Name ?? string.Empty;
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
            DisplayOrder = profile.DisplayOrder;
            SourceType = profile.SourceType;
            SourceSpecificMetadata = profile.SourceSpecificMetadata;
        }
    }
}
