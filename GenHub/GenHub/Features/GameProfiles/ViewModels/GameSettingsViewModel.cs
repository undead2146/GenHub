using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Constants;
using GenHub.Core.Extensions;
using GenHub.Core.Interfaces.GameSettings;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameSettings;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for the Game Settings tab in Profile Settings.
/// Manages Options.ini for Generals and Zero Hour.
/// </summary>
public partial class GameSettingsViewModel(IGameSettingsService gameSettingsService, ILogger<GameSettingsViewModel> logger) : ViewModelBase
{
    private const TextureQuality MaxTextureQuality = TextureQuality.High; // Will be VeryHigh when SH version supports 'very high' texture quality (see TheSuperHackers/GeneralsGameCode#1629)
    private const int TextureReductionOffset = GameSettingsConstants.TextureQuality.ReductionOffset;

    // Resolution validation constants
    private const int MinResolutionWidth = GameSettingsConstants.Resolution.MinWidth;
    private const int MaxResolutionWidth = GameSettingsConstants.Resolution.MaxWidth; // Supports up to 8K resolution; can be adjusted for larger displays in the future
    private const int MinResolutionHeight = GameSettingsConstants.Resolution.MinHeight;
    private const int MaxResolutionHeight = GameSettingsConstants.Resolution.MaxHeight;

    // Volume validation constants
    private const int MinVolume = GameSettingsConstants.Volume.Min;
    private const int MaxVolume = GameSettingsConstants.Volume.Max;

    // NumSounds validation constants
    private const int MinNumSounds = GameSettingsConstants.Audio.MinNumSounds;
    private const int MaxNumSounds = GameSettingsConstants.Audio.MaxNumSounds;

    private static bool TryParseResolution(string? preset, out int width, out int height)
    {
        width = height = 0;
        if (string.IsNullOrWhiteSpace(preset)) return false;

        var parts = preset.Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        if (!int.TryParse(parts[0], out width) || width < MinResolutionWidth || width > MaxResolutionWidth)
            return false;

        if (!int.TryParse(parts[1], out height) || height < MinResolutionHeight || height > MaxResolutionHeight)
            return false;

        return true;
    }

    private readonly IGameSettingsService? _gameSettingsService = gameSettingsService;
    private readonly ILogger<GameSettingsViewModel> _logger = logger;

    [ObservableProperty]
    private GameType _selectedGameType;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _optionsFileExists;

    [ObservableProperty]
    private string _optionsFilePath = string.Empty;

    // Audio Settings
    [ObservableProperty]
    private int _soundVolume = 70;

    [ObservableProperty]
    private int _threeDSoundVolume = 70;

    [ObservableProperty]
    private int _speechVolume = 70;

    [ObservableProperty]
    private int _musicVolume = 70;

    [ObservableProperty]
    private bool _audioEnabled = true;

    [ObservableProperty]
    private int _numSounds = 16;

    // Video Settings
    [ObservableProperty]
    private int _resolutionWidth = 800;

    [ObservableProperty]
    private int _resolutionHeight = 600;

    [ObservableProperty]
    private bool _windowed;

    [ObservableProperty]
    private TextureQuality _textureQuality = TextureQuality.High;

    [ObservableProperty]
    private bool _shadows = true;

    [ObservableProperty]
    private bool _particleEffects = true;

    [ObservableProperty]
    private bool _extraAnimations = true;

    [ObservableProperty]
    private bool _buildingAnimations = true;

    [ObservableProperty]
    private int _gamma = 50;

    [ObservableProperty]
    private string _colorValue = "#8E44AD";

    [ObservableProperty]
    private ObservableCollection<string> _resolutionPresets = new(ResolutionPresetsProvider.StandardResolutions);

    [ObservableProperty]
    private string? _selectedResolutionPreset;

    // ===== TheSuperHackers Client Settings =====
    [ObservableProperty]
    private bool _tshArchiveReplays;

    [ObservableProperty]
    private bool _tshShowMoneyPerMinute;

    [ObservableProperty]
    private bool _tshPlayerObserverEnabled;

    [ObservableProperty]
    private int _tshSystemTimeFontSize = 8;

    [ObservableProperty]
    private int _tshNetworkLatencyFontSize = 8;

