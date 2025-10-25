using GenHub.Core.Constants;
using GenHub.Core.Extensions.GameInstallations;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Manifest;
using System;

namespace GenHub.Features.GameProfiles.Services;

/// <summary>
/// Service for formatting content items for display in the UI.
/// Follows Single Responsibility Principle by centralizing all display formatting logic.
/// </summary>
public sealed class ContentDisplayFormatter : IContentDisplayFormatter
{
    private const string SteamPublisher = "Steam";
    private const string EaAppPublisher = "EA App";
    private const string FirstDecadePublisher = "The First Decade";
    private const string WinePublisher = "Wine/Proton";
    private const string CdRomPublisher = "CD-ROM";
    private const string RetailPublisher = "Retail Installation";
    private const string GeneralsOnlinePublisher = "GeneralsOnline";
    private const string SuperHackersPublisher = "TheSuperHackers";
    private const string CncLabsPublisher = "CNClabs";
    private const string UnknownPublisher = "Unknown";

    private const string GeneralsShortName = "Generals";
    private const string ZeroHourShortName = "Zero Hour";
    private const string GeneralsFullName = "Command & Conquer: Generals";
    private const string ZeroHourFullName = "Command & Conquer: Generals Zero Hour";

    private const string CommunityPatchIdentifier = "CommunityPatch";
    private const string VersionPrefix = "v";

    /// <inheritdoc/>
    public ContentDisplayItem CreateDisplayItem(ContentManifest manifest, bool isEnabled = false)
    {
        var publisher = GetPublisherFromManifest(manifest);
        var installationType = GetInstallationTypeFromManifest(manifest);
        var normalizedVersion = NormalizeVersion(manifest.Version);
        var displayName = BuildDisplayName(manifest.TargetGame, normalizedVersion, manifest.Name);

        return new ContentDisplayItem
        {
            ManifestId = manifest.Id.Value,
            DisplayName = displayName,
            ContentType = manifest.ContentType,
            GameType = manifest.TargetGame,
            InstallationType = installationType,
            Publisher = publisher,
            Version = normalizedVersion,
            IsEnabled = isEnabled,
        };
    }

    /// <inheritdoc/>
    public ContentDisplayItem CreateDisplayItemFromInstallation(
        GameInstallation installation,
        GameClient gameClient,
        ManifestId manifestId,
        bool isEnabled = false)
    {
        var publisherName = GetPublisherFromInstallationType(installation.InstallationType);
        var normalizedVersion = NormalizeVersion(gameClient.Version);
        var displayName = BuildDisplayName(gameClient.GameType, normalizedVersion);

        return new ContentDisplayItem
        {
            ManifestId = manifestId.Value,
            DisplayName = displayName,
            ContentType = ContentType.GameInstallation,
            GameType = gameClient.GameType,
            InstallationType = installation.InstallationType,
            Publisher = publisherName,
            Version = normalizedVersion,
            IsEnabled = isEnabled,
            SourceId = installation.Id,
            GameClientId = gameClient.Id,
        };
    }

    /// <inheritdoc/>
    public string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var (detectedGameType, hashVersion) = GameClientHashRegistry.GetGameInfoFromHash(version);
        if (detectedGameType != GameType.Unknown && !string.IsNullOrEmpty(hashVersion))
        {
            return hashVersion;
        }

