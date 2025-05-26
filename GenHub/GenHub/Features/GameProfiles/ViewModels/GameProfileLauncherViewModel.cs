using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

using GenHub.Features.GameProfiles.Services;
using GenHub.Features.GameProfiles.Views;
using GenHub.Features.GameVersions.Services;
using GenHub.Core.Interfaces.GameVersions;

namespace GenHub.Features.GameProfiles.ViewModels
{
    /// <summary>
    /// ViewModel responsible for launching games and managing game profiles
    /// </summary>
    public partial class GameProfileLauncherViewModel : ViewModelBase
    {
        private readonly ILogger<GameProfileLauncherViewModel> _logger;
        private readonly IGameLauncherService _gameLauncherService;
        private readonly IGameVersionServiceFacade _gameVersionService;
        private readonly IGameProfileManagerService _profileManagerService;
        private readonly GameDetectionFacade _gameDetectionFacade;
        private readonly IGameExecutableLocator _gameExecutableLocator;
        private readonly IGameProfileFactory _gameProfileFactory;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        public bool _isLaunching;

        [ObservableProperty]
        public bool _isEditMode;

        // Add a property for binding in the ItemTemplate
        [ObservableProperty]
        private ObservableCollection<bool> _editModeTracker = new() { false };

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private GameProfileItemViewModel? _selectedProfile;

        [ObservableProperty]
        private ObservableCollection<GameProfileItemViewModel> _profiles = new();

        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Initializes a new instance of the GameProfileLauncherViewModel class
        /// </summary>
        public GameProfileLauncherViewModel(
            ILogger<GameProfileLauncherViewModel> logger,
            IGameLauncherService gameLauncherService,
            IGameVersionServiceFacade gameVersionService,
            IGameProfileManagerService profileManagerService,
            GameDetectionFacade gameDetectionFacade,
            IGameExecutableLocator gameExecutableLocator,
            IGameProfileFactory gameProfileFactory
            )
        {
            _logger = logger;
            _gameLauncherService = gameLauncherService;
            _gameVersionService = gameVersionService;
            _profileManagerService = profileManagerService;
            _gameDetectionFacade = gameDetectionFacade;
            _gameExecutableLocator = gameExecutableLocator;
            _gameProfileFactory = gameProfileFactory;

            // Subscribe to profile updates event
            _profileManagerService.ProfilesUpdated += OnProfilesUpdated;
        }

