using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Features.GameInstallations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenHub.ViewModels;

/// <summary>
/// Main view model for the application.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IGameInstallationDetectionOrchestrator _gameInstallationDetectionOrchestrator;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        if (AppLocator.Services is null)
        {
            throw new System.InvalidOperationException("AppLocator.Services is not initialized");
        }

        var detectors = AppLocator.Services.GetServices<IGameInstallationDetector>();
        var logger = AppLocator.Services.GetRequiredService<ILogger<GameInstallationDetectionOrchestrator>>();
        _gameInstallationDetectionOrchestrator = new GameInstallationDetectionOrchestrator(detectors, logger);
        _logger = AppLocator.Services.GetRequiredService<ILogger<MainViewModel>>();

        GameInstallations = new ObservableCollection<string>();
    }

    /// <summary>
    /// Gets the collection of detected game installations.
    /// </summary>
    public ObservableCollection<string> GameInstallations { get; }

    /// <summary>
    /// Scans for game installations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ScanForGamesAsync()
    {
        _logger.LogInformation("Starting game installation scan");

        try
        {
            GameInstallations.Clear();

            var result = await _gameInstallationDetectionOrchestrator.DetectAllInstallationsAsync();

            if (result.Success)
            {
                foreach (var installation in result.Items)
                {
                    GameInstallations.Add(installation.ToString());
                }

                _logger.LogInformation("Found {Count} game installations", result.Items.Count);
            }
            else
            {
                _logger.LogWarning("Game installation scan failed: {Errors}", string.Join("; ", result.Errors));
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error occurred during game installation scan");
        }
    }
}
