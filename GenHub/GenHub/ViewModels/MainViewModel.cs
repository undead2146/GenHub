using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core;
using GenHub.Services;

namespace GenHub.ViewModels;

public partial class MainViewModel(GameDetectionService gameDetectionService) : ViewModelBase
{
    /// <summary>
    /// Design-time only constructor
    /// </summary>
    public MainViewModel() : this(new GameDetectionService(new DummyGameDetector())) { }

    [ObservableProperty]
    private string? _vanillaGamePath;

    [ObservableProperty]
    private string? _zeroHourGamePath;

    [RelayCommand]
    private void Detect()
    {
        gameDetectionService.DetectGames();

        VanillaGamePath = gameDetectionService.VanillaGamePath;
        ZeroHourGamePath = gameDetectionService.ZerHourGamePath;
    }
}