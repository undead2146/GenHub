using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Extensions.GameInstallations;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for the Game Profile Settings window.
/// Provides comprehensive interface for profile configuration including content selection.
/// </summary>
/// <remarks>
/// Integrates with <see cref="IContentManifestPool"/> to provide real-time content
/// selection and validation for game profiles.
/// </remarks>
public partial class GameProfileSettingsViewModel(
    IGameInstallationService? gameInstallationService,
    IGameProfileManager? gameProfileManager,
    IContentManifestPool? contentManifestPool,
    ILogger<GameProfileSettingsViewModel>? logger) : ViewModelBase
{
    private readonly IGameInstallationService? _gameInstallationService = gameInstallationService;
    private readonly IGameProfileManager? _gameProfileManager = gameProfileManager;
    private readonly IContentManifestPool? _contentManifestPool = contentManifestPool;
    private readonly ILogger<GameProfileSettingsViewModel> _logger = logger ?? NullLogger<GameProfileSettingsViewModel>.Instance;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _colorValue = "#1976D2";

    [ObservableProperty]
    private ContentType _selectedContentType = ContentType.GameInstallation;

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _availableContent = new();

    [ObservableProperty]
    private ContentDisplayItem? _selectedContent;

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _availableGameInstallations = new();

    [ObservableProperty]
    private ContentDisplayItem? _selectedGameInstallation;

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _enabledContent = new();

    [ObservableProperty]
    private bool _isInitializing;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _loadingError;

    [ObservableProperty]
    private WorkspaceStrategy _selectedWorkspaceStrategy = WorkspaceStrategy.HybridCopySymlink;

    private string? _currentProfileId;

    /// <summary>
    /// Event that is raised when the window should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets available content types for selection.
    /// </summary>
    public static ContentType[] AvailableContentTypes { get; } =
    {
        ContentType.GameInstallation,
        ContentType.GameClient,
        ContentType.Mod,
        ContentType.MapPack,
        ContentType.Patch,
    };

    /// <summary>
    /// Gets available workspace strategies for selection.
    /// </summary>
    public static WorkspaceStrategy[] AvailableWorkspaceStrategies { get; } =
    {
        WorkspaceStrategy.FullCopy,
        WorkspaceStrategy.HybridCopySymlink,
        WorkspaceStrategy.SymlinkOnly,
        WorkspaceStrategy.HardLink,
        WorkspaceStrategy.FullSymlink,
        WorkspaceStrategy.ContentAddressable,
    };

    /// <summary>
    /// Initializes the view model for creating a new profile.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeForNewProfileAsync()
    {
        try
        {
            IsInitializing = true;
            LoadingError = false;
            StatusMessage = "Loading available content...";

            _currentProfileId = null;
            Name = "New Profile";
            Description = "A new game profile";
            ColorValue = "#1976D2";
            SelectedWorkspaceStrategy = WorkspaceStrategy.HybridCopySymlink;
            SelectedContentType = ContentType.GameInstallation;

            await LoadAvailableGameInstallationsAsync();
            await LoadAvailableContentAsync();

            StatusMessage = $"Found {AvailableGameInstallations.Count} installations and {AvailableContent.Count} content items";
            _logger.LogInformation(
                "Initialized new profile creation with {InstallationCount} installations and {ContentCount} content items",
                AvailableGameInstallations.Count,
                AvailableContent.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing new profile");
            StatusMessage = "Error loading content";
            LoadingError = true;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// Initializes the view model for editing an existing profile.
    /// </summary>
    /// <param name="profileId">The profile ID to edit.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeForProfileAsync(string profileId)
    {
        try
        {
            IsInitializing = true;
            LoadingError = false;
            StatusMessage = "Loading profile...";

            _currentProfileId = profileId;

            // Load the existing profile
            if (_gameProfileManager != null)
            {
                var profileResult = await _gameProfileManager.GetProfileAsync(profileId);
                if (profileResult.Success && profileResult.Data != null)
                {
                    var profile = profileResult.Data;
                    Name = profile.Name;
                    Description = profile.Description ?? string.Empty;
                    ColorValue = profile.ThemeColor ?? "#1976D2";
                    SelectedWorkspaceStrategy = profile.WorkspaceStrategy;

                    // Load enabled content for this profile
                    await LoadEnabledContentForProfileAsync(profile);

                    _logger.LogInformation("Loaded profile {ProfileName} for editing", profile.Name);
                }
                else
                {
                    _logger.LogWarning("Failed to load profile {ProfileId}: {Errors}", profileId, string.Join(", ", profileResult.Errors));
                    StatusMessage = "Failed to load profile";
                    LoadingError = true;
                    return;
                }
            }

            await LoadAvailableGameInstallationsAsync();
            await LoadAvailableContentAsync();

            StatusMessage = $"Profile loaded with {EnabledContent.Count} enabled content items";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing profile {ProfileId}", profileId);
            StatusMessage = "Error loading profile";
            LoadingError = true;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// Extracts installation type from manifest metadata.
    /// </summary>
    /// <param name="manifest">The content manifest.</param>
    /// <returns>The installation type.</returns>
    private static GameInstallationType GetInstallationTypeFromManifest(ContentManifest manifest)
    {
        // Try to extract from manifest name or metadata
        var manifestName = manifest.Name.ToLowerInvariant();

        if (manifestName.Contains("steam"))
            return GameInstallationType.Steam;
        if (manifestName.Contains("ea") || manifestName.Contains("origin"))
            return GameInstallationType.EaApp;
        if (manifestName.Contains("tfd") || manifestName.Contains("firstdecade"))
            return GameInstallationType.TheFirstDecade;
        if (manifestName.Contains("wine") || manifestName.Contains("proton"))
            return GameInstallationType.Wine;

        return GameInstallationType.Retail;
    }

    /// <summary>
    /// Loads available content based on the selected content type.
    /// </summary>
    [RelayCommand]
    private async Task LoadAvailableContentAsync()
    {
        try
        {
            StatusMessage = "Loading content...";
            AvailableContent.Clear();

            if (_contentManifestPool == null)
            {
                StatusMessage = "Content manifest pool not available";
                return;
            }

            var manifestsResult = await _contentManifestPool.GetAllManifestsAsync();
            if (!manifestsResult.Success || manifestsResult.Data == null)
            {
                StatusMessage = $"Failed to load {SelectedContentType} content: {string.Join(", ", manifestsResult.Errors)}";
                _logger.LogWarning("Failed to load manifests: {Errors}", string.Join(", ", manifestsResult.Errors));
                return;
            }

            // Filter by content type
            var filteredManifests = manifestsResult.Data.Where(m => m.ContentType == SelectedContentType);

            foreach (var manifest in filteredManifests)
            {
                var item = new ContentDisplayItem
                {
                    ManifestId = manifest.Id,
                    DisplayName = $"{manifest.Name} v{manifest.Version}",
                    ContentType = manifest.ContentType,
                    GameType = manifest.TargetGame,
                    InstallationType = GetInstallationTypeFromManifest(manifest),
                    IsEnabled = EnabledContent.Any(e => e.ManifestId.Value == manifest.Id.Value),
                };
                AvailableContent.Add(item);
            }

            // Select the first item if available and none is selected
            if (AvailableContent.Any() && SelectedContent == null)
            {
                SelectedContent = AvailableContent.First();
            }

            StatusMessage = $"Loaded {AvailableContent.Count} {SelectedContentType} items";
            _logger.LogInformation("Loaded {Count} content items for content type {ContentType}", AvailableContent.Count, SelectedContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available content");
            StatusMessage = "Error loading content";
        }
    }

    /// <summary>
    /// Loads available game installations from actual detected installations (not manifests).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task LoadAvailableGameInstallationsAsync()
    {
        try
        {
            AvailableGameInstallations.Clear();

            if (_gameInstallationService == null)
            {
                _logger.LogWarning("Game installation service not available for loading game installations");
                return;
            }

            var installationsResult = await _gameInstallationService.GetAllInstallationsAsync();
            if (!installationsResult.Success || installationsResult.Data == null)
            {
                _logger.LogWarning("Failed to load game installations: {Errors}", string.Join(", ", installationsResult.Errors));
                return;
            }

            foreach (var installation in installationsResult.Data)
            {
                if (!installation.AvailableGameClients.Any())
                {
                    _logger.LogDebug("Skipping installation {InstallationId} - no available versions", installation.Id);
                    continue;
                }

                // Use the first available version for display (user can change later)
                var firstVersion = installation.AvailableGameClients.First();
                var gameType = firstVersion.GameType;

                var item = new ContentDisplayItem
                {
                    ManifestId = firstVersion.Id, // Manifest ID for the primary version (GameInstallation or GameClient)
                    SourceId = installation.Id, // GUID of the actual installation
                    DisplayName = $"{installation.InstallationType.GetDisplayName()} {gameType} v{firstVersion.Version}",
                    ContentType = ContentType.GameInstallation,
                    GameType = gameType,
                    InstallationType = installation.InstallationType,
                    IsEnabled = false,
                };
                AvailableGameInstallations.Add(item);
            }

            // Select the first installation if available
            if (AvailableGameInstallations.Any() && SelectedGameInstallation == null)
            {
                SelectedGameInstallation = AvailableGameInstallations.First();
            }

            _logger.LogInformation("Loaded {Count} game installation options from detected installations", AvailableGameInstallations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available game installations");
        }
    }

    /// <summary>
    /// Loads enabled content for a specific profile.
    /// </summary>
    /// <param name="profile">The game profile.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task LoadEnabledContentForProfileAsync(GameProfile profile)
    {
        try
        {
            EnabledContent.Clear();

            if (_contentManifestPool == null || profile.EnabledContentIds == null)
            {
                return;
            }

            foreach (var contentId in profile.EnabledContentIds)
            {
                var manifestResult = await _contentManifestPool.GetManifestAsync(ManifestId.Create(contentId));
                if (manifestResult.Success && manifestResult.Data != null)
                {
                    var manifest = manifestResult.Data;
                    var item = new ContentDisplayItem
                    {
                        ManifestId = manifest.Id,
                        DisplayName = $"{manifest.Name} v{manifest.Version}",
                        ContentType = manifest.ContentType,
                        GameType = manifest.TargetGame,
                        InstallationType = GetInstallationTypeFromManifest(manifest),
                        IsEnabled = true,
                    };
                    EnabledContent.Add(item);
                }
            }

            _logger.LogInformation("Loaded {Count} enabled content items for profile {ProfileName}", EnabledContent.Count, profile.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading enabled content for profile");
        }
    }

    /// <summary>
    /// Enables the selected content item for the profile.
    /// </summary>
    [RelayCommand]
    private void EnableSelectedContent()
    {
        if (SelectedContent == null || SelectedContent.IsEnabled)
        {
            StatusMessage = "No content selected or content already enabled";
            return;
        }

        // Check if content is already enabled
        if (EnabledContent.Any(e => e.ManifestId.Value == SelectedContent.ManifestId.Value))
        {
            StatusMessage = "Content is already enabled";
            return;
        }

        // Add to enabled content
        var enabledItem = new ContentDisplayItem
        {
            ManifestId = SelectedContent.ManifestId,
            DisplayName = SelectedContent.DisplayName,
            ContentType = SelectedContent.ContentType,
            GameType = SelectedContent.GameType,
            InstallationType = SelectedContent.InstallationType,
            IsEnabled = true,
        };

        EnabledContent.Add(enabledItem);
        SelectedContent.IsEnabled = true;

        StatusMessage = $"Enabled {SelectedContent.DisplayName}";
        _logger.LogInformation("Enabled content {ContentName} for profile", SelectedContent.DisplayName);
    }

    /// <summary>
    /// Disables the specified content item.
    /// </summary>
    [RelayCommand]
    private void DisableContent(ContentDisplayItem contentItem)
    {
        if (contentItem == null)
        {
            return;
        }

        // Remove from enabled content
        var itemToRemove = EnabledContent.FirstOrDefault(e => e.ManifestId.Value == contentItem.ManifestId.Value);
        if (itemToRemove != null)
        {
            EnabledContent.Remove(itemToRemove);
        }

        // Update available content item
        var availableItem = AvailableContent.FirstOrDefault(a => a.ManifestId.Value == contentItem.ManifestId.Value);
        if (availableItem != null)
        {
            availableItem.IsEnabled = false;
        }

        StatusMessage = $"Disabled {contentItem.DisplayName}";
        _logger.LogInformation("Disabled content {ContentName} for profile", contentItem.DisplayName);
    }

    /// <summary>
    /// Saves the profile.
    /// </summary>
    [RelayCommand]
    private async Task Save()
    {
        try
        {
            IsSaving = true;
            StatusMessage = "Saving profile...";

            if (_gameProfileManager == null)
            {
                StatusMessage = "Profile manager not available";
                return;
            }

            if (SelectedGameInstallation == null)
            {
                StatusMessage = "Please select a game installation";
                return;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = "Please enter a profile name";
                return;
            }

            // Build enabled content IDs (additional content only)
            var enabledContentIds = EnabledContent.Where(c => c.ContentType != ContentType.GameInstallation)
                .Select(c => c.ManifestId.Value).ToList();

            // Auto-add base manifests for the selected installation
            // Assuming each installation has GameInstallation and GameClient manifests in AvailableVersions
            // For simplicity, add the primary version's manifest ID (GameInstallation) and a GameClient if available
            enabledContentIds.Add(SelectedGameInstallation.ManifestId.Value); // GameInstallation manifest

            if (string.IsNullOrEmpty(_currentProfileId))
            {
                // Create new profile
                var createRequest = new CreateProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    GameInstallationId = SelectedGameInstallation.SourceId, // GUID from actual installation
                    GameClientId = SelectedGameInstallation.ManifestId.Value,
                    PreferredStrategy = SelectedWorkspaceStrategy,
                    EnabledContentIds = enabledContentIds,
                };

                var result = await _gameProfileManager.CreateProfileAsync(createRequest);
                if (result.Success)
                {
                    StatusMessage = "Profile created successfully";
                    _logger.LogInformation("Created new profile {ProfileName} with {ContentCount} enabled content items", Name, enabledContentIds.Count);

                    // Close the window after a brief delay
                    await Task.Delay(1000);
                    ExecuteCancel();
                }
                else
                {
                    StatusMessage = $"Failed to create profile: {string.Join(", ", result.Errors)}";
                    _logger.LogWarning("Failed to create profile: {Errors}", string.Join(", ", result.Errors));
                }
            }
            else
            {
                // Update existing profile
                var updateRequest = new UpdateProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    ThemeColor = ColorValue,
                    PreferredStrategy = SelectedWorkspaceStrategy,
                    EnabledContentIds = enabledContentIds,
                };

                var result = await _gameProfileManager.UpdateProfileAsync(_currentProfileId, updateRequest);
                if (result.Success)
                {
                    StatusMessage = "Profile updated successfully";
                    _logger.LogInformation("Updated profile {ProfileId} with {ContentCount} enabled content items", _currentProfileId, enabledContentIds.Count);

                    // Close the window after a brief delay
                    await Task.Delay(1000);
                    ExecuteCancel();
                }
                else
                {
                    StatusMessage = $"Failed to update profile: {string.Join(", ", result.Errors)}";
                    _logger.LogWarning("Failed to update profile {ProfileId}: {Errors}", _currentProfileId, string.Join(", ", result.Errors));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile");
            StatusMessage = "Error saving profile";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Randomizes the profile color from a predefined set of colors.
    /// </summary>
    [RelayCommand]
    private void RandomizeColor()
    {
        var colors = new List<string>
        {
            "#1976D2",
            "#388E3C",
            "#FBC02D",
            "#FF5722",
            "#7B1FA2",
            "#D32F2F",
            "#0097A7",
            "#689F38",
            "#AFB42B",
            "#0288D1",
            "#C2185B",
            "#512DA8",
        };

        var random = new Random();
        ColorValue = colors[random.Next(colors.Count)];
        StatusMessage = $"Color randomized to {ColorValue}";
        _logger.LogInformation("Randomized profile color to {ColorValue}", ColorValue);
    }

    /// <summary>
    /// Selects a specific theme color for the profile.
    /// </summary>
    /// <param name="color">The color to select.</param>
    [RelayCommand]
    private void SelectThemeColor(string? color)
    {
        if (!string.IsNullOrEmpty(color))
        {
            ColorValue = color;
            StatusMessage = $"Selected theme color {color}";
            _logger.LogInformation("Selected theme color {ColorValue}", color);
        }
        else
        {
            StatusMessage = "Invalid color selected";
            _logger.LogWarning("Invalid color parameter passed to SelectThemeColor");
        }
    }

    /// <summary>
    /// Cancels the operation and closes the window.
    /// </summary>
    [RelayCommand]
    private void ExecuteCancel()
    {
        StatusMessage = "Cancelled";
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called when the selected content type changes.
    /// </summary>
    partial void OnSelectedContentTypeChanged(ContentType value)
    {
        // Reload content when content type changes
        _ = LoadAvailableContentAsync();
    }
}

/// <summary>
/// Represents content that can be selected for a game profile.
/// </summary>
public class ContentDisplayItem
{
    /// <summary>
    /// Gets or sets the manifest ID.
    /// </summary>
    required public ManifestId ManifestId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    required public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    required public ContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the game type.
    /// </summary>
    required public GameType GameType { get; set; }

    /// <summary>
    /// Gets or sets the installation type.
    /// </summary>
    required public GameInstallationType InstallationType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this content is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the source ID (GUID) of the actual installation.
    /// </summary>
    public string? SourceId { get; set; }
}
