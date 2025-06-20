using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Models;
using GenHub.Core.Models.AppUpdate;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.Caching;

namespace GenHub.Features.AppUpdate.ViewModels
{
    public partial class UpdateNotificationViewModel : ObservableObject
    {
        private readonly IAppUpdateService _appUpdateService;
        private readonly ILogger<UpdateNotificationViewModel> _logger;
        private readonly IGitHubCachingRepository _gitHubCachingManager;
        private readonly ICacheService _cacheService;

        [ObservableProperty]
        private UpdateCheckResult _updateCheckResult;

        [ObservableProperty]
        private bool _isUpdating;

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private string _currentAppVersion = "Unknown";

        [ObservableProperty]
        private string _repositoryOwner = "undead2146";

        [ObservableProperty]
        private string _repositoryName = "GenHub";

        [ObservableProperty]
        private string _statusMessage = "Ready to check for updates";

        [ObservableProperty]
        private bool _isInitialized = false;

        [ObservableProperty]
        private bool _isLoadingSettings;

        [ObservableProperty]
        private ObservableCollection<GitHubRepository> _availableRepositories = new();

        [ObservableProperty]
        private GitHubRepository? _selectedRepository;

        // Add this new property
        [ObservableProperty]
        private bool _canInitiateUpdate;

        private readonly string _repositoriesFilePath;

        public UpdateNotificationViewModel(
            IAppUpdateService appUpdateService, 
            ILogger<UpdateNotificationViewModel> logger, 
            IGitHubCachingRepository gitHubCachingRepository,
            ICacheService cacheService)
        {
            _appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitHubCachingManager = gitHubCachingRepository ?? throw new ArgumentNullException(nameof(gitHubCachingRepository));

            // Set the current version from the AppUpdateService or use 0.0.0 as fallback
            CurrentAppVersion = appUpdateService.GetCurrentVersion();
            if (string.IsNullOrWhiteSpace(CurrentAppVersion) || CurrentAppVersion == "Unknown")
            {
                CurrentAppVersion = "0.0.0";
            }

            // Initialize UpdateCheckResult to empty state to avoid null reference exceptions
            UpdateCheckResult = new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                LatestRelease = new Core.Models.GitHub.GitHubRelease
                {
                    Name = "No updates checked",
                    Version = CurrentAppVersion
                }
            };

            // Log that we're initializing
            _logger.LogInformation("Initializing UpdateNotificationViewModel with version {Version}", CurrentAppVersion);

            // Get the repositories file path from the cache service
            _repositoriesFilePath = Path.Combine(
                cacheService.GetSharedSettingsDirectory(),
                "github_repositories.json");
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            IsLoadingSettings = true;

            try
            {
                // Load repositories using the repository manager, not direct file access
                var repositories = await _gitHubCachingManager.GetRepositoriesAsync();
                AvailableRepositories = new ObservableCollection<GitHubRepository>(repositories);

                if (AvailableRepositories.Count > 0)
                {
                    // Attempt to get current repository
                    var currentRepo = await _gitHubCachingManager.GetCurrentRepositoryAsync();
                    SelectedRepository = currentRepo ?? AvailableRepositories.FirstOrDefault();
                }

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UpdateNotificationViewModel");
                StatusMessage = $"Error loading repositories: {ex.Message}";
            }
            finally
            {
                IsLoadingSettings = false;
            }
        }

