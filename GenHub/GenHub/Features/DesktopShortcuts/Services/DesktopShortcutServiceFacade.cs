using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GameProfiles;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.DesktopShortcuts.Services
{
    /// <summary>
    /// Facade service that coordinates desktop shortcut operations across different components
    /// </summary>
    public class DesktopShortcutServiceFacade : IDesktopShortcutServiceFacade
    {
        private readonly IShortcutPlatformService _platformService;
        private readonly IShortcutCommandBuilder _commandBuilder;
        private readonly IShortcutIconExtractor _iconExtractor;
        private readonly IGameProfileManagerService _profileManager;
        private readonly ILogger<DesktopShortcutServiceFacade> _logger;

        public DesktopShortcutServiceFacade(
            IShortcutPlatformService platformService,
            IShortcutCommandBuilder commandBuilder,
            IShortcutIconExtractor iconExtractor,
            IGameProfileManagerService profileManager,
            ILogger<DesktopShortcutServiceFacade> logger)
        {
            _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
            _commandBuilder = commandBuilder ?? throw new ArgumentNullException(nameof(commandBuilder));
            _iconExtractor = iconExtractor ?? throw new ArgumentNullException(nameof(iconExtractor));
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a desktop shortcut for the specified profile
        /// </summary>
        /// <param name="profileId">Profile ID to create shortcut for</param>
        /// <param name="configuration">Shortcut configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<OperationResult> CreateShortcutAsync(string profileId, ShortcutConfiguration? configuration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating desktop shortcut for profile: {ProfileId}", profileId);

                // Get profile information
                var profile = await _profileManager.GetProfileAsync(profileId, cancellationToken);
                if (profile == null)
                {
                    return OperationResult.Failed($"Profile not found: {profileId}");
                }                // Create configuration if not provided
                if (configuration == null)
                {
                    configuration = new ShortcutConfiguration
                    {
                        ProfileId = profileId,
                        Name = profile.Name,
                        Description = profile.Description ?? $"Shortcut for {profile.Name}",
                        IconPath = profile.IconPath,
                        Type = ShortcutType.Profile,
                        Location = ShortcutLocation.Desktop
                    };
                }

                // Build command and create shortcut
                return await _platformService.CreateShortcutAsync(configuration, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating desktop shortcut for profile: {ProfileId}", profileId);
                return OperationResult.Failed($"Failed to create shortcut: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates desktop shortcuts for multiple profiles
        /// </summary>
        /// <param name="profileIds">Profile IDs to create shortcuts for</param>
        /// <param name="configuration">Shortcut configuration template</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with details about each shortcut</returns>
        public async Task<OperationResult> CreateBulkShortcutsAsync(string[] profileIds, ShortcutConfiguration? configuration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating bulk shortcuts for {Count} profiles", profileIds.Length);

                var results = new List<OperationResult>();
                var errors = new List<string>();

                foreach (var profileId in profileIds)
                {
                    var result = await CreateShortcutAsync(profileId, configuration, cancellationToken);
                    results.Add(result);
                    
                    if (!result.Success)
                    {
                        errors.Add($"Profile {profileId}: {result.Message}");
                    }
                }

                var successCount = results.Count(r => r.Success);
                _logger.LogInformation("Bulk shortcut creation completed. Success: {Success}/{Total}", successCount, profileIds.Length);

                if (errors.Any())
                {
                    return OperationResult.Failed($"Some shortcuts failed to create. Success: {successCount}/{profileIds.Length}. Errors: {string.Join("; ", errors)}");
                }

                return OperationResult.Succeeded($"Successfully created {successCount} shortcuts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bulk shortcuts");
                return OperationResult.Failed($"Bulk shortcut creation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Removes a desktop shortcut for the specified profile
        /// </summary>
        /// <param name="profileId">Profile ID to remove shortcut for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<OperationResult> RemoveShortcutAsync(string profileId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Removing desktop shortcut for profile: {ProfileId}", profileId);

                // Get profile information to build configuration
                var profile = await _profileManager.GetProfileAsync(profileId, cancellationToken);
                if (profile == null)
                {
                    return OperationResult.Failed($"Profile not found: {profileId}");
                }                var configuration = new ShortcutConfiguration
                {
                    ProfileId = profileId,
                    Name = profile.Name,
                    Type = ShortcutType.Profile,
                    Location = ShortcutLocation.Desktop
                };

                return await _platformService.RemoveShortcutAsync(configuration, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing desktop shortcut for profile: {ProfileId}", profileId);
                return OperationResult.Failed($"Failed to remove shortcut: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates all existing shortcuts and reports any issues
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation results for all shortcuts</returns>
        public async Task<OperationResult<ShortcutValidationSummary>> ValidateAllShortcutsAsync(CancellationToken cancellationToken = default)
        {
            try
            {                _logger.LogInformation("Validating all desktop shortcuts");

                var profiles = await _profileManager.GetProfilesAsync(cancellationToken);
                var validationSummary = new ShortcutValidationSummary();
                var processedCount = 0;

                foreach (var profile in profiles)
                {
                    var configuration = new ShortcutConfiguration
                    {
                        ProfileId = profile.Id,
                        Name = profile.Name,
                        Type = ShortcutType.Profile,
                        Location = ShortcutLocation.Desktop
                    };

                    var result = await _platformService.ValidateShortcutAsync(configuration, cancellationToken);
                    processedCount++;

                    var validationResult = new ShortcutValidationResult
                    {
                        Configuration = configuration,
                        IsValid = result.Success,
                        ShortcutExists = result.Success
                    };

                    if (!result.Success)
                    {
                        validationResult.Errors.Add(result.Message ?? "Validation failed");
                    }
                    else if (result.Data != null && !result.Data.IsValid)
                    {
                        validationResult.Warnings.Add("Shortcut validation issues found");
                        validationResult.IsValid = false;
                    }

                    validationSummary.Results.Add(validationResult);
                }

                // Update summary statistics
                validationSummary.TotalShortcuts = processedCount;
                validationSummary.ValidShortcuts = validationSummary.Results.Count(r => r.IsValid);
                validationSummary.InvalidShortcuts = validationSummary.TotalShortcuts - validationSummary.ValidShortcuts;

                _logger.LogInformation("Shortcut validation completed for {Count} profiles", processedCount);
                return OperationResult<ShortcutValidationSummary>.Succeeded(validationSummary);
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating shortcuts");
                var errorSummary = new ShortcutValidationSummary
                {
                    TotalShortcuts = 0,
                    ValidShortcuts = 0,
                    InvalidShortcuts = 0
                };
                errorSummary.Results.Add(new ShortcutValidationResult 
                { 
                    IsValid = false,
                    Errors = new List<string> { $"Validation failed: {ex.Message}" }
                });
                return OperationResult<ShortcutValidationSummary>.Failed($"Shortcut validation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Repairs broken shortcuts by updating their targets and arguments
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Repair operation result</returns>
        public async Task<OperationResult> RepairShortcutsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Repairing desktop shortcuts");

                var profiles = await _profileManager.GetProfilesAsync(cancellationToken);
                var repairResults = new List<OperationResult>();
                var errors = new List<string>();

                foreach (var profile in profiles)
                {                    var configuration = new ShortcutConfiguration
                    {
                        ProfileId = profile.Id,
                        Name = profile.Name,
                        Description = profile.Description ?? $"Shortcut for {profile.Name}",
                        IconPath = profile.IconPath,
                        Type = ShortcutType.Profile,
                        Location = ShortcutLocation.Desktop
                    };

                    var result = await _platformService.RepairShortcutAsync(configuration, cancellationToken);
                    repairResults.Add(result);

                    if (!result.Success)
                    {
                        errors.Add($"Profile {profile.Name}: {result.Message}");
                    }
                }

                var successCount = repairResults.Count(r => r.Success);
                _logger.LogInformation("Shortcut repair completed. Success: {Success}/{Total}", successCount, profiles.Count());

                if (errors.Any())
                {
                    return OperationResult.Failed($"Some shortcuts failed to repair. Success: {successCount}/{profiles.Count()}. Errors: {string.Join("; ", errors)}");
                }

                return OperationResult.Succeeded($"Successfully repaired {successCount} shortcuts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error repairing shortcuts");
                return OperationResult.Failed($"Shortcut repair failed: {ex.Message}", ex);
            }
        }
    }
}
