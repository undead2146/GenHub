using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private ReadOnlyCollection<GameInstallation>? _cachedInstallations;
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
            return OperationResult<GameInstallation>.CreateFailure(initResult.Errors[0]);
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
                // Even if detection failing (e.g. no auto-detect), we might have manual installs.
                // But TryInitializeCacheAsync returns Failure if detect fails?
                // Looking at TryInitializeCacheAsync, it returns Failure if detection error.
                // But we should probably allow failure if we have a cache?
                // For now, let's stick to existing logic but maybe we need to be careful.
                // If init failed, we might still want to proceed if we manually injected?
                // But let's assume detection usually succeeds (returns empty list if none found?).
                // "Failed to detect" usually means exception.

                // If cache is null, we can't do much.
                if (_cachedInstallations == null)
                {
                     return OperationResult<IReadOnlyList<GameInstallation>>.CreateFailure(initResult.Errors[0]);
                }
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

    /// <inheritdoc/>
    public async Task<OperationResult<bool>> RegisterManualInstallationAsync(GameInstallation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Ensure cache is initialized (or at least exists)
            if (_cachedInstallations == null)
            {
                // Try to initialize standard way first
                try
                {
                    // logic from TryInitializeCacheAsync but without the lock since we hold it
                    var detectionResult = await _detectionOrchestrator.DetectAllInstallationsAsync(cancellationToken);
                    if (detectionResult.Success)
                    {
                        var installations = detectionResult.Items.ToList();
                        await PopulateGameClientsAndManifestsAsync(installations, cancellationToken);
                        _cachedInstallations = installations.AsReadOnly();
                    }
                    else
                    {
                        // Fallback to empty list
                        _cachedInstallations = new List<GameInstallation>().AsReadOnly();
                    }
                }
                catch
                {
                     _cachedInstallations = new List<GameInstallation>().AsReadOnly();
                }
            }

            var currentList = _cachedInstallations.ToList();

            // Update existing or add new
            var existingIndex = currentList.FindIndex(i => i.Id == installation.Id);
            if (existingIndex >= 0)
            {
                currentList[existingIndex] = installation;
            }
            else
            {
                currentList.Add(installation);
            }

            _cachedInstallations = currentList.AsReadOnly();

            _logger.LogInformation("Registered manual installation: {Id} ({Path})", installation.Id, installation.InstallationPath);

            return OperationResult<bool>.CreateSuccess(true);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc/>
    public void InvalidateCache()
    {
        _cacheLock.Wait();
        try
        {
            _cachedInstallations = null;
            _logger.LogInformation("Installation cache invalidated");
        }
        finally
        {
            _cacheLock.Release();
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
    /// Populates the AvailableGameClients for a detected installation.
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

        // Create and register GameInstallation manifests to the pool
        if (_manifestGenerationService != null && _contentManifestPool != null)
        {
            foreach (var installation in installations)
            {
                await CreateAndRegisterInstallationManifestsAsync(installation, cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning("ManifestGenerationService or ContentManifestPool not available, skipping GameInstallation manifest registration");
        }
    }

    /// <summary>
    /// Creates and registers GameInstallation manifests for an installation.
    /// </summary>
    /// <param name="installation">The installation to create manifests for.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateAndRegisterInstallationManifestsAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        // Create manifest for Generals if it exists
        if (installation.HasGenerals && !string.IsNullOrEmpty(installation.GeneralsPath))
        {
            await CreateAndRegisterSingleInstallationManifestAsync(
                installation,
                GameType.Generals,
                installation.GeneralsPath,
                cancellationToken);
        }

        // Create manifest for Zero Hour if it exists
        if (installation.HasZeroHour && !string.IsNullOrEmpty(installation.ZeroHourPath))
        {
            await CreateAndRegisterSingleInstallationManifestAsync(
                installation,
                GameType.ZeroHour,
                installation.ZeroHourPath,
                cancellationToken);
        }
    }

    /// <summary>
    /// Creates and registers a single GameInstallation manifest.
    /// </summary>
    /// <param name="installation">The installation.</param>
    /// <param name="gameType">The game type (Generals or ZeroHour).</param>
    /// <param name="installationPath">The path to the game installation.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateAndRegisterSingleInstallationManifestAsync(
        GameInstallation installation,
        GameType gameType,
        string installationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find a base game client for this game type to determine version
            var baseGameClient = installation.AvailableGameClients
                .FirstOrDefault(c => c.GameType == gameType && !c.IsPublisherClient);

            if (baseGameClient == null)
            {
                _logger.LogWarning(
                    "No base game client found for {GameType} in installation {InstallationId}, skipping GameInstallation manifest creation",
                    gameType,
                    installation.Id);
                return;
            }

            // Determine version for manifest
            var version = baseGameClient.Version;
            if (string.IsNullOrEmpty(version) ||
                version.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                version.Equals("Auto-Updated", StringComparison.OrdinalIgnoreCase) ||
                version.Equals(GameClientConstants.AutoDetectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                version = gameType == GameType.ZeroHour
                    ? ManifestConstants.ZeroHourManifestVersion
                    : ManifestConstants.GeneralsManifestVersion;
            }

            // Create the GameInstallation manifest
            var manifestBuilder = await _manifestGenerationService!.CreateGameInstallationManifestAsync(
                installationPath,
                gameType,
                installation.InstallationType,
                version);

            var manifest = manifestBuilder.Build();

            // Register the manifest to the pool
            var addResult = await _contentManifestPool!.AddManifestAsync(manifest, installationPath, null, cancellationToken);

            if (addResult.Success)
            {
                _logger.LogInformation(
                    "Registered GameInstallation manifest {ManifestId} for {GameType} in installation {InstallationId}",
                    manifest.Id,
                    gameType,
                    installation.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to register GameInstallation manifest for {GameType} in installation {InstallationId}: {Errors}",
                    gameType,
                    installation.Id,
                    string.Join(", ", addResult.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating GameInstallation manifest for {GameType} in installation {InstallationId}",
                gameType,
                installation.Id);
        }
    }
}