        private async Task LoadRepositoriesAsync()
        {
            try
            {
                _logger.LogInformation("Loading repositories from cache manager");

                // Use GetRepositoriesAsync method
                var repositories = await _gitHubCachingManager.GetRepositoriesAsync();

                _logger.LogInformation("Loaded {Count} repositories from cache manager",
                    repositories?.Count() ?? 0);

                // Clear and populate available repositories
                AvailableRepositories.Clear();

                if (repositories != null)
                {
                    foreach (var repo in repositories)
                    {
                        // Make sure DisplayName is populated
                        if (string.IsNullOrEmpty(repo.DisplayName))
                        {
                            repo.DisplayName = $"{repo.RepoOwner}/{repo.RepoName}";
                        }

                        AvailableRepositories.Add(repo);
                        _logger.LogDebug("Added repository: {Repo}", repo.DisplayName);
                    }
                }

                // Add default repository if it doesn't exist
                if (!AvailableRepositories.Any(r =>
                    r.RepoOwner == "undead2146" && r.RepoName == "GenHub"))
                {
                    var defaultRepo = new GitHubRepository
                    {
                        RepoOwner = "undead2146",
                        RepoName = "GenHub",
                        DisplayName = "GenHub (Default)"
                    };

                    AvailableRepositories.Add(defaultRepo);
                    _logger.LogInformation("Added default repository: {Repo}", defaultRepo.DisplayName);
                }

                _logger.LogInformation("Loaded {Count} repositories", AvailableRepositories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load repositories from cache");
                throw;
            }
        }

        partial void OnSelectedRepositoryChanged(GitHubRepository? oldValue, GitHubRepository? newValue)
        {
            if (newValue != null)
            {
                _logger.LogInformation("Repository selection changed to: {Repo}", newValue.DisplayName);

                RepositoryOwner = newValue.RepoOwner;
                RepositoryName = newValue.RepoName;

                // Save the selection - use Task.Run to not block UI
                Task.Run(async () =>
                {
                    try
                    {
                        await _appUpdateService.SaveRepositorySettingsAsync(newValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save repository settings: {Error}", ex.Message);
                    }
                });
            }
        }

        [RelayCommand]
        public async Task CheckForUpdatesAsync()
        {
            if (IsUpdating)
            {
                _logger.LogWarning("Update check requested while another update operation is in progress");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(RepositoryOwner) ||
                    string.IsNullOrWhiteSpace(RepositoryName))
                {
                    StatusMessage = "Repository settings are not configured";
                    return;
                }

                StatusMessage = "Checking for updates...";
                IsUpdating = true;

                _logger.LogInformation("Checking for updates from {Owner}/{Repo}", RepositoryOwner, RepositoryName);

                // Keep the existing UpdateCheckResult in case of failure
                var previousResult = UpdateCheckResult;

                // Create a dedicated CancellationTokenSource with a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                // Try to perform update check with resilient error handling
                UpdateCheckResult? result = null;
                try
                {
                    // First attempt - normal path
                    result = await _appUpdateService.CheckForUpdatesAsync(RepositoryOwner, RepositoryName, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("First update check attempt was canceled, retrying with no-cache option");

                    // Provide UI feedback
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusMessage = "Update check timed out. Retrying without cache...";
                    });

                    // If canceled (possibly due to cache issues), retry with a fresh token and bypass cache
                    using var retryToken = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    
                    try
                    {
                        // Bypass cache on retry
                        result = await _appUpdateService.CheckForUpdatesNoCache(RepositoryOwner, RepositoryName, retryToken.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("No-cache retry was also canceled");
                        StatusMessage = "Update check timed out. Please try again later.";
                        await EnsureValidUpdateResultAsync(previousResult);
                        return;
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "Retry also failed when checking for updates");
                    }
                }

                // Process the result (whether from first attempt or retry)
                await ProcessUpdateCheckResultAsync(result, previousResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error checking for updates: {ex.Message}";
                    IsUpdating = false;
                });
            }
            finally
            {
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Processes the update check result and updates the UI accordingly
        /// </summary>
        private async Task ProcessUpdateCheckResultAsync(UpdateCheckResult? result, UpdateCheckResult? previousResult)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (result != null)
                    {
                        UpdateCheckResult = result;

                        // Make sure the LatestRelease is not null to prevent binding errors
                        if (UpdateCheckResult.LatestRelease == null)
                        {
                            UpdateCheckResult.LatestRelease = new Core.Models.GitHub.GitHubRelease
                            {
                                Name = "No release information available",
                                Version = CurrentAppVersion
                            };
                        }
                    }
                    else
                    {
                        // Keep current result but mark as not available
                        if (previousResult != null)
                        {
                            previousResult.IsUpdateAvailable = false;
                            previousResult.ErrorMessages.Add("Failed to check for updates");

                            // Ensure LatestRelease is not null
                            if (previousResult.LatestRelease == null)
                            {
                                previousResult.LatestRelease = new Core.Models.GitHub.GitHubRelease
                                {
                                    Name = "No release information available",
                                    Version = CurrentAppVersion
                                };
                            }

                            UpdateCheckResult = previousResult;
                        }
                        else
                        {
                            // Create a default result if there was no previous result
                            UpdateCheckResult = new UpdateCheckResult
                            {
                                IsUpdateAvailable = false,
                                LatestRelease = new Core.Models.GitHub.GitHubRelease
                                {
                                    Name = "No release information available",
                                    Version = CurrentAppVersion
                                },
                                ErrorMessages = { "Failed to check for updates" }
                            };
                        }
                    }

                    // Update status message based on result
                    if (UpdateCheckResult.IsUpdateAvailable && UpdateCheckResult.LatestRelease != null)
                    {
                        StatusMessage = $"Update available: {UpdateCheckResult.LatestRelease.Version}";
                        _logger.LogInformation("Update available: {Version}", UpdateCheckResult.LatestRelease.Version);
                        
                        // Update the CanInitiateUpdate property and log it
                        CanInitiateUpdate = true;
                        _logger.LogDebug("CanInitiateUpdate set to: {Value}", CanInitiateUpdate);
                    }
                    else
                    {
                        StatusMessage = "You have the latest version";
                        _logger.LogInformation("No updates available");
                        
                        // Update the CanInitiateUpdate property and log it
                        CanInitiateUpdate = false;
                        _logger.LogDebug("CanInitiateUpdate set to: {Value}", CanInitiateUpdate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing update check result");
                    StatusMessage = "Error processing update check result";
                    CanInitiateUpdate = false;
                }
                finally
                {
                    IsUpdating = false;
                }
            });
        }

        /// <summary>
        /// Ensures that we always have a valid update result to prevent UI binding errors
        /// </summary>
        private async Task EnsureValidUpdateResultAsync(UpdateCheckResult? previousResult)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    // If we have a previous result, keep it but mark as not available
                    if (previousResult != null)
                    {
                        previousResult.IsUpdateAvailable = false;
                        
                        // Ensure LatestRelease is not null
                        if (previousResult.LatestRelease == null)
                        {
                            previousResult.LatestRelease = new Core.Models.GitHub.GitHubRelease
                            {
                                Name = "No release information available",
                                Version = CurrentAppVersion
                            };
                        }

                        UpdateCheckResult = previousResult;
                    }
                    else
                    {
                        // Create a default result
                        UpdateCheckResult = new UpdateCheckResult
                        {
                            IsUpdateAvailable = false,
                            LatestRelease = new Core.Models.GitHub.GitHubRelease
                            {
                                Name = "No release information available",
                                Version = CurrentAppVersion
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ensuring valid update result");
                }
            });
        }

        [RelayCommand]
        private async Task SaveRepositorySettings()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RepositoryOwner) ||
                    string.IsNullOrWhiteSpace(RepositoryName))
                {
                    StatusMessage = "Please provide valid repository owner and name";
                    return;
                }

                // Create settings object from current values
                var settings = new GitHubRepository
                {
                    RepoOwner = RepositoryOwner,
                    RepoName = RepositoryName,
                    DisplayName = $"{RepositoryOwner}/{RepositoryName}"
                };

                // Save to app update service
                await _appUpdateService.SaveRepositorySettingsAsync(settings);

                // Also ensure this repository is in the available repositories collection
                if (!AvailableRepositories.Any(r =>
                    r.RepoOwner == settings.RepoOwner &&
                    r.RepoName == settings.RepoName))
                {
                    AvailableRepositories.Add(settings);

                    // Save the updated list via caching manager
                    await _gitHubCachingManager.SaveRepositoriesAsync(AvailableRepositories);
                }

                StatusMessage = "Repository settings saved";

                // Select this repository in the dropdown
                SelectedRepository = settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save repository settings");
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewReleaseNotes()
        {
            if (UpdateCheckResult?.LatestRelease?.HtmlUrl != null)
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = UpdateCheckResult.LatestRelease.HtmlUrl,
                        UseShellExecute = true
                    };
                    Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening release notes URL: {Url}", UpdateCheckResult.LatestRelease.HtmlUrl);
                    StatusMessage = $"Error opening browser: {ex.Message}";
                }
            }
        }

        // Change the RelayCommand implementation - remove the CanExecute predicate
        [RelayCommand]
        private async Task InitiateUpdate()
        {
            try
            {
                _logger.LogInformation("InitiateUpdate command executing");

                if (UpdateCheckResult?.LatestRelease == null)
                {
                    StatusMessage = "Error: No release information available.";
                    return;
                }

                IsUpdating = true;
                InstallProgress = 0;
                StatusMessage = "Starting update process...";

                var progressIndicator = new Progress<UpdateProgress>(ReportUpdateProgress);
                await _appUpdateService.InitiateUpdateAsync(UpdateCheckResult.LatestRelease, progressIndicator);
                
                // Update the CanInitiateUpdate property
                CanInitiateUpdate = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update initiation");
                StatusMessage = $"Update failed: {ex.Message}";
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Provides progress reporting for update operations
        /// </summary>
        private void ReportUpdateProgress(UpdateProgress progress)
        {
            // Dispatch UI updates to the UI thread to prevent threading issues
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    // Update UI properties based on progress
                    InstallProgress = progress.PercentageCompleted;
                    StatusMessage = progress.Status;

                    if (!string.IsNullOrEmpty(progress.Message))
                        StatusMessage += $" ({progress.Message})";

                    // Track if the update is still in progress
                    IsUpdating = progress.IsInProgress;

                    // Update status when completed
                    if (!progress.IsInProgress)
                    {
                        StatusMessage = progress.IsSuccessful ?
                            "Update successful. Restarting application..." :
                            $"Update failed: {progress.Status}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UI with progress");
                }
            });
        }
    }
}
