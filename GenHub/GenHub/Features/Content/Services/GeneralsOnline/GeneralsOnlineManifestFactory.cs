using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Factory for creating and updating Generals Online content manifests.
/// Creates separate manifests for each game client variant (30Hz and 60Hz).
/// </summary>
public static class GeneralsOnlineManifestFactory
{
    /// <summary>
    /// Creates two content manifests from a GeneralsOnline release - one for each variant (30Hz and 60Hz).
    /// This creates the initial manifests with download URLs.
    /// </summary>
    /// <param name="release">The GeneralsOnlineRelease to create the manifests from.</param>
    /// <returns>A list containing two ContentManifest instances (30Hz and 60Hz variants).</returns>
    public static List<ContentManifest> CreateManifests(GeneralsOnlineRelease release)
    {
        var manifests = new List<ContentManifest>();

        // Create manifest for 30Hz variant
        manifests.Add(CreateVariantManifest(release, GameClientConstants.GeneralsOnline30HzExecutable, GeneralsOnlineConstants.Variant30HzSuffix, GameClientConstants.GeneralsOnline30HzDisplayName));

        // Create manifest for 60Hz variant
        manifests.Add(CreateVariantManifest(release, GameClientConstants.GeneralsOnline60HzExecutable, GeneralsOnlineConstants.Variant60HzSuffix, GameClientConstants.GeneralsOnline60HzDisplayName));

        return manifests;
    }

    /// <summary>
    /// Updates two manifests (30Hz and 60Hz) with extracted file information.
    /// Computes SHA-256 hashes for all files for CAS integration.
    /// Each variant gets only the files it needs plus shared files.
    /// </summary>
    /// <param name="manifests">The original content manifests to update (30Hz and 60Hz).</param>
    /// <param name="extractPath">The path to the directory containing extracted files.</param>
    /// <param name="logger">The logger for logging progress and errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>Updated content manifests with file hashes and details.</returns>
    public static async Task<List<ContentManifest>> UpdateManifestsWithExtractedFiles(
        List<ContentManifest> manifests,
        string extractPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating manifests with extracted files from: {Path}", extractPath);

        var allFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
        logger.LogInformation("Processing {Count} files", allFiles.Length);

        var filesWithHashes = new List<(string relativePath, FileInfo fileInfo, string hash)>();

        foreach (var filePath in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var relativePath = Path.GetRelativePath(extractPath, filePath);
            var fileInfo = new FileInfo(filePath);

            string hash;
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
                hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            filesWithHashes.Add((relativePath, fileInfo, hash));
            logger.LogDebug("Processed file: {File} ({Size} bytes, hash: {Hash})", relativePath, fileInfo.Length, hash[..8]);
        }

        var updatedManifests = new List<ContentManifest>();

        foreach (var manifest in manifests)
        {
            var manifestFiles = new List<ManifestFile>();

            var is30Hz = manifest.Name.Contains("30Hz", StringComparison.OrdinalIgnoreCase);
            var targetExecutable = is30Hz
                ? GameClientConstants.GeneralsOnline30HzExecutable.ToLowerInvariant()
                : GameClientConstants.GeneralsOnline60HzExecutable.ToLowerInvariant();

            foreach (var (relativePath, fileInfo, hash) in filesWithHashes)
            {
                var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
                var isExecutable = false;

                if (fileName == targetExecutable)
                {
                    isExecutable = true;
                }
                else if (fileName == GameClientConstants.GeneralsOnline30HzExecutable.ToLowerInvariant() ||
                         fileName == GameClientConstants.GeneralsOnline60HzExecutable.ToLowerInvariant())
                {
                    continue;
                }

                manifestFiles.Add(new ManifestFile
                {
                    RelativePath = relativePath,
                    Size = fileInfo.Length,
                    Hash = hash,
                    SourceType = ContentSourceType.ContentAddressable,
                    SourcePath = fileInfo.FullName,
                    IsExecutable = isExecutable,
                });
            }

            logger.LogInformation("Manifest '{Name}' updated with {Count} files", manifest.Name, manifestFiles.Count);

            updatedManifests.Add(new ContentManifest
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                ContentType = manifest.ContentType,
                TargetGame = manifest.TargetGame,
                Publisher = manifest.Publisher,
                Metadata = manifest.Metadata,
                Files = manifestFiles,
                Dependencies = manifest.Dependencies,
            });
        }

