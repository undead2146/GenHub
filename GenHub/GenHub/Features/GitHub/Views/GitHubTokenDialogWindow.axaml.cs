using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using GenHub.Core.Models;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Core.Interfaces.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Views
{
    /// <summary>
    /// Dialog window for GitHub token configuration
    /// </summary>
    public partial class GitHubTokenDialogWindow : Window
    {
        private GitHubTokenDialogViewModel? _viewModel;
        private bool _isClosing = false;
        private readonly ILogger<GitHubTokenDialogWindow>? _logger;

        public GitHubTokenDialogWindow()
        {
            try
            {
                InitializeComponent();
                _logger = AppLocator.GetServiceOrDefault<ILogger<GitHubTokenDialogWindow>>();
                _logger?.LogDebug("GitHubTokenDialogWindow initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GitHubTokenDialogWindow: {ex.Message}");
                throw;
            }
        }

        public GitHubTokenDialogWindow(IGitHubApiClient apiClient, ILogger logger)
        {
            InitializeComponent();
            
            var vmLogger = logger as ILogger<GitHubTokenDialogViewModel> ?? 
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubTokenDialogViewModel>();
            
            _viewModel = new GitHubTokenDialogViewModel(apiClient, vmLogger);
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Sets the initial token value
        /// </summary>
        public void SetToken(string token)
        {
            _viewModel?.SetInitialToken(token);
        }

        /// <summary>
        /// Shows the dialog asynchronously
        /// </summary>
        public Task<GitHubTokenDialogResult> ShowDialogAsync()
        {
            var completionSource = new TaskCompletionSource<GitHubTokenDialogResult>();
            
            _viewModel?.SetCompletionSource(completionSource);
            
            // Handle window closing to complete the task only if not already completed
            Closed += (s, e) =>
            {
                _isClosing = true;
                if (!completionSource.Task.IsCompleted)
                {
                    completionSource.SetResult(new GitHubTokenDialogResult
                    {
                        Success = false,
                        Token = null
                    });
                }
            };
            
            // Handle window closing event to set flag
            Closing += (s, e) =>
            {
                _isClosing = true;
            };
            
            // Handle completion source task completion to close window
            completionSource.Task.ContinueWith(task =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!_isClosing && IsVisible)
                    {
                        Close();
                    }
                });
            }, TaskScheduler.Default);
            
            Show();
            
            return completionSource.Task;
        }
    }
}
