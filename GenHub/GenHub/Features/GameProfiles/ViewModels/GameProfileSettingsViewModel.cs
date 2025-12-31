using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Common.ViewModels;
using GenHub.Core.Extensions;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.GameSettings;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Manifest;
using GenHub.Features.Notifications.Services;
using GenHub.Features.Notifications.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for managing game profile settings, including content selection and configuration.
/// </summary>
public partial class GameProfileSettingsViewModel : ViewModelBase
{
    private readonly IGameProfileManager? gameProfileManager;
    private readonly IGameSettingsService? gameSettingsService;
    private readonly IConfigurationProviderService? configurationProvider;
    private readonly IProfileContentLoader? profileContentLoader;
    private readonly Services.ProfileResourceService? profileResourceService;
    private readonly INotificationService? notificationService;
    private readonly IContentManifestPool? manifestPool;
    private readonly IContentStorageService? contentStorageService;
    private readonly ILocalContentService? localContentService;
    private readonly ILogger<GameProfileSettingsViewModel>? logger;
    private readonly ILogger<GameSettingsViewModel>? gameSettingsLogger;

    private readonly NotificationService _localNotificationService = new(
        NullLogger<NotificationService>.Instance);

    /// <summary>
    /// Gets the notification manager for local window notifications.
    /// </summary>
    public NotificationManagerViewModel NotificationManager { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfileSettingsViewModel"/> class.
    /// </summary>
    /// <param name="gameProfileManager">The game profile manager service.</param>
    /// <param name="gameSettingsService">The game settings service.</param>
    /// <param name="configurationProvider">The configuration provider service.</param>
    /// <param name="profileContentLoader">The profile content loader service.</param>
    /// <param name="profileResourceService">The profile resource service.</param>
    /// <param name="notificationService">The notification service for global notifications.</param>
    /// <param name="manifestPool">The content manifest pool.</param>
    /// <param name="contentStorageService">The content storage service.</param>
    /// <param name="localContentService">The local content service.</param>
    /// <param name="logger">The logger for this view model.</param>
    /// <param name="gameSettingsLogger">The logger for the game settings view model.</param>
    public GameProfileSettingsViewModel(
        IGameProfileManager? gameProfileManager,
        IGameSettingsService? gameSettingsService,
        IConfigurationProviderService? configurationProvider,
        IProfileContentLoader? profileContentLoader,
        Services.ProfileResourceService? profileResourceService,
        INotificationService? notificationService,
        IContentManifestPool? manifestPool,
        IContentStorageService? contentStorageService,
        ILocalContentService? localContentService,
        ILogger<GameProfileSettingsViewModel>? logger,
        ILogger<GameSettingsViewModel>? gameSettingsLogger)
    {
        this.gameProfileManager = gameProfileManager;
        this.gameSettingsService = gameSettingsService;
        this.configurationProvider = configurationProvider;
        this.profileContentLoader = profileContentLoader;
        this.profileResourceService = profileResourceService;
        this.notificationService = notificationService;
        this.manifestPool = manifestPool;
        this.contentStorageService = contentStorageService;
        this.localContentService = localContentService;
        this.logger = logger;
        this.gameSettingsLogger = gameSettingsLogger;

        NotificationManager = new NotificationManagerViewModel(
            _localNotificationService,
            NullLogger<NotificationManagerViewModel>.Instance,
            NullLogger<NotificationItemViewModel>.Instance);

        GameSettingsViewModel = new GameSettingsViewModel(gameSettingsService!, gameSettingsLogger!);
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _colorValue = "#1976D2";

    // Remove [ObservableProperty] for SelectedContentType to implement custom setter
    private ContentType _selectedContentType = ContentType.GameInstallation;

    /// <summary>
    /// Gets or sets the selected content type for filtering available content.
    /// </summary>
    public ContentType SelectedContentType
    {
        get => _selectedContentType;
        set
        {
            if (SetProperty(ref _selectedContentType, value))
            {
                // Property updates immediately - UI shows selection right away
                // Then fire async load in background
                _ = OnContentTypeChangedAsync();
            }
        }
    }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _availableContent = [];

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _availableGameInstallations = [];

    [ObservableProperty]
    private ContentDisplayItem? _selectedGameInstallation;

    [ObservableProperty]
    private ObservableCollection<ContentDisplayItem> _enabledContent = [];

    [ObservableProperty]
    private bool _isInitializing;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _loadingError;

    [ObservableProperty]
    private WorkspaceStrategy _selectedWorkspaceStrategy = WorkspaceStrategy.SymlinkOnly;

    // Track the original workspace strategy when loading a profile to detect changes
    private WorkspaceStrategy? _originalWorkspaceStrategy;

    [ObservableProperty]
    private string _commandLineArguments = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProfileInfoItem> _availableCovers = [];

    [ObservableProperty]
    private ProfileInfoItem? _selectedCover;

    [ObservableProperty]
    private ObservableCollection<ProfileInfoItem> _availableGameClients = [];

    [ObservableProperty]
    private ProfileInfoItem? _selectedClient;

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private string _buildDate = string.Empty;

    [ObservableProperty]
    private string _sourceType = string.Empty;

    [ObservableProperty]
    private string _shortcutPath = string.Empty;

    [ObservableProperty]
    private bool _isShortcutPathValid = true;

    [ObservableProperty]
    private string _shortcutStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _useProfileIcon;

    [ObservableProperty]
    private bool _shortcutRunAsAdmin;

    [ObservableProperty]
    private string _shortcutDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProfileInfoItem> _profileInfos = [];

    [ObservableProperty]
    private ProfileInfoItem? _selectedProfileInfo;

    [ObservableProperty]
    private ObservableCollection<ProfileInfoItem> _availableExecutables = [];

    [ObservableProperty]
    private ProfileInfoItem? _selectedExecutable;

    [ObservableProperty]
    private bool _isExecutableValid = true;

    [ObservableProperty]
    private ObservableCollection<ProfileInfoItem> _availableDataPaths = [];

    [ObservableProperty]
    private ProfileInfoItem? _selectedDataPath;

    [ObservableProperty]
    private bool _isDataPathValid = true;

    [ObservableProperty]
    private bool _runAsAdmin;

    [ObservableProperty]
    private bool _canLaunchGame = true;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private string _coverPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProfileResourceItem> _availableIcons = [];

    [ObservableProperty]
    private ObservableCollection<ProfileResourceItem> _availableCoversForSelection = [];

    [ObservableProperty]
    private ProfileResourceItem? _selectedIcon;

    [ObservableProperty]
    private ProfileResourceItem? _selectedCoverItem;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _sourceTypeName = string.Empty;

    [ObservableProperty]
    private string _gameType = string.Empty;

    [ObservableProperty]
    private string _installPath = string.Empty;

    private string? _currentProfileId;

    [ObservableProperty]
    private bool _isLoadingContent; // Guard flag to prevent cascading EnableContent calls AND UI loading state

    // ===== Local Content Dialog Properties =====
    [ObservableProperty]
    private bool _isAddLocalContentDialogOpen;

    [ObservableProperty]
    private string _localContentName = string.Empty;

    [ObservableProperty]
    private string _localContentDirectoryPath = string.Empty;

    [ObservableProperty]
    private ContentType _selectedLocalContentType = ContentType.Addon;

    /// <summary>
    /// Event that is raised when the window should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets available content types for selection.
    /// </summary>
    public static ContentType[] AvailableContentTypes { get; } =
    [
        ContentType.GameInstallation,
        ContentType.GameClient,
        ContentType.Mod,
        ContentType.MapPack,
        ContentType.Addon,
        ContentType.Patch,
    ];

    /// <summary>
    /// Gets available workspace strategies for selection.
    /// </summary>
    public static WorkspaceStrategy[] AvailableWorkspaceStrategies { get; } =
    [
        WorkspaceStrategy.SymlinkOnly,
        WorkspaceStrategy.HybridCopySymlink,
        WorkspaceStrategy.HardLink,
        WorkspaceStrategy.FullCopy,
    ];

    /// <summary>
    /// Gets the allowed content types for local content creation.
    /// </summary>
    public static ContentType[] AllowedLocalContentTypes { get; } =
    [
        ContentType.GameClient,
        ContentType.Addon,
        ContentType.Map,
        ContentType.MapPack,
        ContentType.Mission,
        ContentType.ModdingTool,
    ];

    /// <summary>
    /// Gets the Game Settings ViewModel for the third tab.
    /// </summary>
    public GameSettingsViewModel GameSettingsViewModel { get; }

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
            SelectedWorkspaceStrategy = GetDefaultWorkspaceStrategy();
            SelectedContentType = ContentType.GameInstallation;

            EnabledContent.Clear();

            await LoadAvailableGameInstallationsAsync();
            await LoadAvailableContentAsync();

            // Set the first game installation as selected (for UI convenience), but don't auto-enable it
            if (AvailableGameInstallations.Any())
            {
                SelectedGameInstallation = AvailableGameInstallations.First();
                logger?.LogInformation("Pre-selected first GameInstallation for UI: {ContentName}", SelectedGameInstallation.DisplayName);

                // Set default icon and cover FIRST
                IconPath = NormalizeResourcePath(
                    profileResourceService?.GetDefaultIconPath(SelectedGameInstallation.GameType.ToString()),
                    Core.Constants.UriConstants.DefaultIconUri);
                CoverPath = NormalizeResourcePath(
                    profileResourceService?.GetDefaultCoverPath(SelectedGameInstallation.GameType.ToString()),
                    string.Empty);

                // then load available icons and covers (so SelectedIcon/SelectedCoverItem get set correctly)
                LoadAvailableIconsAndCovers(SelectedGameInstallation.GameType.ToString());
            }

            // Initialize game settings with defaults for new profile
            GameSettingsViewModel.ColorValue = ColorValue;
            await GameSettingsViewModel.InitializeForProfileAsync(null, null);

            StatusMessage = $"Found {AvailableGameInstallations.Count} installations and {AvailableContent.Count} content items";
            logger?.LogInformation(
                "Initialized new profile creation with {InstallationCount} installations and {ContentCount} content items",
                AvailableGameInstallations.Count,
                AvailableContent.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error initializing new profile");
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
            logger?.LogInformation("InitializeForProfileAsync called with profileId: {ProfileId}", profileId);

            // Load the existing profile
            var profileResult = await gameProfileManager!.GetProfileAsync(profileId);
            if (!profileResult.Success || profileResult.Data == null)
            {
                logger?.LogWarning("Failed to load profile {ProfileId}: {Errors}", profileId, string.Join(", ", profileResult.Errors));
                StatusMessage = "Failed to load profile";
                LoadingError = true;
                return;
            }

            var profile = profileResult.Data;
            logger?.LogInformation("Loaded profile: {ProfileName}, EnabledContentIds count: {Count}", profile.Name, profile.EnabledContentIds?.Count ?? 0);

            Name = profile.Name;
            Description = profile.Description ?? string.Empty;
            ColorValue = profile.ThemeColor ?? "#1976D2";
            var defaultIconPath = profileResourceService?.GetDefaultIconPath(profile.GameClient.GameType.ToString())
                ?? Core.Constants.UriConstants.DefaultIconUri;
            IconPath = NormalizeResourcePath(profile.IconPath, defaultIconPath);
            var defaultCoverPath = profileResourceService?.GetDefaultCoverPath(profile.GameClient.GameType.ToString()) ?? string.Empty;
            CoverPath = NormalizeResourcePath(profile.CoverPath, defaultCoverPath);
            SelectedWorkspaceStrategy = profile.WorkspaceStrategy;
            _originalWorkspaceStrategy = profile.WorkspaceStrategy; // Track original strategy
            CommandLineArguments = profile.CommandLineArguments ?? string.Empty;

            // Load available icons and covers for selection
            LoadAvailableIconsAndCovers(profile.GameClient.GameType.ToString());

            // Load game settings for this profile
            GameSettingsViewModel.ColorValue = ColorValue;
            await GameSettingsViewModel.InitializeForProfileAsync(profileId, profile);

            // If the profile has no custom game settings, save the defaults from Options.ini
            if (!profile.HasCustomSettings())
            {
                logger?.LogInformation("Profile {ProfileId} has no custom settings, saving defaults from Options.ini", profileId);
                var gameSettings = GameSettingsViewModel.GetProfileSettings();
                var updateRequest = new UpdateProfileRequest();
                PopulateGameSettings(updateRequest, gameSettings);

                var updateResult = await gameProfileManager.UpdateProfileAsync(profileId, updateRequest);
                if (updateResult.Success)
                {
                    logger?.LogInformation("Saved default game settings for profile {ProfileId}", profileId);
                }
                else
                {
                    logger?.LogWarning(
                        "Failed to save default game settings for profile {ProfileId}: {Errors}",
                        profileId,
                        string.Join(", ", updateResult.Errors));
                }
            }

            // Load enabled content for this profile
            logger?.LogInformation("About to call LoadEnabledContentForProfileAsync for profile: {ProfileName}", profile.Name);
            await LoadEnabledContentForProfileAsync(profile);
            logger?.LogInformation("After LoadEnabledContentForProfileAsync: EnabledContent count = {Count}", EnabledContent.Count);

            logger?.LogInformation("Loaded profile {ProfileName} for editing", profile.Name);

            await LoadAvailableGameInstallationsAsync();
            await LoadAvailableContentAsync();

            // Auto-select the GameInstallation content type filter if there are enabled GameInstallations
            var hasGameInstallation = EnabledContent.Any(c => c.ContentType == ContentType.GameInstallation);
            if (hasGameInstallation)
            {
                SelectedContentType = ContentType.GameInstallation;
                logger?.LogInformation("Auto-selected GameInstallation content type filter for editing");

                var enabledInstallation = EnabledContent.FirstOrDefault(c => c.ContentType == ContentType.GameInstallation);
                if (enabledInstallation != null)
                {
                    // Find the matching item in AvailableGameInstallations
                    SelectedGameInstallation = AvailableGameInstallations
                        .FirstOrDefault(a => a.ManifestId.Value == enabledInstallation.ManifestId.Value)
                        ?? enabledInstallation;

                    logger?.LogInformation(
                        "Set SelectedGameInstallation to {DisplayName} from existing profile",
                        SelectedGameInstallation.DisplayName);
                }
            }

            StatusMessage = $"Profile loaded with {EnabledContent.Count} enabled content items";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error initializing profile {ProfileId}", profileId);
            StatusMessage = "Error loading profile";
            LoadingError = true;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// Normalizes a resource path to ensure it's a valid absolute URI or file path.
    /// Handles legacy relative paths by converting them to full avares:// URIs.
    /// </summary>
    private static string NormalizeResourcePath(string? path, string defaultUri)
    {
        if (string.IsNullOrWhiteSpace(path))
            return defaultUri;

        // If it's already a full avares:// URI, return as-is (don't double-prefix)
        if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return path;

        // If it's already an absolute URI (http, file, etc.), return as-is
        if (Uri.TryCreate(path, UriKind.Absolute, out _))
            return path;

        // Legacy relative resource path - convert to full URI
        // Remove leading slash if present to avoid double slashes
        var relativePath = path.TrimStart('/');
        return $"avares://GenHub/{relativePath}";
    }

    /// <summary>
    /// Populates game settings into an UpdateProfileRequest.
    /// </summary>
    /// <param name="request">The update request to populate.</param>
    /// <param name="gameSettings">The game settings to apply, or null to skip.</param>
    private static void PopulateGameSettings(
        UpdateProfileRequest request,
        UpdateProfileRequest? gameSettings)
    {
        if (gameSettings == null)
            return;

        request.VideoResolutionWidth = gameSettings.VideoResolutionWidth;
        request.VideoResolutionHeight = gameSettings.VideoResolutionHeight;
        request.VideoWindowed = gameSettings.VideoWindowed;
        request.VideoTextureQuality = gameSettings.VideoTextureQuality;
        request.EnableVideoShadows = gameSettings.EnableVideoShadows;
        request.VideoParticleEffects = gameSettings.VideoParticleEffects;
        request.VideoExtraAnimations = gameSettings.VideoExtraAnimations;
        request.VideoBuildingAnimations = gameSettings.VideoBuildingAnimations;
        request.VideoGamma = gameSettings.VideoGamma;
        request.AudioSoundVolume = gameSettings.AudioSoundVolume;
        request.AudioThreeDSoundVolume = gameSettings.AudioThreeDSoundVolume;
        request.AudioSpeechVolume = gameSettings.AudioSpeechVolume;
        request.AudioMusicVolume = gameSettings.AudioMusicVolume;
        request.AudioEnabled = gameSettings.AudioEnabled;
        request.AudioNumSounds = gameSettings.AudioNumSounds;

        // TheSuperHackers settings
        request.TshArchiveReplays = gameSettings.TshArchiveReplays;
        request.TshShowMoneyPerMinute = gameSettings.TshShowMoneyPerMinute;
        request.TshPlayerObserverEnabled = gameSettings.TshPlayerObserverEnabled;
        request.TshSystemTimeFontSize = gameSettings.TshSystemTimeFontSize;
        request.TshNetworkLatencyFontSize = gameSettings.TshNetworkLatencyFontSize;
        request.TshRenderFpsFontSize = gameSettings.TshRenderFpsFontSize;
        request.TshResolutionFontAdjustment = gameSettings.TshResolutionFontAdjustment;
        request.TshCursorCaptureEnabledInFullscreenGame = gameSettings.TshCursorCaptureEnabledInFullscreenGame;
        request.TshCursorCaptureEnabledInFullscreenMenu = gameSettings.TshCursorCaptureEnabledInFullscreenMenu;
        request.TshCursorCaptureEnabledInWindowedGame = gameSettings.TshCursorCaptureEnabledInWindowedGame;
        request.TshCursorCaptureEnabledInWindowedMenu = gameSettings.TshCursorCaptureEnabledInWindowedMenu;
        request.TshScreenEdgeScrollEnabledInFullscreenApp = gameSettings.TshScreenEdgeScrollEnabledInFullscreenApp;
        request.TshScreenEdgeScrollEnabledInWindowedApp = gameSettings.TshScreenEdgeScrollEnabledInWindowedApp;
        request.TshMoneyTransactionVolume = gameSettings.TshMoneyTransactionVolume;

        // GeneralsOnline settings
        request.GoShowFps = gameSettings.GoShowFps;
        request.GoShowPing = gameSettings.GoShowPing;
        request.GoShowPlayerRanks = gameSettings.GoShowPlayerRanks;
        request.GoAutoLogin = gameSettings.GoAutoLogin;
        request.GoRememberUsername = gameSettings.GoRememberUsername;
        request.GoEnableNotifications = gameSettings.GoEnableNotifications;
        request.GoEnableSoundNotifications = gameSettings.GoEnableSoundNotifications;
        request.GoChatFontSize = gameSettings.GoChatFontSize;

        // Camera settings
        request.GoCameraMaxHeightOnlyWhenLobbyHost = gameSettings.GoCameraMaxHeightOnlyWhenLobbyHost;
        request.GoCameraMinHeight = gameSettings.GoCameraMinHeight;
        request.GoCameraMoveSpeedRatio = gameSettings.GoCameraMoveSpeedRatio;

        // Chat settings
        request.GoChatDurationSecondsUntilFadeOut = gameSettings.GoChatDurationSecondsUntilFadeOut;

        // Debug settings
        request.GoDebugVerboseLogging = gameSettings.GoDebugVerboseLogging;

        // Render settings
        request.GoRenderFpsLimit = gameSettings.GoRenderFpsLimit;
        request.GoRenderLimitFramerate = gameSettings.GoRenderLimitFramerate;
        request.GoRenderStatsOverlay = gameSettings.GoRenderStatsOverlay;

        // Social notification settings
        request.GoSocialNotificationFriendComesOnlineGameplay = gameSettings.GoSocialNotificationFriendComesOnlineGameplay;
        request.GoSocialNotificationFriendComesOnlineMenus = gameSettings.GoSocialNotificationFriendComesOnlineMenus;
        request.GoSocialNotificationFriendGoesOfflineGameplay = gameSettings.GoSocialNotificationFriendGoesOfflineGameplay;
        request.GoSocialNotificationFriendGoesOfflineMenus = gameSettings.GoSocialNotificationFriendGoesOfflineMenus;
        request.GoSocialNotificationPlayerAcceptsRequestGameplay = gameSettings.GoSocialNotificationPlayerAcceptsRequestGameplay;
        request.GoSocialNotificationPlayerAcceptsRequestMenus = gameSettings.GoSocialNotificationPlayerAcceptsRequestMenus;
        request.GoSocialNotificationPlayerSendsRequestGameplay = gameSettings.GoSocialNotificationPlayerSendsRequestGameplay;
        request.GoSocialNotificationPlayerSendsRequestMenus = gameSettings.GoSocialNotificationPlayerSendsRequestMenus;
        request.GameSpyIPAddress = gameSettings.GameSpyIPAddress;
    }

    /// <summary>
    /// Gets the default workspace strategy from configuration.
    /// </summary>
    private WorkspaceStrategy GetDefaultWorkspaceStrategy()
    {
        return configurationProvider!.GetDefaultWorkspaceStrategy();
    }

    /// <summary>
    /// Loads available content based on the selected content type.
    /// </summary>
    [RelayCommand]
    private async Task LoadAvailableContentAsync()
    {
        try
        {
            IsLoadingContent = true;
            StatusMessage = "Loading content...";
            AvailableContent.Clear();

            // Get enabled content IDs for marking items as enabled
            var enabledContentIds = EnabledContent.Select(e => e.ManifestId.Value).ToList();

            // Convert AvailableGameInstallations to Core items for the service
            var coreAvailableInstallations = new List<Core.Models.Content.ContentDisplayItem>();
            foreach (var vmItem in AvailableGameInstallations)
            {
                coreAvailableInstallations.Add(new Core.Models.Content.ContentDisplayItem
                {
                    Id = vmItem.ManifestId.Value,
                    ManifestId = vmItem.ManifestId.Value,
                    DisplayName = vmItem.DisplayName,
                    ContentType = vmItem.ContentType,
                    GameType = vmItem.GameType,
                    InstallationType = vmItem.InstallationType,
                    Publisher = vmItem.Publisher ?? string.Empty,
                    Version = vmItem.Version ?? string.Empty,
                    SourceId = vmItem.SourceId ?? string.Empty,
                    GameClientId = vmItem.GameClientId ?? string.Empty,
                    IsEnabled = vmItem.IsEnabled,
                });
            }

            var coreItems = await profileContentLoader!.LoadAvailableContentAsync(
                SelectedContentType,
                new ObservableCollection<Core.Models.Content.ContentDisplayItem>(coreAvailableInstallations),
                enabledContentIds);

            // Convert Core items to ViewModel items, excluding already-enabled content
            foreach (var coreItem in coreItems)
            {
                try
                {
                    // Skip items that are already in the EnabledContent list
                    if (enabledContentIds.Contains(coreItem.ManifestId))
                    {
                        continue;
                    }

                    var viewModelItem = ConvertToViewModelContentDisplayItem(coreItem);
                    AvailableContent.Add(viewModelItem);
                }
                catch (ArgumentException argEx)
                {
                    logger?.LogWarning("Skipping invalid content item {DisplayName} (ID: {Id}): {Message}", coreItem.DisplayName, coreItem.ManifestId, argEx.Message);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error converting content item {DisplayName}", coreItem.DisplayName);
                }
            }

            StatusMessage = $"Loaded {AvailableContent.Count} {SelectedContentType} items";
            logger?.LogInformation("Loaded {Count} content items for content type {ContentType}", AvailableContent.Count, SelectedContentType);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading available content");
            StatusMessage = "Error loading content";
        }
        finally
        {
            IsLoadingContent = false;
        }
    }

    /// <summary>
    /// Loads available game installations from actual detected installations (not manifests).
    /// Creates entries for each available GameClient within each installation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task LoadAvailableGameInstallationsAsync()
    {
        try
        {
            AvailableGameInstallations.Clear();

            var coreItems = await profileContentLoader!.LoadAvailableGameInstallationsAsync();

            // Convert Core.ContentDisplayItem to ViewModel items
            foreach (var coreItem in coreItems)
            {
                try
                {
                    var viewModelItem = ConvertToViewModelContentDisplayItem(coreItem);
                    AvailableGameInstallations.Add(viewModelItem);
                }
                catch (ArgumentException argEx)
                {
                    logger?.LogWarning("Skipping invalid game installation {DisplayName} (ID: {Id}): {Message}", coreItem.DisplayName, coreItem.ManifestId, argEx.Message);
                }
            }

            // Select the first installation if available
            if (AvailableGameInstallations.Any() && SelectedGameInstallation == null)
            {
                SelectedGameInstallation = AvailableGameInstallations.First();
            }

            logger?.LogInformation(
                "Loaded {Count} game installation options",
                AvailableGameInstallations.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading available game installations");
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

            var coreItems = await profileContentLoader!.LoadEnabledContentForProfileAsync(profile);

            // Convert Core items to ViewModel items
            foreach (var coreItem in coreItems)
            {
                var viewModelItem = ConvertToViewModelContentDisplayItem(coreItem);
                EnabledContent.Add(viewModelItem);
                viewModelItem.IsEnabled = true; // Ensure enabled status
            }

            logger?.LogInformation("Loaded {Count} enabled content items for profile {ProfileName}", EnabledContent.Count, profile.Name);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading enabled content for profile");
        }
    }

    /// <summary>
    /// Enables the specified content item for the profile.
    /// This is called with CommandParameter from the UI.
    /// </summary>
    /// <param name="contentItem">The content item to enable.</param>
    [RelayCommand]
    private void EnableContent(ContentDisplayItem? contentItem)
    {
        if (contentItem == null)
        {
            StatusMessage = "No content selected";
            logger?.LogWarning("EnableContent: contentItem parameter is NULL");
            return;
        }

        // Prevent cascading calls during content loading
        if (IsLoadingContent)
        {
            logger?.LogDebug("EnableContent: Blocked during content loading (guard flag set) - {DisplayName}", contentItem.DisplayName);
            return;
        }

        logger?.LogInformation(
            "EnableContent called with: {DisplayName} (ManifestId: {ManifestId}, SourceId: {SourceId}, GameClientId: {GameClientId})",
            contentItem.DisplayName,
            contentItem.ManifestId.Value,
            contentItem.SourceId ?? "NULL",
            contentItem.GameClientId ?? "NULL");

        if (contentItem.IsEnabled)
        {
            StatusMessage = "Content already enabled";
            logger?.LogWarning("EnableContent: {DisplayName} is already marked as enabled", contentItem.DisplayName);
            return;
        }

        // Check if content is already enabled by manifest ID
        var alreadyEnabled = EnabledContent.FirstOrDefault(e => e.ManifestId.Value == contentItem.ManifestId.Value);
        if (alreadyEnabled != null)
        {
            StatusMessage = "Content is already enabled";
            logger?.LogWarning(
                "EnableContent: ManifestId {ManifestId} is already in EnabledContent as {DisplayName}",
                contentItem.ManifestId.Value,
                alreadyEnabled.DisplayName);
            return;
        }

        if (contentItem.ContentType == ContentType.GameInstallation || contentItem.ContentType == ContentType.GameClient)
        {
            // Disable any existing items of the same type (enforce cardinality of 1)
            var existingItems = EnabledContent.Where(e => e.ContentType == contentItem.ContentType).ToList();
            foreach (var existing in existingItems)
            {
                existing.IsEnabled = false;
                EnabledContent.Remove(existing);

                // Re-add to AvailableContent if it matches the current filter
                if (existing.ContentType == SelectedContentType)
                {
                    var alreadyInAvailable = AvailableContent.FirstOrDefault(a => a.ManifestId.Value == existing.ManifestId.Value);
                    if (alreadyInAvailable == null)
                    {
                        var reAddedItem = new ContentDisplayItem
                        {
                            ManifestId = existing.ManifestId,
                            DisplayName = existing.DisplayName,
                            ContentType = existing.ContentType,
                            GameType = existing.GameType,
                            InstallationType = existing.InstallationType,
                            Publisher = existing.Publisher,
                            IsEnabled = false,
                            SourceId = existing.SourceId,
                            GameClientId = existing.GameClientId,
                            Version = existing.Version,
                        };
                        AvailableContent.Add(reAddedItem);
                    }
                    else
                    {
                        alreadyInAvailable.IsEnabled = false;
                    }
                }

                logger?.LogInformation(
                    "Disabled existing {ContentType}: {DisplayName} (enforcing cardinality of 1)",
                    existing.ContentType,
                    existing.DisplayName);
            }
        }

        // Add to enabled content
        contentItem.IsEnabled = true;
        EnabledContent.Add(contentItem);

        // Remove from AvailableContent list since it's now enabled
        var itemToRemoveFromAvailable = AvailableContent.FirstOrDefault(a => a.ManifestId.Value == contentItem.ManifestId.Value);
        if (itemToRemoveFromAvailable != null)
        {
            AvailableContent.Remove(itemToRemoveFromAvailable);
        }

        if (contentItem.ContentType == ContentType.GameInstallation)
        {
            SelectedGameInstallation = contentItem;
            logger?.LogInformation(
                "Updated SelectedGameInstallation to {DisplayName} (SourceId={SourceId}, GameClientId={GameClientId})",
                contentItem.DisplayName,
                contentItem.SourceId,
                contentItem.GameClientId);
        }

        StatusMessage = $"Enabled {contentItem.DisplayName}";
        logger?.LogInformation("Enabled content {ContentName} for profile", contentItem.DisplayName);

        // Auto-resolve dependencies (switch GameInstallation or enable other content)
        _ = ResolveDependenciesAsync(contentItem);
    }

    /// <summary>
    /// Resolves dependencies for the newly enabled content.
    /// Automatically switches GameInstallation or enables other required content if needed.
    /// </summary>
    /// <param name="contentItem">The content item that was just enabled.</param>
    private async Task ResolveDependenciesAsync(ContentDisplayItem contentItem)
    {
        try
        {
            if (manifestPool == null)
            {
                return;
            }

            ContentManifest? manifest = null;
            var manifestResult = await manifestPool.GetManifestAsync(contentItem.ManifestId.Value);

            if (manifestResult.Success && manifestResult.Data != null)
            {
                manifest = manifestResult.Data;
            }
            // Fallback: If no manifest exists but it's a GameClient, create a synthetic one
            // This handles standard GameInstallations/Clients detected at runtime
            else if (contentItem.ContentType == ContentType.GameClient && !string.IsNullOrEmpty(contentItem.SourceId))
            {
                logger?.LogDebug("Creating synthetic manifest for GameClient {ContentName} depending on {SourceId}", contentItem.DisplayName, contentItem.SourceId);

                manifest = new ContentManifest
                {
                    Id = ManifestId.Create(contentItem.ManifestId.Value),
                    Name = contentItem.DisplayName,
                    ContentType = ContentType.GameClient,
                    TargetGame = contentItem.GameType,
                    Dependencies =
                    [
                        new ContentDependency
                        {
                            Id = ManifestId.Create(contentItem.SourceId),
                            Name = "Required Game Installation",
                            DependencyType = ContentType.GameInstallation,
                            CompatibleGameTypes = [contentItem.GameType],
                            IsOptional = false,
                            InstallBehavior = DependencyInstallBehavior.RequireExisting
                        }
                    ]
                };
            }
            else
            {
                // Valid manifest not found and not a handled dynamic type
                return;
            }

            if (manifest.Dependencies == null || manifest.Dependencies.Count == 0)
            {
                // No dependencies to resolve
                _ = ValidateEnabledContentDependenciesAsync(contentItem.DisplayName);
                return;
            }

            foreach (var dependency in manifest.Dependencies)
            {
                // 1. Handle GameInstallation Dependency
                if (dependency.DependencyType == ContentType.GameInstallation)
                {
                    // Check if current installation satisfies the dependency
                    bool isSatisfied = false;

                    // Check compatible game types
                    if (dependency.CompatibleGameTypes != null && dependency.CompatibleGameTypes.Count > 0)
                    {
                        if (SelectedGameInstallation != null &&
                            SelectedGameInstallation.IsEnabled &&
                            dependency.CompatibleGameTypes.Contains(SelectedGameInstallation.GameType))
                        {
                            isSatisfied = true;
                        }
                    }

                    // Check specific ID if not strictly just type-based
                    if (!isSatisfied && dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                    {
                        if (SelectedGameInstallation != null &&
                            SelectedGameInstallation.IsEnabled &&
                            SelectedGameInstallation.ManifestId.Value == dependency.Id.ToString())
                        {
                            isSatisfied = true;
                        }
                    }

                    if (!isSatisfied)
                    {
                        // Need to switch GameInstallation
                        // Find a compatible one in AvailableGameInstallations
                        ContentDisplayItem? compatibleInstallation = null;

                        // First try finding by specific ID
                        if (dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                        {
                            compatibleInstallation = AvailableGameInstallations
                                .FirstOrDefault(x => x.ManifestId.Value == dependency.Id.ToString());
                        }

                        // If not found or no specific ID, try by compatible game type
                        if (compatibleInstallation == null && dependency.CompatibleGameTypes != null)
                        {
                            compatibleInstallation = AvailableGameInstallations
                                .FirstOrDefault(x => dependency.CompatibleGameTypes.Contains(x.GameType));
                        }

                        if (compatibleInstallation != null)
                        {
                            // We found a compatible installation. We must ENABLE it properly so it gets added to EnabledContent
                            // and the previous one gets removed/disabled.
                            // Simply setting SelectedGameInstallation is NOT enough as validation relies on EnabledContent list.

                            logger?.LogInformation(
                                "Auto-resolving dependency: Switching GameInstallation to {InstallationName} for {ContentName}",
                                compatibleInstallation.DisplayName,
                                contentItem.DisplayName);

                            _localNotificationService.ShowSuccess(
                                "Auto-Resolved",
                                $"Switched Game Installation to '{compatibleInstallation.DisplayName}' as required by '{contentItem.DisplayName}'.");

                            // Recursively call EnableContent to handle the full switch logic (disabling old, enabling new, setting property)
                            EnableContent(compatibleInstallation);
                        }
                    }
                }
                // 2. Handle Content Dependencies (MapPack, Addon, etc)
                else
                {
                    // Check if already enabled
                    bool alreadyEnabled = false;

                    // Check by ID
                    if (dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                    {
                        alreadyEnabled = EnabledContent.Any(x => x.ManifestId.Value == dependency.Id.ToString());
                    }
                    else
                    {
                        // Check if any content of the required type is enabled
                        alreadyEnabled = EnabledContent.Any(x => x.ContentType == dependency.DependencyType);
                    }

                    if (!alreadyEnabled && !dependency.IsOptional)
                    {
                        // Try to find it in "AvailableContent" first? 
                        // It might not be in the visible list if filtered.

                        // Use loader to find the best match
                        var availableOfTargetType = await profileContentLoader!.LoadAvailableContentAsync(
                            dependency.DependencyType,
                            new ObservableCollection<Core.Models.Content.ContentDisplayItem>(
                                AvailableGameInstallations.Select(x => new Core.Models.Content.ContentDisplayItem
                                {
                                    Id = x.ManifestId.Value,
                                    ManifestId = x.ManifestId.Value,
                                    DisplayName = x.DisplayName,
                                    ContentType = x.ContentType,
                                    GameType = x.GameType
                                })),
                            EnabledContent.Select(x => x.ManifestId.Value).ToList());

                        Core.Models.Content.ContentDisplayItem? match = null;

                        // Find specific match by ID
                        if (dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                        {
                            match = availableOfTargetType.FirstOrDefault(x => x.ManifestId == dependency.Id.ToString());
                        }

                        if (match != null)
                        {
                            var viewModelItem = ConvertToViewModelContentDisplayItem(match);

                            // Check clearly if it's already enabled to avoid recursion (though EnableContent handles it)
                            if (!viewModelItem.IsEnabled)
                            {
                                logger?.LogInformation(
                                    "Auto-resolved dependency: Found required content {DependencyName}. Enabling it.",
                                    viewModelItem.DisplayName);

                                // Show specific notification for this action
                                _localNotificationService.ShowSuccess(
                                    "Auto-Resolved",
                                    $"Automatically enabled required content: '{viewModelItem.DisplayName}'");

                                // Call EnableContent to handle standard logic (moving from available, cardinality, etc.)
                                // This is recursive but safe because we check IsEnabled
                                EnableContent(viewModelItem);
                            }
                        }
                    }
                }
            }

            // Finally, perform standard validation (shows warnings if anything is still missing)
            await ValidateEnabledContentDependenciesAsync(contentItem.DisplayName);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error resolving dependencies for {ContentName}", contentItem.DisplayName);
            // Fallback to standard validation
            _ = ValidateEnabledContentDependenciesAsync(contentItem.DisplayName);
        }
    }

    /// <summary>
    /// Validates dependencies for enabled content and shows warning notifications if conflicts are detected.
    /// </summary>
    /// <param name="justEnabledContentName">The name of the content that was just enabled.</param>
    private async Task ValidateEnabledContentDependenciesAsync(string justEnabledContentName)
    {
        try
        {
            if (manifestPool == null)
            {
                logger?.LogDebug("Skipping dependency validation - manifestPool not available");
                return;
            }

            // Get all enabled content manifest IDs
            var enabledManifestIds = EnabledContent.Select(e => e.ManifestId.Value).ToList();
            if (enabledManifestIds.Count == 0)
            {
                return;
            }

            logger?.LogDebug(
                "Validating dependencies for {Count} enabled content items: {ContentIds}",
                enabledManifestIds.Count,
                string.Join(", ", enabledManifestIds));

            // Load manifests for all enabled content (except GameInstallations which aren't in the pool)
            var manifests = new List<ContentManifest>();
            foreach (var manifestId in enabledManifestIds)
            {
                var manifestResult = await manifestPool.GetManifestAsync(manifestId);
                if (manifestResult.Success && manifestResult.Data != null)
                {
                    manifests.Add(manifestResult.Data);
                    logger?.LogDebug("Loaded manifest for validation: {ManifestId} ({ContentType})", manifestId, manifestResult.Data.ContentType);
                }
                else
                {
                    logger?.LogDebug("Manifest {ManifestId} not found in pool (likely a GameInstallation)", manifestId);
                }
            }

            // Check for missing dependencies
            var warnings = new List<string>();
            var manifestsById = manifests.ToDictionary(m => m.Id.ToString(), m => m);
            var manifestsByType = manifests.GroupBy(m => m.ContentType).ToDictionary(g => g.Key, g => g.ToList());

            // Also track enabled content types from EnabledContent (includes GameInstallations not in manifest pool)
            var enabledContentByType = EnabledContent.GroupBy(e => e.ContentType).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var manifest in manifests)
            {
                if (manifest.Dependencies == null || manifest.Dependencies.Count == 0)
                {
                    continue;
                }

                logger?.LogDebug("Checking {Count} dependencies for {ManifestName}", manifest.Dependencies.Count, manifest.Name);

                foreach (var dependency in manifest.Dependencies)
                {
                    // For GameInstallation and GameClient, check EnabledContent directly (not just manifest pool)
                    // because GameInstallations are created on-the-fly and not stored in the manifest pool
                    if (dependency.DependencyType == ContentType.GameInstallation ||
                        dependency.DependencyType == ContentType.GameClient)
                    {
                        if (!enabledContentByType.TryGetValue(dependency.DependencyType, out var enabledOfType) || enabledOfType.Count == 0)
                        {
                            if (dependency.DependencyType == ContentType.GameInstallation)
                            {
                                warnings.Add($"'{manifest.Name}' requires a Game Installation to be selected.");
                                logger?.LogWarning(
                                    "Dependency validation failed: {ManifestName} requires GameInstallation but none found in EnabledContent",
                                    manifest.Name);
                            }
                            else if (dependency.DependencyType == ContentType.GameClient)
                            {
                                warnings.Add($"'{manifest.Name}' requires a Game Client to be selected.");
                                logger?.LogWarning(
                                    "Dependency validation failed: {ManifestName} requires GameClient but none found in EnabledContent",
                                    manifest.Name);
                            }

                            continue;
                        }

                        // Type-based match is sufficient for GameInstallation/GameClient - we found at least one
                        logger?.LogDebug(
                            "Dependency satisfied: {ManifestName} requires {DependencyType}, found {Count} enabled",
                            manifest.Name,
                            dependency.DependencyType,
                            enabledOfType.Count);
                        continue;
                    }

                    // Check if a content of the required type exists (for other content types)
                    if (!manifestsByType.TryGetValue(dependency.DependencyType, out var potentialMatches) || potentialMatches.Count == 0)
                    {
                        if (!dependency.IsOptional)
                        {
                            warnings.Add($"'{manifest.Name}' requires {dependency.DependencyType} content, but none is enabled.");
                            logger?.LogWarning(
                                "Dependency validation failed: {ManifestName} requires {DependencyType} but none found",
                                manifest.Name,
                                dependency.DependencyType);
                        }

                        continue;
                    }

                    // Check for specific content ID requirement (not a generic type-based constraint)
                    if (dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                    {
                        // First try exact match
                        bool found = manifestsById.ContainsKey(dependency.Id.ToString());

                        // If StrictPublisher is false, try semantic matching
                        if (!found && !dependency.StrictPublisher)
                        {
                            var depIdSegments = dependency.Id.ToString().Split('.');
                            if (depIdSegments.Length >= 5)
                            {
                                var depContentType = depIdSegments[3];
                                var depContentName = depIdSegments[4];

                                found = potentialMatches.Any(m =>
                                {
                                    var manifestIdSegments = m.Id.ToString().Split('.');
                                    if (manifestIdSegments.Length >= 5)
                                    {
                                        return string.Equals(manifestIdSegments[3], depContentType, StringComparison.OrdinalIgnoreCase) &&
                                               string.Equals(manifestIdSegments[4], depContentName, StringComparison.OrdinalIgnoreCase);
                                    }

                                    return false;
                                });
                            }
                        }

                        if (!found && !dependency.IsOptional)
                        {
                            warnings.Add($"'{manifest.Name}' requires '{dependency.Name}' which is not enabled.");
                        }
                    }

                    // Check for conflicts
                    if (dependency.ConflictsWith.Count > 0)
                    {
                        foreach (var conflictId in dependency.ConflictsWith)
                        {
                            if (manifestsById.TryGetValue(conflictId.ToString(), out var conflictingManifest))
                            {
                                warnings.Add($"'{manifest.Name}' conflicts with '{conflictingManifest.Name}' - these cannot be used together.");
                            }
                        }
                    }
                }
            }

            // Show warning notifications if any issues found
            if (warnings.Count > 0)
            {
                var warningMessage = string.Join("\n• ", warnings);

                // Use local notification service so it appears on the window
                _localNotificationService.ShowWarning(
                    "Dependency Warning",
                    $"After enabling '{justEnabledContentName}':\n• {warningMessage}",
                    15000); // Show for 15 seconds since this is important info

                logger?.LogWarning(
                    "Dependency validation warnings after enabling {ContentName}: {Warnings}",
                    justEnabledContentName,
                    string.Join("; ", warnings));
            }
            else
            {
                logger?.LogInformation(
                    "Dependency validation passed after enabling {ContentName}",
                    justEnabledContentName);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during dependency validation for enabled content");
        }
    }

    /// <summary>
    /// Validates all dependencies for the given content IDs and returns a list of error messages.
    /// </summary>
    /// <param name="enabledContentIds">The list of enabled content manifest IDs.</param>
    /// <returns>A list of error messages for missing dependencies.</returns>
    private async Task<List<string>> ValidateAllDependenciesAsync(List<string> enabledContentIds)
    {
        var errors = new List<string>();

        try
        {
            if (manifestPool == null)
            {
                return errors;
            }

            // Load manifests for all enabled content
            var manifests = new List<ContentManifest>();
            foreach (var manifestId in enabledContentIds)
            {
                var manifestResult = await manifestPool.GetManifestAsync(manifestId);
                if (manifestResult.Success && manifestResult.Data != null)
                {
                    manifests.Add(manifestResult.Data);
                }
            }

            var manifestsById = manifests.ToDictionary(m => m.Id.ToString(), m => m);
            var manifestsByType = manifests.GroupBy(m => m.ContentType).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var manifest in manifests)
            {
                if (manifest.Dependencies == null || manifest.Dependencies.Count == 0)
                {
                    continue;
                }

                foreach (var dependency in manifest.Dependencies)
                {
                    // Check if a content of the required type exists
                    if (!manifestsByType.TryGetValue(dependency.DependencyType, out var potentialMatches) || potentialMatches.Count == 0)
                    {
                        if (!dependency.IsOptional)
                        {
                            // Missing type-based dependency
                            if (dependency.DependencyType == ContentType.GameInstallation)
                            {
                                errors.Add($"• '{manifest.Name}' requires a Game Installation");
                            }
                            else if (dependency.DependencyType == ContentType.GameClient)
                            {
                                errors.Add($"• '{manifest.Name}' requires a Game Client");
                            }
                            else
                            {
                                errors.Add($"• '{manifest.Name}' requires {dependency.DependencyType} content");
                            }
                        }

                        continue;
                    }

                    // Check for specific content ID requirement
                    if (dependency.Id.ToString() != Core.Constants.ManifestConstants.DefaultContentDependencyId)
                    {
                        bool found = manifestsById.ContainsKey(dependency.Id.ToString());

                        // If StrictPublisher is false, try semantic matching
                        if (!found && !dependency.StrictPublisher)
                        {
                            var depIdSegments = dependency.Id.ToString().Split('.');
                            if (depIdSegments.Length >= 5)
                            {
                                var depContentType = depIdSegments[3];
                                var depContentName = depIdSegments[4];

                                found = potentialMatches.Any(m =>
                                {
                                    var manifestIdSegments = m.Id.ToString().Split('.');
                                    return manifestIdSegments.Length >= 5 &&
                                           manifestIdSegments[3] == depContentType &&
                                           manifestIdSegments[4] == depContentName;
                                });
                            }
                        }

                        if (!found && !dependency.IsOptional)
                        {
                            // Try to get the dependency manifest to show a friendly name
                            var depManifestResult = await manifestPool.GetManifestAsync(dependency.Id.ToString());
                            var depName = depManifestResult.Success && depManifestResult.Data != null
                                ? depManifestResult.Data.Name
                                : dependency.Id.ToString();

                            errors.Add($"• '{manifest.Name}' requires '{depName}'");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during comprehensive dependency validation");
            errors.Add($"• Validation error: {ex.Message}");
        }

        return errors;
    }

    /// <summary>
    /// Disables the specified content item.
    /// </summary>
    /// <param name="contentItem">The content item to disable.</param>
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
            itemToRemove.IsEnabled = false;
            EnabledContent.Remove(itemToRemove);
        }

        // If the disabled content matches the current content type filter, add it back to AvailableContent
        if (contentItem.ContentType == SelectedContentType)
        {
            var alreadyInAvailable = AvailableContent.FirstOrDefault(a => a.ManifestId.Value == contentItem.ManifestId.Value);
            if (alreadyInAvailable == null)
            {
                // Create a new instance with IsEnabled = false
                var availableItem = new ContentDisplayItem
                {
                    ManifestId = contentItem.ManifestId,
                    DisplayName = contentItem.DisplayName,
                    ContentType = contentItem.ContentType,
                    GameType = contentItem.GameType,
                    InstallationType = contentItem.InstallationType,
                    Publisher = contentItem.Publisher,
                    IsEnabled = false,
                    SourceId = contentItem.SourceId,
                    GameClientId = contentItem.GameClientId,
                    Version = contentItem.Version,
                };
                AvailableContent.Add(availableItem);
            }
            else
            {
                alreadyInAvailable.IsEnabled = false;
            }
        }

        if (contentItem.ContentType == ContentType.GameInstallation &&
            SelectedGameInstallation?.ManifestId.Value == contentItem.ManifestId.Value)
        {
            // Try to find another enabled GameInstallation
            var anotherEnabledInstallation = EnabledContent.FirstOrDefault(c => c.ContentType == ContentType.GameInstallation && c.IsEnabled);

            if (anotherEnabledInstallation != null)
            {
                SelectedGameInstallation = anotherEnabledInstallation;
                logger?.LogInformation(
                    "Switched SelectedGameInstallation to {DisplayName} after disabling previous selection",
                    anotherEnabledInstallation.DisplayName);
            }
            else
            {
                // No GameInstallation enabled - try to select from available list
                SelectedGameInstallation = AvailableGameInstallations.FirstOrDefault();
                logger?.LogWarning(
                    "No GameInstallation enabled - reset to first available: {DisplayName}",
                    SelectedGameInstallation?.DisplayName ?? "None");
            }
        }

        StatusMessage = $"Disabled {contentItem.DisplayName}";
        logger?.LogInformation("Disabled content {ContentName} for profile", contentItem.DisplayName);
    }

    /// <summary>
    /// Deletes the specified content item from storage.
    /// </summary>
    /// <param name="contentItem">The content item to delete.</param>
    [RelayCommand]
    private async Task DeleteContentAsync(ContentDisplayItem? contentItem)
    {
        if (contentItem == null)
        {
            StatusMessage = "No content selected";
            logger?.LogWarning("DeleteContentAsync: contentItem parameter is NULL");
            return;
        }

        if (contentStorageService == null)
        {
            StatusMessage = "Content storage service not available";
            logger?.LogError("DeleteContentAsync: contentStorageService is NULL");
            return;
        }

        // Check if content is currently enabled in this profile
        var isEnabled = EnabledContent.Any(e => e.ManifestId.Value == contentItem.ManifestId.Value);
        if (isEnabled)
        {
            _localNotificationService.ShowWarning(
                "Cannot Delete",
                $"Cannot delete '{contentItem.DisplayName}' because it is currently enabled in this profile. Please disable it first.");
            logger?.LogWarning(
                "DeleteContentAsync: Cannot delete {ContentName} because it is enabled in the profile",
                contentItem.DisplayName);
            return;
        }

        // Prevent deletion of GameInstallation content types
        // GameInstallations reference existing files on the user's drive, not CAS-stored content
        if (contentItem.ContentType == Core.Models.Enums.ContentType.GameInstallation)
        {
            _localNotificationService.ShowWarning(
                "Cannot Delete",
                $"Cannot delete '{contentItem.DisplayName}' because it references an existing game installation on your drive. Only downloaded content (mods, maps, etc.) can be deleted.");
            logger?.LogWarning(
                "DeleteContentAsync: Cannot delete {ContentName} because it is a GameInstallation",
                contentItem.DisplayName);
            return;
        }

        // Show confirmation dialog
        var confirmationMessage = $"Are you sure you want to delete '{contentItem.DisplayName}' from storage? This action cannot be undone.";
        var confirmed = await ShowConfirmationDialogAsync("Delete Content", confirmationMessage);

        if (!confirmed)
        {
            logger?.LogInformation("DeleteContentAsync: User cancelled deletion of {ContentName}", contentItem.DisplayName);
            return;
        }

        try
        {
            StatusMessage = $"Deleting {contentItem.DisplayName}...";
            logger?.LogInformation(
                "DeleteContentAsync: Deleting content {ContentName} (ManifestId: {ManifestId})",
                contentItem.DisplayName,
                contentItem.ManifestId.Value);

            // Call the content storage service to remove the content
            var result = await contentStorageService.RemoveContentAsync(contentItem.ManifestId);

            if (result.Success)
            {
                // Remove from AvailableContent collection
                var itemToRemove = AvailableContent.FirstOrDefault(a => a.ManifestId.Value == contentItem.ManifestId.Value);
                if (itemToRemove != null)
                {
                    AvailableContent.Remove(itemToRemove);
                }

                StatusMessage = $"Deleted {contentItem.DisplayName}";
                _localNotificationService.ShowSuccess(
                    "Content Deleted",
                    $"Successfully deleted '{contentItem.DisplayName}' from storage.");
                logger?.LogInformation(
                    "DeleteContentAsync: Successfully deleted content {ContentName}",
                    contentItem.DisplayName);
            }
            else
            {
                StatusMessage = $"Failed to delete {contentItem.DisplayName}";
                _localNotificationService.ShowError(
                    "Deletion Failed",
                    $"Failed to delete '{contentItem.DisplayName}': {string.Join(", ", result.Errors)}");
                logger?.LogError(
                    "DeleteContentAsync: Failed to delete content {ContentName}: {Errors}",
                    contentItem.DisplayName,
                    string.Join(", ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting {contentItem.DisplayName}";
            _localNotificationService.ShowError(
                "Deletion Error",
                $"An error occurred while deleting '{contentItem.DisplayName}': {ex.Message}");
            logger?.LogError(
                ex,
                "DeleteContentAsync: Exception while deleting content {ContentName}",
                contentItem.DisplayName);
        }
    }

    /// <summary>
    /// Shows a confirmation dialog and returns the user's choice.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <returns>True if the user confirmed, false otherwise.</returns>
    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        // Show warning notification to inform the user
        // Note: In a production scenario, this would integrate with a proper modal dialog service
        _localNotificationService.ShowWarning(title, message, autoDismissMs: 5000);

        // Wait a moment for the user to see the notification
        await Task.Delay(1000);

        // TODO: Integrate with a proper confirmation dialog service that returns user choice
        // For now, we proceed with the action (return true)
        return true;
    }

    /// <summary>
    /// Saves the profile.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            IsSaving = true;
            StatusMessage = "Saving profile...";

            if (gameProfileManager == null)
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

            // Validate that GameInstallation content is enabled
            var hasGameInstallation = EnabledContent.Any(c => c.ContentType == ContentType.GameInstallation && c.IsEnabled);
            if (!hasGameInstallation)
            {
                StatusMessage = "Error: A Game Installation must be enabled for the profile to be launchable.";
                _localNotificationService.ShowError(
                    "Missing Game Installation",
                    "Please enable a Game Installation before saving the profile. The profile cannot be launched without one.");
                logger?.LogWarning("Profile save blocked: No GameInstallation content enabled");
                return;
            }

            // Build enabled content IDs from all enabled content
            var enabledContentIds = EnabledContent.Where(c => c.IsEnabled).Select(c => c.ManifestId.Value).ToList();

            // Validate all dependencies before saving
            if (manifestPool != null)
            {
                var validationErrors = await ValidateAllDependenciesAsync(enabledContentIds);
                if (validationErrors.Count > 0)
                {
                    var errorMessage = string.Join("\n", validationErrors);
                    StatusMessage = "Error: Missing required dependencies";
                    _localNotificationService.ShowError(
                        "Missing Dependencies",
                        $"Cannot save profile with missing dependencies:\n\n{errorMessage}");
                    logger?.LogWarning("Profile save blocked: {Errors}", errorMessage);
                    return;
                }
            }

            logger?.LogInformation(
                "Profile will be created/updated with {Count} enabled content items: {ContentIds}",
                enabledContentIds.Count,
                string.Join(", ", enabledContentIds));

            if (string.IsNullOrEmpty(_currentProfileId))
            {
                // Create new profile

                // Ensure SelectedGameInstallation manifest is added if not already present
                if (!enabledContentIds.Contains(SelectedGameInstallation.ManifestId.Value, StringComparer.OrdinalIgnoreCase))
                {
                    enabledContentIds.Insert(0, SelectedGameInstallation.ManifestId.Value);
                    logger?.LogInformation("Auto-enabled SelectedGameInstallation: {ManifestId}", SelectedGameInstallation.ManifestId.Value);
                }

                // Auto-enable GameClient ONLY if no GameClient content is already enabled
                var hasGameClientEnabled = EnabledContent.Any(c => c.IsEnabled && c.ContentType == ContentType.GameClient);
                if (!hasGameClientEnabled &&
                    !string.IsNullOrEmpty(SelectedGameInstallation.GameClientId) &&
                    !enabledContentIds.Contains(SelectedGameInstallation.GameClientId, StringComparer.OrdinalIgnoreCase))
                {
                    // Add after GameInstallation (index 1) if we just added it, or anywhere if already present
                    var insertIndex = enabledContentIds.IndexOf(SelectedGameInstallation.ManifestId.Value) + 1;
                    enabledContentIds.Insert(Math.Min(insertIndex, enabledContentIds.Count), SelectedGameInstallation.GameClientId);
                    logger?.LogInformation("Auto-enabled default GameClient content: {GameClientId}", SelectedGameInstallation.GameClientId);
                }
                else if (hasGameClientEnabled)
                {
                    logger?.LogInformation("Skipping auto-enable GameClient - user has already selected a GameClient");
                }

                var createRequest = new CreateProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    GameInstallationId = SelectedGameInstallation.SourceId,
                    GameClientId = SelectedGameInstallation.GameClientId,
                    PreferredStrategy = SelectedWorkspaceStrategy,
                    EnabledContentIds = enabledContentIds,
                    CommandLineArguments = CommandLineArguments,
                    IconPath = IconPath,
                    CoverPath = CoverPath,
                };

                var result = await gameProfileManager.CreateProfileAsync(createRequest);
                if (result.Success && result.Data != null)
                {
                    StatusMessage = "Profile created successfully";
                    logger?.LogInformation("Created new profile {ProfileName} with {ContentCount} enabled content items", Name, enabledContentIds.Count);

                    // Notify other components that a profile was created
                    WeakReferenceMessenger.Default.Send(new ProfileCreatedMessage(result.Data));

                    // Close the window after a brief delay
                    await Task.Delay(1000);
                    ExecuteCancel();
                }
                else
                {
                    StatusMessage = $"Failed to create profile: {string.Join(", ", result.Errors)}";
                    logger?.LogWarning("Failed to create profile: {Errors}", string.Join(", ", result.Errors));
                }
            }
            else
            {
                // Update existing profile
                var gameSettings = GameSettingsViewModel?.GetProfileSettings();

                var updateRequest = new UpdateProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    ThemeColor = ColorValue,
                    GameInstallationId = SelectedGameInstallation?.SourceId, // Update installation ID when user changes installation

                    // Only update strategy if it was actually changed from the original
                    PreferredStrategy = _originalWorkspaceStrategy.HasValue && SelectedWorkspaceStrategy != _originalWorkspaceStrategy.Value
                        ? SelectedWorkspaceStrategy
                        : null,
                    EnabledContentIds = enabledContentIds,
                    CommandLineArguments = CommandLineArguments,
                    IconPath = IconPath,
                    CoverPath = CoverPath,
                };

                PopulateGameSettings(updateRequest, gameSettings);

                var result = await gameProfileManager.UpdateProfileAsync(_currentProfileId, updateRequest);
                if (result.Success && result.Data != null)
                {
                    StatusMessage = "Profile updated successfully";
                    logger?.LogInformation("Updated profile {ProfileId} with {ContentCount} enabled content items", _currentProfileId, enabledContentIds.Count);

                    // Notify other components that a profile was updated
                    WeakReferenceMessenger.Default.Send(new ProfileUpdatedMessage(result.Data));

                    // Close the window after a brief delay
                    await Task.Delay(1000);
                    ExecuteCancel();
                }
                else
                {
                    StatusMessage = $"Failed to update profile: {string.Join(", ", result.Errors)}";
                    logger?.LogWarning("Failed to update profile {ProfileId}: {Errors}", _currentProfileId, string.Join(", ", result.Errors));
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error saving profile");
            StatusMessage = "Error saving profile";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Loads available icons and covers based on the game type.
    /// </summary>
    private void LoadAvailableIconsAndCovers(string gameType)
    {
        try
        {
            if (profileResourceService == null)
            {
                logger?.LogWarning("ProfileResourceService is not available");
                return;
            }

            // Load icons for this game type
            var icons = profileResourceService.GetIconsForGameType(gameType);
            AvailableIcons = new ObservableCollection<ProfileResourceItem>(icons);
            logger?.LogInformation("Loaded {Count} icons for game type {GameType}", icons.Count, gameType);

            // Load ALL covers (not filtered by game type) so users can choose any cover
            var covers = profileResourceService.GetAvailableCovers();
            AvailableCoversForSelection = new ObservableCollection<ProfileResourceItem>(covers);
            logger?.LogInformation("Loaded {Count} covers (all types)", covers.Count);

            // Set selected icon based on current IconPath
            if (!string.IsNullOrEmpty(IconPath))
            {
                SelectedIcon = AvailableIcons.FirstOrDefault(i => i.Path == IconPath);
            }

            // Set selected cover based on current CoverPath
            if (!string.IsNullOrEmpty(CoverPath))
            {
                SelectedCoverItem = AvailableCoversForSelection.FirstOrDefault(c => c.Path == CoverPath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading available icons and covers");
        }
    }

    /// <summary>
    /// Selects an icon for the profile.
    /// </summary>
    [RelayCommand]
    private void SelectIcon(ProfileResourceItem? icon)
    {
        if (icon == null)
        {
            return;
        }

        SelectedIcon = icon;
        IconPath = icon.Path;
        logger?.LogInformation("Selected icon: {DisplayName} ({Path})", icon.DisplayName, icon.Path);
    }

    /// <summary>
    /// Selects a cover for the profile.
    /// </summary>
    [RelayCommand]
    private void SelectCover(ProfileResourceItem? cover)
    {
        if (cover == null)
        {
            return;
        }

        SelectedCoverItem = cover;
        CoverPath = cover.Path;
        logger?.LogInformation("Selected cover: {DisplayName} ({Path})", cover.DisplayName, cover.Path);
    }

    /// <summary>
    /// Opens a file dialog to browse for a custom icon.
    /// </summary>
    [RelayCommand]
    private async Task BrowseForCustomIconAsync()
    {
        try
        {
            var openFileDialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Custom Icon",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
                    {
                        Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico" ],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var storageProvider = topLevel.StorageProvider;
                var result = await storageProvider.OpenFilePickerAsync(openFileDialog);

                if (result.Count > 0)
                {
                    var selectedFile = result[0];
                    IconPath = selectedFile.Path.LocalPath;
                    SelectedIcon = null; // Clear built-in selection when using custom
                    logger?.LogInformation("Selected custom icon: {Path}", IconPath);
                    StatusMessage = "Custom icon selected";
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error browsing for custom icon");
            StatusMessage = "Error selecting custom icon";
        }
    }

    /// <summary>
    /// Opens a file dialog to browse for a custom cover.
    /// </summary>
    [RelayCommand]
    private async Task BrowseForCustomCoverAsync()
    {
        try
        {
            var openFileDialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Custom Cover",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
                    {
                        Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.bmp" ],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var storageProvider = topLevel.StorageProvider;
                var result = await storageProvider.OpenFilePickerAsync(openFileDialog);

                if (result.Count > 0)
                {
                    var selectedFile = result[0];
                    CoverPath = selectedFile.Path.LocalPath;
                    SelectedCoverItem = null; // Clear built-in selection when using custom
                    logger?.LogInformation("Selected custom cover: {Path}", CoverPath);
                    StatusMessage = "Custom cover selected";
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error browsing for custom cover");
            StatusMessage = "Error selecting custom cover";
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
            "#1976D2", "#388E3C", "#FBC02D", "#FF5722", "#7B1FA2",
            "#D32F2F", "#0097A7", "#689F38", "#AFB42B", "#0288D1",
            "#C2185B", "#512DA8",
        };

        var random = new Random();
        ColorValue = colors[random.Next(colors.Count)];
        if (GameSettingsViewModel != null)
        {
            GameSettingsViewModel.ColorValue = ColorValue;
        }

        StatusMessage = $"Color randomized to {ColorValue}";
        logger?.LogInformation("Randomized profile color to {ColorValue}", ColorValue);
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
            if (GameSettingsViewModel != null)
            {
                GameSettingsViewModel.ColorValue = ColorValue;
            }

            StatusMessage = $"Selected theme color {color}";
            logger?.LogInformation("Selected theme color {ColorValue}", color);
        }
        else
        {
            StatusMessage = "Invalid color selected";
            logger?.LogWarning("Invalid color parameter passed to SelectThemeColor");
        }
    }

    /// <summary>
    /// Browses for a custom cover image.
    /// </summary>
    [RelayCommand]
    private void BrowseCustomCover()
    {
        // TODO: Implement file dialog for selecting cover image
        StatusMessage = "Browse custom cover: TODO - Implement file dialog";
        logger?.LogInformation("BrowseCustomCoverCommand executed");
    }

    /// <summary>
    /// Browses for a shortcut path.
    /// </summary>
    [RelayCommand]
    private void BrowseShortcutPath()
    {
        // TODO: Implement file dialog for shortcut path
        StatusMessage = "Browse shortcut path: TODO - Implement file dialog";
        logger?.LogInformation("BrowseShortcutPathCommand executed");
    }

    /// <summary>
    /// Changes the selected content type filter.
    /// </summary>
    /// <param name="contentType">The content type to filter by.</param>
    [RelayCommand]
    private void SelectContentTypeFilter(ContentType? contentType)
    {
        if (contentType.HasValue && contentType.Value != SelectedContentType)
        {
            SelectedContentType = contentType.Value;
            logger?.LogInformation("Content type filter changed to {ContentType}", contentType.Value);
        }
    }

    /// <summary>
    /// Selects a tab by index.
    /// </summary>
    /// <param name="tabIndexStr">The tab index as a string.</param>
    [RelayCommand]
    private void SelectTab(string? tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out var tabIndex))
        {
            SelectedTabIndex = tabIndex;
            logger?.LogDebug("Tab selected: {TabIndex}", tabIndex);
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
    /// Opens a folder picker dialog and shows the local content configuration dialog.
    /// </summary>
    [RelayCommand]
    private async Task AddLocalContentAsync()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                StatusMessage = "Unable to open folder picker";
                return;
            }

            var folderPickerOptions = new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Local Content Folder",
                AllowMultiple = false,
            };

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (result.Count > 0)
            {
                var selectedFolder = result[0];
                LocalContentDirectoryPath = selectedFolder.Path.LocalPath;
                LocalContentName = System.IO.Path.GetFileName(LocalContentDirectoryPath);
                SelectedLocalContentType = ContentType.Addon; // Default
                IsAddLocalContentDialogOpen = true;

                logger?.LogInformation("Selected local content folder: {Path}", LocalContentDirectoryPath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error opening folder picker for local content");
            StatusMessage = "Error selecting folder";
        }
    }

    /// <summary>
    /// Confirms adding the local content and creates a manifest.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAddLocalContent()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(LocalContentName))
            {
                StatusMessage = "Please enter a content name";
                return;
            }

            if (string.IsNullOrWhiteSpace(LocalContentDirectoryPath))
            {
                StatusMessage = "No folder selected";
                return;
            }

            // Determine the game type from enabled content or default to Generals
            var firstContent = EnabledContent.FirstOrDefault();
            Core.Models.Enums.GameType targetGameType = (firstContent != null) ? firstContent.GameType : Core.Models.Enums.GameType.Generals;

            // Call local content service to create manifest and store content
            IsLoadingContent = true;
            StatusMessage = "Initializing content import...";

            // Show notification for user awareness of potentially long operation
            _localNotificationService?.ShowInfo(
                "Importing Content",
                $"Importing '{LocalContentName}' - this may take a moment for large folders...",
                autoDismissMs: 5000);

            // Create progress handler
            var progress = new Progress<Core.Models.Content.ContentStorageProgress>(p =>
            {
                // Update status message on UI thread
                if (p.TotalCount > 0)
                {
                    // Show percentage for large operations
                    StatusMessage = $"Importing: {p.Percentage:0}% ({p.ProcessedCount}/{p.TotalCount} files)";
                }
                else
                {
                    StatusMessage = $"Importing: {p.ProcessedCount} files processed";
                }
            });

            if (localContentService == null)
            {
                StatusMessage = "Local content service not available";
                IsLoadingContent = false;
                return;
            }

            var result = await localContentService.CreateLocalContentManifestAsync(
                LocalContentDirectoryPath,
                LocalContentName,
                SelectedLocalContentType,
                targetGameType,
                progress);

            if (!result.Success)
            {
                StatusMessage = $"Import failed: {result.FirstError}";
                notificationService?.ShowError("Import Error", result.FirstError ?? "Unknown error");
                return;
            }

            var manifest = result.Data;

            // Create a ContentDisplayItem for the local content
            var localContentItem = new ContentDisplayItem
            {
                ManifestId = ManifestId.Create(manifest.Id),
                DisplayName = manifest.Name ?? LocalContentName,
                ContentType = manifest.ContentType,
                GameType = manifest.TargetGame,
                InstallationType = GameInstallationType.Unknown,
                Publisher = manifest.Publisher?.Name ?? "GenHub (Local)",
                Version = manifest.Version ?? "1.0.0",
                SourceId = LocalContentDirectoryPath,
                IsEnabled = true,
            };

            EnabledContent.Add(localContentItem);

            // Add to AvailableContent if it matches the current filter to update UI immediately
            if (localContentItem.ContentType == SelectedContentType)
            {
                // Check if not already in available list (shouldn't be, but defensive)
                if (!AvailableContent.Any(a => a.ManifestId.Value == localContentItem.ManifestId.Value))
                {
                    AvailableContent.Add(localContentItem);
                    logger?.LogDebug("Added local content to AvailableContent for immediate UI update");
                }
            }

            StatusMessage = $"Added local content: {LocalContentName}";
            logger?.LogInformation(
                "Added local content '{Name}' as {ContentType} from {Path}",
                LocalContentName,
                SelectedLocalContentType,
                LocalContentDirectoryPath);

            // Notify user that content is stored in CAS
            _localNotificationService?.ShowSuccess(
                "Local Content Added",
                $"'{LocalContentName}' has been imported. {manifest.Files.Count} files stored.\nYou can safely delete the source folder '{LocalContentDirectoryPath}' if desired.",
                autoDismissMs: 10000);

            // Close the dialog and reset state
            IsAddLocalContentDialogOpen = false;
            LocalContentName = string.Empty;
            LocalContentDirectoryPath = string.Empty;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error adding local content");
            StatusMessage = $"Error adding local content: {ex.Message}";
        }
        finally
        {
            IsLoadingContent = false;
        }
    }

    /// <summary>
    /// Cancels the add local content dialog.
    /// </summary>
    [RelayCommand]
    private void CancelAddLocalContent()
    {
        IsAddLocalContentDialogOpen = false;
        LocalContentName = string.Empty;
        LocalContentDirectoryPath = string.Empty;
        StatusMessage = "Add local content cancelled";
    }

    /// <summary>
    /// Called when the selected content type changes.
    /// </summary>
    private async Task OnContentTypeChangedAsync()
    {
        await LoadAvailableContentAsync();
    }

    /// <summary>
    /// Converts a Core ContentDisplayItem to the ViewModel's ContentDisplayItem.
    /// </summary>
    /// <param name="coreItem">The core content display item.</param>
    /// <returns>A ViewModel content display item.</returns>
    private ContentDisplayItem ConvertToViewModelContentDisplayItem(Core.Models.Content.ContentDisplayItem coreItem)
    {
        return new ContentDisplayItem
        {
            ManifestId = ManifestId.Create(coreItem.ManifestId),
            DisplayName = coreItem.DisplayName,
            ContentType = coreItem.ContentType,
            GameType = coreItem.GameType,
            InstallationType = coreItem.InstallationType,
            Publisher = coreItem.Publisher,
            Version = coreItem.Version,
            SourceId = coreItem.SourceId,
            GameClientId = coreItem.GameClientId,
            IsEnabled = coreItem.IsEnabled,
        };
    }

    /// <summary>
    /// Converts a collection of Core ContentDisplayItems to ViewModel ContentDisplayItems.
    /// </summary>
    /// <param name="coreItems">The core content display items.</param>
    /// <returns>An observable collection of ViewModel content display items.</returns>
    private ObservableCollection<ContentDisplayItem> ConvertToViewModelContentDisplayItems(
        IEnumerable<Core.Models.Content.ContentDisplayItem> coreItems)
        => new(coreItems.Select(ConvertToViewModelContentDisplayItem));
}
