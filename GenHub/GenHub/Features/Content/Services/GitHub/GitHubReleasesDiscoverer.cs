using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using GenHub.Features.Content.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services.GitHub;

/// <summary>
/// Discovers content from GitHub releases.
/// Optimized to minimize API calls by loading only the latest release by default.
/// </summary>
public class GitHubReleasesDiscoverer(IGitHubApiClient gitHubClient, ILogger<GitHubReleasesDiscoverer> logger, IConfigurationProviderService configurationProvider) : IContentDiscoverer
{
    /// <inheritdoc />
    public string SourceName => ContentSourceNames.GitHubDiscoverer;

    /// <inheritdoc />
    public string Description => GitHubConstants.GitHubReleasesDiscovererDescription;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public ContentSourceCapabilities Capabilities => ContentSourceCapabilities.RequiresDiscovery;

    /// <inheritdoc />
    public async Task<OperationResult<ContentDiscoveryResult>> DiscoverAsync(
        ContentSearchQuery query, CancellationToken cancellationToken = default)
    {
        var results = new List<ContentSearchResult>();
        var errors = new List<string>();

        // Use configuration for repositories
        var repoList = configurationProvider.GetGitHubDiscoveryRepositories();
        var relevantRepos = repoList
            .Select(r =>
            {
                var parts = r.Split('/');
                if (parts.Length != ContentConstants.GitHubRepoPartsCount)
                {
                    logger.LogWarning("Invalid repository format: {Repository}. Expected 'owner/repo'", r);
                    return (Owner: string.Empty, Repo: string.Empty);
                }

                return (Owner: parts[0].Trim(), Repo: parts[1].Trim());
            })
            .Where(t => !string.IsNullOrEmpty(t.Owner) && !string.IsNullOrEmpty(t.Repo))
            .ToList();

        // Determine whether to load all releases or just the latest
        // Page 1 with default Take = load only latest releases (1 per repo) to conserve API calls
        // LoadMore (page > 1 or explicitly requesting all) = load additional releases
        bool loadOnlyLatest = (query.Page ?? 1) == 1 && query.Take <= relevantRepos.Count;

        foreach (var (owner, repo) in relevantRepos)
        {
            try
            {
                var repository = await gitHubClient.GetRepositoryAsync(owner, repo, cancellationToken);
                var topics = repository?.Topics ?? [];
                var releases = await FetchReleasesForRepoAsync(owner, repo, loadOnlyLatest, cancellationToken);

                foreach (var release in releases)
                {
                    ProcessRelease(release, owner, repo, topics, query, results);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to discover releases for {Owner}/{Repo}", owner, repo);
                errors.Add($"GitHub {owner}/{repo}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Encountered {ErrorCount} errors during discovery: {Errors}", errors.Count, string.Join("; ", errors));
        }

        // Sort by date descending (newest first)
        results = [.. results.OrderByDescending(r => r.LastUpdated)];

        // Apply pagination
        var totalItems = results.Count;
        int pageSize = query.Take > 0 ? query.Take : 24;
        int currentPage = query.Page ?? 1;
        if (currentPage < 1) currentPage = 1;
        int skip = (currentPage - 1) * pageSize;

        var paginatedResults = results.Skip(skip).Take(pageSize).ToList();

        // HasMoreItems is true if we loaded only latest releases (user can request more)
        // or if there are more items in the paginated results
        var hasMoreItems = totalItems > 0 && (loadOnlyLatest || (skip + paginatedResults.Count < totalItems));

        logger.LogInformation(
            "GitHubReleasesDiscoverer: Returning page {Page}, {ReturnCount} items of {TotalCount} total. HasMore: {HasMore}, LoadedOnlyLatest: {LoadedOnlyLatest}",
            query.Page,
            paginatedResults.Count,
            totalItems,
            hasMoreItems,
            loadOnlyLatest);

        return errors.Count > 0 && paginatedResults.Count == 0
            ? OperationResult<ContentDiscoveryResult>.CreateFailure(errors)
            : OperationResult<ContentDiscoveryResult>.CreateSuccess(new ContentDiscoveryResult
            {
                Items = paginatedResults,
                TotalItems = loadOnlyLatest ? -1 : totalItems, // -1 indicates unknown total when only latest loaded
                HasMoreItems = hasMoreItems,
            });
    }

    /// <summary>
    /// Builds a single SuperHackers game-client variant card. TheSuperHackers releases contain both
    /// a Generals and a Zero Hour executable; one card is emitted per game type so each variant has
    /// its own manifest ID, download state, and an obvious, clearly-labeled name.
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="release">The GitHub release.</param>
    /// <param name="baseName">Display name of the release.</param>
    /// <param name="totalSize">Total asset size in bytes.</param>
    /// <param name="variantCount">Number of assets in the release.</param>
    /// <param name="gameType">The game type this card represents.</param>
    /// <param name="gameDisplayName">Human-readable game name for the card title.</param>
    /// <param name="variantGroupId">Stable group key shared by all sibling variant cards.</param>
    /// <returns>A content search result for one variant.</returns>
    private ContentSearchResult BuildSuperHackersVariantCard(
        string owner,
        string repo,
        GitHubRelease release,
        string baseName,
        long totalSize,
        int variantCount,
        GameType gameType,
        string gameDisplayName,
        string variantGroupId)
    {
        var suffix = gameType == GameType.Generals
            ? SuperHackersConstants.GeneralsSuffix
            : SuperHackersConstants.ZeroHourSuffix;

        var result = new ContentSearchResult
        {
            Id = $"github.{owner}.{repo}.{release.TagName}.{suffix}",
            Name = $"{baseName} — {gameDisplayName}",
            Description = string.IsNullOrEmpty(release.Body)
                ? $"{gameDisplayName} game client from TheSuperHackers."
                : ReleaseDescriptionHelper.ToSummary(release.Body),
            Version = release.TagName.TrimStart('v', 'V'),
            AuthorName = release.Author,
            ContentType = ContentType.GameClient,
            TargetGame = gameType,
            IsInferred = false,
            ProviderName = ContentSourceNames.GitHubDiscoverer,
            RequiresResolution = true,
            ResolverId = ContentSourceNames.GitHubResolverId,
            SourceUrl = release.HtmlUrl,
            IconUrl = "avares://GenHub/Assets/Logos/thesuperhackers-logo.png",
            LastUpdated = release.PublishedAt?.DateTime ?? release.CreatedAt.DateTime,
            DownloadSize = totalSize,
            ResolverMetadata =
            {
                [GitHubConstants.OwnerMetadataKey] = owner,
                [GitHubConstants.RepoMetadataKey] = repo,
                [GitHubConstants.TagMetadataKey] = release.TagName,
                ["VariantCount"] = variantCount.ToString(),
                ["RequestedGameType"] = gameType.ToString(),
            },
        };

        // A release can contain a separate archive for each game.  Record the exact asset on
        // the card so resolving a single variant never downloads its siblings.
        var assetName = FindSuperHackersAssetName(release.Assets, gameType);
        if (!string.IsNullOrEmpty(assetName))
        {
            result.ResolverMetadata["asset-name"] = assetName;
        }

        // Declare the variant group so the downloads browser collapses both game-type
        // cards into a single card with a variant picker.
        result.VariantGroupId = variantGroupId;
        result.VariantFamilyName = baseName;
        result.Variants =
        [
            new ContentVariantInfo
            {
                Id = $"github.{owner}.{repo}.{release.TagName}.{SuperHackersConstants.ZeroHourSuffix}",
                Name = $"{baseName} — {SuperHackersConstants.ZeroHourDisplayName}",
                ManifestId = $"github.{owner}.{repo}.{release.TagName}.{SuperHackersConstants.ZeroHourSuffix}",
                VariantType = "game-type",
                IsDefault = true,
            },
            new ContentVariantInfo
            {
                Id = $"github.{owner}.{repo}.{release.TagName}.{SuperHackersConstants.GeneralsSuffix}",
                Name = $"{baseName} — {SuperHackersConstants.GeneralsDisplayName}",
                ManifestId = $"github.{owner}.{repo}.{release.TagName}.{SuperHackersConstants.GeneralsSuffix}",
                VariantType = "game-type",
                IsDefault = false,
            },
        ];

        return result;
    }

    private async Task<IEnumerable<GitHubRelease>> FetchReleasesForRepoAsync(
        string owner,
        string repo,
        bool loadOnlyLatest,
        CancellationToken cancellationToken)
    {
        if (loadOnlyLatest)
        {
            logger.LogDebug("Fetching only latest release for {Owner}/{Repo}", owner, repo);
            var latestRelease = await gitHubClient.GetLatestReleaseAsync(owner, repo, cancellationToken);
            return latestRelease != null ? [latestRelease] : [];
        }

        logger.LogDebug("Fetching all releases for {Owner}/{Repo}", owner, repo);
        return (await gitHubClient.GetReleasesAsync(owner, repo, cancellationToken)) ?? [];
    }

    private void ProcessRelease(
        GitHubRelease release,
        string owner,
        string repo,
        IReadOnlyList<string> topics,
        ContentSearchQuery query,
        List<ContentSearchResult> results)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchTerm) &&
            release.Name?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var totalSize = release.Assets?.Sum(a => a.Size) ?? 0;
        var variantCount = release.Assets?.Count ?? 0;

        var (contentType, isTypeInferred) = GitHubInferenceHelper.InferContentTypeFromTopics(topics);
        if (isTypeInferred)
        {
            var nameInference = GitHubInferenceHelper.InferContentType(repo, release.Name);
            contentType = nameInference.Type;
            isTypeInferred = nameInference.IsInferred;
        }

        var (gameType, isGameInferred) = GitHubInferenceHelper.InferGameTypeFromTopics(topics);
        if (isGameInferred)
        {
            var nameInference = GitHubInferenceHelper.InferTargetGame(repo, release.Name);
            gameType = nameInference.Type;
            isGameInferred = nameInference.IsInferred;
        }

        var baseName = release.Name ?? $"{repo} {release.TagName}";
        var isSuperHackersGameClient = contentType == ContentType.GameClient
            && !isTypeInferred
            && owner.Equals(PublisherTypeConstants.TheSuperHackers, StringComparison.OrdinalIgnoreCase);

        if (isSuperHackersGameClient)
        {
            var variantGroupId = $"{owner}.{ContentType.GameClient.ToString().ToLowerInvariant()}.{release.TagName}";
            results.Add(BuildSuperHackersVariantCard(owner, repo, release, baseName, totalSize, variantCount, GameType.Generals, SuperHackersConstants.GeneralsDisplayName, variantGroupId));
            results.Add(BuildSuperHackersVariantCard(owner, repo, release, baseName, totalSize, variantCount, GameType.ZeroHour, SuperHackersConstants.ZeroHourDisplayName, variantGroupId));
        }
        else
        {
            results.Add(BuildStandardSearchResult(release, owner, repo, baseName, contentType, gameType, isTypeInferred, isGameInferred, totalSize, variantCount));
        }
    }