        return updatedManifests;
    }

    /// <summary>
    /// Creates a content manifest for a specific Generals Online variant.
    /// </summary>
    /// <param name="release">The Generals Online release information.</param>
    /// <param name="executableName">The executable filename for this variant.</param>
    /// <param name="variantSuffix">The suffix for the manifest ID (e.g., "30hz").</param>
    /// <param name="displayName">The display name for this variant (e.g., "GeneralsOnline 30Hz").</param>
    /// <returns>A content manifest for the specified variant.</returns>
    private static ContentManifest CreateVariantManifest(
        GeneralsOnlineRelease release,
        string executableName,
        string variantSuffix,
        string displayName)
    {
        // Parse version to extract numeric version (remove dots and QFE markers)
        var userVersion = ParseVersionForManifestId(release.Version);

        // Content name for GeneralsOnline (publisher is "generalsonline", content is the variant)
        // This will create IDs like: 1.1015255.generalsonline.gameclient.30hz
        var contentName = variantSuffix;

        var manifestId = ManifestId.Create(ManifestIdGenerator.GeneratePublisherContentId(
            GeneralsOnlineConstants.PublisherType,
            ContentType.GameClient,
            contentName,
            userVersion));

        return new ContentManifest
        {
            Id = manifestId,
            Name = displayName,
            Version = release.Version,
            ContentType = ContentType.GameClient,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo
            {
                Name = GeneralsOnlineConstants.PublisherName,
                PublisherType = GeneralsOnlineConstants.PublisherType,
                Website = GeneralsOnlineConstants.WebsiteUrl,
                SupportUrl = GeneralsOnlineConstants.SupportUrl,
                ContentIndexUrl = GeneralsOnlineConstants.DownloadPageUrl,
                UpdateCheckIntervalHours = GeneralsOnlineConstants.UpdateCheckIntervalHours,
            },
            Metadata = new ContentMetadata
            {
                Description = GeneralsOnlineConstants.ShortDescription,
                ReleaseDate = release.ReleaseDate,
                IconUrl = GeneralsOnlineConstants.IconUrl,
                Tags = new List<string>(GeneralsOnlineConstants.Tags),
                ChangelogUrl = release.Changelog,
            },
            Files = new List<ManifestFile>
            {
                new ManifestFile
                {
                    RelativePath = Path.GetFileName(release.PortableUrl),
                    DownloadUrl = release.PortableUrl,
                    Size = release.PortableSize,
                    SourceType = ContentSourceType.RemoteDownload,
                    Hash = string.Empty,
                },
            },
            Dependencies = new List<ContentDependency>
            {
                new ContentDependency
                {
                    Name = GameClientConstants.ZeroHourInstallationDependencyName,
                    DependencyType = ContentType.GameInstallation,
                    MinVersion = ManifestConstants.ZeroHourManifestVersion,
                },
            },
        };
    }

    /// <summary>
    /// Parses a Generals Online version string to extract a numeric user version for manifest IDs.
    /// Converts versions like "101525_QFE5" to a numeric value like 1015255.
    /// </summary>
    /// <param name="version">The version string (e.g., "101525_QFE5").</param>
    /// <returns>A numeric version suitable for manifest IDs.</returns>
    private static int ParseVersionForManifestId(string version)
    {
        try
        {
            var parts = version.Split(GeneralsOnlineConstants.QfeSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return 0;
            }

            var datePart = parts[0]; // "101525"
            var qfePart = parts[1].Replace("QFE", string.Empty).Replace("qfe", string.Empty); // "5"

            if (!int.TryParse(datePart, out var dateValue) || !int.TryParse(qfePart, out var qfeValue))
            {
                return 0;
            }

            // Combine: 101525 * 10 + 5 = 1015255
            return (dateValue * 10) + qfeValue;
        }
        catch
        {
            return 0;
        }
    }

}