    [ObservableProperty]
    private int _tshRenderFpsFontSize;

    [ObservableProperty]
    private int _tshResolutionFontAdjustment;

    [ObservableProperty]
    private bool _tshCursorCaptureEnabledInFullscreenGame;

    [ObservableProperty]
    private bool _tshCursorCaptureEnabledInFullscreenMenu;

    [ObservableProperty]
    private bool _tshCursorCaptureEnabledInWindowedGame;

    [ObservableProperty]
    private bool _tshCursorCaptureEnabledInWindowedMenu;

    [ObservableProperty]
    private bool _tshScreenEdgeScrollEnabledInFullscreenApp;

    [ObservableProperty]
    private bool _tshScreenEdgeScrollEnabledInWindowedApp;

    [ObservableProperty]
    private int _tshMoneyTransactionVolume = 50;

    // ===== GeneralsOnline Client Settings =====
    [ObservableProperty]
    private bool _goShowFps;

    [ObservableProperty]
    private bool _goShowPing;

    [ObservableProperty]
    private bool _goShowPlayerRanks;

    [ObservableProperty]
    private bool _goAutoLogin;

    [ObservableProperty]
    private bool _goRememberUsername;

    [ObservableProperty]
    private bool _goEnableNotifications;

    [ObservableProperty]
    private bool _goEnableSoundNotifications;

    [ObservableProperty]
    private int _goChatFontSize = 12;

    // Camera settings
    [ObservableProperty]
    private float _goCameraMaxHeightOnlyWhenLobbyHost = 310.0f;

    [ObservableProperty]
    private float _goCameraMinHeight = 310.0f;

    [ObservableProperty]
    private float _goCameraMoveSpeedRatio = 1.5f;

    // Chat settings
    [ObservableProperty]
    private int _goChatDurationSecondsUntilFadeOut = 30;

    // Debug settings
    [ObservableProperty]
    private bool _goDebugVerboseLogging;

    // Render settings
    [ObservableProperty]
    private int _goRenderFpsLimit = 144;

    [ObservableProperty]
    private bool _goRenderLimitFramerate = true;

    [ObservableProperty]
    private bool _goRenderStatsOverlay = true;

    // Social notification settings
    [ObservableProperty]
    private bool _goSocialNotificationFriendComesOnlineGameplay = true;

    [ObservableProperty]
    private bool _goSocialNotificationFriendComesOnlineMenus = true;

    [ObservableProperty]
    private bool _goSocialNotificationFriendGoesOfflineGameplay = true;

    [ObservableProperty]
    private bool _goSocialNotificationFriendGoesOfflineMenus = true;

    [ObservableProperty]
    private bool _goSocialNotificationPlayerAcceptsRequestGameplay = true;

    [ObservableProperty]
    private bool _goSocialNotificationPlayerAcceptsRequestMenus = true;

    [ObservableProperty]
    private bool _goSocialNotificationPlayerSendsRequestGameplay = true;

    [ObservableProperty]
    private bool _goSocialNotificationPlayerSendsRequestMenus = true;

    [ObservableProperty]
    private string? _gameSpyIPAddress;

    /// <summary>
    /// Initializes the ViewModel and loads settings for a specific profile.
    /// </summary>
    /// <param name="profileId">The profile ID to load settings for.</param>
    /// <param name="profile">The game profile with settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeForProfileAsync(string? profileId, Core.Models.GameProfile.GameProfile? profile = null)
    {
        _initializationDepth++;
        IsLoading = true;  // Provide UI feedback for loading state

        try
        {
            _currentProfileId = profileId;

            // Auto-select game type from profile
            if (profile != null)
            {
                SelectedGameType = profile.GameClient.GameType;
                _logger.LogInformation(
                    "Auto-selected game type {GameType} for profile {ProfileId}",
                    SelectedGameType,
                    profileId);
            }
            else
            {
                // Default to Generals for new profiles
                SelectedGameType = GameType.Generals;
                _logger.LogInformation("Defaulted to Generals for new profile");
            }

            // If profile has settings, load them
            if (profile != null && profile.HasCustomSettings())
            {
                LoadSettingsFromProfile(profile);
            }
            else
            {
                // For new profiles or profiles without settings, load from Options.ini as defaults
                _isLoadingFromOptions = true;
                await LoadSettingsCommand.ExecuteAsync(null);
                _isLoadingFromOptions = false;
                StatusMessage = "Loaded default settings from Options.ini. Save the profile to persist these settings.";
            }
        }
        finally
        {
            _initializationDepth--;
            IsLoading = false;  // Clear UI loading feedback
        }
    }

