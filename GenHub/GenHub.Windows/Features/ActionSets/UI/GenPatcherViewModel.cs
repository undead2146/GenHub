namespace GenHub.Windows.Features.ActionSets.UI;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Messages;
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
    /// <param name="logger">The logger.</param>
    public GenPatcherViewModel(
        IActionSetOrchestrator orchestrator,
        IGameInstallationDetector installationDetector,
        IRegistryService registryService,
        ILogger<GenPatcherViewModel> logger)
    {
        this.orchestrator = orchestrator;
        this.installationDetector = installationDetector;
        this.registryService = registryService;
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
            WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                "Please restart GenHub as Administrator to ensure GenPatcher can apply registry-based fixes.",
                IsInfo: true));
        }

        await LoadFixesCommand.ExecuteAsync(null);
    }

    private async Task LoadFixesAsync()
    {
        try
        {
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
                WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                    "No valid game installation found. Please ensure Command & Conquer Generals or Zero Hour is installed.",
                    ToolMessageType.Error));
                return;
            }

            var fixes = this.orchestrator.GetAllActionSets();
            ActionSets.Clear();

            foreach (var fix in fixes)
            {
                var vm = new ActionSetViewModel(fix, currentInstallation, registryService, logger);
                await vm.CheckStatusAsync();
                ActionSets.Add(vm);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load fixes");
        }
    }

    private async Task ApplyAllFixesAsync()
    {
        if (this.currentInstallation == null) return;

        if (!this.registryService.IsRunningAsAdministrator())
        {
            WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                "Administrator privileges required for 'Apply Recommended'. Please restart GenHub as Administrator.",
                IsError: true));
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

        await this.orchestrator.ApplyActionSetsAsync(this.currentInstallation, applicableFixes);

        // Refresh status
        foreach(var vm in ActionSets)
        {
            await vm.CheckStatusAsync();
        }
    }
}
