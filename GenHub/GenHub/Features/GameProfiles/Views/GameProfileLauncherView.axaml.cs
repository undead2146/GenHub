using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;

using System;
using System.Windows.Input;
using System.ComponentModel;
using System.Linq;

using CommunityToolkit.Mvvm.Input;

using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.GameProfiles.Views
{
    public partial class GameProfileLauncherView : UserControl
    {
        // Add a command property that acts as a proxy between the DataTemplate and ViewModel
        public ICommand EditProfileProxyCommand { get; }
        
        private readonly GameProfileLauncherViewModel? vm;

        public GameProfileLauncherView()
        {
            InitializeComponent();
            
            // Initialize the command in the constructor
            EditProfileProxyCommand = new RelayCommand<GameProfileItemViewModel>(EditProfileProxy);
            
            if (!Design.IsDesignMode)
            {
                vm = AppLocator.Services?.GetService(typeof(GameProfileLauncherViewModel)) as GameProfileLauncherViewModel;
                if (vm == null)
                {
                    Console.WriteLine("ERROR: Failed to resolve GameProfileLauncherViewModel");
                    return;
                }
                
                DataContext = vm;
                
                // Subscribe to property changed to ensure IsEditMode changes trigger visual updates
                if (vm is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += OnViewModelPropertyChanged;
                }
                
                this.Loaded += async (s, e) =>
                {
                    try 
                    {
                        Console.WriteLine("LauncherDashboardView: Starting initialization");
                        await vm.InitializeAsync();
                        Console.WriteLine("LauncherDashboardView: Initialization completed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR initializing GameProfileLauncherViewModel: {ex}");
                    }
                };
            }
            
            // Add event handler for button clicks that will handle profile card clicks as well
            this.AddHandler(Button.ClickEvent, new EventHandler<RoutedEventArgs>(OnButtonClick), RoutingStrategies.Tunnel);
            
            // Add event handler specifically for profile card borders
            this.AddHandler(Border.PointerPressedEvent, OnProfileCardPressed, RoutingStrategies.Tunnel);
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameProfileLauncherViewModel.IsEditMode))
            {
                // Force UI refresh when IsEditMode changes
                this.InvalidateVisual();
                this.InvalidateMeasure();
            }
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (e.Source is Button button && button.Tag?.ToString() == "OnProfileCardPressed" && 
                button.CommandParameter is GameProfileItemViewModel profile)
            {
                // Handle the edit button click
                EditProfileProxy(profile);
                e.Handled = true;
            }
        }

        private void EditProfileProxy(GameProfileItemViewModel? profile)
        {
            if (DataContext is GameProfileLauncherViewModel vm && profile != null)
            {
                // Use the view model's EditProfile command
                vm.EditProfileCommand.Execute(profile);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        /// <summary>
        /// Handle showing the edit button when a profile card is pressed/clicked
        /// </summary>
        private void OnProfileCardPressed(object? sender, PointerPressedEventArgs e)
        {
            // Filter by Tag to make sure we're only handling profile cards
            if (e.Source is Border border && border.Tag?.ToString() == "profileCard" && 
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is GameProfileLauncherViewModel viewModel)
            {
                // Get the DataContext which should be a GameProfileItemViewModel
                if (border.DataContext is GameProfileItemViewModel profile)
                {
                    e.Handled = true;
                    viewModel.EditProfileImplCommand.Execute(profile);
                }
            }
        }
    }
}
