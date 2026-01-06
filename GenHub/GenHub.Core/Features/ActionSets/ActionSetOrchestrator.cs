namespace GenHub.Core.Features.ActionSets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of the ActionSet orchestrator.
/// </summary>
public class ActionSetOrchestrator : IActionSetOrchestrator
{
    private readonly IEnumerable<IActionSet> _actionSets;
    private readonly ILogger<ActionSetOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionSetOrchestrator"/> class.
    /// </summary>
    /// <param name="actionSets">The initial collection of action sets.</param>
    /// <param name="providers">The collection of action set providers.</param>
    /// <param name="logger">The logger instance.</param>
    public ActionSetOrchestrator(
        IEnumerable<IActionSet> actionSets,
        IEnumerable<IActionSetProvider> providers,
        ILogger<ActionSetOrchestrator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var allSets = new List<IActionSet>(actionSets ?? []);

        if (providers != null)
        {
            foreach (var provider in providers)
            {
                try
                {
                    allSets.AddRange(provider.GetActionSets());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load action sets from provider {Provider}", provider.GetType().Name);
                }
            }
        }

        _actionSets = allSets;
    }

    /// <inheritdoc/>
    public IEnumerable<IActionSet> GetAllActionSets() => _actionSets;

    /// <inheritdoc/>
    public async Task<IEnumerable<IActionSet>> GetApplicableCoreFixesAsync(GameInstallation installation)
    {
        var applicable = new List<IActionSet>();
        foreach (var actionSet in _actionSets.Where(x => x.IsCoreFix))
        {
            if (await actionSet.IsApplicableAsync(installation))
            {
                applicable.Add(actionSet);
            }
        }

        return applicable;
    }

    /// <inheritdoc/>
    public async Task<OperationResult<int>> ApplyActionSetsAsync(
        GameInstallation installation,
        IEnumerable<IActionSet> actionSets,
        CancellationToken ct = default)
    {
        int successCount = 0;
        var errors = new List<string>();

        foreach (var actionSet in actionSets)
        {
            if (ct.IsCancellationRequested)
                break;

            // Double check applicability and applied state to avoid redundant work
            if (!await actionSet.IsApplicableAsync(installation))
                continue;

            if (await actionSet.IsAppliedAsync(installation))
                continue;

            var result = await actionSet.ApplyAsync(installation, ct);
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                errors.Add($"Failed to apply {actionSet.Title}: {result.ErrorMessage}");
                if (actionSet.IsCrucialFix)
                {
                    _logger.LogError("Critical fix {Title} failed for {Installation}. Aborting sequence.", actionSet.Title, installation.InstallationPath);
                    return OperationResult<int>.CreateFailure(errors, default);
                }
            }
        }

        if (errors.Count > 0)
        {
            return OperationResult<int>.CreateFailure(errors);
        }

        return OperationResult<int>.CreateSuccess(successCount);
    }
}
