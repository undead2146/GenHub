using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;

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

        private readonly ILogger<LoadingOverlay>? _logger;

        public LoadingOverlay()
        {
            try
            {
                InitializeComponent();
                _logger = AppLocator.GetServiceOrDefault<ILogger<LoadingOverlay>>();
                _logger?.LogDebug("LoadingOverlay initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing LoadingOverlay: {ex.Message}");
                throw;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Sets the status message displayed in the loading overlay
        /// </summary>
        /// <param name="message">The status message to display</param>
        public void SetStatusMessage(string message)
        {
            try
            {
                if (this.FindControl<TextBlock>("StatusText") is TextBlock statusText)
                {
                    statusText.Text = message ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting status message");
            }
        }
    }
}
