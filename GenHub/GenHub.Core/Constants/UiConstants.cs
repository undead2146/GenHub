namespace GenHub.Core.Constants;

/// <summary>
/// UI-related constants for consistent user experience.
/// </summary>
public static class UiConstants
{
    /// <summary>
    /// Default main window width in pixels.
    /// </summary>
    public const double DefaultWindowWidth = 1200;

    /// <summary>
    /// Default main window height in pixels.
    /// </summary>
    public const double DefaultWindowHeight = 800;

    // Status colors

    /// <summary>
    /// Color used to indicate success or positive status.
    /// </summary>
    public const string StatusSuccessColor = "#4CAF50";

    /// <summary>
    /// Color used to indicate error or negative status.
    /// </summary>
    public const string StatusErrorColor = "#F44336";

    // Error Messages

    /// <summary>
    /// Generic error message for failed service status loading.
    /// </summary>
    public const string FailedToLoadServiceStatus = "Failed to load service status. Please try again.";

    /// <summary>
    /// Generic error message for failed player data loading.
    /// </summary>
    public const string FailedToLoadPlayerData = "Failed to load player data. Please try again.";

    /// <summary>
    /// Generic error message for failed leaderboard loading.
    /// </summary>
    public const string FailedToLoadLeaderboard = "Failed to load leaderboard data. Please try again.";

    /// <summary>
    /// Generic error message for failed match history loading.
    /// </summary>
    public const string FailedToLoadMatchHistory = "Failed to load match history. Please try again.";
}