    /// <summary>
    /// Gets the current settings as an UpdateProfileRequest for saving to a profile.
    /// </summary>
    /// <returns>An UpdateProfileRequest with the current settings.</returns>
    public Core.Models.GameProfile.UpdateProfileRequest GetProfileSettings()
    {
        return new Core.Models.GameProfile.UpdateProfileRequest
        {
            VideoResolutionWidth = ResolutionWidth,
            VideoResolutionHeight = ResolutionHeight,
            VideoWindowed = Windowed,
            VideoTextureQuality = TextureQuality,
            EnableVideoShadows = Shadows,
            VideoParticleEffects = ParticleEffects,
            VideoExtraAnimations = ExtraAnimations,
            VideoBuildingAnimations = BuildingAnimations,
            VideoGamma = Gamma,
            AudioSoundVolume = SoundVolume,
            AudioThreeDSoundVolume = ThreeDSoundVolume,
            AudioSpeechVolume = SpeechVolume,
            AudioMusicVolume = MusicVolume,
            AudioEnabled = AudioEnabled,
            AudioNumSounds = NumSounds,

            // TheSuperHackers settings
            TshArchiveReplays = TshArchiveReplays,
            TshShowMoneyPerMinute = TshShowMoneyPerMinute,
            TshPlayerObserverEnabled = TshPlayerObserverEnabled,
            TshSystemTimeFontSize = TshSystemTimeFontSize,
            TshNetworkLatencyFontSize = TshNetworkLatencyFontSize,
            TshRenderFpsFontSize = TshRenderFpsFontSize,
            TshResolutionFontAdjustment = TshResolutionFontAdjustment,
            TshCursorCaptureEnabledInFullscreenGame = TshCursorCaptureEnabledInFullscreenGame,
            TshCursorCaptureEnabledInFullscreenMenu = TshCursorCaptureEnabledInFullscreenMenu,
            TshCursorCaptureEnabledInWindowedGame = TshCursorCaptureEnabledInWindowedGame,
            TshCursorCaptureEnabledInWindowedMenu = TshCursorCaptureEnabledInWindowedMenu,
            TshScreenEdgeScrollEnabledInFullscreenApp = TshScreenEdgeScrollEnabledInFullscreenApp,
            TshScreenEdgeScrollEnabledInWindowedApp = TshScreenEdgeScrollEnabledInWindowedApp,
            TshMoneyTransactionVolume = TshMoneyTransactionVolume,

            // GeneralsOnline settings
            GoShowFps = GoShowFps,
            GoShowPing = GoShowPing,
            GoShowPlayerRanks = GoShowPlayerRanks,
            GoAutoLogin = GoAutoLogin,
            GoRememberUsername = GoRememberUsername,
            GoEnableNotifications = GoEnableNotifications,
            GoEnableSoundNotifications = GoEnableSoundNotifications,
            GoChatFontSize = GoChatFontSize,

            // Camera settings
            GoCameraMaxHeightOnlyWhenLobbyHost = GoCameraMaxHeightOnlyWhenLobbyHost,
            GoCameraMinHeight = GoCameraMinHeight,
            GoCameraMoveSpeedRatio = GoCameraMoveSpeedRatio,

            // Chat settings
            GoChatDurationSecondsUntilFadeOut = GoChatDurationSecondsUntilFadeOut,

            // Debug settings
            GoDebugVerboseLogging = GoDebugVerboseLogging,

            // Render settings
            GoRenderFpsLimit = GoRenderFpsLimit,
            GoRenderLimitFramerate = GoRenderLimitFramerate,
            GoRenderStatsOverlay = GoRenderStatsOverlay,

            // Social notification settings
            GoSocialNotificationFriendComesOnlineGameplay = GoSocialNotificationFriendComesOnlineGameplay,
            GoSocialNotificationFriendComesOnlineMenus = GoSocialNotificationFriendComesOnlineMenus,
            GoSocialNotificationFriendGoesOfflineGameplay = GoSocialNotificationFriendGoesOfflineGameplay,
            GoSocialNotificationFriendGoesOfflineMenus = GoSocialNotificationFriendGoesOfflineMenus,
            GoSocialNotificationPlayerAcceptsRequestGameplay = GoSocialNotificationPlayerAcceptsRequestGameplay,
            GoSocialNotificationPlayerAcceptsRequestMenus = GoSocialNotificationPlayerAcceptsRequestMenus,
            GoSocialNotificationPlayerSendsRequestGameplay = GoSocialNotificationPlayerSendsRequestGameplay,
            GoSocialNotificationPlayerSendsRequestMenus = GoSocialNotificationPlayerSendsRequestMenus,
            GameSpyIPAddress = GameSpyIPAddress,
        };
    }

