using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Models.GameProfiles;
namespace GenHub.Features.GameProfiles.Services
{
    /// <summary>
    /// Factory implementation for creating GameProfile instances
    /// </summary>
    public class GameProfileFactory : IGameProfileFactory
    {
        private readonly ILogger<GameProfileFactory> _logger;
        private readonly ProfileResourceService _resourceService;
        private readonly ProfileMetadataService _metadataService;
        private readonly IGameExecutableLocator _executableLocator;

        public GameProfileFactory(
            ILogger<GameProfileFactory> logger,
            ProfileResourceService resourceService,
            ProfileMetadataService metadataService,
            IGameExecutableLocator executableLocator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
            _executableLocator = executableLocator ?? throw new ArgumentNullException(nameof(executableLocator));
        }

        /// <summary>
        /// Creates a GameProfile from a GameVersion
        /// </summary>
        public GameProfile CreateFromVersion(GameVersion version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));
                
            try
            {
                _logger.LogInformation("Creating profile from version: {VersionId}", version.Id);
                
                // Determine game type
                string gameType = version.IsZeroHour ? "Zero Hour" : "Generals";
                
                // Find appropriate resources for the game type
                var icon = _resourceService.FindIconForGameType(gameType);
                var cover = _resourceService.FindCoverForGameType(gameType);
                
                // Generate a unique ID for the profile
                var profile = new GameProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = version.Name,
                    ExecutablePath = version.ExecutablePath,
                    DataPath = version.InstallPath,
                    IconPath = icon?.Path ?? "avares://GenHub/Assets/icon-default.png",
                    CoverImagePath = cover?.Path ?? "avares://GenHub/Assets/default-cover.png",
                    VersionId = version.Id,
                    IsCustomProfile = true,
                    IsDefaultProfile = false,
                    SourceType = version.SourceType,
                    // Use ProfileThemeColor for correct coloring
                    ColorValue = ProfileThemeColor.GetColorForGameType(gameType) ?? 
                               (version.IsZeroHour ? ProfileThemeColor.ZeroHourColor : ProfileThemeColor.GeneralsColor)
                };
                
                // Set description based on version information
                profile.Description = _metadataService.GenerateGameDescription(version);
                
