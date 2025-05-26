using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubManagerWindow : Window
    {
        private GitHubManagerViewModel? ViewModel => DataContext as GitHubManagerViewModel;
        
        public GitHubManagerWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            // Subscribe to close event in MVVM way
            if (ViewModel != null)
            {
                ViewModel.CloseRequested += OnCloseRequested;
                ViewModel.ViewLoadedCommand?.Execute(null);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Clean MVVM unsubscription
            if (ViewModel != null)
            {
                ViewModel.CloseRequested -= OnCloseRequested;
                ViewModel.Cleanup();
            }
        }
        
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
