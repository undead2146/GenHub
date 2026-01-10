namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Interfaces.GameSettings;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.GameSettings;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix that improves edge scrolling for modern high-resolution displays.
/// This fix adjusts edge scrolling sensitivity in Options.ini to ensure
/// smooth scrolling when mouse cursor reaches the screen edge.
/// </summary>
public class EdgeScrollerFix(ILogger<EdgeScrollerFix> logger, IGameSettingsService gameSettingsService) : BaseActionSet(logger)
{
    private readonly ILogger<EdgeScrollerFix> _logger = logger;
    private readonly IGameSettingsService _gameSettingsService = gameSettingsService;

    /// <inheritdoc/>
    public override string Id => "EdgeScrollerFix";

    /// <inheritdoc/>
    public override string Title => "Edge Scrolling Fix";

    /// <inheritdoc/>
    public override bool IsCoreFix => false;

    /// <inheritdoc/>
    public override bool IsCrucialFix => false;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        return Task.FromResult(installation.HasGenerals || installation.HasZeroHour);
    }

    /// <inheritdoc/>
    public override async Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        try
        {
            if (installation.HasGenerals)
            {
                var result = await _gameSettingsService.LoadOptionsAsync(GameType.Generals);
                if (!result.Success || result.Data == null || !IsEdgeScrollingOptimal(result.Data))
                {
                    return false;
                }
            }

            if (installation.HasZeroHour)
            {
                var result = await _gameSettingsService.LoadOptionsAsync(GameType.ZeroHour);
                if (!result.Success || result.Data == null || !IsEdgeScrollingOptimal(result.Data))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking edge scrolling status");
            return false;
        }
    }

    /// <inheritdoc/>
    protected override async Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        try
        {
            var details = new List<string>();

            if (installation.HasGenerals)
            {
                var gameDetails = await ApplyEdgeScrollingFixAsync(GameType.Generals, cancellationToken);
                details.AddRange(gameDetails);
            }

            if (installation.HasZeroHour)
            {
                var gameDetails = await ApplyEdgeScrollingFixAsync(GameType.ZeroHour, cancellationToken);
                details.AddRange(gameDetails);
            }

            if (details.Count == 0)
            {
                details.Add("No games found to apply edge scrolling fix to.");
            }

            return new ActionSetResult(true, null, details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying edge scrolling fix");
            return new ActionSetResult(false, ex.Message, [$"Error: {ex.Message}"]);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Undoing Edge Scrolling Fix is not supported via GenHub.");
        return Task.FromResult(new ActionSetResult(true, null, ["Undo not supported for Edge Scrolling Fix."]));
    }

    private bool IsEdgeScrollingOptimal(IniOptions options)
    {
        // Check if edge scrolling settings are optimal
        // The key settings are:
        // - ScrollEdgeZone: The size of edge detection zone (default: 0.1, optimal: 0.05-0.15)
        // - ScrollEdgeSpeed: The speed of edge scrolling (default: 1.0, optimal: 1.0-2.0)

        if (!options.AdditionalSections.TryGetValue("TheSuperHackers", out var tshSection))
        {
            return false;
        }

        string scrollEdgeZone = "0.1";
        string scrollEdgeSpeed = "1.0";

        if (tshSection.TryGetValue("ScrollEdgeZone", out var zoneValue)) scrollEdgeZone = zoneValue;
        if (tshSection.TryGetValue("ScrollEdgeSpeed", out var speedValue)) scrollEdgeSpeed = speedValue;

        // Parse values
        if (double.TryParse(scrollEdgeZone, out var zone) && double.TryParse(scrollEdgeSpeed, out var speed))
        {
            // Check if settings are within optimal range
            return zone >= 0.05 && zone <= 0.15 && speed >= 1.0 && speed <= 2.0;
        }

        return false;
    }

    private async Task<List<string>> ApplyEdgeScrollingFixAsync(GameType gameType, CancellationToken cancellationToken)
    {
        var details = new List<string>();

        try
        {
            _logger.LogInformation("Applying edge scrolling fix for {GameType}", gameType);

            var result = await _gameSettingsService.LoadOptionsAsync(gameType);
            if (!result.Success || result.Data == null)
            {
                var msg = $"⚠ Could not load Options.ini for {gameType}";
                details.Add(msg);
                _logger.LogWarning("Could not load settings for {GameType}", gameType);
                return details;
            }

            var options = result.Data;
            var optionsPath = _gameSettingsService.GetOptionsFilePath(gameType);

            // Apply optimal edge scrolling settings
            if (!options.AdditionalSections.TryGetValue("TheSuperHackers", out var tshSection))
            {
                tshSection = new Dictionary<string, string>();
                options.AdditionalSections["TheSuperHackers"] = tshSection;
                details.Add($"✓ Created [TheSuperHackers] section in Options.ini for {gameType}");
            }

            tshSection["ScrollEdgeZone"] = "0.1";
            tshSection["ScrollEdgeSpeed"] = "1.5";
            tshSection["ScrollEdgeAcceleration"] = "1.0";

            details.Add($"✓ Set ScrollEdgeZone=0.1 for {gameType}");
            details.Add($"✓ Set ScrollEdgeSpeed=1.5 for {gameType}");
            details.Add($"✓ Set ScrollEdgeAcceleration=1.0 for {gameType}");

            await _gameSettingsService.SaveOptionsAsync(gameType, options);

            details.Add($"✓ Saved Options.ini: {optionsPath}");
            _logger.LogInformation("Successfully applied edge scrolling fix for {GameType}", gameType);
        }
        catch (Exception ex)
        {
            details.Add($"✗ Error applying edge scrolling for {gameType}: {ex.Message}");
            _logger.LogError(ex, "Error applying edge scrolling fix for {GameType}", gameType);
        }

        return details;
    }
}
