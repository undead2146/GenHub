namespace GenHub.Windows.Features.ActionSets.UI;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Windows.Features.ActionSets.Infrastructure;
using Microsoft.Extensions.Logging;

/// <summary>
/// ViewModel for the GenPatcher feature.
/// </summary>
public partial class GenPatcherViewModel : ObservableObject
{
    private readonly IActionSetOrchestrator orchestrator;
    private readonly IGameInstallationDetector installationDetector;
    private readonly IRegistryService registryService;
    private readonly INotificationService notificationService;
    private readonly ILogger<GenPatcherViewModel> logger;
    private GameInstallation? currentInstallation;

    [ObservableProperty]
    private AsyncRelayCommand loadFixesCommand;

    [ObservableProperty]
    private AsyncRelayCommand applyAllFixesCommand;

    [ObservableProperty]
    private ObservableCollection<ActionSetViewModel> actionSets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GenPatcherViewModel"/> class.
    /// </summary>
    /// <param name="orchestrator">The action set orchestrator.</param>
    /// <param name="installationDetector">The game installation detector.</param>
    /// <param name="registryService">The registry service.</param>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="logger">The logger.</param>
    public GenPatcherViewModel(
        IActionSetOrchestrator orchestrator,
        IGameInstallationDetector installationDetector,
        IRegistryService registryService,
        INotificationService notificationService,
        ILogger<GenPatcherViewModel> logger)
    {
        this.orchestrator = orchestrator;
        this.installationDetector = installationDetector;
        this.registryService = registryService;
        this.notificationService = notificationService;
        this.logger = logger;
        this.loadFixesCommand = new AsyncRelayCommand(LoadFixesAsync);
        this.applyAllFixesCommand = new AsyncRelayCommand(ApplyAllFixesAsync);
    }

    /// <summary>
    /// Initializes the ViewModel asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        if (!this.registryService.IsRunningAsAdministrator())
        {
            this.notificationService.ShowWarning(
                "Administrator Rights Required",
                "Please restart GenHub as Administrator to ensure GenPatcher can apply registry-based fixes.");
        }

        await LoadFixesCommand.ExecuteAsync(null);
    }

    private async Task LoadFixesAsync()
    {
        try
        {
            this.notificationService.ShowInfo(
                "Loading GenPatcher",
                "Detecting game installations and loading available fixes...");

            var result = await this.installationDetector.DetectInstallationsAsync();
            var detected = result.Items;
            GameInstallation? preferred = null;
            foreach (var item in detected)
            {
                if (item.InstallationType != GameInstallationType.Unknown)
                {
                    preferred = item;
                    break;
                }
            }

            this.currentInstallation = preferred ?? (detected.Count > 0 ? detected[0] : null);

            if (this.currentInstallation == null)
            {
                this.logger.LogWarning("No valid game installation found for GenPatcher");
                this.notificationService.ShowError(
                    "No Game Installation Found",
                    "Please ensure Command & Conquer Generals or Zero Hour is installed.");
                return;
            }

            var fixes = this.orchestrator.GetAllActionSets();
            ActionSets.Clear();

            var installation = currentInstallation;

            // Parallelize status checks to prevent UI blocking
            var tasks = new List<Task<ActionSetViewModel>>();
            foreach (var fix in fixes)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var vm = new ActionSetViewModel(fix, installation, registryService, notificationService, logger);
                    await vm.CheckStatusAsync();
                    return vm;
                }));
            }

            var loadedVms = await Task.WhenAll(tasks);
            foreach (var vm in loadedVms)
            {
                ActionSets.Add(vm);
            }

            this.notificationService.ShowSuccess(
                "GenPatcher Loaded",
                $"Successfully loaded {ActionSets.Count} fixes for {this.currentInstallation.InstallationType}.");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load fixes");
            this.notificationService.ShowError(
                "Failed to Load Fixes",
                $"An error occurred while loading fixes: {ex.Message}");
        }
    }

    private async Task ApplyAllFixesAsync()
    {
        if (this.currentInstallation == null) return;

        if (!this.registryService.IsRunningAsAdministrator())
        {
            this.notificationService.ShowError(
                "Administrator Rights Required",
                "Administrator privileges required for 'Apply Recommended'. Please restart GenHub as Administrator.");
            return;
        }

        var applicableFixes = new List<IActionSet>();
        foreach (var vm in ActionSets)
        {
            if (vm.IsApplicable && !vm.IsApplied)
            {
                applicableFixes.Add(vm.ActionSet);
            }
        }

        if (applicableFixes.Count == 0)
        {
            this.notificationService.ShowInfo(
                "No Fixes to Apply",
                "All applicable fixes are already applied.");
            return;
        }

        this.notificationService.ShowInfo(
            "Applying Fixes",
            $"Applying {applicableFixes.Count} recommended fix(es)...");

        var result = await this.orchestrator.ApplyActionSetsAsync(this.currentInstallation, applicableFixes);

        // Refresh status
        foreach(var vm in ActionSets)
        {
            await vm.CheckStatusAsync();
        }

        if (!result.Success)
        {
            this.notificationService.ShowError(
                "Fixes Failed",
                $"Failed to apply some fixes: {string.Join(", ", result.Errors ?? [])}");
        }
        else
        {
            this.notificationService.ShowSuccess(
                "Fixes Applied",
                $"Successfully applied {applicableFixes.Count} fix(es).");
        }
    }
}
