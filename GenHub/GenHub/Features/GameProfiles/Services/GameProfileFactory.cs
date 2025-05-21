using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Core.Models;
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
    }
}
