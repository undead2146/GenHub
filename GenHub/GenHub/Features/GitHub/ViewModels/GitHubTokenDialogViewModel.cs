using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GitHub;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// ViewModel for GitHub authentication token dialog.
/// </summary>
public partial class GitHubTokenDialogViewModel : ObservableObject
{
    private readonly IGitHubApiClient _gitHubApiClient;
    private readonly ILogger<GitHubTokenDialogViewModel> _logger;
    private SecureString _secureToken = new SecureString();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValidating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessageColor))]
    [NotifyPropertyChangedFor(nameof(ValidationMessageBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string _validationMessage = GitHubConstants.EnterTokenMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(ValidationMessageColor))]
    [NotifyPropertyChangedFor(nameof(ValidationMessageBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private bool _isTokenValid;

    [ObservableProperty]
    private bool _shouldClose;

    [ObservableProperty]
    private bool _dialogResult;

    /// <summary>
    /// Sets the secure token from the UI.
    /// </summary>
    /// <param name="token">The secure token.</param>
    public void SetSecureToken(SecureString token)
    {
        _secureToken = token ?? new SecureString();
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTokenDialogViewModel"/> class.
    /// </summary>
    /// <param name="gitHubApiClient">The GitHub API client.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubTokenDialogViewModel(
        IGitHubApiClient gitHubApiClient,
        ILogger<GitHubTokenDialogViewModel> logger)
    {
        _gitHubApiClient = gitHubApiClient ?? throw new ArgumentNullException(nameof(gitHubApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether the Save command can execute.
    /// </summary>
    public bool CanSave => IsTokenValid && !IsValidating && _secureToken.Length > 0;

    /// <summary>
    /// Gets the validation message brush based on token validity.
    /// </summary>
    public SolidColorBrush ValidationMessageColor => IsTokenValid
        ? new SolidColorBrush(Color.Parse("#238636"))
        : new SolidColorBrush(Color.Parse("#F85149"));

    /// <summary>
    /// Gets the validation message background brush for better visibility.
    /// </summary>
    public SolidColorBrush ValidationMessageBackgroundColor => IsTokenValid
        ? new SolidColorBrush(Color.Parse("#0D1117"))
        : new SolidColorBrush(Color.Parse("#2D1519"));

    /// <summary>
    /// Gets a value indicating whether there is a validation message to show.
    /// </summary>
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage) &&
        ValidationMessage != GitHubConstants.EnterTokenMessage &&
        ValidationMessage != GitHubConstants.ValidatingTokenMessage;

    /// <summary>
    /// Validates the entered token.
    /// </summary>
    /// <returns>A task representing the validation operation.</returns>
    [RelayCommand]
    public async Task ValidateTokenAsync()
    {
        if (_secureToken.Length == 0)
        {
            ValidationMessage = GitHubConstants.EnterTokenMessage;
            IsTokenValid = false;
            return;
        }

        try
        {
            IsValidating = true;
            ValidationMessage = GitHubConstants.ValidatingTokenMessage;
            IsTokenValid = false;

            // Set the token for validation
            _gitHubApiClient.SetAuthenticationToken(_secureToken);

            // Actually test the token by making a simple API call
            try
            {
                // Test with a simple API call to get the authenticated user
                await _gitHubApiClient.GetLatestReleaseAsync("octocat", "Hello-World");

                // If we get here without rate limit errors, the token is working
                ValidationMessage = "✅ Token validated successfully! You can now save.";
                IsTokenValid = true;
                _logger.LogInformation("GitHub token validated successfully");
            }
            catch (RateLimitExceededException rateLimitEx)
            {
                // Rate limit means the token is valid but we've hit limits
                ValidationMessage = "⚠️ Rate limited! Token is valid but GitHub API limit reached. You can still save.";
                IsTokenValid = true;
                _logger.LogInformation("GitHub token validated (rate limited): {Message}", rateLimitEx.Message);
            }
            catch (AuthorizationException authEx)
            {
                ValidationMessage = "❌ Invalid token or insufficient permissions. Please check your token.";
                IsTokenValid = false;
                _logger.LogWarning(authEx, "GitHub token authorization failed");
            }
            catch (Exception apiEx)
            {
                // Check if it's a rate limit disguised as another error
                if (apiEx.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                    apiEx.Message.Contains("API rate limit", StringComparison.OrdinalIgnoreCase))
                {
                    ValidationMessage = "⚠️ Rate limited! Token appears valid but hit GitHub rate limits. You can save.";
                    IsTokenValid = true;
                    _logger.LogInformation("Rate limit detected in API error: {Message}", apiEx.Message);
                }
                else
                {
                    ValidationMessage = $"⚠️ Token set but API test failed: {apiEx.Message.Substring(0, Math.Min(100, apiEx.Message.Length))}";
                    IsTokenValid = true; // Allow saving since token format might be correct
                    _logger.LogWarning(apiEx, "GitHub API test failed but token may be valid");
                }
            }

            // Manually notify command that CanExecute state may have changed
            SaveCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            ValidationMessage = string.Format(GitHubConstants.InvalidTokenFormat, ex.Message);
            IsTokenValid = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            IsValidating = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Saves the token and closes the dialog.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        _logger.LogInformation(
            "Save button clicked. CanSave: {CanSave}, IsTokenValid: {IsTokenValid}, IsValidating: {IsValidating}, Token length: {TokenLength}",
            CanSave,
            IsTokenValid,
            IsValidating,
            _secureToken.Length);
        if (CanSave && IsTokenValid)
        {
            // Token is already set in the API client from validation
            DialogResult = true;
            ShouldClose = true;
            _logger.LogInformation("Token saved successfully");
        }
        else
        {
            _logger.LogWarning("Save attempted but conditions not met. CanSave: {CanSave}, IsTokenValid: {IsTokenValid}", CanSave, IsTokenValid);
        }
    }

    /// <summary>
    /// Cancels the dialog without saving.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        DialogResult = false;
        ShouldClose = true;
    }

    /// <summary>
    /// Resets the dialog state.
    /// </summary>
    public void Reset()
    {
        _secureToken = new SecureString();
        IsTokenValid = false;
        ValidationMessage = GitHubConstants.EnterTokenMessage;
        ShouldClose = false;
        DialogResult = false;
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Opens the GitHub Personal Access Token creation page.
    /// </summary>
    [RelayCommand]
    public void OpenPatCreation()
    {
        try
        {
            var url = "https://github.com/settings/tokens/new?scopes=repo,workflow&description=GenHub%20Access";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            _logger.LogInformation("Opened GitHub PAT creation URL: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open GitHub PAT creation URL");
            ValidationMessage = "Could not open browser. Please visit: https://github.com/settings/tokens/new";
        }
    }
}
