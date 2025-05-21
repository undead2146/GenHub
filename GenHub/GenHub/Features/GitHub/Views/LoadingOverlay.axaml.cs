using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GenHub.Features.GitHub.Views
{
    public partial class LoadingOverlay : UserControl
    {
        /// <summary>
        /// Defines the Text styled property
        /// </summary>
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<LoadingOverlay, string>(nameof(Text), defaultValue: "Loading...");

        /// <summary>
        /// Gets or sets the text to display in the loading overlay
        /// </summary>
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
