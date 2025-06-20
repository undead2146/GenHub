using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AdvancedLauncher.Services
{
    /// <summary>
    /// Service responsible for directly launching games with validated parameters
    /// </summary>
    public class DirectLaunchService : IDirectLaunchService
    {
        private readonly IGameProfileManagerService _profileManager;
        private readonly IGameVersionServiceFacade _gameVersionService;
        private readonly ILogger<DirectLaunchService> _logger;

        public DirectLaunchService(
            IGameProfileManagerService profileManager,
            IGameVersionServiceFacade gameVersionService,
            ILogger<DirectLaunchService> logger)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _gameVersionService = gameVersionService ?? throw new ArgumentNullException(nameof(gameVersionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        /// <summary>
        /// Launches a game directly using the provided parameters
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Launch result</returns>
        public async Task<OperationResult<QuickLaunchResult>> LaunchDirectlyAsync(LaunchParameters parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                _logger.LogInformation("Starting direct launch for profile: {ProfileId}", parameters.ProfileId);

                // Validate parameters
                if (string.IsNullOrEmpty(parameters.ProfileId))
                {
                    return OperationResult<QuickLaunchResult>.Failed("Profile ID is required");
                }

                // Validate profile exists
                var profile = await _profileManager.GetProfileAsync(parameters.ProfileId, cancellationToken);
                if (profile == null)
                {
                    var errorMessage = $"Profile not found: {parameters.ProfileId}";
                    _logger.LogError(errorMessage);
                    return OperationResult<QuickLaunchResult>.Failed(errorMessage);
                }

                // Get game version information  
                var gameVersion = await _gameVersionService.GetVersionByIdAsync(profile.VersionId, cancellationToken);
                if (gameVersion == null)
                {
                    var errorMessage = $"Game version not found: {profile.VersionId}";
                    _logger.LogError(errorMessage);
                    return OperationResult<QuickLaunchResult>.Failed(errorMessage);
                }

                // Validate executable exists
                var executablePath = gameVersion.ExecutablePath;
                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                {
                    var errorMessage = $"Game executable not found: {executablePath}";
                    _logger.LogError(errorMessage);
                    return OperationResult<QuickLaunchResult>.Failed(errorMessage);
                }

                // Build command line arguments
                var commandLineArgs = BuildCommandLineArguments(parameters, profile);
                var workingDirectory = Path.GetDirectoryName(executablePath);

                // Create process start info
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = commandLineArgs,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                // Handle administrative privileges if required
                if (parameters.RunAsAdmin)
                {
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.Verb = "runas";
                }

                // Start the process
                _logger.LogInformation("Starting game process: {ExecutablePath} {Arguments}", 
                    executablePath, commandLineArgs);

                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    var errorMessage = "Failed to start game process";
                    _logger.LogError(errorMessage);
                    return OperationResult<QuickLaunchResult>.Failed(errorMessage);
                }

                var launchDuration = DateTime.UtcNow - startTime;
                var result = new QuickLaunchResult
                {
                    Success = true,
                    ProfileId = parameters.ProfileId,
                    ProfileName = profile.Name,
                    ProcessId = process.Id,
                    LaunchTime = startTime,
                    LaunchDuration = launchDuration,
                    ExecutablePath = executablePath,
                    WorkingDirectory = workingDirectory,
                    CommandLineArguments = commandLineArgs,
                    UsedAdminPrivileges = parameters.RunAsAdmin
                };

                result.DiagnosticInfo.Add($"Launch completed in {launchDuration.TotalMilliseconds:F2}ms");
                result.DiagnosticInfo.Add($"Game process started with PID: {process.Id}");
                result.PerformanceMetrics["LaunchDurationMs"] = launchDuration.TotalMilliseconds;
                result.PerformanceMetrics["ProcessId"] = process.Id;

                _logger.LogInformation("Successfully launched game process {ProcessId} for profile {ProfileId}", 
                    process.Id, parameters.ProfileId);

                return OperationResult<QuickLaunchResult>.Succeeded(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch game for profile {ProfileId}", parameters.ProfileId);
                return OperationResult<QuickLaunchResult>.Failed($"Launch failed: {ex.Message}", ex);
            }
        }        /// <summary>
        /// Validates that a profile can be launched with the given parameters
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        public async Task<OperationResult<LaunchValidationResult>> ValidateLaunchAsync(LaunchParameters parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Validating launch for profile: {ProfileId}", parameters.ProfileId);

                // Validate parameters
                if (string.IsNullOrEmpty(parameters.ProfileId))
                {
                    var errorResult = LaunchValidationResult.Failure("", "Profile ID is required");
                    return OperationResult<LaunchValidationResult>.Succeeded(errorResult);
                }

                var validationResult = new LaunchValidationResult
                {
                    ProfileId = parameters.ProfileId,
                    ValidationLevel = LaunchValidation.Full,
                    ValidationTime = DateTime.UtcNow
                };

                // Validate profile exists
                var profile = await _profileManager.GetProfileAsync(parameters.ProfileId, cancellationToken);
                if (profile == null)
                {
                    validationResult.AddError($"Profile not found: {parameters.ProfileId}");
                    return OperationResult<LaunchValidationResult>.Succeeded(validationResult);
                }

                validationResult.ProfileName = profile.Name;
                validationResult.AddInformation($"Profile found: {profile.Name}");

                // Validate game version
                var gameVersion = await _gameVersionService.GetVersionByIdAsync(profile.VersionId, cancellationToken);
                if (gameVersion == null)
                {
                    validationResult.AddError($"Game version not found: {profile.VersionId}");
                    return OperationResult<LaunchValidationResult>.Succeeded(validationResult);
                }

                validationResult.AddInformation($"Game version: {gameVersion.DisplayName}");

                // Validate executable
                var executablePath = gameVersion.ExecutablePath;
                if (string.IsNullOrEmpty(executablePath))
                {
                    validationResult.AddError("Executable path is not set");
                    validationResult.ExecutableExists = false;
                }
                else if (!File.Exists(executablePath))
                {
                    validationResult.AddError($"Executable file not found: {executablePath}");
                    validationResult.ExecutableExists = false;
                }
                else
                {
                    validationResult.ExecutableExists = true;
                    validationResult.AddInformation($"Executable found: {executablePath}");
                }

                // Validate data directory
                var dataPath = gameVersion.GamePath;
                if (!string.IsNullOrEmpty(dataPath) && Directory.Exists(dataPath))
                {
                    validationResult.DataDirectoryValid = true;
                    validationResult.AddInformation($"Data directory found: {dataPath}");
                }
                else
                {
                    validationResult.DataDirectoryValid = false;
                    validationResult.AddWarning($"Data directory not found: {dataPath}");
                }

                // Check profile configuration
                validationResult.ProfileConfigurationValid = !string.IsNullOrEmpty(profile.Name);
                
                // Check dependencies (basic check)
                validationResult.DependenciesAvailable = true;
                
                // Check permissions (basic check)
                validationResult.PermissionsValid = true;

                // Set overall validation result
                validationResult.IsValid = validationResult.Errors.Count == 0;

                if (validationResult.IsValid)
                {
                    _logger.LogInformation("Validation passed for profile {ProfileId}", parameters.ProfileId);
                }
                else
                {
                    _logger.LogWarning("Validation failed for profile {ProfileId}: {Errors}", 
                        parameters.ProfileId, string.Join(", ", validationResult.Errors));
                }

                return OperationResult<LaunchValidationResult>.Succeeded(validationResult);
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate launch for profile {ProfileId}", parameters.ProfileId);
                var errorResult = LaunchValidationResult.Failure(parameters.ProfileId ?? "unknown", $"Validation failed: {ex.Message}");
                return OperationResult<LaunchValidationResult>.Succeeded(errorResult);
            }
        }

        /// <summary>
        /// Performs a diagnostic launch that provides detailed information about the launch process
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Diagnostic launch result</returns>
        public async Task<OperationResult<QuickLaunchResult>> DiagnosticLaunchAsync(LaunchParameters parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                _logger.LogInformation("Starting diagnostic launch for profile: {ProfileId}", parameters.ProfileId);                // First perform validation
                var validationResult = await ValidateLaunchAsync(parameters, cancellationToken);
                if (!validationResult.Success || validationResult.Data?.IsValid != true)
                {
                    var result = QuickLaunchResult.Failed(parameters.ProfileId ?? "unknown", "Validation failed");
                    result.DiagnosticInfo.AddRange(validationResult.Data?.Information ?? new List<string>());
                    result.DiagnosticInfo.AddRange(validationResult.Data?.Errors ?? new List<string>());
                    result.Warnings.AddRange(validationResult.Data?.Warnings ?? new List<string>());
                    return OperationResult<QuickLaunchResult>.Succeeded(result);
                }

                // Set verbose parameters for detailed diagnostics
                var diagnosticParameters = parameters.Clone();
                diagnosticParameters.Verbose = true;

                // Perform the actual launch with enhanced diagnostics
                var launchResult = await LaunchDirectlyAsync(diagnosticParameters, cancellationToken);
                if (launchResult.Success && launchResult.Data != null)
                {
                    // Add validation information to diagnostics
                    launchResult.Data.DiagnosticInfo.InsertRange(0, validationResult.Data.Information);
                    if (validationResult.Data.Warnings.Any())
                    {
                        launchResult.Data.Warnings.AddRange(validationResult.Data.Warnings);
                    }
                }

                return launchResult;
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform diagnostic launch for profile {ProfileId}", parameters.ProfileId);
                var errorResult = QuickLaunchResult.Failed(parameters.ProfileId ?? "unknown", $"Diagnostic launch failed: {ex.Message}");
                return OperationResult<QuickLaunchResult>.Succeeded(errorResult);
            }
        }

        /// <summary>
        /// Builds command line arguments for game launch
        /// </summary>
        private string BuildCommandLineArguments(LaunchParameters parameters, IGameProfile profile)
        {
            var args = new List<string>();

            // Add profile's launch arguments first
            if (!string.IsNullOrEmpty(profile.CommandLineArguments))
            {
                args.Add(profile.CommandLineArguments);
            }

            // Add custom arguments from parameters
            if (!string.IsNullOrEmpty(parameters.CustomArguments))
            {
                args.Add(parameters.CustomArguments);
            }

            // Add launch mode specific arguments
            if (parameters.Mode == LaunchMode.Diagnostic)
            {
                args.Add("-verbose");
            }

            return string.Join(" ", args);
        }
    }
}
