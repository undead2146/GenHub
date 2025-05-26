using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for GitHub token configuration dialog
    /// </summary>
    public partial class GitHubTokenDialogViewModel : ObservableObject
    {
        private readonly IGitHubApiClient _apiClient;
        private readonly ILogger<GitHubTokenDialogViewModel> _logger;
        private TaskCompletionSource<GitHubTokenDialogResult>? _completionSource;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OkCommand))]
        private string _token = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private bool _isValidating = false;

        [ObservableProperty]
        private bool _isValidated = false;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private bool _showValidationSuccess = false;

        public GitHubTokenDialogViewModel(IGitHubApiClient apiClient, ILogger<GitHubTokenDialogViewModel> logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetCompletionSource(TaskCompletionSource<GitHubTokenDialogResult> completionSource)
        {
            _completionSource = completionSource;
        }

        public void SetInitialToken(string token)
        {
            Token = token ?? string.Empty;
            // Clear validation state when setting initial token
            ClearValidationState();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteOk))]
        private async Task OkAsync()
        {
            try
            {
                // Prevent multiple concurrent executions
                if (IsValidating)
                    return;

                // If already validated successfully, just save and close without re-validation
                if (IsValidated && ShowValidationSuccess && !string.IsNullOrWhiteSpace(Token))
                {
                    _logger.LogInformation("Token already validated, saving and closing");
                    CompleteDialog(true, Token);
                    return;
                }

                // Otherwise, validate first
                ClearError();
                ClearValidationState();
                
                if (!ValidateTokenFormat())
                    return;

                IsValidating = true;
                ValidationMessage = "Validating token...";
                _logger.LogInformation("Validating GitHub token");

                // Test the token
                var oldToken = _apiClient.GetAuthToken();
                await _apiClient.SetAuthTokenAsync(Token);
                
                var isValid = await _apiClient.TestAuthenticationAsync();
                
                if (!isValid)
                {
                    // Restore old token if validation failed
                    if (!string.IsNullOrEmpty(oldToken))
                    {
                        await _apiClient.SetAuthTokenAsync(oldToken);
                    }
                    
                    SetError("Token validation failed. Please check your token and try again.");
                    _logger.LogWarning("GitHub token validation failed");
                    return;
                }

                // Validation successful - show success message briefly then close
                ShowValidationSuccess = true;
                ValidationMessage = "Token validated successfully!";
                IsValidated = true;
                
                _logger.LogInformation("GitHub token validated successfully");
                
                // Brief delay to show success message
                await Task.Delay(500);
                
                // Now complete the task and close dialog
                CompleteDialog(true, Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating GitHub token");
                SetError("An error occurred while validating the token. Please try again.");
            }
            finally
            {
                IsValidating = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _logger.LogInformation("GitHub token configuration cancelled by user");
            CompleteDialog(false, null);
        }

        [RelayCommand]
        private void OpenGitHubTokenPage()
        {
            try
            {
                var url = "https://github.com/settings/tokens";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                _logger.LogInformation("Opened GitHub token creation page");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open GitHub token page");
            }
        }

        [RelayCommand]
        private async Task ValidateTokenAsync()
        {
            try
            {
                ClearError();
                ClearValidationState();
                
                if (!ValidateTokenFormat())
                    return;

                IsValidating = true;
                ValidationMessage = "Testing token...";
                _logger.LogInformation("Manual token validation requested");

                // Store current token to restore if validation fails
                var oldToken = _apiClient.GetAuthToken();
                
                // Set the new token temporarily
                await _apiClient.SetAuthTokenAsync(Token);
                
                // Test the token
                var isValid = await _apiClient.TestAuthenticationAsync();
                
                if (isValid)
                {
                    ShowValidationSuccess = true;
                    ValidationMessage = "Token is valid and ready to use!";
                    IsValidated = true;
                    _logger.LogInformation("Manual token validation successful");
                }
                else
                {
                    // Restore old token since validation failed
                    if (!string.IsNullOrEmpty(oldToken))
                    {
                        await _apiClient.SetAuthTokenAsync(oldToken);
                    }
                    
                    SetError("Token validation failed. Please check your token permissions and try again.");
                    _logger.LogWarning("Manual token validation failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual token validation");
                SetError("An error occurred while validating the token. Please try again.");
            }
            finally
            {
                IsValidating = false;
            }
        }

        private bool CanExecuteOk() => !string.IsNullOrWhiteSpace(Token) && !IsValidating;

        private bool ValidateTokenFormat()
        {
            if (string.IsNullOrWhiteSpace(Token))
            {
                SetError("Token cannot be empty");
                return false;
            }

            // GitHub Personal Access Tokens validation
            if (!Token.StartsWith("ghp_") && !Token.StartsWith("github_pat_") && Token.Length < 20)
            {
                SetError("Token doesn't appear to be valid. GitHub tokens usually start with 'ghp_' or 'github_pat_'");
                return false;
            }

            return true;
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = true;
            ClearValidationState();
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        private void ClearValidationState()
        {
            ShowValidationSuccess = false;
            ValidationMessage = string.Empty;
            IsValidated = false;
        }

        /// <summary>
        /// Safely completes the dialog with the given result
        /// </summary>
        private void CompleteDialog(bool success, string? token)
        {
            try
            {
                // Only complete if not already completed
                if (_completionSource != null && !_completionSource.Task.IsCompleted)
                {
                    var result = new GitHubTokenDialogResult
                    {
                        Success = success,
                        Token = token
                    };
                    
                    _completionSource.SetResult(result);
                    _logger.LogInformation("Dialog completed with success={Success}", success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing dialog");
            }
        }

        // Handle token changes to clear validation state
        partial void OnTokenChanged(string value)
        {
            ClearError();
            ClearValidationState();
        }
    }

    /// <summary>
    /// Result returned from the GitHub token dialog
    /// </summary>
    public class GitHubTokenDialogResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
    }
}
