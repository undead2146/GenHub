using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GeneralsOnline;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GeneralsOnline.ViewModels;

/// <summary>
/// ViewModel for the GeneralsOnline login screen.
/// Handles gamecode-based polling authentication flow.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IGeneralsOnlineApiClient _apiClient;
    private readonly IGeneralsOnlineAuthService _authService;
    private readonly IExternalLinkService _externalLinkService;
    private readonly ILogger<LoginViewModel> _logger;

    private CancellationTokenSource? _pollingCancellation;

    [ObservableProperty]
    private bool _isPolling;

    [ObservableProperty]
    private string _gameCode = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready to log in";

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginViewModel"/> class.
    /// </summary>
    /// <param name="apiClient">The API client.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="externalLinkService">The external link service.</param>
    /// <param name="logger">The logger instance.</param>
    public LoginViewModel(
        IGeneralsOnlineApiClient apiClient,
        IGeneralsOnlineAuthService authService,
        IExternalLinkService externalLinkService,
        ILogger<LoginViewModel> logger)
    {
        _apiClient = apiClient;
        _authService = authService;
        _externalLinkService = externalLinkService;
        _logger = logger;
    }

    /// <summary>
    /// Command to initiate the login flow with gamecode polling.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        try
        {
            // Generate 5-digit gamecode
            GameCode = Random.Shared.Next(10000, 99999).ToString();
            _logger.LogInformation("Generated gamecode: {GameCode}", GameCode);

            // Open browser with gamecode
            var loginUrl = $"https://www.playgenerals.online/login/?gamecode={GameCode}";
            StatusMessage = "Opening browser for login...";

            var success = _externalLinkService.OpenUrl(loginUrl);
            if (!success)
            {
                _logger.LogWarning("Failed to open login URL");
                StatusMessage = "Failed to open browser. Please try again.";
                GameCode = string.Empty;
                return;
            }

            // Start polling
            IsPolling = true;
            StatusMessage = "Waiting for authentication...";
            _logger.LogInformation("Starting polling for gamecode: {GameCode}", GameCode);

            await PollForLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login process");
            StatusMessage = "An error occurred. Please try again.";
            IsPolling = false;
            GameCode = string.Empty;
        }
    }

    /// <summary>
    /// Cancels the current polling operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelLogin))]
    private void CancelLogin()
    {
        _pollingCancellation?.Cancel();
        IsPolling = false;
        GameCode = string.Empty;
        StatusMessage = "Login cancelled";
        _logger.LogInformation("Login cancelled by user");
    }

    /// <summary>
    /// Command to open the registration page.
    /// </summary>
    [RelayCommand]
    private void OpenRegistrationPage()
    {
        try
        {
            if (!_externalLinkService.OpenUrl("https://www.playgenerals.online/register"))
            {
                _logger.LogWarning("Failed to open registration URL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening registration page");
        }
    }

    private bool CanLogin() => !IsPolling;

    private bool CanCancelLogin() => IsPolling;

    private async Task PollForLoginAsync()
    {
        _pollingCancellation?.Cancel();
        _pollingCancellation = new CancellationTokenSource();

        try
        {
            // Poll for 5 minutes (60 attempts at 5 second intervals)
            var maxAttempts = 60;
            var pollInterval = TimeSpan.FromSeconds(5);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Wait before polling (skip first iteration)
                if (attempt > 1)
                {
                    await Task.Delay(pollInterval, _pollingCancellation.Token);
                }

                _logger.LogDebug("Polling attempt {Attempt}/{MaxAttempts} for gamecode {GameCode}", attempt, maxAttempts, GameCode);

                // TODO: Replace with actual CheckLogin endpoint when available
                // For now, this will throw NotImplementedException
                var refreshToken = await _apiClient.CheckLoginAsync(GameCode);

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    // Success! Save credentials
                    _logger.LogInformation("Login successful for gamecode {GameCode}", GameCode);
                    StatusMessage = "Login successful!";

                    await _authService.SaveRefreshTokenAsync(refreshToken);

                    IsPolling = false;
                    GameCode = string.Empty;
                    return;
                }
            }

            // Timed out
            _logger.LogWarning("Login timed out for gamecode {GameCode}", GameCode);
            StatusMessage = "Login timed out. Please try again.";
            IsPolling = false;
            GameCode = string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Polling cancelled");
            // Status already set by CancelLogin
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("CheckLogin endpoint not yet implemented");
            StatusMessage = "API not yet available. Please wait for implementation.";
            IsPolling = false;
            GameCode = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during polling");
            StatusMessage = "An error occurred. Please try again.";
            IsPolling = false;
            GameCode = string.Empty;
        }
        finally
        {
            _pollingCancellation?.Dispose();
            _pollingCancellation = null;
        }
    }

    partial void OnIsPollingChanged(bool value)
    {
        LoginCommand.NotifyCanExecuteChanged();
        CancelLoginCommand.NotifyCanExecuteChanged();
    }
}
