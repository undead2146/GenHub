using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using System;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubManagerWindow : Window
    {
        private GitHubManagerViewModel? ViewModel => DataContext as GitHubManagerViewModel;
        private Grid? _titleBarGrid;
        
        public GitHubManagerWindow()
        {
            Console.WriteLine("[DIAGNOSTIC] GitHubManagerWindow constructor START");
            
            try
            {
                InitializeComponent();
                InitializeWindowDragHandling();
                Console.WriteLine("[DIAGNOSTIC] GitHubManagerWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GitHubManagerWindow constructor: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                throw; 
            }
        }

        private void InitializeComponent()
        {
            Console.WriteLine("[DIAGNOSTIC] GitHubManagerWindow.InitializeComponent START");
            try
            {
                AvaloniaXamlLoader.Load(this);
                Console.WriteLine("[DIAGNOSTIC] AvaloniaXamlLoader.Load completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] InitializeComponent: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void InitializeWindowDragHandling()
        {
            try
            {
                _titleBarGrid = this.FindControl<Grid>("TitleBarGrid");
                if (_titleBarGrid != null)
                {
                    _titleBarGrid.PointerPressed += TitleBar_PointerPressed;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] InitializeWindowDragHandling: {ex.Message}");
            }
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TitleBar_PointerPressed: {ex.Message}");
            }
        }
        
        protected override void OnOpened(EventArgs e)
        {
            Console.WriteLine("[DIAGNOSTIC] GitHubManagerWindow.OnOpened START");
            base.OnOpened(e);
            
            // Set up the DataContext when the window is opened
            try
            {
                Console.WriteLine("[DIAGNOSTIC] About to load DataContext");
                
                if (DataContext == null)
                {
                    DataContext = AppLocator.GetService<GitHubManagerViewModel>();
                    Console.WriteLine($"[DIAGNOSTIC] DataContext created: {DataContext != null}");
                }
                
                // Subscribe to close event
                if (ViewModel != null)
                {
                    ViewModel.CloseRequested += ViewModel_CloseRequested;
                    ViewModel.ViewLoadedCommand?.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnOpened DataContext: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("[DIAGNOSTIC] GitHubManagerWindow.OnOpened END");
        }
        
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Unsubscribe from events
            if (ViewModel != null)
            {
                ViewModel.CloseRequested -= ViewModel_CloseRequested;
                ViewModel.Cleanup();
            }
        }
        
        private void ViewModel_CloseRequested(object? sender, EventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Execute the CloseWindow command on the ViewModel
                if (ViewModel != null)
                {
                    ViewModel.CloseWindowCommand.Execute(null);
                }
                else
                {
                    // Fallback if ViewModel is not available
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CloseButton_Click: {ex.Message}");
                this.Close(); // Fallback in case of error
            }
        }
    }
}