        // Update the event handler to match the delegate signature
        private void OnProfilesUpdated(object sender, IGameProfileManagerService.ProfilesUpdatedEventArgs e)
        {
            // If this ViewModel initiated the update, don't reload
            if (e.Source == this)
                return;

            // Only reload for external updates
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProfilesAsync();
                _logger.LogDebug("Profiles reloaded after external update");
            });
        }

        // Implement IDisposable to cleanup event subscriptions
        public void Dispose()
        {
            _profileManagerService.ProfilesUpdated -= OnProfilesUpdated;
        }

        ~GameProfileLauncherViewModel()
        {
            Dispose();
        }

        /// <summary>
        /// Initializes the dashboard and loads profiles
        /// </summary>
        [RelayCommand]
        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading profiles...";

                await LoadProfilesAsync();

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Error initializing dashboard");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Toggle edit mode for profile management
        /// </summary>
        [RelayCommand]
        private void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            
            // Use the editModeTracker to force item template refresh
            EditModeTracker[0] = IsEditMode;
            
            _logger.LogInformation("Edit mode toggled to: {IsEditMode}", IsEditMode);
            
            // Force profile item refresh by raising collection changed event
            RefreshProfilesDisplay();
        }

        // Method to refresh the profiles display without reloading from storage
        private void RefreshProfilesDisplay()
        {
            // Create a temporary profile to add/remove
            // This triggers the collection changed notification which forces template refresh
            if (Profiles.Count > 0)
            {
                var tempItem = Profiles[0];
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Save all profiles to a temp list
                    var allProfiles = Profiles.ToList();
                    
                    // Clear and re-add all profiles to force template refresh
                    Profiles.Clear();
                    foreach (var profile in allProfiles)
                    {
                        Profiles.Add(profile);
                    }
                });
            }
            
            // Force property change notification for IsEditMode again
            // after UI has had time to process the collection change
            Dispatcher.UIThread.Post(() => 
            {
                OnPropertyChanged(nameof(IsEditMode));
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Save all profiles
        /// </summary>
        [RelayCommand]
        private async Task SaveProfiles()
        {
            try
            {
                StatusMessage = "Saving profiles...";

                // Convert view models back to model objects for saving
                var profileModels = Profiles.Select(p => p.ToModel()).ToList();
                await _profileManagerService.SaveCustomProfilesAsync(profileModels);

                StatusMessage = $"Saved {Profiles.Count} profiles successfully";
                
                // Exit edit mode
                IsEditMode = false;
                
                // Update the editModeTracker to match
                EditModeTracker[0] = false;
                
                // Force UI refresh to ensure all elements update consistently
                RefreshProfilesDisplay();
                
                _logger.LogInformation("Profiles saved and exited edit mode");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving profiles: {ex.Message}";
                _logger.LogError(ex, "Error saving profiles");
            }
        }

        /// <summary>
        /// Loads game profiles
        /// </summary>
        private async Task LoadProfilesAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                _logger.LogInformation("Loading profiles");
                var customProfiles = await _profileManagerService.LoadCustomProfilesAsync(cancellationTokenSource.Token);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Clear and repopulate to maintain reactivity
                    Profiles.Clear();

                    foreach (var profile in customProfiles)
                    {
                        // Create a proper view model that preserves ALL properties
                        var viewModel = new GameProfileItemViewModel(profile);

                        // Add launch command - this is important!
                        viewModel.LaunchCommand = new RelayCommand(() => LaunchProfile(viewModel));

                        // Add to the collection
                        Profiles.Add(viewModel);
                    }
                });

                _logger.LogInformation("Loaded {Count} profiles from storage", Profiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profiles");
                StatusMessage = $"Error loading profiles: {ex.Message}";
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Edit an existing profile
        /// </summary>
        [RelayCommand]
public async Task EditProfile(GameProfileItemViewModel profile)
{
    if (profile == null || IsLoading)
        return;

    _logger.LogInformation("Editing profile: {ProfileName} ({ProfileId})", profile.Name, profile.Id);

    try
    {
        // Get parent window for dialog
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            StatusMessage = "Cannot show settings dialog: No parent window found.";
            return;
        }
        
        // Create settings dialog with the profile to edit - use the constructor properly
        var settingsWindow = new GameProfileSettingsWindow(profile, mainWindow);
        
        // Show dialog and wait for result - use a consistent approach
        var result = await settingsWindow.ShowAsync(mainWindow);
        
        if (result == DialogResult.OK)
        {
            _logger.LogInformation("Profile edited successfully");
            await LoadProfilesAsync();
        }
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error editing profile: {ex.Message}";
        _logger.LogError(ex, "Error editing profile {ProfileId}", profile.Id);
    }
}

        /// <summary>
        /// Edits a profile by showing the game profile settings dialog
        /// </summary>
        [RelayCommand]
        private async Task EditProfileImplAsync(GameProfileItemViewModel? profile)
        {
            if (profile == null)
            {
                _logger.LogWarning("Cannot edit null profile");
                return;
            }

            try
            {
                _logger.LogInformation("Editing profile: {ProfileName}, ID: {ProfileId}", profile.Name, profile.Id);
                
                // Get the main window
                var mainWindow = GetMainWindow();
                if (mainWindow == null)
                {
                    _logger.LogError("Cannot edit profile - Main window not found");
                    return;
                }
                
                // Create settings dialog with the profile to edit
                var settingsWindow = new GameProfileSettingsWindow(profile, mainWindow);
                
                // Show dialog and wait for result
                bool? result = await settingsWindow.ShowDialog<bool?>(mainWindow);
                
                if (result == true)
                {
                    _logger.LogInformation("Profile edited successfully");
                    await LoadProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing profile {ProfileName}", profile.Name);
            }
        }

        /// <summary>
        /// Saves changes from an edited profile copy back to the original profile
        /// </summary>
        private async Task SaveProfileChanges(GameProfileItemViewModel originalProfile, GameProfileItemViewModel editedProfile)
        {
            try
            {
                _logger.LogInformation("Saving changes for profile: {ProfileName}", originalProfile.Name);

                // Store the original index so we can preserve it
                int originalIndex = Profiles.IndexOf(originalProfile);

                // Update the view model properties directly first
                originalProfile.Name = editedProfile.Name;
                originalProfile.Description = editedProfile.Description;
                originalProfile.ExecutablePath = editedProfile.ExecutablePath;
                originalProfile.DataPath = editedProfile.DataPath;
                originalProfile.IconPath = editedProfile.IconPath;
                originalProfile.CoverImagePath = editedProfile.CoverImagePath;
                originalProfile.ColorValue = editedProfile.ColorValue;
                originalProfile.CommandLineArguments = editedProfile.CommandLineArguments;
                originalProfile.VersionId = editedProfile.VersionId;
                originalProfile.RunAsAdmin = editedProfile.RunAsAdmin;
                originalProfile.PullRequestNumber = editedProfile.PullRequestNumber;
                originalProfile.WorkflowNumber = editedProfile.WorkflowNumber;
                originalProfile.CommitMessage = editedProfile.CommitMessage;
                originalProfile.CommitSha = editedProfile.CommitSha;
                originalProfile.BuildPreset = editedProfile.BuildPreset;
                originalProfile.HasWorkflowInfo = editedProfile.HasWorkflowInfo;
                originalProfile.BuildInfo = editedProfile.BuildInfo;

                // Temporarily unsubscribe from the event
                _profileManagerService.ProfilesUpdated -= OnProfilesUpdated;

                // Update the model in the repository
                var updatedModel = originalProfile.ToModel(); // Use the updated originalProfile
                await _profileManagerService.UpdateProfileAsync(updatedModel);

                // Resubscribe to the event
                _profileManagerService.ProfilesUpdated += OnProfilesUpdated;

                // No collection reload happens, position is preserved

                StatusMessage = $"Profile '{originalProfile.Name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profile changes for {ProfileId}", originalProfile.Id);
                StatusMessage = $"Error saving profile: {ex.Message}";

                // Make sure we're always resubscribing even if there's an error
                try
                {
                    // Try to resubscribe if we failed during update
                    _profileManagerService.ProfilesUpdated += OnProfilesUpdated;
                }
                catch { /* Ignore if already subscribed */ }
            }
        }

        /// <summary>
        /// Helper method to get main window
        /// </summary>
        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        /// <summary>
        /// Create a new profile
        /// </summary>
        [RelayCommand]
        public async Task CreateNewProfile()
        {
            try
            {
                _logger.LogInformation("Creating new profile");

                // Create a new profile with proper defaults
                var newProfile = new GameProfileItemViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "New Profile",
                    Description = "New game profile",
                    ColorValue = ProfileThemeColor.GetRandomColor(),
                    IsCustomProfile = true,
                    IsDefaultProfile = false,
                    IconPath = "avares://GenHub/Assets/Icons/genhub-logo.png",
                    CoverImagePath = "avares://GenHub/Assets/Covers/generals-cover-2.png",
                    SourceType = GameInstallationType.Unknown,
                    ExecutablePath = string.Empty,
                    DataPath = string.Empty,
                    CommandLineArguments = string.Empty,
                    RunAsAdmin = false,
                    // Initialize BuildInfo to prevent binding errors
                    BuildInfo = null
                };

                // Show the settings dialog first
                var result = await ShowProfileSettingsDialog(newProfile);
                
                _logger.LogInformation("Profile settings dialog result: {Result}", result);

                if (result == DialogResult.OK)
                {
                    await SaveNewProfile(newProfile);
                }
                else
                {
                    _logger.LogInformation("Profile creation cancelled by user");
                    // Explicit cleanup to ensure we're ready for next operation
                    GC.Collect();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating profile: {ex.Message}";
                _logger.LogError(ex, "Error creating profile");
            }
        }

        /// <summary>
        /// Saves a new profile with proper error handling
        /// </summary>
        private async Task SaveNewProfile(GameProfileItemViewModel newProfile)
        {
            try
            {
                // Add launch command
                newProfile.LaunchCommand = new RelayCommand(() => LaunchProfile(newProfile));

                // Add to UI collection safely on UI thread
                await Dispatcher.UIThread.InvokeAsync(() => 
                {
                    Profiles.Add(newProfile);
                });

                // Save to service with error handling - explicitly use source to prevent event loop
                try
                {
                    // Temporarily unsubscribe from profile updates to avoid potential circular events
                    _profileManagerService.ProfilesUpdated -= OnProfilesUpdated;
                    
                    await _profileManagerService.UpdateProfileAsync(newProfile.ToModel(), this);
                    
                    // Re-subscribe after save operation
                    _profileManagerService.ProfilesUpdated += OnProfilesUpdated;
                    
                    StatusMessage = $"Profile '{newProfile.Name}' created";
                    _logger.LogInformation("Profile created successfully: {ProfileId}", newProfile.Id);
                }
                catch (Exception ex)
                {
                    // Make sure we're always resubscribing even if there's an error
                    _profileManagerService.ProfilesUpdated += OnProfilesUpdated;
                    
                    _logger.LogError(ex, "Error saving new profile to repository");
                    StatusMessage = $"Profile displayed but not saved: {ex.Message}";
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing profile creation");
                StatusMessage = $"Error creating profile: {ex.Message}";
            }
        }

        /// <summary>
        /// Helper method to show profile settings dialog
        /// </summary>
        private async Task<DialogResult> ShowProfileSettingsDialog(GameProfileItemViewModel profile)
        {
            try
            {
                // Find the main window
                Window? mainWindow = GetMainWindow();
                if (mainWindow == null)
                {
                    _logger.LogWarning("Cannot show dialog - no window found");
                    return DialogResult.Cancel;
                }

                // FIXED: Use the same constructor approach as the EditProfile method
                // This ensures consistent initialization between editing and creating
                var dialog = new GameProfileSettingsWindow(profile, mainWindow);
                
                try
                {
                    // Show the dialog and get the result - using the same approach as EditProfile
                    _logger.LogDebug("Showing profile settings dialog for new profile");
                    var result = await dialog.ShowAsync(mainWindow);
                    _logger.LogDebug("Profile settings dialog closed with result: {result}", result);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing dialog");
                    return DialogResult.Cancel;
                }
                finally
                {
                    // Still keep the cleanup code from the original
                    dialog.DataContext = null;
                    
                    if (!Design.IsDesignMode)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShowProfileSettingsDialog");
                return DialogResult.Cancel;
            }
        }

        /// <summary>
        /// Scans for installed games
        /// </summary>
        [RelayCommand]
        public async Task DeleteProfile(GameProfileItemViewModel profile)
        {
            if (profile == null || profile.IsDefaultProfile) return;

            try
            {
                _logger.LogInformation("Deleting profile: {ProfileName} ({ProfileId})", profile.Name, profile.Id);

                // Delete from service
                await _profileManagerService.DeleteProfileAsync(profile.Id);

                // Remove from UI collection
                Profiles.Remove(profile);
                StatusMessage = $"Profile '{profile.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting profile: {ex.Message}";
                _logger.LogError(ex, "Error deleting profile {ProfileId}", profile.Id);
            }
        }

        /// <summary>
        /// Scans for installed games
        /// </summary>
        [RelayCommand]
        public async Task ScanForGames()
        {
            if (IsScanning) return;

            try
            {
                IsScanning = true;
                StatusMessage = "Scanning for games...";

                _logger.LogInformation("Starting game scanning");

                // First, discover all versions using the service
                var detectedVersions = await _gameVersionService.DiscoverVersionsAsync();
                int versionsFound = detectedVersions.Count();
                _logger.LogInformation("Detection completed. Found {Count} games", versionsFound);

                // Create profiles for discovered versions
                int profilesCreated = 0;

                foreach (var version in detectedVersions)
                {
                    // Skip empty paths or non-existent executables
                    if (string.IsNullOrEmpty(version.ExecutablePath) || !File.Exists(version.ExecutablePath))
                    {
                        _logger.LogWarning("Skipping version with invalid executable: {Path}", version.ExecutablePath);
                        continue;
                    }

                    // Check if profile already exists for this version
                    var existingProfiles = await _profileManagerService.LoadCustomProfilesAsync();
                    bool exists = existingProfiles.Any(p =>
                        (!string.IsNullOrEmpty(p.ExecutablePath) &&
                         !string.IsNullOrEmpty(version.ExecutablePath) &&
                         p.ExecutablePath.Equals(version.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(p.VersionId) && !string.IsNullOrEmpty(version.Id) &&
                         p.VersionId.Equals(version.Id, StringComparison.OrdinalIgnoreCase)));

                    if (!exists)
                    {
                        // Create profile for this version using the service
                        var profile = _gameProfileFactory.CreateFromVersion(version);

                        // Save the new profile
                        await _profileManagerService.UpdateProfileAsync(profile);
                        profilesCreated++;

                        _logger.LogInformation("Created profile for {GameType} at {Path}",
                            version.GameType, version.ExecutablePath);
                    }
                    else
                    {
                        _logger.LogDebug("Profile already exists for {Path}", version.ExecutablePath);
                    }
                }

                // Update UI
                if (profilesCreated > 0)
                {
                    StatusMessage = $"Created {profilesCreated} new profiles from detected installations";
                    await LoadProfilesAsync(); // Refresh profiles
                }
                else if (versionsFound > 0)
                {
                    StatusMessage = $"Found {versionsFound} installations, but all profiles already exist";
                    await LoadProfilesAsync(); // Refresh profiles in case something was updated
                }
                else
                {
                    StatusMessage = "No game installations found";
                }

                _logger.LogInformation("Game scanning complete. Created {Count} profiles", profilesCreated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for games: {ErrorMessage}", ex.Message);
                StatusMessage = $"Error during scan: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// Launches a game profile
        /// </summary>
        private void LaunchProfile(GameProfileItemViewModel profile)
        {
            _ = LaunchProfileAsync(profile);
        }

        /// <summary>
        /// Launches a game profile
        /// </summary>
        [RelayCommand]
        public async Task LaunchProfileAsync(IGameProfile profile)
        {
            if (profile == null) return;

            try
            {
                IsLaunching = true;
                StatusMessage = $"Launching {profile.Name}...";
                _logger.LogInformation("Launching profile: {ProfileName} ({ProfileId})", profile.Name, profile.Id);

                // Launch via the game version service
                await _gameLauncherService.LaunchVersionAsync(profile);

                StatusMessage = $"Launched {profile.Name}";
                _logger.LogInformation("Successfully launched profile: {ProfileName}", profile.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching profile: {ProfileId}", profile.Id);
                StatusMessage = $"Error launching profile: {ex.Message}";
            }
            finally
            {
                IsLaunching = false;
            }
        }
    }

    /// <summary>
    /// Extension methods for service location
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Get a required service with additional constructor parameters
        /// </summary>
        public static T GetRequiredService<T>(this IServiceProvider provider, params object[] additionalArgs)
        {
            // Try to find a suitable constructor that can take the additional args
            var type = typeof(T);
            var constructors = type.GetConstructors();

            // Sort constructors by parameter count (descending) to find the most specific match
            Array.Sort(constructors, (x, y) => y.GetParameters().Length.CompareTo(x.GetParameters().Length));

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var resolvedArgs = new object[parameters.Length];
                bool canResolve = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];

                    // Check if this parameter matches an additional arg by type
                    var additionalArg = additionalArgs.FirstOrDefault(a => a != null && param.ParameterType.IsInstanceOfType(a));
                    if (additionalArg != null)
                    {
                        resolvedArgs[i] = additionalArg;
                    }
                    else
                    {
                        // Try to resolve from DI
                        try
                        {
                            var service = provider.GetService(param.ParameterType);
                            if (service != null)
                            {
                                resolvedArgs[i] = service;
                            }
                            else
                            {
                                canResolve = false;
                                break;
                            }
                        }
                        catch
                        {
                            canResolve = false;
                            break;
                        }
                    }
                }

                if (canResolve)
                {
                    try
                    {
                        return (T)constructor.Invoke(resolvedArgs);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating instance: {ex.Message}");
                        continue; // Try next constructor
                    }
                }
            }

            throw new InvalidOperationException($"Could not find a suitable constructor for {typeof(T).Name}");
        }
    }
}
