using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using GenHub.Features.GitHub.ViewModels;
using System;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubManagerWindow : Window
    {
        private readonly ILogger<GitHubManagerWindow>? _logger;

        public GitHubManagerWindow()
        {
            try
            {
                InitializeComponent();
                
                _logger = AppLocator.GetServiceOrDefault<ILogger<GitHubManagerWindow>>();
                _logger?.LogDebug("GitHubManagerWindow initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GitHubManagerWindow: {ex.Message}");
                throw;
            }
        }

        public GitHubManagerWindow(GitHubManagerViewModel viewModel) : this()
        {
            try
            {
                DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                
                // Subscribe to close event only if it exists and is accessible
                try
                {
                    viewModel.CloseRequested += OnCloseRequested;
                }
                catch (Exception)
                {
                    // CloseRequested event may not be accessible or may not exist
                    _logger?.LogDebug("CloseRequested event not available for subscription");
                }

                _logger?.LogDebug("GitHubManagerWindow created with ViewModel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating GitHubManagerWindow with ViewModel");
                throw;
            }
        }

        private void OnCloseRequested(object? sender, EventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing GitHubManagerWindow");
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            try
            {
                if (DataContext is GitHubManagerViewModel viewModel)
                {
                    _logger?.LogDebug("GitHubManagerWindow loaded with ViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during window loaded event");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (DataContext is GitHubManagerViewModel viewModel)
                {
                    try
                    {
                        viewModel.CloseRequested -= OnCloseRequested;
                    }
                    catch (Exception)
                    {
                        // Event may not be accessible, ignore
                    }
                }
                _logger?.LogDebug("GitHubManagerWindow closed and cleaned up");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during window cleanup");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
