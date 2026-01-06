namespace GenHub.Windows.Features.ActionSets.UI;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Messages;
using GenHub.Core.Models.GameInstallations;
using GenHub.Windows.Features.ActionSets.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

/// <summary>
/// View model for an individual action set.
/// </summary>
public partial class ActionSetViewModel : ObservableObject
{
    /// <summary>
    /// Gets the underlying action set.
    /// </summary>
    public IActionSet ActionSet { get; }

    private readonly GameInstallation _installation;
    private readonly IRegistryService _registryService;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets the title of the action set.
    /// </summary>
    public string Title => ActionSet.Title;

    /// <summary>
    /// Gets the description of the action set.
    /// </summary>
    public string Description => $"Fix ID: {ActionSet.Id}"; // Placeholder description

    /// <summary>
    /// Gets a value indicating whether this is a core fix.
    /// </summary>
    public bool IsCore => ActionSet.IsCoreFix;

    [ObservableProperty]
    private bool isApplicable;

    [ObservableProperty]
    private bool isApplied;

    [ObservableProperty]
    private AsyncRelayCommand _applyCommand;

    [ObservableProperty]
    private AsyncRelayCommand _forceApplyCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionSetViewModel"/> class.
    /// </summary>
    /// <param name="actionSet">The action set.</param>
    /// <param name="installation">The game installation.</param>
    /// <param name="registryService">The registry service.</param>
    /// <param name="logger">The logger instance.</param>
    public ActionSetViewModel(IActionSet actionSet, GameInstallation installation, IRegistryService registryService, ILogger logger)
    {
        ActionSet = actionSet;
        _installation = installation;
        _registryService = registryService;
        _logger = logger;
        _applyCommand = new AsyncRelayCommand(ApplyAsync);
        _forceApplyCommand = new AsyncRelayCommand(ForceApplyAsync);
    }

    /// <summary>
    /// Checks the status of the action set (applicable and applied).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckStatusAsync()
    {
        IsApplicable = await ActionSet.IsApplicableAsync(_installation);
        IsApplied = await ActionSet.IsAppliedAsync(_installation);
    }

    private async Task ApplyAsync()
    {
        if (!this.registryService.IsRunningAsAdministrator())
        {
            WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                "Administrator privileges required. Please restart GenHub as Administrator to apply this fix.",
                IsError: true));
            return;
        }

        await ActionSet.ApplyAsync(_installation);
        await CheckStatusAsync();
    }

    private async Task ForceApplyAsync()
    {
        if (!_registryService.IsRunningAsAdministrator())
        {
            WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                "Administrator privileges required for force apply. Please restart GenHub as Administrator.",
                IsError: true));
            return;
        }

        try
        {
            await ActionSet.ApplyAsync(this.installation);
            await CheckStatusAsync();
        }
        catch (System.Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new ToolStatusMessage(
                $"Failed to apply fix: {ex.Message}",
                ToolMessageType.Error));
        }
    }
}