    private ContentSearchResult BuildStandardSearchResult(
        GitHubRelease release,
        string owner,
        string repo,
        string baseName,
        ContentType contentType,
        GameType gameType,
        bool isTypeInferred,
        bool isGameInferred,
        long totalSize,
        int variantCount)
    {
        return new ContentSearchResult
        {
            Id = $"github.{owner}.{repo}.{release.TagName}",
            Name = baseName,
            Description = string.IsNullOrEmpty(release.Body)
                ? "GitHub release - full details available after resolution"
                : ReleaseDescriptionHelper.ToSummary(release.Body),
            Version = release.TagName.TrimStart('v', 'V'),
            AuthorName = release.Author,
            ContentType = contentType,
            TargetGame = gameType,
            IsInferred = isTypeInferred || isGameInferred,
            ProviderName = SourceName,
            RequiresResolution = true,
            ResolverId = ContentSourceNames.GitHubResolverId,
            SourceUrl = release.HtmlUrl,
            IconUrl = PublisherInfoConstants.GitHub.LogoSource,
            LastUpdated = release.PublishedAt?.DateTime ?? release.CreatedAt.DateTime,
            DownloadSize = totalSize,
            ResolverMetadata =
            {
                [GitHubConstants.OwnerMetadataKey] = owner,
                [GitHubConstants.RepoMetadataKey] = repo,
                [GitHubConstants.TagMetadataKey] = release.TagName,
                ["VariantCount"] = variantCount.ToString(),
            },
        };
    }

