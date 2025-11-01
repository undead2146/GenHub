namespace GenHub.Core.Constants;

/// <summary>
/// Constants specific to Generals Online content provider and multiplayer service.
/// </summary>
public static class GeneralsOnlineConstants
{
    // ===== API Endpoints =====

    /// <summary>Base URL for Generals Online CDN.</summary>
    public const string CdnBaseUrl = "https://cdn.playgenerals.online";

    /// <summary>API endpoint for JSON manifest with full release information.</summary>
    public const string ManifestApiUrl = "https://cdn.playgenerals.online/manifest.json";

    /// <summary>Endpoint for latest version information (plain text version string).</summary>
    public const string LatestVersionUrl = "https://cdn.playgenerals.online/latest.txt";

    /// <summary>Base URL for release downloads.</summary>
    public const string ReleasesUrl = "https://cdn.playgenerals.online/releases";

    // ===== Web URLs =====

    /// <summary>Official Generals Online website.</summary>
    public const string WebsiteUrl = "https://www.playgenerals.online/";

    /// <summary>Download page URL.</summary>
    public const string DownloadPageUrl = "https://www.playgenerals.online/#download";

    /// <summary>Support/discord URL.</summary>
    public const string SupportUrl = "https://discord.playgenerals.online/";

    // ===== Content Metadata =====

    /// <summary>Publisher name for manifests.</summary>
    public const string PublisherName = "Generals Online Team";

    /// <summary>Content name for manifests.</summary>
    public const string ContentName = "Generals Online";

    /// <summary>Full content description.</summary>
    public const string Description = "Community-driven multiplayer service for C&C Generals Zero Hour. Features 60Hz tick rate, automatic updates, encrypted traffic, and improved stability.";

    /// <summary>Short content description.</summary>
    public const string ShortDescription = "Community-driven multiplayer service for C&C Generals Zero Hour";

    /// <summary>Content icon URL.</summary>
    public const string IconUrl = "https://www.playgenerals.online/logo.png";

    // ===== Version Parsing =====

    /// <summary>Format for parsing version dates (DDMMYY).</summary>
    public const string VersionDateFormat = "ddMMyy";

    /// <summary>Separator between date and QFE number in versions.</summary>
    public const string QfeSeparator = "_QFE";

    // ===== File Extensions =====

    /// <summary>File extension for portable downloads.</summary>
    public const string PortableExtension = ".zip";

    // ===== Default File Sizes =====

    /// <summary>
    /// Default portable ZIP file size (38MB) - placeholder until actual size is known.
    /// </summary>
    public const long DefaultPortableSize = 38_000_000;

    // ===== Update Intervals =====

    /// <summary>Hours between update checks.</summary>
    public const int UpdateCheckIntervalHours = 24;

    // ===== Manifest Generation =====

    /// <summary>Publisher type identifier for GeneralsOnline.</summary>
    public const string PublisherType = "generalsonline";

    /// <summary>Content type for GeneralsOnline game clients.</summary>
    public const string ContentType = "gameclient";

    /// <summary>Manifest name suffix for 30Hz variant.</summary>
    public const string Variant30HzSuffix = "30hz";

    /// <summary>Manifest name suffix for 60Hz variant.</summary>
    public const string Variant60HzSuffix = "60hz";

    // ===== Content Tags =====

    /// <summary>Content tags for search and categorization.</summary>
    public static readonly string[] Tags = new[] { "multiplayer", "online", "community", "enhancement" };
}
