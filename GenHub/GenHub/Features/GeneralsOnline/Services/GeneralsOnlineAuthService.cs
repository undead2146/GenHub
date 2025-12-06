using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GeneralsOnline;
using GenHub.Core.Models.GeneralsOnline;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GeneralsOnline.Services;

/// <summary>
/// Service for managing Generals Online authentication state.
/// Monitors the credentials file and provides reactive updates.
/// </summary>
/// <param name="credentialsStorage">The credentials storage service.</param>
/// <param name="logger">The logger instance.</param>
public class GeneralsOnlineAuthService(
    ICredentialsStorageService credentialsStorage,
    ILogger<GeneralsOnlineAuthService> logger) : IGeneralsOnlineAuthService, IDisposable
{
    private readonly BehaviorSubject<bool> _isAuthenticatedSubject = new(false);
    private FileSystemWatcher? _fileWatcher;
    private IDisposable? _fileChangeSubscription;
    private CredentialsModel? _currentCredentials;
    private bool _disposed;

    /// <inheritdoc />
    public IObservable<bool> IsAuthenticated => _isAuthenticatedSubject.AsObservable();

    /// <inheritdoc />
    public string? CurrentToken => _currentCredentials?.RefreshToken;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            await ValidateCredentialsAsync();
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize GeneralsOnlineAuthService");
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync()
    {
        try
        {
            var credentials = await credentialsStorage.LoadCredentialsAsync().ConfigureAwait(false);

            if (credentials == null)
            {
                logger.LogDebug("No valid credentials found");
                UpdateAuthState(null);
                return false;
            }

            if (!credentials.IsValid())
            {
                logger.LogWarning("Loaded credentials are invalid");
                UpdateAuthState(null);
                return false;
            }

            logger.LogInformation("Successfully validated credentials");
            UpdateAuthState(credentials);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating credentials file");
            UpdateAuthState(null);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTokenWithServerAsync(string token)
    {
        // TODO: Implement actual API call to verify token endpoint.
        // For now, we assume valid if non-empty to avoid circular dependencies.
        await Task.Delay(100).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <inheritdoc />
    public Task<string?> GetAuthTokenAsync()
    {
        return Task.FromResult(_currentCredentials?.RefreshToken);
    }

    /// <inheritdoc />
    public async Task SaveRefreshTokenAsync(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        try
        {
            var credentials = new CredentialsModel
            {
                RefreshToken = refreshToken,
            };

            await credentialsStorage.SaveCredentialsAsync(credentials).ConfigureAwait(false);
            logger.LogInformation("Successfully saved refresh token");

            // Update auth state immediately
            UpdateAuthState(credentials);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save refresh token");
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _fileWatcher?.Dispose();
            _fileChangeSubscription?.Dispose();
            _isAuthenticatedSubject.Dispose();
        }

        _disposed = true;
    }

    private void SetupFileWatcher()
    {
        try
        {
            var path = credentialsStorage.GetCredentialsPath();
            var directory = Path.GetDirectoryName(path);
            var filename = Path.GetFileName(path);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filename))
            {
                logger.LogError("Invalid credentials path: {Path}", path);
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileWatcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            // Create observable from file system events and throttle to avoid excessive validation calls
            var fileChangedObservable = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _fileWatcher.Changed += h,
                h => _fileWatcher.Changed -= h)
                .Select(e => e.EventArgs)
                .Merge(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => _fileWatcher.Created += h,
                    h => _fileWatcher.Created -= h)
                    .Select(e => e.EventArgs))
                .Merge(Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                    h => _fileWatcher.Renamed += h,
                    h => _fileWatcher.Renamed -= h)
                    .Select(e => e.EventArgs))
                .Merge(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => _fileWatcher.Deleted += h,
                    h => _fileWatcher.Deleted -= h)
                    .Select(e => e.EventArgs));

            // Throttle events to avoid excessive validation calls during file writes
            _fileChangeSubscription = fileChangedObservable
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(async _ => await ValidateCredentialsAsync());

            logger.LogInformation("Started monitoring credentials file at {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to setup file watcher");
        }
    }

    private void UpdateAuthState(CredentialsModel? credentials)
    {
        _currentCredentials = credentials;
        var isAuthenticated = credentials != null && credentials.IsValid();

        if (_isAuthenticatedSubject.Value != isAuthenticated)
        {
            _isAuthenticatedSubject.OnNext(isAuthenticated);
            logger.LogInformation("Authentication state changed: {IsAuthenticated}", isAuthenticated);
        }
    }
}
