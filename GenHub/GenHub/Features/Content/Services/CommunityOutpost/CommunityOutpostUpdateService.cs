using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Constants;
using GenHub.Core.Helpers;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Results.Content;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services.CommunityOutpost;

/// <summary>
/// Background service that periodically checks for new Community Outpost patches.
/// </summary>
public class CommunityOutpostUpdateService
    : ContentUpdateServiceBase,
      ICommunityOutpostUpdateService,
      IRecipient<ContentAcquiredMessage>,
      IRecipient<ReconciliationCompletedEvent>,
      IRecipient<ContentRemovingEvent>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(5);

    private readonly CommunityOutpostDiscoverer _discoverer;
    private readonly CommunityOutpostResolver _resolver;
    private readonly IContentManifestPool _manifestPool;
    private readonly IContentVersionComparer _versionComparer;
    private readonly ILogger<CommunityOutpostUpdateService> _logger;

    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private ContentUpdateCheckResult? _cachedResult;
    private DateTimeOffset _lastCheckTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityOutpostUpdateService"/> class.
    /// </summary>
    /// <param name="discoverer">Content discoverer.</param>
    /// <param name="resolver">Content resolver.</param>
    /// <param name="manifestPool">Manifest pool.</param>
    /// <param name="versionComparer">Publisher-aware version comparer.</param>
    /// <param name="logger">Logger instance.</param>
    public CommunityOutpostUpdateService(
        CommunityOutpostDiscoverer discoverer,
        CommunityOutpostResolver resolver,
        IContentManifestPool manifestPool,
        IContentVersionComparer versionComparer,
        ILogger<CommunityOutpostUpdateService> logger)
        : base(logger)
    {
        _discoverer = discoverer;
        _resolver = resolver;
        _manifestPool = manifestPool;
        _versionComparer = versionComparer;
        _logger = logger;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <inheritdoc/>
    protected override string ServiceName => CommunityOutpostConstants.PublisherName;

    /// <inheritdoc/>
    protected override TimeSpan UpdateCheckInterval => TimeSpan.FromDays(1);

    /// <inheritdoc/>
    public void InvalidateCache()
    {
        _checkLock.Wait();
        try
        {
            _cachedResult = null;
            _lastCheckTime = DateTimeOffset.MinValue;
            _logger.LogDebug("[CO UpdateService] Update check cache invalidated");
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>
    /// Handles content acquisition messages to invalidate stale update caches.
    /// </summary>
    public void Receive(ContentAcquiredMessage message)
    {
        InvalidateCache();
    }

    /// <summary>
    /// Handles reconciliation completion events to invalidate stale update caches.
    /// </summary>
    public void Receive(ReconciliationCompletedEvent message)
    {
        InvalidateCache();
    }

    /// <summary>
    /// Handles content removal events to invalidate stale update caches.
    /// </summary>
    public void Receive(ContentRemovingEvent message)
    {
        InvalidateCache();
    }

    /// <inheritdoc/>
    public override async Task<ContentUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var validDuration = _cachedResult?.Success == true ? CacheDuration : FailureCooldown;
            if (_cachedResult != null && (now - _lastCheckTime) < validDuration)
            {
                _logger.LogDebug("[CO UpdateService] Returning cached update check result (age: {Age})", now - _lastCheckTime);
                return _cachedResult;
            }

            var result = await CheckForUpdatesInternalAsync(cancellationToken);
            _cachedResult = result;
            _lastCheckTime = DateTimeOffset.UtcNow;
            return result;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _checkLock.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ContentUpdateCheckResult> CheckForUpdatesInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for Community Outpost patch updates...");

            // Discover latest content
            var discoveryResult = await _discoverer.DiscoverAsync(new ContentSearchQuery(), cancellationToken);
            if (!discoveryResult.Success || discoveryResult.Data?.Items == null || !discoveryResult.Data.Items.Any())
            {
                _logger.LogWarning("No Community Outpost content discovered");
                return ContentUpdateCheckResult.CreateNoUpdateAvailable();
            }

            // Get currently installed manifests from this publisher
            var manifestsResult = await _manifestPool.GetAllManifestsAsync(cancellationToken);
            var installedManifests = (manifestsResult.Data ?? [])
                .Where(m => m.Publisher?.PublisherType == CommunityOutpostConstants.PublisherType)
                .ToList();

            if (installedManifests.Count == 0)
            {
                _logger.LogInformation("No Community Outpost content installed. No updates possible.");
                return ContentUpdateCheckResult.CreateNoUpdateAvailable();
            }

            // Check if any installed manifest has a newer version in the catalog
            ContentSearchResult? latestToResolve = null;
            string? currentVersionAtLatest = null;

            foreach (var discovered in discoveryResult.Data.Items)
            {
                var installed = installedManifests.FirstOrDefault(m =>
                    m.Id.Value.Equals(discovered.Id, StringComparison.OrdinalIgnoreCase));

                if (installed != null &&
                    _versionComparer.IsNewer(discovered.Version, installed.Version, CommunityOutpostConstants.PublisherType) &&
                    (latestToResolve == null || _versionComparer.IsNewer(discovered.Version, latestToResolve.Version, CommunityOutpostConstants.PublisherType)))
                {
                    _logger.LogInformation("Newer update candidate found for {Id}: {OldVersion} -> {NewVersion}", discovered.Id, installed.Version, discovered.Version);
                    latestToResolve = discovered;
                    currentVersionAtLatest = installed.Version;
                }
            }

            if (latestToResolve == null)
            {
                _logger.LogInformation("All installed Community Outpost content is up to date");
                return ContentUpdateCheckResult.CreateNoUpdateAvailable(installedManifests.FirstOrDefault()?.Version);
            }

            var latestVersion = latestToResolve.Version;
            _logger.LogInformation("Update available: {Id} version {Version}", latestToResolve.Id, latestVersion);

            // Resolve new content to manifest for verification
            var resolveResult = await _resolver.ResolveAsync(latestToResolve, cancellationToken);

            if (!resolveResult.Success || resolveResult.Data == null)
            {
                _logger.LogError("Failed to resolve Community Outpost content: {Error}", resolveResult.FirstError);
                return ContentUpdateCheckResult.CreateFailure($"Failed to resolve: {resolveResult.FirstError}", currentVersionAtLatest);
            }

            return ContentUpdateCheckResult.CreateUpdateAvailable(
                latestVersion,
                currentVersionAtLatest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for Community Outpost updates");
            return ContentUpdateCheckResult.CreateFailure(ex.Message);
        }
    }
}
