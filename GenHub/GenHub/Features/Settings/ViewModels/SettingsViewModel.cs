using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using GenHub.Common.ViewModels;

namespace GenHub.Features.Settings.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings tab following MVVM architecture
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ILogger<SettingsViewModel> _logger;

        [ObservableProperty]
        private string _title = "Settings";

        [ObservableProperty]
        private string _description = "Configure application settings and preferences";

        public SettingsViewModel(ILogger<SettingsViewModel> logger)
        {
            _logger = logger;
            _logger.LogDebug("SettingsViewModel initialized");
        }
    }
}
