using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GenHub.Core.Interfaces.UI;
using GenHub.Core.Models.UI;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.UI
{
    /// <summary>
    /// Service for handling file and folder selection dialogs
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly ILogger<DialogService> _logger;
        private Window? _parentWindow;

        /// <summary>
        /// Gets or sets the parent window for dialogs
        /// </summary>
        public Window? ParentWindow
        {
            get => _parentWindow;
            set => _parentWindow = value;
        }

        public DialogService(ILogger<DialogService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sets the parent window for dialogs
        /// </summary>
        public void SetParentWindow(Window window)
        {
            _parentWindow = window;
        }

        /// <summary>
        /// Gets the current main window if parent window isn't set
        /// </summary>
        private Window GetWindow()
        {
            if (_parentWindow != null) return _parentWindow;
            
            // Try to get main window if no parent window specified
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            
            throw new InvalidOperationException("No valid window found for dialog");
        }

        /// <summary>
        /// Opens a file picker dialog
        /// </summary>
        public async Task<IReadOnlyList<string>> PickFilesAsync(string title, Dictionary<string, string> fileTypes, bool allowMultiple = false)
        {
            try
            {
                var window = GetWindow();
                
                var fileTypeFilters = new List<FilePickerFileType>();
                
                foreach (var type in fileTypes)
                {
                    fileTypeFilters.Add(new FilePickerFileType(type.Key)
                    {
                        Patterns = type.Value.Split(';').Select(p => p.Trim()).ToArray(),
                        MimeTypes = GetMimeTypesForExtensions(type.Value)
                    });
                }
                
                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = allowMultiple,
                    FileTypeFilter = fileTypeFilters
                };
                
                var result = await window.StorageProvider.OpenFilePickerAsync(options);
                
                return result.Select(f => f.Path.LocalPath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error picking files with title: {Title}", title);
                return new List<string>();
            }
        }

        /// <summary>
        /// Opens a folder picker dialog
        /// </summary>
        public async Task<IReadOnlyList<string>> PickFoldersAsync(string title, bool allowMultiple = false)
        {
            try
            {
                var window = GetWindow();
                
                var options = new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = allowMultiple
                };
                
                var result = await window.StorageProvider.OpenFolderPickerAsync(options);
                
                return result.Select(f => f.Path.LocalPath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error picking folders with title: {Title}", title);
                return new List<string>();
            }
        }

        /// <summary>
        /// Opens an image file picker dialog
        /// </summary>
        public async Task<string?> PickImageFileAsync(string title)
        {
            var fileTypes = new Dictionary<string, string>
            {
                { "Image Files", "*.png;*.jpg;*.jpeg;*.bmp" }
            };
            
            var result = await PickFilesAsync(title, fileTypes, false);
            return result.FirstOrDefault();
        }

        /// <summary>
        /// Opens an executable file picker dialog
        /// </summary>
        public async Task<string?> PickExecutableFileAsync(string title)
        {
            var fileTypes = new Dictionary<string, string>
            {
                { "Executable Files", "*.exe" }
            };
            
            var result = await PickFilesAsync(title, fileTypes, false);
            return result.FirstOrDefault();
        }

        /// <summary>
        /// Helper to get MIME types for file extensions
        /// </summary>
        private string[] GetMimeTypesForExtensions(string extensions)
        {
            var mimeTypes = new List<string>();
            
            foreach (var ext in extensions.Split(';').Select(e => e.Trim().ToLower()))
            {
                switch (ext)
                {
                    case "*.png": mimeTypes.Add("image/png"); break;
                    case "*.jpg": case "*.jpeg": mimeTypes.Add("image/jpeg"); break;
                    case "*.gif": mimeTypes.Add("image/gif"); break;
                    case "*.bmp": mimeTypes.Add("image/bmp"); break;
                    case "*.exe": mimeTypes.Add("application/x-msdownload"); break;
                    default: break;
                }
            }
            
            return mimeTypes.ToArray();
        }

        /// <summary>
        /// Shows a message box with the specified parameters
        /// </summary>
        public async Task<MessageBoxResult> ShowMessageBoxAsync(
            string title, 
            string message, 
            MessageBoxButtons buttons = MessageBoxButtons.OK, 
            MessageBoxIcon icon = MessageBoxIcon.None)
        {
            try
            {
                // Use Avalonia's built-in MessageBox implementation via the Window.ShowDialog method
                var window = GetWindow();
                
                // Create a simple message dialog window
                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    MinWidth = 300,
                    MinHeight = 150,
                    MaxWidth = 600,
                    MaxHeight = 400,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                // Create content
                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 20
                };
                
                // Add message
                mainPanel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });
                
                // Add buttons panel
                var buttonsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10
                };
                
                // Variable to store result
                var resultCompletionSource = new TaskCompletionSource<MessageBoxResult>();
                
                // Add appropriate buttons based on the buttons parameter
                switch (buttons)
                {
                    case MessageBoxButtons.OK:
                        AddButton("OK", MessageBoxResult.OK);
                        break;
                    case MessageBoxButtons.OKCancel:
                        AddButton("OK", MessageBoxResult.OK);
                        AddButton("Cancel", MessageBoxResult.Cancel);
                        break;
                    case MessageBoxButtons.YesNo:
                        AddButton("Yes", MessageBoxResult.Yes);
                        AddButton("No", MessageBoxResult.No);
                        break;
                    case MessageBoxButtons.YesNoCancel:
                        AddButton("Yes", MessageBoxResult.Yes);
                        AddButton("No", MessageBoxResult.No);
                        AddButton("Cancel", MessageBoxResult.Cancel);
                        break;
                    default:
                        AddButton("OK", MessageBoxResult.OK);
                        break;
                }
                
                mainPanel.Children.Add(buttonsPanel);
                dialog.Content = mainPanel;
                
                // Show dialog and await result
                await dialog.ShowDialog(window);
                var result = await resultCompletionSource.Task;
                return result;
                
                // Helper to add buttons with click handlers
                void AddButton(string text, MessageBoxResult buttonResult)
                {
                    var button = new Button { Content = text };
                    button.Click += (s, e) => 
                    {
                        resultCompletionSource.TrySetResult(buttonResult);
                        dialog.Close();
                    };
                    buttonsPanel.Children.Add(button);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing message box: {Title} - {Message}", title, message);
                return MessageBoxResult.None;
            }
        }

        /// <summary>
        /// Shows a confirmation dialog with custom button texts.
        /// </summary>
        public async Task<MessageBoxResult> ShowConfirmationDialogAsync(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText)
        {
            try
            {
                var window = GetWindow();
                var dialog = new Window
                {
                    Title = title,
                    Width = 400, MinWidth = 300, MinHeight = 150,
                    MaxWidth = 600, MaxHeight = 400,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var mainPanel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
                mainPanel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

                var buttonsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10
                };

                var resultCompletionSource = new TaskCompletionSource<MessageBoxResult>();

                // Secondary Button (e.g., "Cancel", "Keep Editing", "No")
                var secondaryButton = new Button { Content = secondaryButtonText };
                secondaryButton.Click += (s, e) =>
                {
                    resultCompletionSource.TrySetResult(MessageBoxResult.No); // Or Cancel, depending on desired semantics
                    dialog.Close();
                };
                buttonsPanel.Children.Add(secondaryButton);

                // Primary Button (e.g., "OK", "Save", "Discard", "Yes")
                var primaryButton = new Button { Content = primaryButtonText, Classes = { "accent" } }; // Optional: style as accent
                primaryButton.Click += (s, e) =>
                {
                    resultCompletionSource.TrySetResult(MessageBoxResult.Yes); // Or OK
                    dialog.Close();
                };
                buttonsPanel.Children.Add(primaryButton);
                
                mainPanel.Children.Add(buttonsPanel);
                dialog.Content = mainPanel;

                // Ensure dialog closes if user clicks 'X' on the dialog itself
                dialog.Closed += (s, e) => {
                    resultCompletionSource.TrySetResult(MessageBoxResult.Cancel); // Or No, if secondary is "No"
                };

                await dialog.ShowDialog(window);
                return await resultCompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing confirmation dialog: {Title} - {Message}", title, message);
                return MessageBoxResult.None; // Or Cancel
            }
        }
    }
}
