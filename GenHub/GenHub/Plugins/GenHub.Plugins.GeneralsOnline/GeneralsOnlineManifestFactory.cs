using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace GenHub.Plugins.GeneralsOnline;

/// <summary>
/// Factory for creating and updating Generals Online ContentManifests.
/// </summary>
public static class GeneralsOnlineManifestFactory
{
    /// <summary>
    /// Creates a ContentManifest from a GeneralsOnlineRelease.
    /// This creates the initial manifest with download URLs.
    /// </summary>
    /// <param name="release">The GeneralsOnlineRelease to create the manifest from.</param>
    /// <returns>A new ContentManifest instance based on the provided release.</returns>
    public static ContentManifest CreateManifest(GenHub.Plugins.GeneralsOnline.Models.GeneralsOnlineRelease release)
    {
        var manifestId = ManifestId.Create(ManifestIdGenerator.GeneratePublisherContentId(
            "generalsonline",
            ContentType.Mod,
            "GeneralsOnline",
            0,
            string.Empty));

        return new ContentManifest
        {
            Id = manifestId,
            Name = "Generals Online",
            Version = release.Version,
            ContentType = ContentType.Mod,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo
            {
                Name = "Generals Online Team",
                PublisherType = "GeneralsOnline",
                Website = "https://www.playgenerals.online/",
                SupportUrl = "https://discord.playgenerals.online/",
                ContentIndexUrl = "https://www.playgenerals.online/#download",
                UpdateCheckIntervalHours = 24,
            },
            Metadata = new ContentMetadata
            {
                Description = "Community-driven multiplayer service for C&C Generals Zero Hour",
                ReleaseDate = release.ReleaseDate,
                IconUrl = "https://www.playgenerals.online/logo.png",
                Tags = new List<string> { "multiplayer", "online", "community", "enhancement" },
                ChangelogUrl = release.Changelog,
            },
            Files = new List<ManifestFile>
            {
                new ManifestFile
                {
                    RelativePath = System.IO.Path.GetFileName(release.PortableUrl),
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
                    Name = "Command & Conquer: Generals Zero Hour",
                    DependencyType = ContentType.GameInstallation,
                    MinVersion = "1.04",
                },
            },
        };
    }

    /// <summary>
    /// Updates the manifest with extracted file information.
    /// Computes SHA-256 hashes for all files for CAS integration.
    /// </summary>
    /// <param name="manifest">The original ContentManifest to update.</param>
    /// <param name="extractPath">The path to the directory containing extracted files.</param>
    /// <param name="logger">The logger for logging progress and errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>An updated ContentManifest with file hashes and details.</returns>
    public static async Task<ContentManifest> UpdateManifestWithExtractedFiles(
        ContentManifest manifest,
        string extractPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating manifest with extracted files from: {Path}", extractPath);

        var files = new List<ManifestFile>();
        var allFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);

        logger.LogInformation("Processing {Count} files", allFiles.Length);

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

            files.Add(new ManifestFile
            {
                RelativePath = relativePath,
                Size = fileInfo.Length,
                Hash = hash,
                SourceType = ContentSourceType.ContentAddressable,
                SourcePath = filePath,
                IsExecutable = IsExecutableFile(relativePath),
            });

            logger.LogDebug("Processed file: {File} ({Size} bytes, hash: {Hash})", relativePath, fileInfo.Length, hash[..8]);
        }

        logger.LogInformation("Manifest updated with {Count} files", files.Count);

        return new ContentManifest
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            ContentType = manifest.ContentType,
            TargetGame = manifest.TargetGame,
            Publisher = manifest.Publisher,
            Metadata = manifest.Metadata,
            Files = files,
            Dependencies = manifest.Dependencies,
        };
    }

    /// <summary>
    /// Determines if a file is an executable based on its extension.
    /// </summary>
    /// <param name="relativePath">The relative path of the file.</param>
    /// <returns>True if the file is an executable, false otherwise.</returns>
    private static bool IsExecutableFile(string relativePath)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension is ".exe" or ".bat" or ".sh" or ".cmd";
    }
}
