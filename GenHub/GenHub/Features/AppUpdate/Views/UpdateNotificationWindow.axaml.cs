using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using GenHub.Features.AppUpdate.ViewModels;

namespace GenHub.Features.AppUpdate.Views
{
    public partial class UpdateNotificationWindow : Window
    {
        public UpdateNotificationWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Handle the close button click event
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// Initialize the window with the view model
        /// </summary>
        /// <returns>Task to await initialization</returns>
        public async Task InitializeAsync()
        {
            if (DataContext is UpdateNotificationViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
        
        /// <summary>
        /// Shows the update notification as a dialog
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <returns>Task representing the operation</returns>
        public static async Task ShowDialogAsync(Window owner)
        {
            var window = new UpdateNotificationWindow();
            
            // Create and set the view model
            var viewModel = AppLocator.GetService<UpdateNotificationViewModel>();
            window.DataContext = viewModel;
            
            // Initialize before showing
            await window.InitializeAsync();
            
            // Show as dialog
            await window.ShowDialog(owner);
        }

        // Keep only this method for window dragging
        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Start window drag operation when the title bar is clicked
            BeginMoveDrag(e);
        }
    }
}
