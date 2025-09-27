using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Manifest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenHub.Features.Manifest;

/// <summary>
/// Service for generating content manifests from game installations and content packages.
/// </summary>
/// <remarks>
/// Provides methods to create <see cref="ContentManifest"/> objects for different content types
/// including GameInstallation and GameClient manifests with proper metadata and file references.
/// </remarks>
public class ManifestGenerationService(
    ILogger<ManifestGenerationService> logger,
    IFileHashProvider hashProvider,
    IManifestIdService manifestIdService) : IManifestGenerationService
{
    private readonly ILogger<ManifestGenerationService> _logger = logger;
    private readonly IFileHashProvider _hashProvider = hashProvider;
    private readonly IManifestIdService _manifestIdService = manifestIdService;

    /// <summary>
    /// Creates a manifest builder for a game installation.
    /// </summary>
    /// <param name="gameInstallationPath">Path to the game installation.</param>
    /// <param name="gameType">The game type (Generals, ZeroHour).</param>
    /// <param name="installationType">The installation type (Steam, EaApp).</param>
    /// <param name="manifestVersion">The manifest version (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <returns>A <see cref="Task"/> that returns a configured manifest builder.</returns>
    public async Task<IContentManifestBuilder> CreateGameInstallationManifestAsync(
        string gameInstallationPath,
        GameType gameType,
        GameInstallationType installationType,
        int manifestVersion = 0)
    {
        try
        {
            _logger.LogDebug(
                "Creating GameInstallation manifest for {GameType} at {GameInstallationPath}",
                gameType,
                gameInstallationPath);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(installationType, gameType, manifestVersion)
                .WithContentType(ContentType.GameInstallation, gameType);

            // Add publisher info
            var publisher = new PublisherInfo
            {
                Name = installationType.ToString().ToLowerInvariant(),
                Website = installationType switch
                {
                    GameInstallationType.Steam => "https://store.steampowered.com",
                    GameInstallationType.EaApp => "https://www.ea.com",
                    GameInstallationType.TheFirstDecade => "https://westwood.com",
                    _ => string.Empty,
                },
                SupportUrl = installationType switch
                {
                    GameInstallationType.Steam => "https://help.steampowered.com",
                    GameInstallationType.EaApp => "https://help.ea.com",
                    _ => string.Empty,
                },
            };
            builder.WithPublisher(publisher.Name, publisher.Website, publisher.SupportUrl, string.Empty);

            // Add essential game files
            await AddGameFilesToManifest(builder, gameInstallationPath, gameType);

            _logger.LogInformation(
                "Created GameInstallation manifest for {InstallationType} {GameType} (Publisher: {PublisherName})",
                installationType,
                gameType,
                publisher.Name);

            return builder;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating GameInstallation manifest for {GameType} at {GameInstallationPath}",
                gameType,
                gameInstallationPath);
            throw;
        }
    }

    /// <summary>
    /// Creates a content manifest for a content package.
    /// </summary>
    /// <param name="contentDirectory">Path to the content directory.</param>
    /// <param name="publisherId">The publisher identifier used to deterministically generate the manifest id.</param>
    /// <param name="contentName">Content display name.</param>
    /// <param name="manifestVersion">Manifest version (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="contentType">Type of content (Mod, Patch, Addon, etc).</param>
    /// <param name="targetGame">Target game type.</param>
    /// <param name="dependencies">Dependencies for this content.</param>
    /// <returns>A <see cref="Task"/> that returns a configured manifest builder.</returns>
    public async Task<IContentManifestBuilder> CreateContentManifestAsync(
        string contentDirectory,
        string publisherId,
        string contentName,
        int manifestVersion = 0,
        ContentType contentType = ContentType.Mod,
        GameType targetGame = GameType.Generals,
        params ContentDependency[] dependencies)
    {
        try
        {
            _logger.LogDebug(
                "Creating {ContentType} manifest for {ContentName} at {ContentDirectory} (Publisher: {PublisherId})",
                contentType,
                contentName,
                contentDirectory,
                publisherId);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(publisherId, contentName, manifestVersion)
                .WithContentType(contentType, targetGame);

            // Add dependencies
            foreach (var dependency in dependencies)
            {
                builder.AddDependency(
                    dependency.Id,
                    dependency.Name,
                    dependency.DependencyType,
                    dependency.InstallBehavior);
            }

            await Task.CompletedTask; // Make method async for consistency
            return builder;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating content manifest for {ContentName} at {ContentDirectory}",
                contentName,
                contentDirectory);
            throw;
        }
    }

    /// <summary>
    /// Creates a manifest builder for a standalone game version.
    /// </summary>
    /// <param name="gameDirectory">Path to the standalone game directory.</param>
    /// <param name="publisherId">The publisher identifier used to generate the manifest id.</param>
    /// <param name="gameName">Game version display name.</param>
    /// <param name="manifestVersion">Manifest version (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="executablePath">Path to the main executable.</param>
    /// <returns>A <see cref="Task"/> that returns a configured manifest builder.</returns>
    public async Task<IContentManifestBuilder> CreateGameClientManifestAsync(
        string gameDirectory,
        string publisherId,
        string gameName,
        int manifestVersion = 0,
        string executablePath = "")
    {
        try
        {
            _logger.LogDebug(
                "Creating GameClient manifest for {GameName} at {GameDirectory}",
                gameName,
                gameDirectory);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(publisherId, gameName, manifestVersion)
                .WithContentType(ContentType.GameClient, GameType.Generals); // TODO: Determine game type dynamically

            await Task.CompletedTask; // Make method async for consistency
            return builder;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating game version manifest for {GameName} at {GameDirectory}",
                gameName,
                gameDirectory);
            throw;
        }
    }

    /// <summary>
    /// Creates a content bundle from multiple content items.
    /// </summary>
    /// <param name="publisherId">The publisher identifier used to generate the bundle id.</param>
    /// <param name="bundleName">The bundle name.</param>
    /// <param name="manifestVersion">The manifest version (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="publisher">The publisher information.</param>
    /// <param name="items">The bundle items.</param>
    /// <returns>A <see cref="Task"/> that returns the created <see cref="ContentBundle"/>.</returns>
    public async Task<ContentBundle> CreateContentBundleAsync(
        string publisherId,
        string bundleName,
        int manifestVersion = 0,
        PublisherInfo? publisher = null,
        params BundleItem[] items)
    {
        try
        {
            _logger.LogDebug("Creating content bundle {BundleName} version {ManifestVersion}", bundleName, manifestVersion);

            var bundleId = ManifestId.Create($"{manifestVersion}.0.bundle.{bundleName.ToLowerInvariant().Replace(" ", string.Empty)}");

            var bundle = new ContentBundle
            {
                Id = bundleId,
                Name = bundleName,
                Version = manifestVersion.ToString(),
                Items = items.ToList(),
            };

            return await Task.FromResult(bundle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content bundle {BundleName}", bundleName);
            throw;
        }
    }

    /// <summary>
    /// Creates a publisher referral manifest with a deterministic id.
    /// </summary>
    /// <param name="publisherId">The publisher identifier used to generate the referral id.</param>
    /// <param name="referralName">Display name for the referral.</param>
    /// <param name="manifestVersion">Manifest version (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="targetPublisherId">The target publisher id being referred to.</param>
    /// <param name="referralUrl">The URL for the referral.</param>
    /// <param name="description">Optional description for the referral.</param>
    /// <returns>A <see cref="Task"/> that returns the created <see cref="ContentManifest"/>.</returns>
    public async Task<ContentManifest> CreatePublisherReferralAsync(
        string publisherId,
        string referralName,
        int manifestVersion = 0,
        string targetPublisherId = "",
        string referralUrl = "",
        string description = "")
    {
        try
        {
            _logger.LogDebug(
                "Creating publisher referral {ReferralName} for {TargetPublisherId}",
                referralName,
                targetPublisherId);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(publisherId, referralName, manifestVersion)
                .WithContentType(ContentType.PublisherReferral, GameType.Generals) // Default to Generals
                .WithMetadata(description);

            return await Task.FromResult(builder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating publisher referral {ReferralName}",
                referralName);
            throw;
        }
    }

    /// <summary>
    /// Creates a content referral.
    /// </summary>
    /// <param name="publisherId">The publisher identifier.</param>
    /// <param name="referralName">The referral name.</param>
    /// <param name="manifestVersion">The manifest version.</param>
    /// <param name="targetContentId">The target content ID.</param>
    /// <param name="targetPublisherId">The target publisher ID.</param>
    /// <param name="referralUrl">The referral URL.</param>
    /// <param name="description">The description.</param>
    /// <returns>A <see cref="ContentManifest"/> for the referral.</returns>
    public async Task<ContentManifest> CreateContentReferralAsync(
        string publisherId,
        string referralName,
        int manifestVersion,
        string targetContentId,
        string targetPublisherId,
        string referralUrl,
        string description)
    {
        try
        {
            _logger.LogDebug(
                "Creating content referral {ReferralName} for {TargetContentId}",
                referralName,
                targetContentId);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(publisherId, referralName, manifestVersion)
                .WithContentType(ContentType.ContentReferral, GameType.Generals) // Default to Generals
                .WithMetadata(description);

            return await Task.FromResult(builder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating content referral {ReferralName}",
                referralName);
            throw;
        }
    }

    /// <summary>
    /// Saves a manifest to the specified output path.
    /// </summary>
    /// <param name="manifest">The manifest to save.</param>
    /// <param name="outputPath">The output path.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveManifestAsync(ContentManifest manifest, string outputPath)
    {
        try
        {
            _logger.LogDebug("Saving manifest {ManifestId} to {OutputPath}", manifest.Id, outputPath);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            await using var stream = File.Create(outputPath);
            await JsonSerializer.SerializeAsync(stream, manifest, options);

            _logger.LogInformation("Manifest {ManifestId} saved to {OutputPath}", manifest.Id, outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save manifest {ManifestId} to {OutputPath}", manifest.Id, outputPath);
            throw;
        }
    }

    /// <summary>
    /// Creates a manifest builder for a game client.
    /// </summary>
    /// <param name="installationPath">Path to the game client installation.</param>
    /// <param name="gameType">The game type (Generals, ZeroHour).</param>
    /// <param name="clientName">The name of the game client.</param>
    /// <param name="clientVersion">The version of the game client.</param>
    /// <returns>A <see cref="Task"/> that returns a configured manifest builder.</returns>
    public async Task<IContentManifestBuilder> CreateGameClientManifestAsync(
        string installationPath,
        GameType gameType,
        string clientName,
        string clientVersion)
    {
        try
        {
            _logger.LogDebug("Creating GameClient manifest for {ClientName} at {InstallationPath}", clientName, installationPath);

            var builderLogger = NullLogger<ContentManifestBuilder>.Instance;
            var publisherName = clientName.ToLowerInvariant().Contains("steam") ? "steam" :
                                clientName.ToLowerInvariant().Contains("ea") ? "eaapp" : "retail";
            var publisher = new PublisherInfo { Name = publisherName };
            var contentName = gameType.ToString().ToLowerInvariant() + "-client";
            var builder = new ContentManifestBuilder(builderLogger, _hashProvider, _manifestIdService)
                .WithBasicInfo(publisher, contentName, 1)
                .WithContentType(ContentType.GameClient, gameType);

            await AddClientFilesToManifest(builder, installationPath, gameType);

            _logger.LogInformation("Created GameClient manifest for {ClientName} (Publisher: {PublisherName})", clientName, publisher.Name);

            return builder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GameClient manifest for {ClientName} at {InstallationPath}", clientName, installationPath);
            throw;
        }
    }

    /// <summary>
    /// Adds game files to a manifest builder.
    /// </summary>
    /// <param name="builder">The manifest builder.</param>
    /// <param name="installationPath">The installation path.</param>
    /// <param name="gameType">The game type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AddGameFilesToManifest(IContentManifestBuilder builder, string installationPath, GameType gameType)
    {
        try
        {
            // Add essential executable files
            var executableName = gameType == GameType.Generals ? "generals.exe" : "game.exe";
            var executablePath = Path.Combine(installationPath, executableName);

            if (File.Exists(executablePath))
            {
                await builder.AddGameInstallationFileAsync(executableName, executablePath, true);
            }

            // Add common game files
            var commonFiles = new[]
            {
                "*.exe",
                "*.dat",
                "*.ini",
                "*.cfg",
            };

            foreach (var pattern in commonFiles)
            {
                try
                {
                    var files = Directory.GetFiles(installationPath, pattern, SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var relativePath = Path.GetFileName(file);
                        await builder.AddGameInstallationFileAsync(relativePath, file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate files with pattern {Pattern} at {InstallationPath}", pattern, installationPath);
                }
            }

            _logger.LogDebug("Added game files to manifest for {GameType} at {InstallationPath}", gameType, installationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding game files to manifest");
        }
    }

    /// <summary>
    /// Adds client files to a manifest builder.
    /// </summary>
    /// <param name="builder">The manifest builder.</param>
    /// <param name="installationPath">The installation path.</param>
    /// <param name="gameType">The game type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AddClientFilesToManifest(IContentManifestBuilder builder, string installationPath, GameType gameType)
    {
        try
        {
            // Add client-specific configuration files
            var configFiles = new[]
            {
                "options.ini",
                "skirmish.ini",
                "network.ini",
            };

            foreach (var configFile in configFiles)
            {
                var configPath = Path.Combine(installationPath, configFile);
                if (File.Exists(configPath))
                {
                    await builder.AddGameInstallationFileAsync(configFile, configPath);
                }
            }

            _logger.LogDebug("Added client files to manifest for {GameType} at {InstallationPath}", gameType, installationPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding client files to manifest");
        }
    }
}
