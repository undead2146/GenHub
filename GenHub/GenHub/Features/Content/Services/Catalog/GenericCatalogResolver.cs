using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Providers;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using GenHub.Core.Services.Dependencies;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services.Catalog;

/// <summary>
/// Resolves a ContentSearchResult (from GenericCatalogDiscoverer) into a full ContentManifest.
/// </summary>
public partial class GenericCatalogResolver(
    ILogger<GenericCatalogResolver> logger,
    Func<IContentManifestBuilder> manifestBuilderFactory) : IContentResolver
{
    /// <inheritdoc />
    public string ResolverId => CatalogConstants.GenericCatalogResolverId;

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> ResolveAsync(
        ContentSearchResult discoveredItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveredItem);

        try
        {
            // Extract catalog item and release metadata
            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.ReleaseJsonMetadataKey, out var releaseJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing release metadata");
            }

            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.CatalogItemJsonMetadataKey, out var contentItemJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing content item metadata");
            }

            if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.PublisherProfileJsonMetadataKey, out var publisherJson))
            {
                return OperationResult<ContentManifest>.CreateFailure("Missing publisher profile");
            }

            // Deserialize from JSON
            var release = JsonSerializer.Deserialize<ContentRelease>(releaseJson);
            var contentItem = JsonSerializer.Deserialize<CatalogContentItem>(contentItemJson);
            var publisher = JsonSerializer.Deserialize<PublisherProfile>(publisherJson);

            if (release == null || contentItem == null || publisher == null)
            {
                return OperationResult<ContentManifest>.CreateFailure("Failed to deserialize catalog metadata");
            }

            logger.LogInformation(
                "Resolving content '{ContentName}' v{Version} from publisher '{PublisherId}'",
                contentItem.Name,
                release.Version,
                publisher.Id);

            var declaredPublisherId = CatalogManifestIdentity.ResolveDeclaredPublisherType(contentItem);

            var publisherDisplayName = !string.IsNullOrWhiteSpace(contentItem.Metadata?.Author)
                ? contentItem.Metadata.Author
                : publisher.Name;

            var website = !string.IsNullOrWhiteSpace(contentItem.Metadata?.DocumentationUrl)
                ? contentItem.Metadata.DocumentationUrl
                : (publisher.Website ?? string.Empty);

            // ContentBundle (and other meta-packages) may ship no downloadable artifacts —
            // their payload is the dependency graph alone. Skip remote-file registration.
            var primaryArtifact = release.Artifacts?.FirstOrDefault(a => a.IsPrimary)
                ?? release.Artifacts?.FirstOrDefault();

            var effectiveContentId = !string.IsNullOrWhiteSpace(primaryArtifact?.Variant)
                ? $"{contentItem.Id}-{primaryArtifact.Variant.Trim()}"
                : contentItem.Id;

            var resolvedName = ResolveManifestName(discoveredItem, contentItem, primaryArtifact);
            var resolvedTargetGame = ResolveTargetGame(contentItem, primaryArtifact, discoveredItem);

            var builder = manifestBuilderFactory()
                .WithBasicInfo(declaredPublisherId, effectiveContentId, release.Version)
                .WithContentType(contentItem.ContentType, resolvedTargetGame)
                .WithName(resolvedName)
                .WithPublisher(
                    publisherDisplayName,
                    website,
                    publisher.SupportUrl ?? string.Empty,
                    publisher.ContactEmail ?? string.Empty,
                    publisherType: declaredPublisherId)
                .WithMetadata(
                    description: contentItem.Description,
                    tags: [.. contentItem.Tags],
                    iconUrl: contentItem.Metadata?.BannerUrl ?? string.Empty,
                    screenshotUrls: contentItem.Metadata?.ScreenshotUrls?.ToList(),
                    changelogUrl: contentItem.Metadata?.DocumentationUrl ?? string.Empty);

            if (primaryArtifact != null)
            {
                var filename = SanitizeArtifactFilename(primaryArtifact, contentItem);
                logger.LogDebug(
                    "Adding remote file {Filename} with download URL {Url}",
                    filename,
                    primaryArtifact.DownloadUrl);

                await builder.AddRemoteFileAsync(
                    relativePath: filename,
                    downloadUrl: primaryArtifact.DownloadUrl,
                    sourceType: ContentSourceType.RemoteDownload,
                    isExecutable: false,
                    permissions: null);
            }
            else
            {
                logger.LogInformation(
                    "Content '{ContentName}' has no downloadable artifacts (dependency-only package)",
                    contentItem.Name);
            }

            AddDependencies(logger, builder, discoveredItem, release, contentItem, resolvedTargetGame);

            var manifest = builder.Build();

            ApplyManifestPostProcessing(
                manifest,
                contentItem,
                primaryArtifact,
                declaredPublisherId,
                resolvedName,
                discoveredItem.Id,
                resolvedTargetGame);

            logger.LogInformation(
                "Successfully resolved manifest for '{ContentName}' with {FileCount} files",
                manifest.Name,
                manifest.Files.Count);

            return OperationResult<ContentManifest>.CreateSuccess(manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve content from catalog");
            return OperationResult<ContentManifest>.CreateFailure($"Resolution failed: {ex.Message}");
        }
    }

    private static string ResolveManifestName(
        ContentSearchResult searchResult,
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact)
    {
        if (!string.IsNullOrWhiteSpace(searchResult.Name))
        {
            return searchResult.Name;
        }

        return !string.IsNullOrWhiteSpace(primaryArtifact?.Variant)
            ? $"{contentItem.Name} ({primaryArtifact.Variant})"
            : contentItem.Name;
    }

    private static GameType ResolveTargetGame(
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact,
        ContentSearchResult searchResult)
    {
        if (primaryArtifact?.VariantAxis?.Equals("game-type", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (primaryArtifact.Variant?.Equals("Generals", StringComparison.OrdinalIgnoreCase) == true)
            {
                return GameType.Generals;
            }

            if (primaryArtifact.Variant?.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) == true ||
                primaryArtifact.Variant?.Equals("ZeroHour", StringComparison.OrdinalIgnoreCase) == true)
            {
                return GameType.ZeroHour;
            }
        }

        if (searchResult.TargetGame != GameType.Unknown)
        {
            return searchResult.TargetGame;
        }

        return contentItem.TargetGame;
    }

    private static string SanitizeArtifactFilename(ReleaseArtifact primaryArtifact, CatalogContentItem contentItem)
    {
        var filename = primaryArtifact.Filename;
        if (string.IsNullOrEmpty(filename) ||
            filename.Contains("fetch.aspx", StringComparison.OrdinalIgnoreCase) ||
            filename.Contains('?') ||
            filename.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var rawUrl = primaryArtifact.DownloadUrl;
            var queryIndex = rawUrl.IndexOfAny(['?', '#']);
            var cleanUrl = queryIndex >= 0 ? rawUrl[..queryIndex] : rawUrl;
            var extension = primaryArtifact.ContentType?.ToLowerInvariant() switch
            {
                "application/zip" => ".zip",
                "application/x-rar-compressed" => ".rar",
                "application/x-7z-compressed" => ".7z",
                _ => Path.GetExtension(cleanUrl) is { Length: > 1 } urlExt
                    ? urlExt
                    : ".zip",
            };
            return SanitizeFileName($"{Path.GetFileName(contentItem.Name)}{extension}");
        }

        return SanitizeFileName(Path.GetFileName(filename));
    }

    private static string SanitizeFileName(string filename)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(filename.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    private static void AddDependencies(
        ILogger logger,
        IContentManifestBuilder builder,
        ContentSearchResult discoveredItem,
        ContentRelease release,
        CatalogContentItem contentItem,
        GameType resolvedTargetGame)
    {
        var bundleComponents = TryDeserializeBundleComponents(logger, discoveredItem, contentItem.Id);

        foreach (var dependency in release.Dependencies)
        {
            var dependencyType = CatalogManifestIdentity.ResolveDependencyContentType(dependency, contentItem);
            var (minVersion, maxVersion, minInclusive, maxInclusive, compatibleVersions) = ParseVersionConstraint(dependency.VersionConstraint);

            if (dependencyType == ContentType.GameInstallation ||
                CatalogManifestIdentity.IsBaseGameDependency(dependency))
            {
                AddBaseGameDependency(
                    builder,
                    dependency,
                    resolvedTargetGame,
                    minVersion,
                    maxVersion,
                    minInclusive,
                    maxInclusive,
                    compatibleVersions);
                continue;
            }

            AddCatalogDependency(
                builder,
                dependency,
                dependencyType,
                contentItem,
                bundleComponents,
                minVersion,
                maxVersion,
                minInclusive,
                maxInclusive,
                compatibleVersions);
        }
    }

    private static List<CatalogBundleComponentDescriptor>? TryDeserializeBundleComponents(
        ILogger logger,
        ContentSearchResult discoveredItem,
        string contentItemId)
    {
        if (!discoveredItem.ResolverMetadata.TryGetValue(CatalogConstants.BundleComponentsJsonMetadataKey, out var bundleJson) ||
            string.IsNullOrWhiteSpace(bundleJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<CatalogBundleComponentDescriptor>>(bundleJson)
                ?? throw new JsonException("Bundle component metadata deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize bundle component metadata for '{ContentId}'", contentItemId);
            throw new InvalidOperationException($"Bundle component metadata for '{contentItemId}' is invalid.", ex);
        }
    }

    private static void AddBaseGameDependency(
        IContentManifestBuilder builder,
        CatalogDependency dependency,
        GameType resolvedTargetGame,
        string minVersion,
        string maxVersion,
        bool minInclusive,
        bool maxInclusive,
        List<string>? compatibleVersions)
    {
        var isGenerals = dependency.ContentId.Equals("generals", StringComparison.OrdinalIgnoreCase) ||
                         resolvedTargetGame == GameType.Generals;
        var foundation = isGenerals &&
                         !dependency.ContentId.Equals("zerohour", StringComparison.OrdinalIgnoreCase)
            ? BaseDependencyBuilder.CreateGenerals108Dependency()
            : BaseDependencyBuilder.CreateZeroHour104Dependency();

        var effectiveMinVersion = foundation.MinVersion ?? string.Empty;
        var effectiveMinInclusive = true;

        if (!string.IsNullOrEmpty(minVersion))
        {
            if (string.IsNullOrEmpty(effectiveMinVersion) ||
                CatalogManifestIdentity.CompareVersions(minVersion, effectiveMinVersion) > 0)
            {
                effectiveMinVersion = minVersion;
                effectiveMinInclusive = minInclusive;
            }
            else if (CatalogManifestIdentity.CompareVersions(minVersion, effectiveMinVersion) == 0)
            {
                effectiveMinInclusive = minInclusive;
            }
        }
        else if (compatibleVersions is { Count: > 0 })
        {
            effectiveMinVersion = string.Empty;
        }

        builder.AddDependency(
            id: foundation.Id,
            name: foundation.Name,
            dependencyType: ContentType.GameInstallation,
            installBehavior: DependencyInstallBehavior.RequireExisting,
            minVersion: effectiveMinVersion,
            maxVersion: maxVersion,
            compatibleVersions: compatibleVersions,
            compatibleGameTypes: foundation.CompatibleGameTypes,
            minInclusive: effectiveMinInclusive,
            maxInclusive: maxInclusive);
    }

    private static void AddCatalogDependency(
        IContentManifestBuilder builder,
        CatalogDependency dependency,
        ContentType initialDependencyType,
        CatalogContentItem contentItem,
        List<CatalogBundleComponentDescriptor>? bundleComponents,
        string minVersion,
        string maxVersion,
        bool minInclusive,
        bool maxInclusive,
        List<string>? compatibleVersions)
    {
        var (depPublisherId, depVersion, dependencyType) = ResolveDependencyIdentity(dependency, initialDependencyType, bundleComponents);

        var dependencyId = CatalogManifestIdentity.CreateContentId(
            depPublisherId,
            dependencyType,
            dependency.ContentId,
            depVersion);

        var installBehavior = DependencyInstallBehavior.RequireExisting;
        if (dependency.IsOptional)
        {
            installBehavior = DependencyInstallBehavior.Optional;
        }
        else if (contentItem.ContentType == ContentType.ContentBundle)
        {
            installBehavior = DependencyInstallBehavior.AutoInstall;
        }

        builder.AddDependency(
            id: ManifestId.Create(dependencyId),
            name: dependency.ContentId,
            dependencyType: dependencyType,
            installBehavior: installBehavior,
            minVersion: minVersion,
            maxVersion: maxVersion,
            compatibleVersions: compatibleVersions,
            minInclusive: minInclusive,
            maxInclusive: maxInclusive);
    }

    private static (string PublisherId, string Version, ContentType DependencyType) ResolveDependencyIdentity(
        CatalogDependency dependency,
        ContentType initialDependencyType,
        List<CatalogBundleComponentDescriptor>? bundleComponents)
    {
        var cleanConstraint = CatalogManifestIdentity.StripVersionConstraint(dependency.VersionConstraint);
        var depPublisherId = dependency.PublisherId;
        var depVersion = cleanConstraint;
        var dependencyType = initialDependencyType;

        if (bundleComponents?.FirstOrDefault(c => string.Equals(c.ContentId, dependency.ContentId, StringComparison.OrdinalIgnoreCase)) is { } matched)
        {
            if (!string.IsNullOrWhiteSpace(matched.PublisherId))
            {
                depPublisherId = matched.PublisherId;
            }

            if (!string.IsNullOrWhiteSpace(matched.ReleaseVersion))
            {
                depVersion = matched.ReleaseVersion;
            }

            if (!string.IsNullOrWhiteSpace(matched.ContentType) &&
                Enum.TryParse<ContentType>(matched.ContentType, true, out var matchedType))
            {
                dependencyType = matchedType;
            }
        }

        return (depPublisherId, depVersion, dependencyType);
    }

    [GeneratedRegex(@"([><=^~]+)\s+")]
    private static partial Regex OperatorWhitespaceRegex();

    private static (string MinVersion, string MaxVersion, bool MinInclusive, bool MaxInclusive, List<string>? CompatibleVersions) ParseVersionConstraint(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint))
        {
            return (string.Empty, string.Empty, true, true, null);
        }

        var trimmed = OperatorWhitespaceRegex().Replace(constraint.Trim(), "$1");
        if (trimmed.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return (string.Empty, string.Empty, true, true, null);
        }

        if (trimmed.Contains(',') || trimmed.Contains('|'))
        {
            return ParseListConstraint(trimmed);
        }

        return ParseRangedTokens(trimmed);
    }

    private static (string MinVersion, string MaxVersion, bool MinInclusive, bool MaxInclusive, List<string>? CompatibleVersions) ParseListConstraint(string trimmed)
    {
        var parts = trimmed.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CatalogManifestIdentity.StripVersionConstraint)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (string.Empty, string.Empty, true, true, parts.Count > 0 ? parts : null);
    }

    private static (string MinVersion, string MaxVersion, bool MinInclusive, bool MaxInclusive, List<string>? CompatibleVersions) ParseRangedTokens(string trimmed)
    {
        var tokens = trimmed.Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1 && CatalogManifestIdentity.TryParseExactVersion(tokens[0], out var exactVersion))
        {
            return (exactVersion, exactVersion, true, true, [exactVersion]);
        }

        string minVersion = string.Empty;
        string maxVersion = string.Empty;
        var minInclusive = true;
        var maxInclusive = true;

        foreach (var token in tokens)
        {
            ApplyTokenBound(token, ref minVersion, ref maxVersion, ref minInclusive, ref maxInclusive);
        }

        return (minVersion, maxVersion, minInclusive, maxInclusive, null);
    }

    private static void ApplyTokenBound(
        string token,
        ref string minVersion,
        ref string maxVersion,
        ref bool minInclusive,
        ref bool maxInclusive)
    {
        if (token.StartsWith(">=", StringComparison.Ordinal))
        {
            UpdateLowerBound(CatalogManifestIdentity.StripVersionConstraint(token), true, ref minVersion, ref minInclusive);
        }
        else if (token.StartsWith('>'))
        {
            UpdateLowerBound(CatalogManifestIdentity.StripVersionConstraint(token), false, ref minVersion, ref minInclusive);
        }
        else if (token.StartsWith("<=", StringComparison.Ordinal))
        {
            UpdateUpperBound(CatalogManifestIdentity.StripVersionConstraint(token), true, ref maxVersion, ref maxInclusive);
        }
        else if (token.StartsWith('<'))
        {
            UpdateUpperBound(CatalogManifestIdentity.StripVersionConstraint(token), false, ref maxVersion, ref maxInclusive);
        }
        else if (token.StartsWith('^'))
        {
            var target = CatalogManifestIdentity.StripVersionConstraint(token);
            UpdateLowerBound(target, true, ref minVersion, ref minInclusive);
            var parts = target.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out var major))
            {
                UpdateUpperBound($"{major + 1}.0.0", false, ref maxVersion, ref maxInclusive);
            }
        }
        else if (token.StartsWith('~'))
        {
            var target = CatalogManifestIdentity.StripVersionConstraint(token);
            UpdateLowerBound(target, true, ref minVersion, ref minInclusive);
            var parts = target.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                UpdateUpperBound($"{major}.{minor + 1}.0", false, ref maxVersion, ref maxInclusive);
            }
        }
    }

    private static void UpdateLowerBound(
        string candidateMin,
        bool candidateInclusive,
        ref string currentMin,
        ref bool currentInclusive)
    {
        if (string.IsNullOrEmpty(candidateMin))
        {
            return;
        }

        if (string.IsNullOrEmpty(currentMin))
        {
            currentMin = candidateMin;
            currentInclusive = candidateInclusive;
            return;
        }

        var cmp = CatalogManifestIdentity.CompareVersions(candidateMin, currentMin);
        if (cmp > 0)
        {
            currentMin = candidateMin;
            currentInclusive = candidateInclusive;
        }
        else if (cmp == 0)
        {
            currentInclusive = currentInclusive && candidateInclusive;
        }
    }

    private static void UpdateUpperBound(
        string candidateMax,
        bool candidateInclusive,
        ref string currentMax,
        ref bool currentInclusive)
    {
        if (string.IsNullOrEmpty(candidateMax))
        {
            return;
        }

        if (string.IsNullOrEmpty(currentMax))
        {
            currentMax = candidateMax;
            currentInclusive = candidateInclusive;
            return;
        }

        var cmp = CatalogManifestIdentity.CompareVersions(candidateMax, currentMax);
        if (cmp < 0)
        {
            currentMax = candidateMax;
            currentInclusive = candidateInclusive;
        }
        else if (cmp == 0)
        {
            currentInclusive = currentInclusive && candidateInclusive;
        }
    }

    private static void ApplyManifestPostProcessing(
        ContentManifest manifest,
        CatalogContentItem contentItem,
        ReleaseArtifact? primaryArtifact,
        string declaredPublisherId,
        string resolvedName,
        string? searchResultId,
        GameType resolvedTargetGame)
    {
        if (primaryArtifact != null && !string.IsNullOrWhiteSpace(primaryArtifact.Sha256))
        {
            var primaryFile = manifest.Files.FirstOrDefault();
            if (primaryFile != null)
            {
                primaryFile.Hash = primaryArtifact.Sha256;
            }
        }

        if (!string.IsNullOrWhiteSpace(searchResultId) &&
            ManifestIdValidator.IsValid(searchResultId, out _))
        {
            manifest.Id = ManifestId.Create(searchResultId);
        }

        manifest.Name = resolvedName;
        manifest.OriginalProviderName = declaredPublisherId;
        manifest.OriginalContentId = searchResultId ?? contentItem.Id;

        foreach (var dep in manifest.Dependencies)
        {
            if (dep.DependencyType == ContentType.GameInstallation && dep.CompatibleGameTypes.Count == 0)
            {
                dep.CompatibleGameTypes.Add(resolvedTargetGame);
            }
        }

        manifest.Metadata.Description = contentItem.Description;
        manifest.Metadata.Tags = [.. contentItem.Tags];

        if (!string.IsNullOrWhiteSpace(primaryArtifact?.Variant))
        {
            var variantTag = $"variant:{primaryArtifact.Variant.ToLowerInvariant()}";
            if (!manifest.Metadata.Tags.Contains(variantTag, StringComparer.OrdinalIgnoreCase))
            {
                manifest.Metadata.Tags.Add(variantTag);
            }
        }

        if (manifest.Metadata.Tags.All(t => !t.StartsWith("contentCode:", StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Metadata.Tags.Add($"contentCode:{contentItem.Id}");
        }
    }
}
