namespace GenHub.Core.Constants;

/// <summary>
/// Constants for GitHub Topics-based content discovery.
/// Used to discover community-contributed content via GitHub topic tags.
/// </summary>
public static class GitHubTopicsConstants
{
    // ===== Publisher Information =====

    /// <summary>Publisher type identifier for GitHub Topics discovery.</summary>
    public const string PublisherType = "github";

    /// <summary>Display name for the GitHub publisher.</summary>
    public const string PublisherName = "GitHub";

    /// <summary>Icon color for the GitHub publisher card.</summary>
    public const string IconColor = "#6e5494";

    /// <summary>Description for the GitHub publisher.</summary>
    public const string ProviderDescription = "Community-contributed content from GitHub repositories tagged with GenHub topics";

    // ===== Discovery Topics =====

    /// <summary>Primary topic for GenHub content discovery.</summary>
    public const string GenHubTopic = "genhub";

    /// <summary>Topic for Generals Online specific content.</summary>
    public const string GeneralsOnlineTopic = "generalsonline";

    /// <summary>Topic for C&amp;C Generals mods.</summary>
    public const string GeneralsModTopic = "cnc-generals-mod";

    /// <summary>Topic for Zero Hour mods.</summary>
    public const string ZeroHourModTopic = "zerohour-mod";

    /// <summary>Topic for C&amp;C content in general.</summary>
    public const string CncTopic = "command-and-conquer";

    // ===== Content Type Detection Topics =====

    /// <summary>Topic indicating a game client/launcher.</summary>
    public const string GameClientTopic = "game-client";

    /// <summary>Topic indicating a mod.</summary>
    public const string ModTopic = "mod";

    /// <summary>Topic indicating a map pack.</summary>
    public const string MapPackTopic = "map-pack";

    /// <summary>Topic indicating an addon/tool.</summary>
    public const string AddonTopic = "addon";

    /// <summary>Topic indicating a patch.</summary>
    public const string PatchTopic = "patch";

    /// <summary>Topic indicating a language pack.</summary>
    public const string LanguagePackTopic = "language-pack";

    /// <summary>Topic indicating a single mission.</summary>
    public const string MissionTopic = "mission";

    /// <summary>Topic indicating a single map.</summary>
    public const string MapTopic = "map";

    // ===== API Constants =====

    /// <summary>GitHub Search API base URL.</summary>
    public const string SearchApiBaseUrl = "https://api.github.com/search/repositories";

    /// <summary>Default number of results per page.</summary>
    public const int DefaultPerPage = 30;

    /// <summary>Maximum results per page (GitHub API limit).</summary>
    public const int MaxPerPage = 100;

    /// <summary>Cache duration for topic search results in minutes.</summary>
    public const int CacheDurationMinutes = 30;

    // ===== Discoverer Constants =====

    /// <summary>Source name for the GitHub Topics discoverer.</summary>
    public const string DiscovererSourceName = "GitHub Topics";

    /// <summary>Description for the GitHub Topics discoverer.</summary>
    public const string DiscovererDescription = "Discovers community content from GitHub repositories by topic tags";

    /// <summary>Resolver ID for GitHub Topics resolver.</summary>
    public const string ResolverId = "GitHubTopics";

    // ===== Metadata Keys =====

    /// <summary>Metadata key for repository topics.</summary>
    public const string TopicsMetadataKey = "topics";

    /// <summary>Metadata key for star count.</summary>
    public const string StarCountMetadataKey = "stars";

    /// <summary>Metadata key for fork count.</summary>
    public const string ForkCountMetadataKey = "forks";

    /// <summary>Metadata key for repository language.</summary>
    public const string LanguageMetadataKey = "language";

    /// <summary>Metadata key for discovery source topic.</summary>
    public const string SourceTopicMetadataKey = "source_topic";
}