        return version.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase)
            ? version.Substring(VersionPrefix.Length)
            : version;
    }

    /// <inheritdoc/>
    public string BuildDisplayName(GameType gameType, string normalizedVersion, string? name = null)
    {
        var gameShortName = GetGameTypeDisplayName(gameType, useShortName: true);
        var versionDisplay = FormatVersion(normalizedVersion, ContentType.GameInstallation);

        // If name is provided, use it without duplicating version info
        if (!string.IsNullOrWhiteSpace(name))
        {
            // Check if the name already contains version information
            if (name.Contains(normalizedVersion, StringComparison.OrdinalIgnoreCase) ||
                name.Contains($"v{normalizedVersion}", StringComparison.OrdinalIgnoreCase) ||
                name.Contains(versionDisplay, StringComparison.OrdinalIgnoreCase))
            {
                return name; // Name already has version, don't append it
            }

            return $"{name} {versionDisplay}";
        }

        // No name provided, use game type + version
        return $"{gameShortName} {versionDisplay}";
    }

    /// <inheritdoc/>
    public string FormatVersion(string version, ContentType contentType)
    {
        var normalizedVersion = NormalizeVersion(version);

        if (string.IsNullOrWhiteSpace(normalizedVersion))
        {
            return string.Empty;
        }

        return IsCommunityVersion(normalizedVersion)
            ? normalizedVersion
            : $"{VersionPrefix}{normalizedVersion}";
    }

    /// <inheritdoc/>
    public string GetGameTypeDisplayName(GameType gameType, bool useShortName = false)
    {
        if (useShortName)
        {
            return gameType switch
            {
                GameType.ZeroHour => ZeroHourShortName,
                GameType.Generals => GeneralsShortName,
                _ => gameType.ToString(),
            };
        }

        return gameType switch
        {
            GameType.Generals => GeneralsFullName,
            GameType.ZeroHour => ZeroHourFullName,
            _ => gameType.ToString(),
        };
    }

    /// <inheritdoc/>
    public string GetContentTypeDisplayName(ContentType contentType)
    {
        return contentType switch
        {
            ContentType.GameInstallation => "Game Installation",
            ContentType.GameClient => "Game Client",
            ContentType.Mod => "Modification",
            ContentType.Patch => "Patch",
            ContentType.Addon => "Add-on",
            ContentType.MapPack => "Map Pack",
            ContentType.Map => "Map",
            ContentType.Mission => "Mission",
            ContentType.LanguagePack => "Language Pack",
            ContentType.ContentBundle => "Content Bundle",
            _ => contentType.ToString(),
        };
    }

    /// <inheritdoc/>
    public string GetPublisherFromInstallationType(GameInstallationType installationType)
    {
        return installationType switch
        {
            GameInstallationType.Steam => SteamPublisher,
            GameInstallationType.EaApp => EaAppPublisher,
            GameInstallationType.TheFirstDecade => FirstDecadePublisher,
            GameInstallationType.Wine => WinePublisher,
            GameInstallationType.CDISO => CdRomPublisher,
            GameInstallationType.Retail => RetailPublisher,
            _ => UnknownPublisher,
        };
    }

    /// <inheritdoc/>
    public string GetPublisherFromManifest(ContentManifest manifest)
    {
        if (manifest.Publisher?.Name is { } publisherName && !string.IsNullOrWhiteSpace(publisherName))
        {
            return publisherName;
        }

        var lowerName = manifest.Name.ToLowerInvariant();

        if (lowerName.Contains("steam")) return SteamPublisher;
        if (lowerName.Contains("ea") || lowerName.Contains("origin")) return EaAppPublisher;
        if (lowerName.Contains("generalsonline")) return GeneralsOnlinePublisher;
        if (lowerName.Contains("thesuperhackers") || lowerName.Contains("superhacker")) return SuperHackersPublisher;
        if (lowerName.Contains("cnclabs")) return CncLabsPublisher;

        var installationType = GetInstallationTypeFromManifest(manifest);
        return installationType.GetDisplayName();
    }

    /// <inheritdoc/>
    public GameInstallationType GetInstallationTypeFromManifest(ContentManifest manifest)
    {
        var lowerName = manifest.Name.ToLowerInvariant();

        if (lowerName.Contains("steam")) return GameInstallationType.Steam;
        if (lowerName.Contains("ea") || lowerName.Contains("origin")) return GameInstallationType.EaApp;
        if (lowerName.Contains("tfd") || lowerName.Contains("firstdecade")) return GameInstallationType.TheFirstDecade;
        if (lowerName.Contains("wine") || lowerName.Contains("proton")) return GameInstallationType.Wine;

        return GameInstallationType.Retail;
    }

    private static bool IsCommunityVersion(string version)
    {
        return version.Contains(GeneralsOnlinePublisher, StringComparison.OrdinalIgnoreCase) ||
               version.Contains(CommunityPatchIdentifier, StringComparison.OrdinalIgnoreCase);
    }
}
