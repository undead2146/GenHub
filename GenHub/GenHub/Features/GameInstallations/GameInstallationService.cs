using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GameClients;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenHub.Features.GameInstallations;

/// <summary>
/// Provides services for managing game installations with content manifest generation.
/// </summary>
/// <remarks>
/// Integrates with <see cref="IManifestGenerationService"/> to automatically generate
/// content manifests for detected installations and populate their AvailableClients.
/// </remarks>
public class GameInstallationService(
    IGameInstallationDetectionOrchestrator detectionOrchestrator,
    IGameClientDetectionOrchestrator clientOrchestrator,
    IManifestGenerationService? manifestGenerationService = null,
    IContentManifestPool? contentManifestPool = null,
    ILogger<GameInstallationService>? logger = null) : IGameInstallationService, IDisposable
{
    private readonly IGameInstallationDetectionOrchestrator _detectionOrchestrator = detectionOrchestrator ?? throw new ArgumentNullException(nameof(detectionOrchestrator));
    private readonly IGameClientDetectionOrchestrator _clientOrchestrator = clientOrchestrator ?? throw new ArgumentNullException(nameof(clientOrchestrator));
    private readonly IManifestGenerationService? _manifestGenerationService = manifestGenerationService;
    private readonly IContentManifestPool? _contentManifestPool = contentManifestPool;
    private readonly ILogger<GameInstallationService> _logger = logger ?? NullLogger<GameInstallationService>.Instance;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private IReadOnlyList<GameInstallation>? _cachedInstallations;
    private bool _disposed = false;

    /// <summary>
    /// Gets a game installation by its ID.
    /// </summary>
    /// <param name="installationId">The installation ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="OperationResult{GameInstallation}"/> containing the installation or error.</returns>
    public async Task<OperationResult<GameInstallation>> GetInstallationAsync(string installationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installationId))
        {
            return OperationResult<GameInstallation>.CreateFailure("Installation ID cannot be null or empty.");
        }

        var initResult = await TryInitializeCacheAsync(cancellationToken);
        if (!initResult.Success)
        {
            return OperationResult<GameInstallation>.CreateFailure(initResult.Errors.First());
        }

        if (_cachedInstallations == null)
        {
            return OperationResult<GameInstallation>.CreateFailure("Failed to detect game installations.");
        }

        var installation = _cachedInstallations.FirstOrDefault(i => i.Id == installationId);

        if (installation == null)
        {
            return OperationResult<GameInstallation>.CreateFailure($"Game installation with ID '{installationId}' not found.");
        }

        return OperationResult<GameInstallation>.CreateSuccess(installation);
    }

    /// <summary>
    /// Gets all available game installations.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="OperationResult{T}"/> containing all installations or error.</returns>
    public async Task<OperationResult<IReadOnlyList<GameInstallation>>> GetAllInstallationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var initResult = await TryInitializeCacheAsync(cancellationToken);
            if (!initResult.Success)
            {
                return OperationResult<IReadOnlyList<GameInstallation>>.CreateFailure(initResult.Errors.First());
            }

            if (_cachedInstallations == null)
            {
                return OperationResult<IReadOnlyList<GameInstallation>>.CreateFailure("Failed to detect game installations.");
            }

            return OperationResult<IReadOnlyList<GameInstallation>>.CreateSuccess(_cachedInstallations);
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<GameInstallation>>.CreateFailure($"Error retrieving installations: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases resources used by the <see cref="GameInstallationService"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="GameInstallationService"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cacheLock?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Parses a version string into an integer for manifest ID generation.
    /// Examples: "1.08" -> 108, "1.04" -> 104, "2.0" -> 200, "5" -> 5, null -> 0.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The parsed integer version.</returns>
    private static int ParseVersionStringToInt(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return 0;

        if (version.Contains('.'))
        {
            var parts = version.Split('.');
            if (parts.Length == 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                return (major * 100) + minor;
            }
        }
        else if (int.TryParse(version, out int parsed))
        {
            return parsed;
        }

        return 0;
    }

    /// <summary>
    /// Generates and pools a content manifest for a specific game type within an installation.
    /// </summary>
    /// <param name="installation">The game installation.</param>
    /// <param name="gameType">The type of game (Generals or ZeroHour).</param>
    /// <param name="gamePath">The path to the game directory.</param>
    /// <param name="gameClient">The detected game client, if available.</param>
    /// <param name="defaultManifestVersion">The default manifest version if detection fails.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task GenerateAndPoolManifestForGameTypeAsync(
        GameInstallation installation,
        GameType gameType,
        string gamePath,
        GameClient? gameClient,
        string defaultManifestVersion,
        CancellationToken cancellationToken)
    {
        var detectedVersion = gameClient?.Version;
        int versionForId;
        string versionForManifest;

        if (string.IsNullOrEmpty(detectedVersion) ||
            detectedVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            versionForId = 0;
            versionForManifest = defaultManifestVersion;
        }
        else
        {
            versionForId = ParseVersionStringToInt(detectedVersion);
            versionForManifest = detectedVersion;
        }

        var idResult = ManifestIdGenerator.GenerateGameInstallationId(
            installation, gameType, versionForId);
        var manifestId = ManifestId.Create(idResult);

        var existingManifest = await _contentManifestPool!.GetManifestAsync(
            manifestId, cancellationToken);

        if (existingManifest.Success && existingManifest.Data != null)
        {
            _logger.LogDebug(
                "Manifest {Id} already exists in pool, skipping generation",
                manifestId);
            return;
        }

        var manifestBuilder = await _manifestGenerationService!
            .CreateGameInstallationManifestAsync(
                gamePath,
                gameType,
                installation.InstallationType,
                versionForManifest);

        var manifest = manifestBuilder.Build();
        manifest.ContentType = ContentType.GameInstallation;
        manifest.Id = manifestId;

        var addResult = await _contentManifestPool!.AddManifestAsync(
            manifest, gamePath);

        if (addResult.Success)
        {
            _logger.LogDebug(
                "Pooled GameInstallation manifest {Id} for {InstallationId} ({GameType})",
                manifest.Id,
                installation.Id,
                gameType);
        }
        else
        {
            _logger.LogWarning(
                "Failed to pool {GameType} GameInstallation manifest for {InstallationId}: {Errors}",
                gameType,
                installation.Id,
                string.Join(", ", addResult.Errors));
        }
    }

    /// <summary>
    /// Attempts to initialize the installation cache if not already initialized.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing a result indicating success/failure.</returns>
    private async Task<OperationResult<bool>> TryInitializeCacheAsync(CancellationToken cancellationToken)
    {
        if (_cachedInstallations != null)
        {
            return OperationResult<bool>.CreateSuccess(true);
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedInstallations != null)
            {
                return OperationResult<bool>.CreateSuccess(true);
            }

            var detectionResult = await _detectionOrchestrator.DetectAllInstallationsAsync(cancellationToken);
            if (detectionResult.Success)
            {
                var installations = detectionResult.Items.ToList();

                // Generate manifests and populate AvailableVersions for each installation
                await PopulateGameClientsAndManifestsAsync(installations, cancellationToken);

                _cachedInstallations = installations.AsReadOnly();

                _logger.LogInformation(
                    "Initialized installation cache with {Count} installations",
                    _cachedInstallations.Count);

                return OperationResult<bool>.CreateSuccess(true);
            }
            else
            {
                return OperationResult<bool>.CreateFailure(
                    $"Failed to detect game installations: {string.Join(", ", detectionResult.Errors)}");
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Populates the AvailableGameClients for a detected installation by generating content manifests.
    /// </summary>
    /// <param name="installations">The installations to populate clients for.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PopulateGameClientsAndManifestsAsync(List<GameInstallation> installations, CancellationToken cancellationToken)
    {
        foreach (var installation in installations)
        {
            installation.Fetch();
        }

        var clientResult = await _clientOrchestrator.DetectGameClientsFromInstallationsAsync(installations, cancellationToken);
        if (!clientResult.Success)
        {
            _logger.LogWarning("Client detection failed: {Errors}", string.Join(", ", clientResult.Errors));
            return;
        }

        var clientsByInstallation = clientResult.Items.GroupBy(v => v.InstallationId);
        foreach (var installation in installations)
        {
            var installationClients = clientsByInstallation.FirstOrDefault(g => g.Key == installation.Id)?.ToList() ?? Enumerable.Empty<GameClient>();
            installation.PopulateGameClients(installationClients);
            _logger.LogDebug("Populated {ClientCount} clients for installation {Id}", installationClients.Count(), installation.Id);
        }

        if (_manifestGenerationService == null || _contentManifestPool == null)
        {
            _logger.LogDebug("Manifest generation skipped: services not available");
            return;
        }

        foreach (var installation in installations)
        {
            var gameDir = installation.InstallationPath;
            if (!Directory.Exists(gameDir)) continue;

            if (installation.HasGenerals && !string.IsNullOrEmpty(installation.GeneralsPath) && Directory.Exists(installation.GeneralsPath))
            {
                await GenerateAndPoolManifestForGameTypeAsync(
                    installation,
                    GameType.Generals,
                    installation.GeneralsPath,
                    installation.GeneralsClient,
                    ManifestConstants.GeneralsManifestVersion,
                    cancellationToken);
            }

            if (installation.HasZeroHour && !string.IsNullOrEmpty(installation.ZeroHourPath) && Directory.Exists(installation.ZeroHourPath))
            {
                await GenerateAndPoolManifestForGameTypeAsync(
                    installation,
                    GameType.ZeroHour,
                    installation.ZeroHourPath,
                    installation.ZeroHourClient,
                    ManifestConstants.ZeroHourManifestVersion,
                    cancellationToken);
            }
        }
    }
}