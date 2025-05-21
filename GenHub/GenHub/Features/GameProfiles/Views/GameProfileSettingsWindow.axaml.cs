using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using Avalonia.Interactivity;
using Avalonia.Data;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Models;
using GenHub.Core.Models.UI;
using GenHub.Core.Interfaces.UI;
using GenHub.Features.GameProfiles.ViewModels;

using System.ComponentModel;
using System.Windows.Input;

namespace GenHub.Features.GameProfiles.Views
{
    /// <summary>
    /// Window for editing game profile settings
    /// </summary>
    public partial class GameProfileSettingsWindow : Window
    {
        private readonly ILogger<GameProfileSettingsWindow> _logger;
        private readonly GameProfileItemViewModel? _profileToEdit;
        private readonly IDialogService? _dialogService;
        private bool _isClosingProgrammatically = false;
        /// <summary>
        /// The ViewModel for this window
        /// </summary>
        public GameProfileSettingsViewModel ViewModel { get; }

        /// <summary>
        /// Constructor for dependency injection / designer
        /// </summary>
        public GameProfileSettingsWindow() : this(null, null)
        {
#if DEBUG
            _logger = AppLocator.GetService<ILogger<GameProfileSettingsWindow>>();
            _logger.LogDebug("Creating GameProfileSettingsWindow using parameterless constructor (design-time)");
#endif
        }
        public async Task<DialogResult> ShowAsync(Window parent)
        {
            var tcs = new TaskCompletionSource<DialogResult>();
            EventHandler? closedEventHandler = null;

            if (DataContext is GameProfileSettingsViewModel viewModel)
            {
                viewModel.DialogConfirmed += (s, e) => tcs.TrySetResult(DialogResult.OK);
                viewModel.DialogCancelled += (s, e) => tcs.TrySetResult(DialogResult.Cancel);

                closedEventHandler = (s, e) =>
                {
                    // If TCS hasn't been completed by DialogConfirmed/Cancelled, assume Cancel.
                    tcs.TrySetResult(DialogResult.Cancel);
                    if (closedEventHandler != null) this.Closed -= closedEventHandler; // Clean up
                };
                this.Closed += closedEventHandler;
            }
            else
            {
                // If DataContext is not set or not the right type, dialog can't function as expected.
                tcs.TrySetResult(DialogResult.Cancel); // Default to cancel immediately.
                _logger?.LogError("ShowAsync: DataContext is not GameProfileSettingsViewModel. Dialog will likely not behave as expected.");
            }

            await this.ShowDialog(parent);

            // Ensure cleanup if closed by other means before TCS is set by VM events
            if (closedEventHandler != null && !tcs.Task.IsCompleted)
            {
                this.Closed -= closedEventHandler; // Defensive cleanup
                tcs.TrySetResult(DialogResult.Cancel); // Ensure completion
            }
            return await tcs.Task;
        }
        /// <summary>
        /// Constructor for editing an existing profile
        /// </summary>
        public GameProfileSettingsWindow(GameProfileItemViewModel? profile, Window? owner)
        {
#if DEBUG
            _logger = AppLocator.GetService<ILogger<GameProfileSettingsWindow>>();
            _logger.LogDebug("Creating GameProfileSettingsWindow with profile: {ProfileName}", profile?.Name ?? "New Profile");
#endif
            _dialogService = AppLocator.GetService<IDialogService>();

            _profileToEdit = profile;

            if (owner != null)
            {
                // Initialize ViewModel with profile to edit and pass this window instance
                ViewModel = new GameProfileSettingsViewModel(_profileToEdit, this);

                // Set up window properties
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                this.Owner = owner;

                InitializeComponent();

                // Set the DataContext immediately after component initialization
                this.DataContext = ViewModel;
                ViewModel.SetDialogWindow(this); // Ensure CloseAction is set

                // Initialize asynchronously after window is fully loaded
                this.Loaded += (s, e) => _ = InitializeAsync();
                this.Closing += Window_Closing; // Subscribe to Closing event
            }
            else
            {
                // Handle case where owner is null (e.g. new profile without explicit owner)
                ViewModel = new GameProfileSettingsViewModel(_profileToEdit, this); // Pass 'this' as ownerWindow

                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                InitializeComponent();
                this.DataContext = ViewModel;
                ViewModel.SetDialogWindow(this); // Ensure CloseAction is set

                this.Loaded += (s, e) => _ = InitializeAsync();
                this.Closing += Window_Closing; // Subscribe to Closing event
            }

            // Subscribe to ViewModel property changes to refresh UI
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        public void SetProgrammaticCloseFlag(bool value)
        {
            _isClosingProgrammatically = value;
        }

        private async void Window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_isClosingProgrammatically || e.Cancel) // If already closing via ViewModel or cancelled by another handler
            {
                _isClosingProgrammatically = false; // Reset flag for next time if needed
                return;
            }

