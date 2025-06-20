using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GameProfiles.Services
{
    /// <summary>
    /// Service for extracting and managing profile metadata
    /// </summary>
    public class ProfileMetadataService
    {
        private readonly ILogger<ProfileMetadataService> _logger;
        private readonly IGameExecutableLocator _executableLocator;
        private readonly IGitHubApiClient _gitHubApiClient;

        /// <summary>
        /// Initializes a new instance of the ProfileMetadataService class
        /// </summary>
        public ProfileMetadataService(
            ILogger<ProfileMetadataService> logger,
            IGameExecutableLocator executableLocator,
            IGitHubApiClient gitHubApiClient = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executableLocator = executableLocator ?? throw new ArgumentNullException(nameof(executableLocator));
            _gitHubApiClient = gitHubApiClient;
        }

        /// <summary>
        /// Extracts GitHub information from various profile properties and populates GitHubSourceMetadata if possible.
        /// </summary>
        public void ExtractGitHubInfo(GameProfile profile)
        {
            if (profile == null)
                return;

            try
            {
                // Only operate if this is a GitHub-based profile
                if (!profile.IsFromGitHub)
                    return;

                var githubMeta = profile.SourceSpecificMetadata as GitHubSourceMetadata ?? new GitHubSourceMetadata();

                // Try to extract PR number and workflow/build info from description
                if (!string.IsNullOrEmpty(profile.Description))
                {
                    var prMatch = Regex.Match(profile.Description, @"PR\s*#?\s*(\d+)");
                    if (prMatch.Success && int.TryParse(prMatch.Groups[1].Value, out int prNumber))
                    {
                        if (githubMeta.AssociatedArtifact == null)
                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                        githubMeta.AssociatedArtifact.PullRequestNumber = prNumber;
                        _logger.LogDebug("Extracted PR number {PR} from description", prNumber);
                    }

                    var wfMatch = Regex.Match(profile.Description, @"(?:Workflow|Run|Build|WF)?\s*#\s*(\d+)");
                    if (wfMatch.Success && int.TryParse(wfMatch.Groups[1].Value, out int wfNumber))
                    {
                        if (githubMeta.AssociatedArtifact == null)
                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                        githubMeta.AssociatedArtifact.WorkflowNumber = wfNumber;
                        _logger.LogDebug("Extracted workflow number {WF} from description", wfNumber);
                    }

                    var configMatch = Regex.Match(profile.Description, @"(?:Build|Config|Preset):\s*(\w+(?:-\w+)+)");
                    if (configMatch.Success && string.IsNullOrEmpty(githubMeta.AssociatedArtifact?.BuildPreset))
                    {
                        if (githubMeta.AssociatedArtifact == null)
                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                        githubMeta.AssociatedArtifact.BuildPreset = configMatch.Groups[1].Value;
                        _logger.LogDebug("Extracted build preset {Preset} from description", githubMeta.AssociatedArtifact.BuildPreset);
                    }
                }

                // Try extracting from name if not found in description
                if ((githubMeta.AssociatedArtifact?.WorkflowNumber ?? 0) == 0 && !string.IsNullOrEmpty(profile.Name))
                {
                    var wfMatch = Regex.Match(profile.Name, @"(?:WF|#)(\d+)");
                    if (wfMatch.Success && int.TryParse(wfMatch.Groups[1].Value, out int wfNumber))
                    {
                        if (githubMeta.AssociatedArtifact == null)
                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                        githubMeta.AssociatedArtifact.WorkflowNumber = wfNumber;
                        _logger.LogDebug("Extracted workflow number {WF} from name", wfNumber);
                    }
                }

                // Try extracting from executable path for version/build info
                if (!string.IsNullOrEmpty(profile.ExecutablePath))
                {
                    var dirName = Path.GetDirectoryName(profile.ExecutablePath);
                    if (!string.IsNullOrEmpty(dirName))
                    {
                        var folderName = Path.GetFileName(dirName);

                        // Extract version parts from folder name format like "20250509_Generals-vc6-debug+t+e"
                        if (folderName != null && folderName.Contains("_") && folderName.Contains("-"))
                        {
                            var match = Regex.Match(folderName, @"(\d+)_(?:WF(\d+)_)?(.+)");
                            if (match.Success)
                            {
                                var datePart = match.Groups[1].Value;
                                var workflowPart = match.Groups[2].Value;
                                var buildPart = match.Groups[3].Value;

                                // Set workflow number if not already set
                                if (!string.IsNullOrEmpty(workflowPart) && (githubMeta.AssociatedArtifact?.WorkflowNumber ?? 0) == 0)
                                {
                                    if (int.TryParse(workflowPart, out int wfNumber))
                                    {
                                        if (githubMeta.AssociatedArtifact == null)
                                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                                        githubMeta.AssociatedArtifact.WorkflowNumber = wfNumber;
                                        _logger.LogDebug("Extracted workflow number {WF} from path", wfNumber);
                                    }
                                }

                                // Extract build preset if available
                                if (!string.IsNullOrEmpty(buildPart) && string.IsNullOrEmpty(githubMeta.AssociatedArtifact?.BuildPreset))
                                {
                                    var presetMatch = Regex.Match(buildPart, @"^[^-]+-(.+)$");
                                    if (presetMatch.Success)
                                    {
                                        if (githubMeta.AssociatedArtifact == null)
                                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                                        githubMeta.AssociatedArtifact.BuildPreset = presetMatch.Groups[1].Value;
                                        _logger.LogDebug("Extracted build preset {Preset} from path", githubMeta.AssociatedArtifact.BuildPreset);
                                    }
                                    else
                                    {
                                        if (githubMeta.AssociatedArtifact == null)
                                            githubMeta.AssociatedArtifact = new GitHubArtifact();
                                        githubMeta.AssociatedArtifact.BuildPreset = buildPart;
                                    }
                                }
                            }
                        }
                    }
                }

                // Assign back if we created a new metadata object
                if (profile.GitHubMetadata == null)
                    profile.SourceSpecificMetadata = githubMeta;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting GitHub info for profile {ProfileId}", profile.Id);
            }
        }
        /// <summary>
        /// Validates and enhances a profile with additional metadata
        /// </summary>
        public async Task ValidateAndEnhanceProfileAsync(GameProfile profile, CancellationToken cancellationToken = default)
        {
            try
            {
                if (profile?.GameVersion == null)
                {
                    _logger.LogWarning("Cannot validate profile with null GameVersion");
                    return;
                }

                var gameVersion = profile.GameVersion;
                bool executableExists = false;
                bool installExists = false;

                if (!string.IsNullOrEmpty(profile.ExecutablePath))
                {
                    executableExists = File.Exists(profile.ExecutablePath);
                    if (!executableExists)
                    {
                        _logger.LogWarning("Executable does not exist for profile {ProfileName}: {ExecutablePath}", 
                            profile.Name, profile.ExecutablePath);
                    }
                    else
                    {
                        _logger.LogDebug("Executable exists for profile {ProfileName}: {ExecutablePath}", 
                            profile.Name, profile.ExecutablePath);
                    }
                }

                if (!string.IsNullOrEmpty(gameVersion.InstallPath))
                {
                    installExists = Directory.Exists(gameVersion.InstallPath);
                    if (!installExists)
                    {
                        _logger.LogWarning("Install path does not exist for profile {ProfileName}: {InstallPath}", 
                            profile.Name, gameVersion.InstallPath);
                    }
                }

                if (string.IsNullOrEmpty(profile.WorkingDirectory) && !string.IsNullOrEmpty(gameVersion.GamePath))
                {
                    profile.WorkingDirectory = gameVersion.GamePath;
                    _logger.LogDebug("Set working directory for profile {ProfileName}: {WorkingDirectory}", 
                        profile.Name, profile.WorkingDirectory);
                }

                _logger.LogInformation("Profile validation completed for {ProfileName}", profile.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating profile {ProfileName}", profile?.Name ?? "unknown");
                throw;
            }
        }
        
/// <summary>
        /// Loads GitHub metadata for a game version asynchronously
        /// </summary>
        public async Task<GitHubSourceMetadata?> LoadGitHubMetadataAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            if (version == null || !version.IsFromGitHub)
                return null;

            try
            {
                // Create a new metadata object if one doesn't exist
                var metadata = version.GitHubMetadata ?? new GitHubSourceMetadata();

                // Initialize artifact object if needed
                if (metadata.AssociatedArtifact == null)
                {
                    metadata.AssociatedArtifact = new GitHubArtifact();
                }

                var artifact = metadata.AssociatedArtifact;

                if (metadata.BuildInfo == null)
                {
                    metadata.BuildInfo = new GitHubBuild
                    {
                        // Default values to avoid null reference exceptions in bindings
                        Compiler = "Unknown",
                        Configuration = "Unknown",
                        Version = "Unknown"
                    };
                }

                // Fix repository info if missing
                if (artifact.RepositoryInfo == null)
                {
                    artifact.RepositoryInfo = new GitHubRepository
                    {
                        RepoOwner = "TheSuperHackers",
                        RepoName = "GeneralsGameCode"
                    };

                    if (!string.IsNullOrEmpty(version.InstallPath))
                    {
                        // Extract from path if possible
                        var pathSegments = version.InstallPath.Split(Path.DirectorySeparatorChar);
                        var repoSegment = pathSegments.FirstOrDefault(p => p.Contains('/'));
                        if (repoSegment != null)
                        {
                            var parts = repoSegment.Split('/');
                            if (parts.Length == 2)
                            {
                                artifact.RepositoryInfo.RepoOwner = parts[0];
                                artifact.RepositoryInfo.RepoName = parts[1];
                            }
                        }
                    }
                }

                if (version.BuildDate.HasValue)
                {
                    artifact.CreatedAt = version.BuildDate.Value;
                }

                if (_gitHubApiClient == null)
                {
                    _logger.LogWarning("GitHubApiClient not available, using default metadata");

                    version.SourceSpecificMetadata = metadata;
                    version.GitHubMetadata = metadata;

                    return metadata;
                }

                // Use a linked cancellation token to ensure we respect timeouts
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(7));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token, cancellationToken);

                try
                {
                    // Load repository details if available
                    if (!string.IsNullOrEmpty(artifact.RepositoryInfo.RepoOwner) &&
                        !string.IsNullOrEmpty(artifact.RepositoryInfo.RepoName))
                    {
                        // Get workflow details if available with timeout
                        if (artifact.WorkflowNumber > 0)
                        {
                            try
                            {
                                var run = await _gitHubApiClient.GetWorkflowRunAsync(
                                    artifact.RepositoryInfo.RepoOwner,
                                    artifact.RepositoryInfo.RepoName,
                                    artifact.WorkflowNumber,
                                    linkedCts.Token);

                                if (run != null)
                                {
                                    metadata.BuildInfo.GameVariant = DetermineGameVariant(run);
                                    metadata.BuildInfo.Configuration = DetermineConfiguration(run);
                                    metadata.BuildInfo.Compiler = DetermineCompiler(run);
                                    metadata.BuildInfo.HasTFlag = run.Name?.Contains("+t") ?? false;
                                    metadata.BuildInfo.HasEFlag = run.Name?.Contains("+e") ?? false;

                                    // Update artifact with workflow details
                                    artifact.WorkflowId = run.WorkflowId;
                                    artifact.WorkflowNumber = run.WorkflowNumber;
                                    artifact.Name = run.Name;
                                    artifact.CommitSha = run.CommitSha;
                                    artifact.CommitMessage = run.CommitMessage;
                                    artifact.PullRequestNumber = run.PullRequestNumber;
                                    artifact.PullRequestTitle = run.PullRequestTitle;

                                    _logger.LogDebug("Updated metadata from workflow run: {RunName}", run.Name);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogWarning("GitHub API request timed out for workflow #{RunNumber}",
                                    artifact.WorkflowNumber);
                                // Continue with default values
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get workflow details for run #{RunNumber}",
                                    artifact.WorkflowNumber);
                                // Continue with default values
                            }
                        }

                        // IMPORTANT: Update the version's metadata before returning
                        version.SourceSpecificMetadata = metadata;
                        version.GitHubMetadata = metadata;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("GitHub metadata loading timed out");
                    // Still return metadata with default values
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading GitHub metadata for version {VersionId}", version.Id);

                // Create a fallback metadata object with valid BuildInfo
                var fallbackMetadata = new GitHubSourceMetadata();
                fallbackMetadata.BuildInfo = new GitHubBuild
                {
                    Compiler = "Unknown",
                    Configuration = "Unknown",
                    Version = "Unknown"
                };

                // IMPORTANT: Update the version's metadata even in error case
                version.SourceSpecificMetadata = fallbackMetadata;
                version.GitHubMetadata = fallbackMetadata;

                return fallbackMetadata;
            }
        }

        // Helper methods to extract information from workflow runs
        private GameVariant DetermineGameVariant(GitHubWorkflow workflow)
        {
            // Logic to determine if this is Generals or Zero Hour based on workflow data
            if (workflow.Name.Contains("ZeroHour") || workflow.Name.Contains("Zero Hour") ||
            workflow.WorkflowPath.Contains("zh") || workflow.Name.Contains("ZH"))
            return GameVariant.ZeroHour;
            
            return GameVariant.Generals;
        }

        private string DetermineConfiguration(GitHubWorkflow workflow)
        {
            // Extract configuration (debug, release, etc.) from workflow data
            if (workflow.Name.Contains("debug", StringComparison.OrdinalIgnoreCase))
            return "debug";
            if (workflow.Name.Contains("release", StringComparison.OrdinalIgnoreCase))
            return "release";
            if (workflow.Name.Contains("profile", StringComparison.OrdinalIgnoreCase))
            return "profile";
            
            return string.Empty;
        }

        private string DetermineCompiler(GitHubWorkflow workflow)
        {
            // Extract compiler info (vc6, etc.) from workflow data
            if (workflow.Name.Contains("vc6", StringComparison.OrdinalIgnoreCase))
            return "vc6";
            if (workflow.Name.Contains("win32", StringComparison.OrdinalIgnoreCase))
            return "win32";
            
            return string.Empty;
        }


        /// <summary>
        /// Generates a description from a game version, using GitHub metadata if present
        /// </summary>
        public string GenerateGameDescription(GameVersion version)
        {
            if (version == null)
                return string.Empty;

            try
            {
                if (version.IsFromGitHub && version.GitHubMetadata != null)
                {
                    var meta = version.GitHubMetadata;
                    var artifact = meta.AssociatedArtifact;
                    var parts = new List<string>();

                    // Add PR title if available
                    if (artifact?.PullRequestNumber.HasValue == true && !string.IsNullOrEmpty(artifact.PullRequestTitle))
                    {
                        parts.Add(artifact.PullRequestTitle);
                    }
                    else if (artifact?.PullRequestNumber.HasValue == true)
                    {
                        parts.Add($"PR #{artifact.PullRequestNumber}");
                    }

                    // Add build preset if available
                    if (!string.IsNullOrEmpty(artifact?.BuildPreset))
                    {
                        parts.Add($"Build: {artifact.BuildPreset}");
                    }
                    else if (!string.IsNullOrEmpty(meta.BuildPreset))
                    {
                        parts.Add($"Build: {meta.BuildPreset}");
                    }

                    // Add build date if available
                    if (version.BuildDate != default && version.BuildDate != null)
                    {
                        parts.Add($"Built: {version.BuildDate:yyyy-MM-dd}");
                    }
                    else if (meta.ArtifactCreationDate.HasValue)
                    {
                        parts.Add($"Built: {meta.ArtifactCreationDate.Value:yyyy-MM-dd}");
                    }

                    if (parts.Count > 0)
                        return string.Join(" - ", parts);
                }

                // For regular installations - delegate to GameExecutableLocator for game type detection
                bool isZeroHour = version.IsZeroHour;
                if (!isZeroHour && !string.IsNullOrEmpty(version.InstallPath))
                {
                    isZeroHour = _executableLocator.IsZeroHourDirectory(version.InstallPath);
                }

                string source = version.SourceType.ToString();
                return isZeroHour ?
                    $"Zero Hour from {source}" :
                    $"Generals from {source}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating description for version {VersionId}", version.Id);
                return "Unknown game version";
            }
        }

        /// <summary>
        /// Determines the appropriate game type name based on metadata
        /// </summary>
        public string DetermineGameTypeName(GameProfile profile)
        {
            try
            {
                // Use ExecutablePath to determine if this is Zero Hour
                if (!string.IsNullOrEmpty(profile.ExecutablePath))
                {
                    if (_executableLocator.IsZeroHourDirectory(Path.GetDirectoryName(profile.ExecutablePath) ?? string.Empty))
                    {
                        return "Zero Hour";
                    }

                    // Check executable name patterns
                    string fileName = Path.GetFileName(profile.ExecutablePath).ToLowerInvariant();
                    if (fileName.Contains("zerohour") || fileName.Contains("zh") || fileName.EndsWith("_zh.exe") || fileName.Contains("zero"))
                    {
                        return "Zero Hour";
                    }
                }

                // Default to Generals
                return "Generals";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining game type for profile {ProfileId}", profile.Id);
                return "Generals"; // Default when in doubt
            }
        }
    }
}