    /// <summary>
    /// Applies a resolution preset.
    /// </summary>
    /// <param name="preset">The resolution preset to apply.</param>
    [RelayCommand]
    public void ApplyResolutionPreset(string? preset)
    {
        if (!TryParseResolution(preset, out var width, out var height))
        {
            StatusMessage = $"Invalid resolution preset: {preset}";
            _logger.LogWarning("Failed to parse resolution preset: {Preset}", preset);
            return;
        }

        ResolutionWidth = width;
        ResolutionHeight = height;
        StatusMessage = $"Resolution set to {width}x{height}";
    }

    private IniOptions? _currentOptions;
    private string? _currentProfileId;
    private int _initializationDepth;
    private bool _isLoadingFromOptions;

    /// <summary>
    /// Loads the options.ini settings for the selected game type.
    /// </summary>
    [RelayCommand]
    private async Task LoadSettings()
    {
        if (_gameSettingsService == null)
        {
            StatusMessage = "Game settings service not available";
            return;
        }

        GameType gameType = SelectedGameType;
        try
        {
            IsLoading = true;

            StatusMessage = $"Loading {gameType} settings...";

            OptionsFilePath = _gameSettingsService.GetOptionsFilePath(gameType);
            OptionsFileExists = _gameSettingsService.OptionsFileExists(gameType);

            var result = await _gameSettingsService.LoadOptionsAsync(gameType);

            if (result.Success && result.Data != null)
            {
                _currentOptions = result.Data;
                ApplyOptionsToViewModel(_currentOptions);

                StatusMessage = OptionsFileExists
                    ? $"Loaded {gameType} settings from {Path.GetFileName(OptionsFilePath)}"
                    : $"Using default {gameType} settings (file not found)";

                _logger.LogInformation("Loaded settings for {GameType}", gameType);
            }
            else
            {
                StatusMessage = $"Failed to load settings: {string.Join(", ", result.Errors)}";
                _logger.LogWarning("Failed to load settings for {GameType}: {Errors}", gameType, string.Join(", ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings for {GameType}", gameType);
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads settings from a game profile.
    /// </summary>
    /// <param name="profile">The game profile.</param>
    private void LoadSettingsFromProfile(Core.Models.GameProfile.GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _logger.LogInformation("Loading settings from profile {ProfileId}", _currentProfileId);

        if (profile.VideoResolutionWidth.HasValue) ResolutionWidth = profile.VideoResolutionWidth.Value;
        if (profile.VideoResolutionHeight.HasValue) ResolutionHeight = profile.VideoResolutionHeight.Value;
        if (profile.VideoWindowed.HasValue) Windowed = profile.VideoWindowed.Value;
        if (profile.VideoTextureQuality.HasValue) TextureQuality = profile.VideoTextureQuality.Value;
        if (profile.EnableVideoShadows.HasValue) Shadows = profile.EnableVideoShadows.Value;
        if (profile.VideoParticleEffects.HasValue) ParticleEffects = profile.VideoParticleEffects.Value;
        if (profile.VideoExtraAnimations.HasValue) ExtraAnimations = profile.VideoExtraAnimations.Value;
        if (profile.VideoBuildingAnimations.HasValue) BuildingAnimations = profile.VideoBuildingAnimations.Value;
        if (profile.VideoGamma.HasValue) Gamma = profile.VideoGamma.Value;

        if (profile.AudioSoundVolume.HasValue) SoundVolume = profile.AudioSoundVolume.Value;
        if (profile.AudioThreeDSoundVolume.HasValue) ThreeDSoundVolume = profile.AudioThreeDSoundVolume.Value;
        if (profile.AudioSpeechVolume.HasValue) SpeechVolume = profile.AudioSpeechVolume.Value;
        if (profile.AudioMusicVolume.HasValue) MusicVolume = profile.AudioMusicVolume.Value;
        if (profile.AudioEnabled.HasValue) AudioEnabled = profile.AudioEnabled.Value;
        if (profile.AudioNumSounds.HasValue) NumSounds = profile.AudioNumSounds.Value;

        // TheSuperHackers settings
        if (profile.TshArchiveReplays.HasValue) TshArchiveReplays = profile.TshArchiveReplays.Value;
        if (profile.TshShowMoneyPerMinute.HasValue) TshShowMoneyPerMinute = profile.TshShowMoneyPerMinute.Value;
        if (profile.TshPlayerObserverEnabled.HasValue) TshPlayerObserverEnabled = profile.TshPlayerObserverEnabled.Value;
        if (profile.TshSystemTimeFontSize.HasValue) TshSystemTimeFontSize = profile.TshSystemTimeFontSize.Value;
        if (profile.TshNetworkLatencyFontSize.HasValue) TshNetworkLatencyFontSize = profile.TshNetworkLatencyFontSize.Value;
        if (profile.TshRenderFpsFontSize.HasValue) TshRenderFpsFontSize = profile.TshRenderFpsFontSize.Value;
        if (profile.TshResolutionFontAdjustment.HasValue) TshResolutionFontAdjustment = profile.TshResolutionFontAdjustment.Value;
        if (profile.TshCursorCaptureEnabledInFullscreenGame.HasValue) TshCursorCaptureEnabledInFullscreenGame = profile.TshCursorCaptureEnabledInFullscreenGame.Value;
        if (profile.TshCursorCaptureEnabledInFullscreenMenu.HasValue) TshCursorCaptureEnabledInFullscreenMenu = profile.TshCursorCaptureEnabledInFullscreenMenu.Value;
        if (profile.TshCursorCaptureEnabledInWindowedGame.HasValue) TshCursorCaptureEnabledInWindowedGame = profile.TshCursorCaptureEnabledInWindowedGame.Value;
        if (profile.TshCursorCaptureEnabledInWindowedMenu.HasValue) TshCursorCaptureEnabledInWindowedMenu = profile.TshCursorCaptureEnabledInWindowedMenu.Value;
        if (profile.TshScreenEdgeScrollEnabledInFullscreenApp.HasValue) TshScreenEdgeScrollEnabledInFullscreenApp = profile.TshScreenEdgeScrollEnabledInFullscreenApp.Value;
        if (profile.TshScreenEdgeScrollEnabledInWindowedApp.HasValue) TshScreenEdgeScrollEnabledInWindowedApp = profile.TshScreenEdgeScrollEnabledInWindowedApp.Value;
        if (profile.TshMoneyTransactionVolume.HasValue) TshMoneyTransactionVolume = profile.TshMoneyTransactionVolume.Value;

        // GeneralsOnline settings
        if (profile.GoShowFps.HasValue) GoShowFps = profile.GoShowFps.Value;
        if (profile.GoShowPing.HasValue) GoShowPing = profile.GoShowPing.Value;
        if (profile.GoShowPlayerRanks.HasValue) GoShowPlayerRanks = profile.GoShowPlayerRanks.Value;
        if (profile.GoAutoLogin.HasValue) GoAutoLogin = profile.GoAutoLogin.Value;
        if (profile.GoRememberUsername.HasValue) GoRememberUsername = profile.GoRememberUsername.Value;
        if (profile.GoEnableNotifications.HasValue) GoEnableNotifications = profile.GoEnableNotifications.Value;
        if (profile.GoEnableSoundNotifications.HasValue) GoEnableSoundNotifications = profile.GoEnableSoundNotifications.Value;
        if (profile.GoChatFontSize.HasValue) GoChatFontSize = profile.GoChatFontSize.Value;

        // Camera settings
        if (profile.GoCameraMaxHeightOnlyWhenLobbyHost.HasValue) GoCameraMaxHeightOnlyWhenLobbyHost = profile.GoCameraMaxHeightOnlyWhenLobbyHost.Value;
        if (profile.GoCameraMinHeight.HasValue) GoCameraMinHeight = profile.GoCameraMinHeight.Value;
        if (profile.GoCameraMoveSpeedRatio.HasValue) GoCameraMoveSpeedRatio = profile.GoCameraMoveSpeedRatio.Value;

        // Chat settings
        if (profile.GoChatDurationSecondsUntilFadeOut.HasValue) GoChatDurationSecondsUntilFadeOut = profile.GoChatDurationSecondsUntilFadeOut.Value;

        // Debug settings
        if (profile.GoDebugVerboseLogging.HasValue) GoDebugVerboseLogging = profile.GoDebugVerboseLogging.Value;

        // Render settings
        if (profile.GoRenderFpsLimit.HasValue) GoRenderFpsLimit = profile.GoRenderFpsLimit.Value;
        if (profile.GoRenderLimitFramerate.HasValue) GoRenderLimitFramerate = profile.GoRenderLimitFramerate.Value;
        if (profile.GoRenderStatsOverlay.HasValue) GoRenderStatsOverlay = profile.GoRenderStatsOverlay.Value;

        // Social notification settings
        if (profile.GoSocialNotificationFriendComesOnlineGameplay.HasValue) GoSocialNotificationFriendComesOnlineGameplay = profile.GoSocialNotificationFriendComesOnlineGameplay.Value;
        if (profile.GoSocialNotificationFriendComesOnlineMenus.HasValue) GoSocialNotificationFriendComesOnlineMenus = profile.GoSocialNotificationFriendComesOnlineMenus.Value;
        if (profile.GoSocialNotificationFriendGoesOfflineGameplay.HasValue) GoSocialNotificationFriendGoesOfflineGameplay = profile.GoSocialNotificationFriendGoesOfflineGameplay.Value;
        if (profile.GoSocialNotificationFriendGoesOfflineMenus.HasValue) GoSocialNotificationFriendGoesOfflineMenus = profile.GoSocialNotificationFriendGoesOfflineMenus.Value;
        if (profile.GoSocialNotificationPlayerAcceptsRequestGameplay.HasValue) GoSocialNotificationPlayerAcceptsRequestGameplay = profile.GoSocialNotificationPlayerAcceptsRequestGameplay.Value;
        if (profile.GoSocialNotificationPlayerAcceptsRequestMenus.HasValue) GoSocialNotificationPlayerAcceptsRequestMenus = profile.GoSocialNotificationPlayerAcceptsRequestMenus.Value;
        if (profile.GoSocialNotificationPlayerSendsRequestGameplay.HasValue) GoSocialNotificationPlayerSendsRequestGameplay = profile.GoSocialNotificationPlayerSendsRequestGameplay.Value;
        if (profile.GoSocialNotificationPlayerSendsRequestMenus.HasValue) GoSocialNotificationPlayerSendsRequestMenus = profile.GoSocialNotificationPlayerSendsRequestMenus.Value;

        if (profile.GameSpyIPAddress != null) GameSpyIPAddress = profile.GameSpyIPAddress;

        // Update selected preset if it matches
        var currentRes = $"{ResolutionWidth}x{ResolutionHeight}";
        SelectedResolutionPreset = ResolutionPresets.Contains(currentRes) ? currentRes : null;

        StatusMessage = $"Loaded profile settings for {profile.GameClient.GameType}";
        _logger.LogInformation(
            "Loaded profile settings - Windowed={Windowed}, Resolution={Width}x{Height}",
            Windowed,
            ResolutionWidth,
            ResolutionHeight);
    }

    /// <summary>
    /// Saves the current settings to options.ini.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettings()
    {
        if (_gameSettingsService == null)
        {
            StatusMessage = "Game settings service not available";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Saving {SelectedGameType} settings...";

            var options = CreateOptionsFromViewModel();
            var result = await _gameSettingsService.SaveOptionsAsync(SelectedGameType, options);

            if (result.Success)
            {
                _currentOptions = options;
                OptionsFileExists = true;
                StatusMessage = $"{SelectedGameType} settings saved successfully";
                _logger.LogInformation("Saved settings for {GameType}", SelectedGameType);
            }
            else
            {
                StatusMessage = $"Failed to save settings: {string.Join(", ", result.Errors)}";
                _logger.LogWarning("Failed to save settings for {GameType}: {Errors}", SelectedGameType, string.Join(", ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings for {GameType}", SelectedGameType);
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens the Options.ini file location in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenFileLocation()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(OptionsFilePath);
            if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start("explorer.exe", directory);
                _logger.LogInformation("Opened file location {Directory}", directory);
            }
            else
            {
                StatusMessage = "Options file directory not found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file location");
            StatusMessage = $"Error opening location: {ex.Message}";
        }
    }

    partial void OnSelectedResolutionPresetChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            ApplyResolutionPreset(value);
        }
    }

    partial void OnSelectedGameTypeChanged(GameType value)
    {
        if (_initializationDepth == 0 && !_isLoadingFromOptions)
        {
            _logger.LogInformation("GameType changed to {GameType} - loading from Options.ini", value);
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadSettingsCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load settings for {GameType}", value);
                    StatusMessage = $"Error loading settings: {ex.Message}";
                }
            });
        }
        else if (_isLoadingFromOptions)
        {
            _logger.LogInformation("GameType changed to {GameType} while loading from Options.ini - skipping auto-load", value);
        }
        else
        {
            _logger.LogInformation("GameType set to {GameType} during initialization - skipping auto-load", value);
        }
    }

