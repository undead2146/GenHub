using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.Services.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services.ContentDiscoverers;

/// <summary>
/// Discovers content from GitHub repositories by searching for specific topics.
/// This enables community-contributed content to be discovered automatically
/// when users tag their repositories with topics like "genhub" or "generalsonline".
/// </summary>
public class GitHubTopicsDiscoverer(
    IGitHubApiClient gitHubApiClient,
    ILogger<GitHubTopicsDiscoverer> logger,
    IMemoryCache cache) : IContentDiscoverer
{
    /// <summary>Maximum number of tags to include in search result.</summary>
    private const int MaxTagsToInclude = 10;

    /// <summary>Rate limit delay between API calls in milliseconds.</summary>
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);

    // Topics to search for, in priority order
    private readonly List<string> _discoveryTopics =
    [
        GitHubTopicsConstants.GenHubTopic,
        GitHubTopicsConstants.GeneralsOnlineTopic,
        GitHubTopicsConstants.GeneralsModTopic,
        GitHubTopicsConstants.ZeroHourModTopic,
    ];

    /// <inheritdoc />
    public string SourceName => GitHubTopicsConstants.DiscovererSourceName;

    /// <inheritdoc />
    public string Description => GitHubTopicsConstants.DiscovererDescription;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public ContentSourceCapabilities Capabilities =>
        ContentSourceCapabilities.RequiresDiscovery |
        ContentSourceCapabilities.SupportsPackageAcquisition;

    /// <inheritdoc />
    public async Task<OperationResult<IEnumerable<ContentSearchResult>>> DiscoverAsync(
        ContentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ContentSearchResult>();
        var processedRepoIds = new HashSet<long>(); // Avoid duplicates across topics

        try
        {
            logger.LogInformation("Starting GitHub Topics discovery for topics: {Topics}", string.Join(", ", _discoveryTopics));

            foreach (var topic in _discoveryTopics)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var searchResponse = await SearchRepositoriesByTopicWithCacheAsync(
                    topic,
                    perPage: GitHubTopicsConstants.DefaultPerPage,
                    page: 1,
                    cancellationToken).ConfigureAwait(false);

                foreach (var repo in searchResponse.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip if already processed (repo might have multiple matching topics)
                    if (!processedRepoIds.Add(repo.Id))
                    {
                        continue;
                    }

                    // Skip archived or disabled repos
                    if (repo.IsArchived || repo.IsDisabled)
                    {
                        logger.LogDebug("Skipping archived/disabled repository: {Repo}", repo.FullName);
                        continue;
                    }

                    // Skip forks (unless they have GenHub topic explicitly)
                    if (repo.IsFork && !repo.Topics.Contains(GitHubTopicsConstants.GenHubTopic, StringComparer.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Skipping fork without genhub topic: {Repo}", repo.FullName);
                        continue;
                    }

                    // Try to get latest release for version info
                    GitHubRelease? latestRelease = null;
                    try
                    {
                        // Apply rate limiting
                        await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            latestRelease = await gitHubApiClient.GetLatestReleaseAsync(
                                repo.Owner.Login,
                                repo.Name,
                                cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            // Add delay before releasing semaphore to maintain rate limit
                            await Task.Delay(RateLimitDelay, cancellationToken).ConfigureAwait(false);
                            _rateLimitSemaphore.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "No releases found for {Repo}, will use repo info", repo.FullName);
                    }

                    var contentResult = CreateSearchResult(repo, latestRelease, topic);

                    // Apply search filters
                    if (MatchesQuery(contentResult, query))
                    {
                        results.Add(contentResult);
                    }
                }
            }

            logger.LogInformation("GitHub Topics discovery found {Count} repositories", results.Count);
            return OperationResult<IEnumerable<ContentSearchResult>>.CreateSuccess(results);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("GitHub Topics discovery was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub Topics discovery failed");
            return OperationResult<IEnumerable<ContentSearchResult>>.CreateFailure($"GitHub Topics discovery failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Infers ContentType from repository topics.
    /// </summary>
    private static (ContentType type, bool isInferred) InferContentTypeFromTopics(List<string> topics)
    {
        // Check for explicit type topics
        if (topics.Contains(GitHubTopicsConstants.GameClientTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.GameClient, false);
        }

        if (topics.Contains(GitHubTopicsConstants.ModTopic, StringComparer.OrdinalIgnoreCase) ||
            topics.Contains(GitHubTopicsConstants.GeneralsModTopic, StringComparer.OrdinalIgnoreCase) ||
            topics.Contains(GitHubTopicsConstants.ZeroHourModTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.Mod, false);
        }

        if (topics.Contains(GitHubTopicsConstants.MapPackTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.MapPack, false);
        }

        if (topics.Contains(GitHubTopicsConstants.AddonTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.Addon, false);
        }

        if (topics.Contains(GitHubTopicsConstants.PatchTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.Patch, false);
        }

        if (topics.Contains(GitHubTopicsConstants.LanguagePackTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.LanguagePack, false);
        }

        if (topics.Contains(GitHubTopicsConstants.MissionTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.Mission, false);
        }

        if (topics.Contains(GitHubTopicsConstants.MapTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (ContentType.Map, false);
        }

        // No explicit type found, will need inference
        return (ContentType.Mod, true);
    }

    /// <summary>
    /// Infers GameType from repository topics.
    /// </summary>
    private static (GameType type, bool isInferred) InferGameTypeFromTopics(List<string> topics)
    {
        // Check for game-specific topics
        if (topics.Contains(GitHubTopicsConstants.ZeroHourModTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (GameType.ZeroHour, false);
        }

        if (topics.Contains(GitHubTopicsConstants.GeneralsModTopic, StringComparer.OrdinalIgnoreCase))
        {
            // Check if also has ZH topic - use exact matching instead of substring matching
            if (topics.Any(t => t.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                               t.Equals("zerohour", StringComparison.OrdinalIgnoreCase) ||
                               t.Equals("zero-hour", StringComparison.OrdinalIgnoreCase)))
            {
                return (GameType.ZeroHour, false);
            }

            return (GameType.Generals, false);
        }

        // Generals Online content is typically for Zero Hour
        if (topics.Contains(GitHubTopicsConstants.GeneralsOnlineTopic, StringComparer.OrdinalIgnoreCase))
        {
            return (GameType.ZeroHour, false);
        }

        // Default to ZeroHour (most common) with inference flag
        return (GameType.ZeroHour, true);
    }

    /// <summary>
    /// Checks if a search result matches the query criteria.
    /// </summary>
    private static bool MatchesQuery(ContentSearchResult result, ContentSearchQuery query)
    {
        // Filter by search term
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm;
            var matchesName = result.Name?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
            var matchesDescription = result.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
            var matchesAuthor = result.AuthorName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
            var matchesTags = result.Tags.Any(t => t.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

            if (!matchesName && !matchesDescription && !matchesAuthor && !matchesTags)
            {
                return false;
            }
        }

        // Filter by content type
        if (query.ContentType.HasValue && result.ContentType != query.ContentType.Value)
        {
            return false;
        }

        // Filter by game type
        if (query.TargetGame.HasValue && result.TargetGame != query.TargetGame.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Searches for repositories by topic with caching to reduce API calls.
    /// </summary>
    private async Task<GitHubRepositorySearchResponse> SearchRepositoriesByTopicWithCacheAsync(
        string topic,
        int perPage,
        int page,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"github_topic_{topic}_{perPage}_{page}";
        var result = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(GitHubTopicsConstants.CacheDurationMinutes);
            return await gitHubApiClient.SearchRepositoriesByTopicAsync(topic, perPage, page, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return result ?? new GitHubRepositorySearchResponse();
    }

    /// <summary>
    /// Creates a ContentSearchResult from a repository and optional release.
    /// </summary>
    private ContentSearchResult CreateSearchResult(
        GitHubRepositorySearchItem repo,
        GitHubRelease? latestRelease,
        string sourceTopic)
    {
        // Infer content type from topics first, then fall back to name-based inference
        var (contentType, isTypeInferred) = InferContentTypeFromTopics(repo.Topics);
        if (isTypeInferred)
        {
            var nameInference = GitHubInferenceHelper.InferContentType(repo.Name, latestRelease?.Name);
            contentType = nameInference.type;
        }

        // Infer game type
        var (gameType, isGameInferred) = InferGameTypeFromTopics(repo.Topics);
        if (isGameInferred)
        {
            var nameInference = GitHubInferenceHelper.InferTargetGame(repo.Name, latestRelease?.Name);
            gameType = nameInference.type;
        }

        // Generate manifest ID
        var version = latestRelease?.TagName ?? "latest";
        var manifestId = ManifestIdGenerator.GenerateGitHubContentId(
            repo.Owner.Login,
            repo.Name,
            contentType,
            version);

        var result = new ContentSearchResult
        {
            Id = manifestId,
            Name = repo.Name,
            Description = repo.Description ?? $"Community content from {repo.Owner.Login}/{repo.Name}",
            Version = version,
            AuthorName = repo.Owner.Login,
            ContentType = contentType,
            TargetGame = gameType,
            IsInferred = isTypeInferred || isGameInferred,
            ProviderName = SourceName,
            RequiresResolution = true,
            ResolverId = GitHubConstants.GitHubReleaseResolverId, // Use existing GitHub resolver
            SourceUrl = repo.HtmlUrl,
            LastUpdated = latestRelease?.PublishedAt?.DateTime ?? repo.UpdatedAt,
            DownloadSize = latestRelease?.Assets.Sum(a => a.Size) ?? 0,
        };

        // Add tags from topics
        foreach (var topic in repo.Topics.Take(MaxTagsToInclude))
        {
            result.Tags.Add(topic);
        }

        // Add resolver metadata
        result.ResolverMetadata[GitHubConstants.OwnerMetadataKey] = repo.Owner.Login;
        result.ResolverMetadata[GitHubConstants.RepoMetadataKey] = repo.Name;
        result.ResolverMetadata[GitHubConstants.TagMetadataKey] = version;
        result.ResolverMetadata[GitHubTopicsConstants.SourceTopicMetadataKey] = sourceTopic;
        result.ResolverMetadata[GitHubTopicsConstants.StarCountMetadataKey] = repo.StargazersCount.ToString();
        result.ResolverMetadata[GitHubTopicsConstants.ForkCountMetadataKey] = repo.ForksCount.ToString();
        if (!string.IsNullOrEmpty(repo.Language))
        {
            result.ResolverMetadata[GitHubTopicsConstants.LanguageMetadataKey] = repo.Language;
        }

        // Store full release data for resolution
        if (latestRelease != null)
        {
            result.SetData(latestRelease);
        }
        else
        {
            result.SetData(repo.ToRepository());
        }

        return result;
    }
}
