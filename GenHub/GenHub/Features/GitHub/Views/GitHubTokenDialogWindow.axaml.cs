using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using GenHub.Core.Models;

namespace GenHub.Features.GitHub.Views

{
    public partial class GitHubTokenDialogWindow : Window
    {
        public string TokenText
        {
            get => _tokenTextBox?.Text ?? string.Empty;
            set
            {
                if (_tokenTextBox != null)
                    _tokenTextBox.Text = value;
            }
        }
        
        // Store the dialog result as a property
        private DialogResult _dialogResult = DialogResult.Cancel;
        
        private TextBox? _tokenTextBox;
        private TextBlock? _errorTextBlock;
        
        public GitHubTokenDialogWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _tokenTextBox = this.FindControl<TextBox>("TokenTextBox");
            _errorTextBlock = this.FindControl<TextBlock>("ErrorMessage");
            
            if (_errorTextBlock != null)
            {
                _errorTextBlock.IsVisible = false;
            }
            
            // Hook up the button events
            var okButton = this.FindControl<Button>("OkButton");
            var cancelButton = this.FindControl<Button>("CancelButton");
            
            if (okButton != null)
                okButton.Click += OnOkButtonClick;
                
            if (cancelButton != null)
                cancelButton.Click += (s, e) => Close(DialogResult.Cancel);
        }

        private void OnOkButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Validate token before closing
            if (string.IsNullOrWhiteSpace(TokenText))
            {
                if (_errorTextBlock != null)
                {
                    _errorTextBlock.Text = "Token cannot be empty";
                    _errorTextBlock.IsVisible = true;
                }
                return;
            }
            
            // Basic format validation (GitHub PATs usually start with ghp_ and have a minimum length)
            if (!TokenText.StartsWith("ghp_") && !TokenText.StartsWith("github_pat_") && TokenText.Length < 20)
            {
                if (_errorTextBlock != null)
                {
                    _errorTextBlock.Text = "Token doesn't appear to be valid. It should start with 'ghp_' or 'github_pat_'";
                    _errorTextBlock.IsVisible = true;
                }
                return;
            }
            
            // If validation passes, close with OK
            Close(DialogResult.OK);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        // Method to show dialog and return result
        public new async Task<(DialogResult Result, string Token)> ShowDialog(Window parent)
        {
            var tcs = new TaskCompletionSource<(DialogResult, string)>();
            
            this.Closed += (sender, args) =>
            {
                tcs.TrySetResult((_dialogResult, TokenText));
            };
            
            await base.ShowDialog(parent);
            return await tcs.Task;
        }
        
        // Alternative method without parent
        public async Task<(DialogResult Result, string Token)> ShowDialog()
        {
            // Get the main window as the parent
            var mainWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;
                
            if (mainWindow != null)
                return await ShowDialog(mainWindow);
                
            // Fallback for cases where we can't determine the main window
            var tcs = new TaskCompletionSource<(DialogResult, string)>();
            
            Closed += (s, e) => {
                tcs.TrySetResult((_dialogResult, TokenText));
            };
            
            Show();
            return await tcs.Task;
        }
        
        // Method to close with a specific result
        public void Close(DialogResult result)
        {
            _dialogResult = result;
            Close();
        }
    }
}
