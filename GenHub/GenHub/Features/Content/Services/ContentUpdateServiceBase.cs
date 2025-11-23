using GenHub.Core.Interfaces.Content;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Content.Services;

/// <summary>
/// Abstract base class for content update services.
/// Provides common background service implementation for periodic update checking.
/// </summary>
public abstract class ContentUpdateServiceBase(ILogger<ContentUpdateServiceBase> logger) : BackgroundService, IContentUpdateService
{
    /// <summary>
    /// Gets the name of the update service for logging purposes.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Gets the interval between update checks.
    /// </summary>
    protected abstract TimeSpan UpdateCheckInterval { get; }

    /// <inheritdoc />
    public Task<(bool UpdateAvailable, string? LatestVersion, string? CurrentVersion)> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        return CheckForUpdatesImplAsync(cancellationToken);
    }

    /// <summary>
    /// Checks for available content updates (implementation-specific).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing update availability, latest version, and current version.</returns>
    protected abstract Task<(bool UpdateAvailable, string? LatestVersion, string? CurrentVersion)> CheckForUpdatesImplAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes the background service that periodically checks for updates.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service.</param>
    /// <returns>A task representing the background execution.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[{ServiceName}] Update service started. Check interval: {Interval}", ServiceName, UpdateCheckInterval);

        // Initial delay to allow application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("[{ServiceName}] Starting update check", ServiceName);

                var (updateAvailable, latestVersion, currentVersion) = await CheckForUpdatesImplAsync(stoppingToken);

                if (updateAvailable)
                {
                    logger.LogInformation(
                        "[{ServiceName}] Update available: {LatestVersion} (current: {CurrentVersion})",
                        ServiceName,
                        latestVersion ?? "unknown",
                        currentVersion ?? "none");
                }
                else
                {
                    logger.LogDebug(
                        "[{ServiceName}] No updates available. Current version: {CurrentVersion}",
                        ServiceName,
                        currentVersion ?? "none");
                }
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped, exit gracefully
                logger.LogInformation("[{ServiceName}] Update service stopped", ServiceName);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "[{ServiceName}] Update check failed. Will retry in {Interval}",
                    ServiceName,
                    UpdateCheckInterval);
            }

            // Wait for the configured interval before next check
            try
            {
                await Task.Delay(UpdateCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped
                logger.LogInformation("[{ServiceName}] Update service stopped", ServiceName);
                break;
            }
        }

        logger.LogInformation("[{ServiceName}] Update service shutdown complete", ServiceName);
    }
}
