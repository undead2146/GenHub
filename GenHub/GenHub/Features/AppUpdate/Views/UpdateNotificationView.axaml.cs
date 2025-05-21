using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GenHub.Features.AppUpdate.ViewModels;

namespace GenHub.Features.AppUpdate.Views
{
    public partial class UpdateNotificationView : UserControl
    {
        public UpdateNotificationView()
        {
            InitializeComponent();
            
            // Automatically initialize the view model when the control is attached
            this.AttachedToVisualTree += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        /// <summary>
        /// Initializes the view model for this control
        /// </summary>
        public async Task InitializeAsync()
        {
            // Ensure we're on the UI thread
            await Dispatcher.UIThread.InvokeAsync(async () => {
                if (DataContext is UpdateNotificationViewModel viewModel && 
                    !viewModel.IsInitialized)
                {
                    await viewModel.InitializeAsync();
                }
            });
        }
    }
}