            if (DataContext is GameProfileSettingsViewModel vm && !vm.DialogHasCompleted)
            {
                _logger?.LogDebug("Window_Closing: User initiated close ('X') before ViewModel completion. Attempting to delegate to ViewModel's cancel logic.");

                // Fix: Get the cancel command through reflection if generated property doesn't work
                var cancelCommand = GetCancelCommand(vm);
                if (cancelCommand != null && cancelCommand.CanExecute(null))
                {
                    e.Cancel = true; // Give ViewModel a chance to keep window open
                    try
                    {
                        // Call the execute method through reflection
                        await ExecuteCancelCommandAsync(vm);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception during cancel command execution in Window_Closing. Forcing window close to prevent unresponsive UI.");
                        e.Cancel = false; // Force close if the cancel command itself fails, to avoid a stuck window
                    }
                }
                else
                {
                    _logger?.LogWarning("Window_Closing: Cancel command cannot execute or was not found. Defaulting to allow close. Unsaved changes might be lost if any.");
                    // e.Cancel remains false (default), so window will close.
                }
            }
        }

        // Helper method to get cancel command through reflection if source generator failed
        private ICommand? GetCancelCommand(GameProfileSettingsViewModel viewModel)
        {
            try
            {
                // First try direct property access
                var cmdProperty = viewModel.GetType().GetProperty("ExecuteCancelAsyncCommand");
                if (cmdProperty != null)
                {
                    return cmdProperty.GetValue(viewModel) as ICommand;
                }

                // If that fails, look for the base command
                var cancelMethod = viewModel.GetType().GetMethod("ExecuteCancelAsync");
                if (cancelMethod != null)
                {
                    // Call the method directly since we have access to it
                    return new RelayCommand<object?>(
                        _ =>
                        {
                            _ = ExecuteCancelCommandAsync(viewModel);
                        },
                        _ => viewModel.GetType().GetMethod("CanCancel")?.Invoke(viewModel, null) as bool? ?? true
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting cancel command via reflection");
            }
            return null;
        }

        private async Task ExecuteCancelCommandAsync(GameProfileSettingsViewModel viewModel)
        {
            try
            {
                var method = viewModel.GetType().GetMethod("ExecuteCancelAsync");
                if (method != null)
                {
                    var task = method.Invoke(viewModel, null) as Task;
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing cancel command");
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (DataContext is GameProfileSettingsViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during initialization");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Handle property changes if needed
        }

        private async void OnCancelButtonClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is GameProfileSettingsViewModel vm)
            {
                try
                {
                    // Try to find the cancel method and invoke it
                    var cancelMethod = vm.GetType().GetMethod("ExecuteCancelAsync", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    
                    if (cancelMethod != null)
                    {
                        var task = cancelMethod.Invoke(vm, null) as Task;
                        if (task != null)
                        {
                            await task;
                            return;
                        }
                    }
                    
                    // Fallback if the method can't be found
                    _logger?.LogWarning("Could not find ExecuteCancelAsync method, using direct cancel logic");
                    if (!vm.IsSaving && vm.CloseAction != null)
                    {
                        vm.CloseAction();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in OnCancelButtonClick");
                }
            }
        }
    }
}

