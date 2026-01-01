namespace GenHub.Core.Models.GameSettings;

/// <summary>GeneralsOnline game client settings (inherits TheSuperHackers settings plus GeneralsOnline-specific options).</summary>
public class GeneralsOnlineSettings : TheSuperHackersSettings
{
    /// <summary>Gets or sets a value indicating whether to show FPS counter.</summary>
    public bool ShowFps { get; set; }

    /// <summary>Gets or sets a value indicating whether to show ping/latency.</summary>
    public bool ShowPing { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to enable auto-login.</summary>
    public bool AutoLogin { get; set; }

    /// <summary>Gets or sets a value indicating whether to remember username.</summary>
    public bool RememberUsername { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to enable notifications.</summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>Gets or sets the chat font size.</summary>
    public int ChatFontSize { get; set; } = 12;

    /// <summary>Gets or sets a value indicating whether to enable sound notifications.</summary>
    public bool EnableSoundNotifications { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show player ranks.</summary>
    public bool ShowPlayerRanks { get; set; } = true;

    /// <summary>Gets or sets the maximum camera height only when lobby host.</summary>
    public float CameraMaxHeightOnlyWhenLobbyHost { get; set; } = 310.0f;

    /// <summary>Gets or sets the minimum camera height.</summary>
    public float CameraMinHeight { get; set; } = 310.0f;

    /// <summary>Gets or sets the camera move speed ratio.</summary>
    public float CameraMoveSpeedRatio { get; set; } = 1.5f;

    /// <summary>Gets or sets the chat duration in seconds until fade out.</summary>
    public int ChatDurationSecondsUntilFadeOut { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether debug verbose logging is enabled.</summary>
    public bool DebugVerboseLogging { get; set; }

    /// <summary>Gets or sets the render FPS limit.</summary>
    public int RenderFpsLimit { get; set; } = 144;

    /// <summary>Gets or sets a value indicating whether to limit framerate.</summary>
    public bool RenderLimitFramerate { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to render stats overlay.</summary>
    public bool RenderStatsOverlay { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when friend comes online in gameplay.</summary>
    public bool SocialNotificationFriendComesOnlineGameplay { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when friend comes online in menus.</summary>
    public bool SocialNotificationFriendComesOnlineMenus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when friend goes offline in gameplay.</summary>
    public bool SocialNotificationFriendGoesOfflineGameplay { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when friend goes offline in menus.</summary>
    public bool SocialNotificationFriendGoesOfflineMenus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when player accepts request in gameplay.</summary>
    public bool SocialNotificationPlayerAcceptsRequestGameplay { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when player accepts request in menus.</summary>
    public bool SocialNotificationPlayerAcceptsRequestMenus { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when player sends request in gameplay.</summary>
    public bool SocialNotificationPlayerSendsRequestGameplay { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to show notification when player sends request in menus.</summary>
    public bool SocialNotificationPlayerSendsRequestMenus { get; set; } = true;
}
