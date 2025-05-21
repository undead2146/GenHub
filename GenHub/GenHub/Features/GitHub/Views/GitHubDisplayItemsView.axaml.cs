using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubDisplayItemsView : UserControl
    {
        public GitHubDisplayItemsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            // No event handlers needed - using MVVM bindings
        }
    }
}