                // Populate GitHub-specific attributes
                PopulateGitHubMetadata(profile, version);
                
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile from version: {VersionId}", version.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Populates a GameProfile with metadata from a GameVersion's GitHub artifact
        /// </summary>
        public void PopulateGitHubMetadata(GameProfile profile, GameVersion version)
        {
            if (profile == null || version == null)
                return;
                
            try
            {
                // Use the new metadata model structure
                if (version.GitHubMetadata?.AssociatedArtifact != null)
                {
                    
                    // Clone the GitHubSourceMetadata from the version
                    profile.SourceSpecificMetadata = version.GitHubMetadata.Clone();
                    
                    var artifact = version.GitHubMetadata.AssociatedArtifact;

                    artifact.PullRequestNumber = version.GitHubMetadata.PullRequestNumber;
                    artifact.PullRequestTitle = version.GitHubMetadata.PullRequestTitle;
                    artifact.WorkflowNumber = version.GitHubMetadata.WorkflowRunNumber ?? 0;
                    artifact.RunId = version.GitHubMetadata.WorkflowRunId ?? 0;
                    artifact.WorkflowId = version.GitHubMetadata.WorkflowDefinitionId ?? 0;
                    artifact.CommitMessage = version.GitHubMetadata.CommitMessage ?? string.Empty;
                    artifact.CommitSha = version.GitHubMetadata.CommitSha ?? string.Empty;
                    artifact.BuildPreset = version.GitHubMetadata.BuildPreset ?? string.Empty;
                    artifact.EventType = version.GitHubMetadata.TriggeringEventType;
                    artifact.CreatedAt = version.GitHubMetadata.ArtifactCreationDate ?? artifact.CreatedAt;
                    artifact.BuildInfo = version.GitHubMetadata.BuildInfo;

                    // Optionally, update profile color based on build preset if available
                    if (!string.IsNullOrEmpty(version.GitHubMetadata.BuildPreset))
                    {
                        profile.ColorValue = ProfileThemeColor.GetColorForBuildPreset(version.GitHubMetadata.BuildPreset);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating GitHub metadata for profile from version: {VersionId}", version.Id);
            }
        }

        /// <summary>
        /// Creates a profile for an executable
        /// </summary>
        public async Task<GameProfile> CreateFromExecutableAsync(string executablePath, string gameType)
        {
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentNullException(nameof(executablePath));
                
            try
            {
                _logger.LogInformation("Creating profile for executable: {ExecutablePath}", executablePath);

                // Normalize the game type to ensure consistent naming
                string normalizedGameType = NormalizeGameType(gameType);
                
                // Get appropriate resources for the game type
                var icon = _resourceService.FindIconForGameType(normalizedGameType);
                var cover = _resourceService.FindCoverForGameType(normalizedGameType);
                
                _logger.LogDebug("Using game type: {GameType} (normalized from {OriginalType})", normalizedGameType, gameType);

                var profile = new GameProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = Path.GetFileNameWithoutExtension(executablePath),
                    ExecutablePath = executablePath,
                    DataPath = Path.GetDirectoryName(executablePath) ?? string.Empty,
                    IconPath = icon?.Path ?? "avares://GenHub/Assets/icon-default.png",
                    CoverImagePath = cover?.Path ?? "avares://GenHub/Assets/default-cover.png",
                    IsCustomProfile = true,
                    IsDefaultProfile = false,
                    SourceType = GameInstallationType.DirectoryImport
                };

                // Use ProfileThemeColor directly for consistent color assignment
                profile.ColorValue = ProfileThemeColor.GetColorForGameType(normalizedGameType) ?? 
                                   (normalizedGameType.Contains("Zero Hour") ? ProfileThemeColor.ZeroHourColor : ProfileThemeColor.GeneralsColor);
                                   
                _logger.LogInformation("Created profile with color {Color} for {GameType}", profile.ColorValue, normalizedGameType);

                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile for executable: {ExecutablePath}", executablePath);
                throw;
            }
        }
        
        /// <summary>
        /// Creates profiles from a list of executable paths
        /// </summary>
        public async Task<List<GameProfile>> CreateFromExecutablesAsync(List<string> executablePaths, IEnumerable<GameVersion>? existingVersions = null)
        {
            if (executablePaths == null)
                throw new ArgumentNullException(nameof(executablePaths));
                
            var profiles = new List<GameProfile>();
            
            try
            {
                foreach (var executablePath in executablePaths)
                {
                    // Skip if the path is empty or the file doesn't exist
                    if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                        continue;
                        
                    // Check if we already have a version with this executable
                    if (existingVersions != null)
                    {
                        bool hasExistingVersion = existingVersions.Any(v => 
                            !string.IsNullOrEmpty(v.ExecutablePath) && 
                            v.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
                            
                        if (hasExistingVersion)
                            continue;
                    }
                        
                    // Try to determine if this is Generals or Zero Hour
                    string gameType;
                    
                    if (_executableLocator != null)
                    {
                        // Use executable locator if available for more accurate detection
                        var executableInfo = _executableLocator.GetExecutableInfo(executablePath);
                        gameType = (executableInfo?.IsZeroHour == true) ? "Zero Hour" : "Generals";
                    }
                    else
                    {
                        // Fall back to simple filename check
                        gameType = Path.GetFileName(executablePath).Contains("zh", StringComparison.OrdinalIgnoreCase) ? 
                            "Zero Hour" : "Generals";
                    }
                        
                    _logger.LogInformation("Creating profile for {ExecutablePath}, determined game type: {GameType}", 
                        executablePath, gameType);
                        
                    // Create a profile for this executable
                    var profile = await CreateFromExecutableAsync(executablePath, gameType);
                    profiles.Add(profile);
                }
                
                _logger.LogInformation("Created {Count} profiles from executables", profiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profiles from executables");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Normalizes a game type string to ensure consistent naming
        /// </summary>
        public string NormalizeGameType(string gameType)
        {
            if (string.IsNullOrWhiteSpace(gameType))
                return "Generals";
                
            // Check if it's Zero Hour with various capitalizations
            if (gameType.IndexOf("zero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gameType.IndexOf("hour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gameType.IndexOf("zh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Zero Hour";
            }
            
            // Default to Generals for anything else
            return "Generals";
        }

        /// <summary>
        /// Creates a base profile from a game version
        /// </summary>
        private GameProfile CreateBaseProfile(GameVersion gameVersion, string profileName)
        {
            if (gameVersion == null)
                throw new ArgumentNullException(nameof(gameVersion));

            var profile = new GameProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = profileName,
                GameVersion = gameVersion,
                GameVariant = gameVersion.GameVariant,
                Description = $"Profile for {gameVersion.Name}",
                ExecutablePath = gameVersion.ExecutablePath,
                DataPath = gameVersion.GamePath,
                WorkingDirectory = gameVersion.GamePath,
                VersionId = gameVersion.Id,
                IsCustomProfile = false,
                IsDefaultProfile = false,
                SourceType = gameVersion.SourceType,
                ColorValue = "#FF4A90E2", // Default blue color
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Set default launch arguments based on game type
            if (gameVersion.IsZeroHour)
            {
                profile.LaunchArguments = "-quickstart";
            }

            return profile;
        }

        private void PopulateFromGitHubSourceMetadata(GameProfile profile, GitHubSourceMetadata githubMetadata)
        {
            profile.Description = $"GitHub Build: {githubMetadata.BuildPreset ?? "Unknown"}";
            
            // Populate additional profile fields based on GitHub metadata
            if (githubMetadata.AssociatedArtifact != null)
            {
                var artifact = githubMetadata.AssociatedArtifact;
                profile.Description += $" - Run #{githubMetadata.WorkflowRunNumber ?? artifact.WorkflowNumber}";
                
                if (githubMetadata.PullRequestNumber.HasValue)
                {
                    profile.Description += $" (PR #{githubMetadata.PullRequestNumber})";
                    profile.ColorValue = "#FF28A745"; // Green for PR builds
                }
                else
                {
                    profile.ColorValue = "#FF007BFF"; // Blue for regular builds
                }
                
                // Set game variant based on build info
                if (githubMetadata.BuildInfo != null)
                {
                    profile.GameVariant = githubMetadata.BuildInfo.GameVariant;
                }
            }
        }

        public async Task<GameProfile> CreateFromGitHubSourceAsync(
            GameVersion gameVersion, 
            string profileName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var profile = CreateBaseProfile(gameVersion, profileName);
                
                if (gameVersion.GitHubMetadata != null)
                {
                    PopulateFromGitHubSourceMetadata(profile, gameVersion.GitHubMetadata);
                }

                // Validate and enhance the profile with metadata service
                await _metadataService.ValidateAndEnhanceProfileAsync(profile, cancellationToken);
                
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile from GitHub source");
                throw;
            }
        }

        /// <summary>
        /// Generates a description for a game profile based on its metadata
        /// </summary>
        public string GenerateGameDescription(GameProfile profile)
        {
            if (profile == null)
                return "Unknown Game Profile";

            try
            {
                var description = new List<string>();

                // Add game variant
                if (profile.GameVariant != GameVariant.Unknown)
                {
                    description.Add($"Game: {profile.GameVariant}");
                }

                // Add source type
                if (profile.SourceType != GameInstallationType.Unknown)
                {
                    description.Add($"Source: {profile.SourceType}");
                }

                // Add GitHub-specific information if available
                if (profile.IsFromGitHub && profile.GitHubMetadata != null)
                {
                    var githubMeta = profile.GitHubMetadata;

                    if (githubMeta.PullRequestNumber.HasValue)
                    {
                        description.Add($"Pull Request: #{githubMeta.PullRequestNumber}");
                        if (!string.IsNullOrEmpty(githubMeta.PullRequestTitle))
                        {
                            description.Add($"PR Title: {githubMeta.PullRequestTitle}");
                        }
                    }

                    if (githubMeta.WorkflowRunNumber.HasValue)
                    {
                        description.Add($"Build: #{githubMeta.WorkflowRunNumber}");
                    }

                    if (!string.IsNullOrEmpty(githubMeta.BuildPreset))
                    {
                        description.Add($"Configuration: {githubMeta.BuildPreset}");
                    }

                    if (githubMeta.BuildInfo != null)
                    {
                        if (!string.IsNullOrEmpty(githubMeta.BuildInfo.Compiler))
                        {
                            description.Add($"Compiler: {githubMeta.BuildInfo.Compiler}");
                        }
                        if (!string.IsNullOrEmpty(githubMeta.BuildInfo.Configuration))
                        {
                            description.Add($"Build Type: {githubMeta.BuildInfo.Configuration}");
                        }
                    }

                    if (githubMeta.ArtifactCreationDate.HasValue)
                    {
                        description.Add($"Built: {githubMeta.ArtifactCreationDate.Value:yyyy-MM-dd}");
                    }
                }

                // Add installation path info
                if (!string.IsNullOrEmpty(profile.ExecutablePath))
                {
                    var directory = Path.GetDirectoryName(profile.ExecutablePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        description.Add($"Location: {Path.GetFileName(directory)}");
                    }
                }

                // Add install date
                description.Add($"Created: {profile.CreatedAt:yyyy-MM-dd}");

                return string.Join(" | ", description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating description for profile: {ProfileName}", profile.Name);
                return $"Profile: {profile.Name}";
            }
        }

        /// <summary>
        /// Generates a description for a game version
        /// </summary>
        public string GenerateGameDescription(GameVersion version)
        {
            if (version == null)
                return "Unknown Version";

            try
            {
                var description = new List<string>();

                // Add game variant
                if (version.GameVariant != GameVariant.Unknown)
                {
                    description.Add($"Game: {version.GameVariant}");
                }

                // Add source type
                if (version.SourceType != GameInstallationType.Unknown)
                {
                    description.Add($"Source: {version.SourceType}");
                }

                // Add GitHub-specific information if available
                if (version.IsFromGitHub && version.GitHubMetadata != null)
                {
                    var githubMeta = version.GitHubMetadata;

                    if (githubMeta.PullRequestNumber.HasValue)
                    {
                        description.Add($"Pull Request: #{githubMeta.PullRequestNumber}");
                    }

                    if (githubMeta.WorkflowRunNumber.HasValue)
                    {
                        description.Add($"Build: #{githubMeta.WorkflowRunNumber}");
                    }

                    if (!string.IsNullOrEmpty(githubMeta.BuildPreset))
                    {
                        description.Add($"Configuration: {githubMeta.BuildPreset}");
                    }

                    if (githubMeta.ArtifactCreationDate.HasValue)
                    {
                        description.Add($"Built: {githubMeta.ArtifactCreationDate.Value:yyyy-MM-dd}");
                    }
                }

                // Add build date
                if (version.BuildDate.HasValue)
                {
                    description.Add($"Built: {version.BuildDate.Value:yyyy-MM-dd}");
                }

                return string.Join(" | ", description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating description for version: {VersionName}", version.Name);
                return $"Version: {version.Name}";
            }
        }

        /// <summary>
        /// Creates a game profile from a game version
        /// </summary>
        public async Task<GameProfile> CreateFromGameVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            try
            {
                if (version == null)
                    throw new ArgumentNullException(nameof(version));

                _logger.LogInformation("Creating profile from game version: {VersionName}", version.Name);

                var profile = new GameProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = version.GetDisplayName(),
                    Description = await Task.Run(() => _metadataService.GenerateGameDescription(version), cancellationToken),
                    GameVersion = version,
                    GameVariant = version.GameVariant,
                    ExecutablePath = version.ExecutablePath,
                    WorkingDirectory = version.GamePath,
                    DataPath = version.InstallPath,
                    SourceType = version.SourceType,
                    SourceSpecificMetadata = version.SourceSpecificMetadata?.Clone() as BaseSourceMetadata,
                    IsInstalled = !string.IsNullOrEmpty(version.ExecutablePath) && File.Exists(version.ExecutablePath),
                    IsCustomProfile = false,
                    IsDefaultProfile = false,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                // Set color based on source type
                profile.ColorValue = version.SourceType switch
                {
                    GameInstallationType.GitHubArtifact => "#FF007BFF", // Blue for GitHub
                    GameInstallationType.GitHubRelease => "#FF28A745", // Green for releases
                    GameInstallationType.Steam => "#FF17A2B8", // Teal for Steam
                    GameInstallationType.EaApp => "#FFFFC107", // Yellow for EA App
                    _ => "#FF6C757D" // Gray for unknown
                };

                // Extract additional metadata if needed
                if (profile.IsFromGitHub)
                {
                    _metadataService?.ExtractGitHubInfo(profile);
                }

                _logger.LogInformation("Created profile from version: {ProfileName}", profile.Name);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile from game version: {VersionName}", version?.Name ?? "Unknown");
                throw;
            }
        }
    }
}
