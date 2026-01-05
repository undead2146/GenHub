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
    private readonly IActionSetOrchestrator _orchestrator;
    private readonly IGameInstallationDetector _installationDetector;
    private readonly IRegistryService _registryService;
    private readonly ILogger<GenPatcherViewModel> _logger;
    private GameInstallation? _currentInstallation;

    [ObservableProperty]
    private AsyncRelayCommand _loadFixesCommand;

    [ObservableProperty]
    private AsyncRelayCommand _applyAllFixesCommand;

    [ObservableProperty]
    private ObservableCollection<ActionSetViewModel> _actionSets = [];

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
        _orchestrator = orchestrator;
        _installationDetector = installationDetector;
        _registryService = registryService;
        _logger = logger;
        _loadFixesCommand = new AsyncRelayCommand(LoadFixesAsync);
        _applyAllFixesCommand = new AsyncRelayCommand(ApplyAllFixesAsync);
    }

    /// <summary>
    /// Initializes the ViewModel asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        if (!_registryService.IsRunningAsAdministrator())
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
            var result = await _installationDetector.DetectInstallationsAsync();
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

            _currentInstallation = preferred ?? (detected.Count > 0 ? detected[0] : null);

            if (_currentInstallation == null)
            {
                // Handle no installation found
                return;
            }

            var fixes = _orchestrator.GetAllActionSets();
            ActionSets.Clear();

            foreach (var fix in fixes)
            {
                var vm = new ActionSetViewModel(fix, _currentInstallation, _registryService, _logger);
                await vm.CheckStatusAsync();
                ActionSets.Add(vm);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load fixes");
        }
    }

    private async Task ApplyAllFixesAsync()
    {
        if (_currentInstallation == null) return;

        if (!_registryService.IsRunningAsAdministrator())
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

        await _orchestrator.ApplyActionSetsAsync(_currentInstallation, applicableFixes);

        // Refresh status
        foreach(var vm in ActionSets)
        {
            await vm.CheckStatusAsync();
        }
    }
}
