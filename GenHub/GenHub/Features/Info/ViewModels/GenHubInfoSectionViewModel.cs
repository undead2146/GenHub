using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Interfaces.Info;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Messages;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Info;
using GenHub.Features.AppUpdate.ViewModels;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.Info.ViewModels;

/// <summary>
/// ViewModel for the GenHub information section, managing detailed feature explanations and guides.
/// </summary>
/// <param name="contentProvider">The info content provider.</param>
/// <param name="changelogsViewModel">The changelogs view model.</param>
/// <param name="notificationService">Optional notification service for demo actions.</param>
public partial class GenHubInfoSectionViewModel(
    IInfoContentProvider contentProvider,
    ChangelogsViewModel changelogsViewModel,
    INotificationService? notificationService = null) : ObservableObject, IInfoSectionViewModel
{
    [ObservableProperty]
    private InfoSectionViewModel? _selectedSection;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isPaneOpen;

    /// <inheritdoc/>
    public string Title => "GenHub Guide";

    /// <inheritdoc/>
    public string IconKey => "InformationOutline";

    /// <inheritdoc/>
    public int Order => 1;

    /// <summary>
    /// Gets the available info sections.
    /// </summary>
    public ObservableCollection<InfoSectionViewModel> Sections { get; } = [];

    /// <summary>
    /// Gets the demo profile card for interactive demonstrations.
    /// </summary>
    public GameProfileItemViewModel? DemoProfileCard { get; private set; }

    /// <summary>
    /// Gets the demo update notification for interactive demonstrations.
    /// </summary>
    public UpdateNotificationViewModel? DemoUpdateNotification { get; private set; }

    /// <summary>
    /// Gets the changelogs view model.
    /// </summary>
    public ChangelogsViewModel Changelogs => changelogsViewModel;

    /// <summary>
    /// Gets the demo game settings for interactive demonstrations.
    /// </summary>
    public GameSettingsViewModel? DemoGameSettings { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the Game Profiles section is selected.
    /// </summary>
    public bool IsGameProfilesSelected => SelectedSection?.Id == "game-profiles";

    /// <summary>
    /// Gets a value indicating whether the App Updates section is selected.
    /// </summary>
    public bool IsAppUpdatesSelected => SelectedSection?.Id == "app-updates";

    /// <summary>
    /// Gets a value indicating whether the Changelogs section is selected.
    /// </summary>
    public bool IsChangelogsSelected => SelectedSection?.Id == "changelogs";

    /// <summary>
    /// Gets a value indicating whether the Profile Settings section is selected.
    /// </summary>
    public bool IsProfileSettingsSelected => SelectedSection?.Id == "profile-settings";

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (Sections.Any())
        {
            // Already initialized, but load changelogs if not loaded
            if (Changelogs.Releases.Count == 0)
            {
                await Changelogs.LoadChangelogsAsync();
            }

            return;
        }

        var sections = await contentProvider.GetAllSectionsAsync();

        foreach (var section in sections)
        {
            Sections.Add(MapToViewModel(section));
        }

        SelectedSection = Sections.FirstOrDefault();

        // Load changelogs automatically
        await Changelogs.LoadChangelogsAsync();

        // Initialize demo ViewModels
        DemoProfileCard = DemoViewModelFactory.CreateDemoProfileCard(notificationService);
        DemoUpdateNotification = DemoViewModelFactory.CreateDemoUpdateViewModel();
        DemoGameSettings = DemoViewModelFactory.CreateDemoGameSettingsViewModel();

        OnPropertyChanged(nameof(DemoProfileCard));
        OnPropertyChanged(nameof(DemoUpdateNotification));
        OnPropertyChanged(nameof(DemoGameSettings));
    }

    private static InfoSectionViewModel MapToViewModel(InfoSection section)
    {
        var vm = new InfoSectionViewModel(section);
        return vm;
    }

    /// <summary>
    /// Handles an action from an info card.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    [RelayCommand]
    private static void HandleAction(InfoAction action)
    {
        if (string.IsNullOrEmpty(action.ActionId))
        {
            return;
        }

        if (action.ActionId.StartsWith("NAV_", StringComparison.OrdinalIgnoreCase))
        {
            var tabName = action.ActionId[4..];
            if (Enum.TryParse<NavigationTab>(tabName, true, out var tab))
            {
                WeakReferenceMessenger.Default.Send(new NavigationMessage(tab));
            }
        }
        else if (action.ActionId.StartsWith("URL_", StringComparison.OrdinalIgnoreCase))
        {
            var url = action.ActionId[4..];
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
    }

    /// <summary>
    /// Toggles the expanded state of a card.
    /// </summary>
    /// <param name="card">The card to toggle.</param>
    [RelayCommand]
    private static void ToggleCardExpansion(InfoCardViewModel card)
    {
        if (card.IsExpandable)
        {
            card.IsExpanded = !card.IsExpanded;
        }
    }

    partial void OnSelectedSectionChanged(InfoSectionViewModel? value)
    {
        OnPropertyChanged(nameof(IsGameProfilesSelected));
        OnPropertyChanged(nameof(IsAppUpdatesSelected));
        OnPropertyChanged(nameof(IsProfileSettingsSelected));
        OnPropertyChanged(nameof(IsChangelogsSelected));

        if (IsChangelogsSelected && !Changelogs.Releases.Any() && !Changelogs.IsLoading)
        {
            _ = Changelogs.LoadChangelogsAsync();
        }
    }
}
