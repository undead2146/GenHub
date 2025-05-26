using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Features.GitHub.Helpers;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Service for launching games
    /// </summary>
    public class GameLauncherService : IGameLauncherService
    {
        private readonly ILogger<GameLauncherService> _logger;
        private readonly IGameVersionRepository _versionRepository;
        private readonly IGameExecutableLocator _executableLocator;

        /// <summary>
        /// Creates a new instance of GameLauncherService
        /// </summary>
        public GameLauncherService(
            ILogger<GameLauncherService> logger,
            IGameVersionRepository versionRepository,
            IGameExecutableLocator executableLocator)
        {
            _logger = logger;
            _versionRepository = versionRepository;
            _executableLocator = executableLocator;
        }

        /// <summary>
        /// Launches a game profile
        /// </summary>
        public async Task<OperationResult> LaunchGameAsync(IGameProfile profile, CancellationToken cancellationToken = default)
        {
            return await LaunchVersionAsync(profile, cancellationToken);
        }

        /// <summary>
        /// Launches a specific game version
        /// </summary>
        public async Task<OperationResult> LaunchVersionAsync(string versionId, string? arguments = null, bool runAsAdmin = false)
        {
            try
            {
                var version = await _versionRepository.GetByIdAsync(versionId); 
                
                if (version == null)
                {
                    _logger.LogWarning("Cannot launch version - version with ID {VersionId} not found", versionId);
                    return OperationResult.Failed($"Version with ID {versionId} not found");
                }
                
                if (string.IsNullOrEmpty(version.ExecutablePath))
                {
                    _logger.LogWarning("Cannot launch version - no executable path specified");
                    return OperationResult.Failed("No executable path specified");
                }
                
                if (!FileExists(version.ExecutablePath))
                {
                    _logger.LogWarning("Cannot launch version - executable not found: {ExecutablePath}", version.ExecutablePath);
                    
                    // Try to find a suitable replacement executable
                    string installPath = version.InstallPath ?? GetDirectoryName(version.ExecutablePath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(installPath) && DirectoryExists(installPath))
                    {
                        bool isZeroHour = version.IsZeroHour || _executableLocator.IsZeroHourDirectory(installPath);
                        var newExecutablePath = await _executableLocator.FindBestGameExecutableAsync(
                            installPath, isZeroHour);
                            
                        if (!string.IsNullOrEmpty(newExecutablePath) && FileExists(newExecutablePath))
                        {
                            _logger.LogInformation("Found alternative executable: {ExecutablePath}", newExecutablePath);
                            
                            // Update the version with the new executable path
                            version.ExecutablePath = newExecutablePath;
                            await _versionRepository.UpdateAsync(version);
                        }
                        else
                        {
                            return OperationResult.Failed($"Executable not found: {version.ExecutablePath}");
                        }
                    }
                    else
                    {
                        return OperationResult.Failed($"Executable not found: {version.ExecutablePath}");
                    }
                }
                
                // Get executable info for better diagnostics
                var executableInfo = _executableLocator.GetExecutableInfo(version.ExecutablePath);
                _logger.LogInformation("Launching executable: {ExecutablePath} of type {GameType}", 
                    version.ExecutablePath, executableInfo.GameType);
                
                // Use the version's install path as working directory if available
                string workingDirectory = !string.IsNullOrEmpty(version.InstallPath) && DirectoryExists(version.InstallPath) 
                    ? version.InstallPath 
                    : GetDirectoryName(version.ExecutablePath) ?? string.Empty;
                
                // Launch the executable and get the result
                var result = await LaunchExecutableAsync(version.ExecutablePath, workingDirectory, arguments, runAsAdmin);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch version with ID: {VersionId}", versionId);
                return OperationResult.Failed($"Failed to launch version: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Launches a game version with optional arguments
        /// </summary>
        public async Task<OperationResult> LaunchVersionAsync(GameVersion version, string? arguments = null, CancellationToken cancellationToken = default)
        {
            if (version == null)
            {
                _logger.LogWarning("Cannot launch null game version");
                return OperationResult.Failed("Game version cannot be null");
            }

            try
            {
                _logger.LogInformation("Launching game version: {VersionId} - {Name}", version.Id, version.Name);

                if (string.IsNullOrEmpty(version.ExecutablePath))
                {
                    _logger.LogWarning("Cannot launch version - no executable path specified");
                    return OperationResult.Failed("No executable path specified");
                }
                
                if (!FileExists(version.ExecutablePath))
                {
                    _logger.LogWarning("Cannot launch version - executable not found: {ExecutablePath}", version.ExecutablePath);
                    return OperationResult.Failed($"Executable not found: {version.ExecutablePath}");
                }

                // Extract arguments and admin flag from version options if they exist
                string effectiveArguments = arguments ?? string.Empty;
                bool runAsAdmin = false;

                if (version.Options?.AdditionalParams != null)
                {
                    if (version.Options.AdditionalParams.TryGetValue("CommandLineArguments", out var commandLineArgs))
                    {
                        effectiveArguments = string.IsNullOrEmpty(arguments) ? commandLineArgs : $"{arguments} {commandLineArgs}";
                    }

                    if (version.Options.AdditionalParams.TryGetValue("RunAsAdmin", out var runAsAdminOption) &&
                        bool.TryParse(runAsAdminOption, out var runAsAdminValue))
                    {
                        runAsAdmin = runAsAdminValue;
                    }
                }

                // Use the version's install path as working directory if available
                string workingDirectory = !string.IsNullOrEmpty(version.InstallPath) && DirectoryExists(version.InstallPath) 
                    ? version.InstallPath 
                    : GetDirectoryName(version.ExecutablePath) ?? string.Empty;
                
                // Launch the executable and return the result
                var result = await LaunchExecutableAsync(version.ExecutablePath, workingDirectory, effectiveArguments, runAsAdmin);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching game version: {VersionId}", version.Id);
                return OperationResult.Failed($"Failed to launch game version: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Launches a game using the specified profile
        /// </summary>
        public async Task<OperationResult> LaunchVersionAsync(IGameProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null)
            {
                _logger.LogWarning("Cannot launch null profile");
                return OperationResult.Failed("Profile cannot be null");
            }
            
            try
            {
                // Prepare the game for launch
                var prepResult = await PrepareGameLaunchAsync(profile);
                if (!prepResult.Success)
                {
                    _logger.LogWarning("Failed to prepare game for launch: {Error}", prepResult.ErrorMessage);
                    return OperationResult.Failed(prepResult.ErrorMessage ?? "Failed to prepare game for launch");
                }
                
                // Launch with the optimized paths
                var result = await LaunchExecutableAsync(
                    prepResult.ExecutablePath!, 
                    prepResult.WorkingDirectory!, 
                    profile.CommandLineArguments, 
                    profile.RunAsAdmin);
                    
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch profile: {ProfileId}", profile.Id);
                return OperationResult.Failed($"Failed to launch profile: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Core method to launch an executable with proper working directory
        /// </summary>
        private async Task<OperationResult> LaunchExecutableAsync(string executablePath, string? workingDirectory, string? arguments = null, bool runAsAdmin = false)
        {
            try
            {
                if (!FileExists(executablePath))
                {
                    _logger.LogWarning("Executable not found: {ExecutablePath}", executablePath);
                    return OperationResult.Failed($"Executable not found: {executablePath}");
                }
                
                // If no working directory specified, use the executable's directory
                string effectiveWorkingDir = !string.IsNullOrEmpty(workingDirectory) && DirectoryExists(workingDirectory)
                    ? workingDirectory
                    : GetDirectoryName(executablePath) ?? string.Empty;
                
                _logger.LogInformation("Launching executable: {ExePath} in working directory: {WorkingDir}",
                    executablePath, effectiveWorkingDir);
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = effectiveWorkingDir,
                    UseShellExecute = true
                };
                
                if (!string.IsNullOrEmpty(arguments))
                {
                    processInfo.Arguments = arguments;
                }
                
                if (runAsAdmin)
                {
                    processInfo.Verb = "runas"; // Run as administrator
                }
                
                var process = StartProcess(processInfo);
                if (process != null)
                {
                    _logger.LogInformation("Successfully launched: {ExePath}", executablePath);
                    return OperationResult.Succeeded();
                }
                else
                {
                    return OperationResult.Failed("Failed to start process");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching executable: {ExePath}", executablePath);
                return OperationResult.Failed($"Failed to launch executable: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Prepares a game for launching by handling executable path and working directory issues
        /// </summary>
        public async Task<GameLaunchPrepResult> PrepareGameLaunchAsync(IGameProfile profile)
        {
            try
            {
                if (profile == null)
                    return GameLaunchPrepResult.Failed("Profile is null");
                
                _logger.LogInformation("Preparing to launch profile: {ProfileId} - {ProfileName}", profile.Id, profile.Name);
                
                string executablePath = profile.ExecutablePath;
                string workingDir = !string.IsNullOrEmpty(profile.DataPath) && DirectoryExists(profile.DataPath) ? 
                    profile.DataPath : GetDirectoryName(profile.ExecutablePath) ?? string.Empty;
                
                // Handle version-specific launch
                if (!string.IsNullOrEmpty(profile.VersionId))
                {
                    var version = await _versionRepository.GetByIdAsync(profile.VersionId);
                    if (version != null)
                    {
                        _logger.LogInformation("Using version information: {VersionId}", version.Id);
                        
                        // Use version's executable path
                        executablePath = version.ExecutablePath;
                        
                        // Check executable validity and game type
                        var executableInfo = _executableLocator.GetExecutableInfo(executablePath);
                        
                        // For tools/utilities, find appropriate game executable instead
                        if (executableInfo?.GameType == "Utility")
                        {
                            var exeDir = GetDirectoryName(executablePath) ?? string.Empty;
                            
                            // First try to find the appropriate game executable based on profile settings
                            bool isZeroHour = profile.SourceSpecificMetadata is GitHubSourceMetadata githubMeta &&
                                            githubMeta.AssociatedArtifact?.BuildInfo?.GameVariant == GameVariant.ZeroHour;
                                            
                            var gameExePath = await _executableLocator.FindBestGameExecutableAsync(exeDir, isZeroHour);
                            
                            if (!string.IsNullOrEmpty(gameExePath) && FileExists(gameExePath))
                            {
                                _logger.LogInformation("Using game executable instead of utility: {NewExe}", 
                                    Path.GetFileName(gameExePath));
                                executablePath = gameExePath;
                            }
                        }
                        
                        // Use the specified data path, or the version's path if none specified
                        workingDir = !string.IsNullOrEmpty(profile.DataPath) && DirectoryExists(profile.DataPath) ? 
                            profile.DataPath : version.InstallPath ?? string.Empty;
                    }
                }
                
                // Basic path validation
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogWarning("No executable path specified for profile: {ProfileId}", profile.Id);
                    return GameLaunchPrepResult.Failed("No executable path specified");
                }

                // Handle special case for separate executable and data directories
                bool needsWorkingDirFix = !string.IsNullOrEmpty(workingDir) && 
                                         !string.Equals(GetDirectoryName(executablePath), workingDir, StringComparison.OrdinalIgnoreCase);
                
                // If it's a GitHub artifact or has different paths, try to ensure executable works from data dir
                if (executablePath.Contains("GenHub") && executablePath.Contains("Versions") && needsWorkingDirFix)
                {
                    // Get the executable filename
                    string exeFileName = Path.GetFileName(executablePath);
                    string targetExecutablePath = Path.Combine(workingDir, exeFileName);
                    
                    // Try several strategies to ensure the executable can be launched from the working directory
                    
                    // Strategy 1: Check if a copy of the executable already exists in the data directory
                    if (FileExists(targetExecutablePath))
                    {
                        _logger.LogInformation("Using existing executable copy in data directory: {ExecutablePath}", targetExecutablePath);
                        executablePath = targetExecutablePath;
                    }
                    // Strategy 2: Try to copy the executable to the data directory
                    else if (FileExists(executablePath))
                    {
                        try
                        {
                            _logger.LogInformation("Copying executable to data directory: {Source} -> {Target}", 
                                executablePath, targetExecutablePath);
                                
                            // Get original attributes
                            var originalAttributes = GetFileAttributes(executablePath);
                            
                            // Copy the file
                            CopyFile(executablePath, targetExecutablePath, true);
                            
                            // Apply original attributes
                            SetFileAttributes(targetExecutablePath, originalAttributes);
                            
                            executablePath = targetExecutablePath;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to copy executable to data directory, will use original with working dir");
                            // Continue with original path, but use working dir
                        }
                    }
                    // Strategy 3: If on Windows, try to create a symbolic link for the executable
                    else if (!FileExists(targetExecutablePath) && 
                             Environment.OSVersion.Platform == PlatformID.Win32NT &&
                             FileExists(executablePath))
                    {
                        try
                        {
                            if (FileExists(targetExecutablePath)) 
                                DeleteFile(targetExecutablePath);
                                
                            _logger.LogInformation("Creating symbolic link in data directory: {Target} -> {Source}", 
                                targetExecutablePath, executablePath);
                                
                            // Create a symbolic link using cmd
                            var psi = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c mklink \"{targetExecutablePath}\" \"{executablePath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };
                            
                            var process = StartProcess(psi);
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                                
                                if (process.ExitCode == 0 && FileExists(targetExecutablePath))
                                {
                                    executablePath = targetExecutablePath;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create symbolic link, will use original path with working dir");
                            // Continue with original path, but use working dir
                        }
                    }
                }
                
                // Final validation - make sure the executable exists
                if (!FileExists(executablePath))
                {
                    // Try to find any valid game executable in the working directory
                    var executableInfo = await _executableLocator.FindBestGameExecutableAsync(
                        workingDir,
                        profile.SourceSpecificMetadata is GitHubSourceMetadata githubMeta &&
                        githubMeta.AssociatedArtifact?.BuildInfo?.GameVariant == GameVariant.ZeroHour);
                    
                    if (!string.IsNullOrEmpty(executableInfo) && FileExists(executableInfo))
                    {
                        _logger.LogInformation("Using alternative executable found in working directory: {ExecutablePath}", executableInfo);
                        executablePath = executableInfo;
                    }
                    else
                    {
                        return GameLaunchPrepResult.Failed($"Executable not found: {executablePath}");
                    }
                }
                
                return GameLaunchPrepResult.Succeeded(executablePath, workingDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing game launch");
                return GameLaunchPrepResult.Failed($"Error preparing game launch: {ex.Message}");
            }
        }

        // Protected virtual methods for file system operations to facilitate testing
        protected virtual bool FileExists(string path) => File.Exists(path);
        protected virtual bool DirectoryExists(string path) => Directory.Exists(path);
        protected virtual string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
        protected virtual void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
        protected virtual FileAttributes GetFileAttributes(string path) => File.GetAttributes(path);
        protected virtual void SetFileAttributes(string path, FileAttributes attributes) => File.SetAttributes(path, attributes);
        protected virtual void DeleteFile(string path) => File.Delete(path);
        protected virtual Process? StartProcess(ProcessStartInfo startInfo) => Process.Start(startInfo);
    }
}
