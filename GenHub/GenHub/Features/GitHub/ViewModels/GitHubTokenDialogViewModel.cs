using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// ViewModel for the GitHub PAT token dialog.
/// </summary>
public partial class GitHubTokenDialogViewModel : ObservableObject, IDisposable
{
    private readonly IGitHubTokenStorage? _tokenStorage;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<GitHubTokenDialogViewModel>? _logger;
    private SecureString? _secureToken;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private IBrush _validationMessageColor = Brushes.White;

    [ObservableProperty]
    private bool _hasValidationMessage;

    [ObservableProperty]
    private bool _isTokenValid;

    /// <summary>
    /// Event raised when the dialog should close with success.
    /// </summary>
    public event Action? SaveCompleted;

    /// <summary>
    /// Event raised when the dialog should close without saving.
    /// </summary>
    public event Action? CancelRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTokenDialogViewModel"/> class.
    /// </summary>
    public GitHubTokenDialogViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTokenDialogViewModel"/> class with dependencies.
    /// </summary>
    /// <param name="tokenStorage">The storage for the GitHub token.</param>
    /// <param name="httpClientFactory">The factory for creating HTTP clients.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubTokenDialogViewModel(
        IGitHubTokenStorage tokenStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubTokenDialogViewModel> logger)
    {
        _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the token from the password box (called from code-behind).
    /// </summary>
    /// <param name="token">The token string to set.</param>
    public void SetToken(string token)
    {
        _secureToken?.Dispose();
        _secureToken = new SecureString();
        foreach (var c in token)
        {
            _secureToken.AppendChar(c);
        }

        _secureToken.MakeReadOnly();

        // Clear validation when token changes
        ValidationMessage = string.Empty;
        HasValidationMessage = false;
        IsTokenValid = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _secureToken?.Dispose();
    }

    /// <summary>
    /// Opens the GitHub PAT creation page in the browser.
    /// </summary>
    [RelayCommand]
    private void OpenPatCreation()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubConstants.PatCreationUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open PAT creation URL");
        }
    }

    /// <summary>
    /// Validates the entered token against GitHub API.
    /// </summary>
    [RelayCommand]
    private async Task ValidateTokenAsync()
    {
        if (_secureToken == null || _secureToken.Length == 0)
        {
            ValidationMessage = GitHubConstants.EnterTokenMessage;
            ValidationMessageColor = Brushes.Orange;
            HasValidationMessage = true;
            return;
        }

        if (_httpClientFactory == null)
        {
            _logger?.LogWarning("HttpClientFactory not available for token validation");
            return;
        }

        IsValidating = true;
        ValidationMessage = string.Empty;
        HasValidationMessage = false;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GenHub", AppConstants.AppVersion));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetPlainToken());

            var response = await client.GetAsync("https://api.github.com/user");

            if (response.IsSuccessStatusCode)
            {
                // Check for required scopes
                if (response.Headers.TryGetValues("X-OAuth-Scopes", out var scopes))
                {
                    var scopeString = string.Join(",", scopes);
                    if (scopeString.Contains("repo", StringComparison.OrdinalIgnoreCase))
                    {
                        ValidationMessage = "✓ Token is valid with repo access!";
                        ValidationMessageColor = Brushes.LightGreen;
                        IsTokenValid = true;
                        _logger?.LogInformation("GitHub token validated successfully with repo scope");
                    }
                    else
                    {
                        ValidationMessage = "⚠ Token is valid but missing 'repo' scope. Some features may not work.";
                        ValidationMessageColor = Brushes.Orange;
                        IsTokenValid = true; // Still allow saving
                        _logger?.LogWarning("GitHub token valid but missing repo scope");
                    }
                }
                else
                {
                    ValidationMessage = "✓ Token is valid!";
                    ValidationMessageColor = Brushes.LightGreen;
                    IsTokenValid = true;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ValidationMessage = "✗ Token is invalid or expired.";
                ValidationMessageColor = Brushes.Salmon;
                IsTokenValid = false;
                _logger?.LogWarning("GitHub token validation failed: Unauthorized");
            }
            else
            {
                ValidationMessage = $"✗ Validation failed: {response.StatusCode}";
                ValidationMessageColor = Brushes.Salmon;
                IsTokenValid = false;
                _logger?.LogWarning("GitHub token validation failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"✗ Error: {ex.Message}";
            ValidationMessageColor = Brushes.Salmon;
            IsTokenValid = false;
            _logger?.LogError(ex, "Error validating GitHub token");
        }
        finally
        {
            IsValidating = false;
            HasValidationMessage = true;
        }
    }

    /// <summary>
    /// Saves the token and closes the dialog.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_secureToken == null || _secureToken.Length == 0)
        {
            ValidationMessage = GitHubConstants.EnterTokenMessage;
            ValidationMessageColor = Brushes.Orange;
            HasValidationMessage = true;
            return;
        }

        if (_tokenStorage == null)
        {
            _logger?.LogWarning("Token storage not available");
            return;
        }

        try
        {
            await _tokenStorage.SaveTokenAsync(_secureToken);
            _logger?.LogInformation("GitHub PAT saved successfully");
            SaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"✗ Failed to save token: {ex.Message}";
            ValidationMessageColor = Brushes.Salmon;
            HasValidationMessage = true;
            _logger?.LogError(ex, "Failed to save GitHub token");
        }
    }

    /// <summary>
    /// Cancels the dialog.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }

    private string GetPlainToken()
    {
        if (_secureToken == null)
            return string.Empty;

        var ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(_secureToken);
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }
}
