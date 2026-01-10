namespace GenHub.Windows.Features.ActionSets.UI;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Interfaces.Notifications;
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
    private readonly INotificationService _notificationService;
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
    /// <param name="notificationService">The notification service.</param>
    /// <param name="logger">The logger instance.</param>
    public ActionSetViewModel(IActionSet actionSet, GameInstallation installation, IRegistryService registryService, INotificationService notificationService, ILogger logger)
    {
        ActionSet = actionSet;
        _installation = installation;
        _registryService = registryService;
        _notificationService = notificationService;
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
        if (!_registryService.IsRunningAsAdministrator())
        {
            _notificationService.ShowError(
                "Administrator Rights Required",
                "Please restart GenHub as Administrator to apply this fix.");
            return;
        }

        try
        {
            var result = await ActionSet.ApplyAsync(_installation);
            await CheckStatusAsync();

            if (result.Success)
            {
                var detailsText = result.Details.Count > 0
                    ? result.FormatDetails()
                    : $"{ActionSet.Title} has been successfully applied.";

                _notificationService.ShowSuccess(
                    $"Fix Applied: {ActionSet.Title}",
                    detailsText);
            }
            else
            {
                var detailsText = result.Details.Count > 0
                    ? result.FormatDetails()
                    : result.ErrorMessage ?? "Unknown error occurred.";

                _notificationService.ShowError(
                    $"Fix Failed: {ActionSet.Title}",
                    detailsText);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to apply action set {ActionSetId}", ActionSet.Id);
            _notificationService.ShowError(
                "Failed to Apply Fix",
                $"Could not apply {ActionSet.Title}: {ex.Message}");
        }
    }

    private async Task ForceApplyAsync()
    {
        if (!_registryService.IsRunningAsAdministrator())
        {
            _notificationService.ShowError(
                "Administrator Rights Required",
                "Please restart GenHub as Administrator for force apply.");
            return;
        }

        try
        {
            var result = await ActionSet.ApplyAsync(_installation);
            await CheckStatusAsync();

            if (result.Success)
            {
                var detailsText = result.Details.Count > 0
                    ? result.FormatDetails()
                    : $"{ActionSet.Title} has been force applied successfully.";

                _notificationService.ShowSuccess(
                    $"Fix Force Applied: {ActionSet.Title}",
                    detailsText);
            }
            else
            {
                var detailsText = result.Details.Count > 0
                    ? result.FormatDetails()
                    : result.ErrorMessage ?? "Unknown error occurred.";

                _notificationService.ShowError(
                    $"Fix Failed: {ActionSet.Title}",
                    detailsText);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to force apply action set {ActionSetId}", ActionSet.Id);
            _notificationService.ShowError(
                "Failed to Force Apply Fix",
                $"Could not apply {ActionSet.Title}: {ex.Message}");
        }
    }
}
