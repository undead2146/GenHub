using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core;
using GenHub.Services;

namespace GenHub.ViewModels;

public partial class MainViewModel(GameDetectionService gameDetectionService) : ViewModelBase
{
    [ObservableProperty]
    private string? _gamePath;

    [RelayCommand]
    private void Detect()
    {
        GamePath = gameDetectionService.GamePath;
    }
}