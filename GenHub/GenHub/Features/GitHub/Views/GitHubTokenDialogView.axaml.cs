using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GenHub.Features.GitHub.Views;

/// <summary>
/// Interaction logic for GitHubTokenDialogView.xaml.
/// </summary>
public partial class GitHubTokenDialogView : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubTokenDialogView"/> class.
    /// </summary>
    public GitHubTokenDialogView()
    {
        InitializeComponent();
    }

    private void OnTokenPasswordChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.GitHubTokenDialogViewModel viewModel)
        {
            // Convert string to SecureString for security
            var secureString = new System.Security.SecureString();
            foreach (char c in textBox.Text ?? string.Empty)
            {
                secureString.AppendChar(c);
            }
            viewModel.SetSecureToken(secureString);
        }
    }
}
