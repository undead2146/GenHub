namespace GenHub.Core.Constants;

/// <summary>
/// URI scheme constants for handling different types of URIs and paths.
/// </summary>
public static class UriConstants
{
    /// <summary>
    /// URI scheme for Avalonia embedded resources.
    /// </summary>
    public const string AvarUriScheme = "avares://";

    /// <summary>
    /// HTTP URI scheme.
    /// </summary>
    public const string HttpUriScheme = "http://";

    /// <summary>
    /// HTTPS URI scheme.
    /// </summary>
    public const string HttpsUriScheme = "https://";

    /// <summary>
    /// Icon URI for Generals game type.
    /// </summary>
    public const string GeneralsIconUri = "avares://GenHub/Assets/Icons/generals-icon.png";

    /// <summary>
    /// Icon URI for Zero Hour game type.
    /// </summary>
    public const string ZeroHourIconUri = "avares://GenHub/Assets/Icons/zerohour-icon.png";

    /// <summary>
    /// Default icon URI for unknown game types.
    /// </summary>
    public const string DefaultIconUri = "avares://GenHub/Assets/Icons/generalshub-icon.png";

    // Icon Path Constants

    /// <summary>
    /// Base path for icon assets.
    /// </summary>
    public const string IconsBasePath = "Assets/Icons";

    /// <summary>
    /// Filename for Generals icon.
    /// </summary>
    public const string GeneralsIconFilename = "generals-icon.png";

    /// <summary>
    /// Filename for Zero Hour icon.
    /// </summary>
    public const string ZeroHourIconFilename = "zerohour-icon.png";

    /// <summary>
    /// Filename for GenHub default icon.
    /// </summary>
    public const string GenHubIconFilename = "generalshub-icon.png";

    /// <summary>
    /// Filename for Steam platform icon.
    /// </summary>
    public const string SteamIconFilename = "steam-icon.png";

    /// <summary>
    /// Filename for EA App platform icon.
    /// </summary>
    public const string EaAppIconFilename = "eaapp-icon.png";
}