    private string? FindSuperHackersAssetName(
        IEnumerable<GitHubReleaseAsset>? assets,
        GameType gameType)
    {
        if (assets == null)
        {
            return null;
        }

        var candidates = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name))
            .ToList();

        return gameType switch
        {
            GameType.ZeroHour => candidates
                .FirstOrDefault(asset => asset.Name.Contains("generalszh", StringComparison.OrdinalIgnoreCase)
                    || asset.Name.Contains("zero-hour", StringComparison.OrdinalIgnoreCase)
                    || asset.Name.Contains("zerohour", StringComparison.OrdinalIgnoreCase)
                    || asset.Name.Contains("_zh", StringComparison.OrdinalIgnoreCase))
                ?.Name,
            GameType.Generals => candidates
                .FirstOrDefault(asset => asset.Name.Contains("generals", StringComparison.OrdinalIgnoreCase)
                    && !asset.Name.Contains("generalszh", StringComparison.OrdinalIgnoreCase)
                    && !asset.Name.Contains("zero-hour", StringComparison.OrdinalIgnoreCase)
                    && !asset.Name.Contains("zerohour", StringComparison.OrdinalIgnoreCase)
                    && !asset.Name.Contains("_zh", StringComparison.OrdinalIgnoreCase))
                ?.Name,
            _ => null,
        };
    }
}
