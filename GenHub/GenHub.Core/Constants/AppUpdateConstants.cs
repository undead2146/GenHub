namespace GenHub.Core.Constants;

/// <summary>
/// Constants related to application updates and Velopack.
/// </summary>
public static class AppUpdateConstants
{
    /// <summary>
    /// Maximum number of HTTP retries for failed requests.
    /// </summary>
    public const int MaxHttpRetries = 3;

    /// <summary>
    /// Delay before exit after applying update (5 seconds).
    /// </summary>
    public static readonly TimeSpan PostUpdateExitDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cache duration for update checks (1 hour).
    /// </summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
}