    private void ApplyOptionsToViewModel(IniOptions options)
    {
        // Audio settings - map from Options.ini names to ViewModel friendly names
        SoundVolume = options.Audio.SFXVolume;
        ThreeDSoundVolume = options.Audio.SFX3DVolume;
        SpeechVolume = options.Audio.VoiceVolume;
        MusicVolume = options.Audio.MusicVolume;
        AudioEnabled = options.Audio.AudioEnabled;
        NumSounds = options.Audio.NumSounds;

        // Video settings
        ResolutionWidth = options.Video.ResolutionWidth;
        ResolutionHeight = options.Video.ResolutionHeight;
        Windowed = options.Video.Windowed;

        // Map TextureReduction (0-3, inverted) to TextureQuality
        TextureQuality = (TextureQuality)Math.Clamp(TextureReductionOffset - options.Video.TextureReduction, 0, (int)TextureQuality.High);
        Shadows = options.Video.UseShadowVolumes;

        // ParticleEffects doesn't exist in Options.ini, keep default
        ParticleEffects = true;
        ExtraAnimations = options.Video.ExtraAnimations;

        // BuildingAnimations doesn't exist in Options.ini, keep default
        BuildingAnimations = true;
        Gamma = options.Video.Gamma;

        GameSpyIPAddress = options.Network.GameSpyIPAddress;

        // Update selected preset if it matches
        var currentRes = $"{ResolutionWidth}x{ResolutionHeight}";
        SelectedResolutionPreset = ResolutionPresets.Contains(currentRes) ? currentRes : null;
    }

