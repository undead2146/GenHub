using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Providers;

namespace GenHub.Features.Content.Services.Catalog;

/// <summary>
/// Builds self-contained bundle-component descriptors from a publisher catalog so the
/// downloads UI can render per-item identity and variant pickers on a ContentBundle card.
/// </summary>
public static class CatalogBundleComponentBuilder
{
    /// <summary>
    /// Builds descriptors for every required (and optional) dependency of a release.
    /// Base-game installation constraints are included and flagged so the UI can skip download.
    /// </summary>
    /// <param name="catalog">The publisher catalog.</param>
    /// <param name="parent">The bundle (or other parent) catalog item.</param>
    /// <param name="release">The selected release.</param>
    /// <returns>Component descriptors in declaration order.</returns>
    public static IReadOnlyList<CatalogBundleComponentDescriptor> Build(
        PublisherCatalog catalog,
        CatalogContentItem parent,
        ContentRelease release)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(release);

        var itemsById = catalog.Content
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var components = new List<CatalogBundleComponentDescriptor>();

        foreach (var dependency in release.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.ContentId))
            {
                continue;
            }

            if (CatalogManifestIdentity.IsBaseGameDependency(dependency))
            {
                components.Add(new CatalogBundleComponentDescriptor
                {
                    PublisherId = dependency.PublisherId,
                    ContentId = dependency.ContentId,
                    Name = CatalogManifestIdentity.HumanizeContentId(dependency.ContentId),
                    ContentType = ContentType.GameInstallation.ToString(),
                    IsOptional = dependency.IsOptional,
                    IsBaseGame = true,
                });
                continue;
            }

            itemsById.TryGetValue(dependency.ContentId, out var sibling);
            var contentType = CatalogManifestIdentity.ResolveDependencyContentType(dependency, parent, itemsById);
            var name = sibling?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = CatalogManifestIdentity.HumanizeContentId(dependency.ContentId);
            }

            var declaredPublisherId = sibling != null
                ? CatalogManifestIdentity.ResolveDeclaredPublisherType(sibling)
                : CatalogConstants.GenericCatalogResolverId;

            var siblingRelease = SelectRelease(sibling, dependency.VersionConstraint);
            if (sibling == null || siblingRelease == null)
            {
                // Skip missing siblings or items with no releases so they don't look downloadable
                continue;
            }

            var descriptor = new CatalogBundleComponentDescriptor
            {
                PublisherId = declaredPublisherId,
                ContentId = dependency.ContentId,
                Name = name,
                ContentType = contentType.ToString(),
                IsOptional = dependency.IsOptional,
                IsBaseGame = false,
                CatalogItemJson = JsonSerializer.Serialize(sibling),
            };

            var resolvedSiblingRelease = CloneReleaseWithResolvedTypes(siblingRelease, sibling, itemsById);
            var variantArtifacts = GetMultiOptionVariantArtifacts(resolvedSiblingRelease);
            if (parent.TargetGame is GameType.Generals or GameType.ZeroHour)
            {
                variantArtifacts = variantArtifacts.Where(artifact =>
                {
                    if (artifact.VariantAxis?.Equals("game-type", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var isGen = artifact.Variant?.Equals("Generals", StringComparison.OrdinalIgnoreCase) == true;
                        var isZh = artifact.Variant?.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) == true ||
                                   artifact.Variant?.Equals("ZeroHour", StringComparison.OrdinalIgnoreCase) == true;
                        if (parent.TargetGame == GameType.ZeroHour && isGen && !isZh)
                        {
                            return false;
                        }

                        if (parent.TargetGame == GameType.Generals && isZh && !isGen)
                        {
                            return false;
                        }
                    }

                    return true;
                }).ToList();
            }

            if (variantArtifacts.Count > 0)
            {
                var defaultAssigned = false;
                foreach (var artifact in variantArtifacts)
                {
                    var label = artifact.Variant!.Trim();
                    var axis = artifact.VariantAxis!.Trim();
                    var isDefault = artifact.IsDefaultVariant && !defaultAssigned;
                    if (isDefault)
                    {
                        defaultAssigned = true;
                    }

                    var variantRelease = CloneVariantRelease(resolvedSiblingRelease, artifact, resolvedSiblingRelease.Artifacts);
                    descriptor.Variants.Add(new CatalogBundleComponentVariantDescriptor
                    {
                        Label = label,
                        Axis = axis,
                        IsDefault = isDefault,
                        CatalogId = CatalogManifestIdentity.CreateVariantContentId(
                            descriptor.PublisherId,
                            sibling.ContentType,
                            sibling.Id,
                            label,
                            resolvedSiblingRelease.Version,
                            axis),
                        ReleaseJson = JsonSerializer.Serialize(variantRelease),
                        DownloadSize = artifact.Size,
                    });
                }

                if (!defaultAssigned && descriptor.Variants.Count > 0)
                {
                    var preferred = descriptor.Variants.FirstOrDefault(v =>
                                        v.Label.Contains("1080p", StringComparison.OrdinalIgnoreCase))
                                    ?? descriptor.Variants[0];
                    preferred.IsDefault = true;
                }
            }
            else
            {
                descriptor.Variants.Add(new CatalogBundleComponentVariantDescriptor
                {
                    Label = string.Empty,
                    Axis = string.Empty,
                    IsDefault = true,
                    CatalogId = CatalogManifestIdentity.CreateContentId(
                        descriptor.PublisherId,
                        sibling.ContentType,
                        sibling.Id,
                        resolvedSiblingRelease.Version),
                    ReleaseJson = JsonSerializer.Serialize(resolvedSiblingRelease),
                    DownloadSize = resolvedSiblingRelease.Artifacts?.FirstOrDefault(a => a.IsPrimary)?.Size
                        ?? resolvedSiblingRelease.Artifacts?.FirstOrDefault()?.Size
                        ?? 0,
                });
            }

            components.Add(descriptor);
        }

        return components;
    }

    /// <summary>
    /// Clones a release and fills missing dependency <c>contentType</c> values from the catalog.
    /// </summary>
    /// <param name="release">The source release.</param>
    /// <param name="parent">The content item that owns the release.</param>
    /// <param name="catalogItems">Catalog index keyed by content id.</param>
    /// <returns>A release whose dependencies have concrete content types.</returns>
    public static ContentRelease CloneReleaseWithResolvedTypes(
        ContentRelease release,
        CatalogContentItem parent,
        IReadOnlyDictionary<string, CatalogContentItem> catalogItems)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(catalogItems);

        return new ContentRelease
        {
            Version = release.Version,
            ReleaseDate = release.ReleaseDate,
            IsPrerelease = release.IsPrerelease,
            IsLatest = release.IsLatest,
            Changelog = release.Changelog,
            Artifacts = release.Artifacts?.Select(a => new ReleaseArtifact
            {
                Filename = a.Filename,
                DownloadUrl = a.DownloadUrl,
                Size = a.Size,
                Sha256 = a.Sha256,
                ContentType = a.ContentType,
                IsPrimary = a.IsPrimary,
                VariantAxis = a.VariantAxis,
                Variant = a.Variant,
                IsDefaultVariant = a.IsDefaultVariant,
            }).ToList() ?? [],
            Dependencies = [.. release.Dependencies.Select(dependency => new CatalogDependency
            {
                PublisherId = dependency.PublisherId,
                ContentId = dependency.ContentId,
                VersionConstraint = dependency.VersionConstraint,
                IsOptional = dependency.IsOptional,
                CatalogUrl = dependency.CatalogUrl,
                ContentType = string.IsNullOrWhiteSpace(dependency.ContentType)
                    ? CatalogManifestIdentity.ResolveDependencyContentType(dependency, parent, catalogItems).ToString()
                    : dependency.ContentType,
            })],
        };
    }

    private static ContentRelease? SelectRelease(CatalogContentItem? item, string? versionConstraint = null)
    {
        if (item?.Releases == null || item.Releases.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(versionConstraint))
        {
            var constraint = new VersionConstraint { ConstraintExpression = versionConstraint };
            var matching = item.Releases
                .OrderByDescending(r => r.IsLatest)
                .ThenByDescending(r => r.ReleaseDate)
                .FirstOrDefault(r => constraint.IsSatisfiedBy(r.Version));

            if (matching != null)
            {
                return matching;
            }
        }

        return item.Releases.FirstOrDefault(r => r.IsLatest) ?? item.Releases[0];
    }

    private static List<ReleaseArtifact> GetMultiOptionVariantArtifacts(ContentRelease release)
    {
        if (release.Artifacts == null || release.Artifacts.Count == 0)
        {
            return [];
        }

        var hinted = release.Artifacts
            .Where(a => !string.IsNullOrWhiteSpace(a.VariantAxis) && !string.IsNullOrWhiteSpace(a.Variant))
            .ToList();

        if (hinted.Count < 2)
        {
            return [];
        }

        var multiAxes = hinted
            .GroupBy(a => a.VariantAxis!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (multiAxes.Count == 0)
        {
            return [];
        }

        return hinted.Where(a => multiAxes.Contains(a.VariantAxis!)).ToList();
    }

    private static ContentRelease CloneVariantRelease(ContentRelease release, ReleaseArtifact selectedArtifact, List<ReleaseArtifact> allArtifacts)
    {
        var selectedAxis = selectedArtifact.VariantAxis ?? string.Empty;
        var artifactsToInclude = new List<ReleaseArtifact> { selectedArtifact };

        // For other axes, pick their default or first artifact
        var otherAxisGroups = allArtifacts
            .Where(a => !string.Equals(a.VariantAxis, selectedAxis, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.VariantAxis))
            .GroupBy(a => a.VariantAxis!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in otherAxisGroups)
        {
            var defaultForAxis = group.FirstOrDefault(a => a.IsDefaultVariant) ?? group.First();
            artifactsToInclude.Add(defaultForAxis);
        }

        // Include any non-variant artifacts
        var nonVariantArtifacts = allArtifacts.Where(a => string.IsNullOrWhiteSpace(a.VariantAxis));
        artifactsToInclude.AddRange(nonVariantArtifacts);

        return new ContentRelease
        {
            Version = release.Version,
            ReleaseDate = release.ReleaseDate,
            IsPrerelease = release.IsPrerelease,
            IsLatest = release.IsLatest,
            Changelog = release.Changelog,
            Artifacts = artifactsToInclude.Select(a => new ReleaseArtifact
            {
                Filename = a.Filename,
                DownloadUrl = a.DownloadUrl,
                Size = a.Size,
                Sha256 = a.Sha256,
                ContentType = a.ContentType,
                IsPrimary = a == selectedArtifact || a.IsPrimary,
                VariantAxis = a.VariantAxis,
                Variant = a.Variant,
                IsDefaultVariant = a.IsDefaultVariant,
            }).ToList(),
            Dependencies = release.Dependencies,
        };
    }
}