    private IniOptions CreateOptionsFromViewModel()
    {
        var options = _currentOptions ?? new IniOptions();

        // Audio settings - map from ViewModel friendly names to Options.ini names
        options.Audio.SFXVolume = SoundVolume;
        options.Audio.SFX3DVolume = ThreeDSoundVolume;
        options.Audio.VoiceVolume = SpeechVolume;
        options.Audio.MusicVolume = MusicVolume;
        options.Audio.AudioEnabled = AudioEnabled;
        options.Audio.NumSounds = NumSounds;

        // Video settings
        options.Video.ResolutionWidth = ResolutionWidth;
        options.Video.ResolutionHeight = ResolutionHeight;
        options.Video.Windowed = Windowed;

        // Map TextureQuality to TextureReduction (0-3, inverted)
        options.Video.TextureReduction = TextureReductionOffset - (int)TextureQuality;
        options.Video.UseShadowVolumes = Shadows;
        options.Video.UseShadowDecals = Shadows; // Enable decals when shadows are on

        // ParticleEffects and BuildingAnimations don't exist in Options.ini, skip
        options.Video.ExtraAnimations = ExtraAnimations;
        options.Video.Gamma = Gamma;

        options.Network.GameSpyIPAddress = GameSpyIPAddress;

        return options;
    }
}
