namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix that creates or fixes start menu shortcuts for Generals and Zero Hour.
/// This fix ensures proper shortcuts are available in Windows Start Menu.
/// </summary>
public class StartMenuFix(ILogger<StartMenuFix> logger) : BaseActionSet(logger)
{
    private readonly ILogger<StartMenuFix> _logger = logger;

    /// <inheritdoc/>
    public override string Id => "StartMenuFix";

    /// <inheritdoc/>
    public override string Title => "Start Menu Shortcuts";

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
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        try
        {
            // Check if start menu shortcuts exist
            var shortcutsExist = DoShortcutsExist(installation);

            return Task.FromResult(shortcutsExist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking start menu shortcuts status");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        var details = new List<string>();

        try
        {
            details.Add("Start Menu Shortcuts Fix - Informational");
            details.Add("");
            details.Add("⚠ NOTE: This fix is informational only");
            details.Add("  Shortcuts are typically created during game installation");
            details.Add("");
            details.Add("To create shortcuts manually:");
            details.Add("  1. Right-click on game executable");
            details.Add("  2. Select 'Show more options' > 'Create shortcut'");
            details.Add("  3. Move shortcut to Start Menu folder");
            details.Add("");
            details.Add("Or use GenHub to create shortcuts:");
            details.Add("  1. Open GenHub");
            details.Add("  2. Go to game installation");
            details.Add("  3. Click 'Create Shortcuts' button");
            details.Add("");

            // Check current status
            var shortcutsExist = DoShortcutsExist(installation);
            if (shortcutsExist)
            {
                details.Add("✓ Start Menu shortcuts already exist");
            }
            else
            {
                details.Add("⚠ No Start Menu shortcuts found");
                details.Add("  Use GenHub's shortcut creation feature to add them");
            }

            _logger.LogInformation("Start Menu Shortcuts Information:");
            _logger.LogInformation("Shortcuts can be created through GenHub's interface.");
            _logger.LogInformation(string.Empty);
            _logger.LogInformation("To create shortcuts manually:");
            _logger.LogInformation("1. Right-click on game executable");
            _logger.LogInformation("2. Select 'Show more options' > 'Create shortcut'");
            _logger.LogInformation("3. Move shortcut to desired location");
            _logger.LogInformation(string.Empty);
            _logger.LogInformation("Or use GenHub to create shortcuts:");
            _logger.LogInformation("1. Open GenHub");
            _logger.LogInformation("2. Go to game installation");
            _logger.LogInformation("3. Click 'Create Shortcuts' button");

            return Task.FromResult(new ActionSetResult(true, "Start menu shortcuts can be created through GenHub. See details for instructions.", details));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying start menu shortcuts fix");
            details.Add($"✗ Error: {ex.Message}");
            return Task.FromResult(new ActionSetResult(false, ex.Message, details));
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Start Menu Shortcuts Fix is informational only. No undo action needed.");
        return Task.FromResult(new ActionSetResult(true));
    }

    private bool DoShortcutsExist(GameInstallation installation)
    {
        try
        {
            // Check for shortcuts in common start menu locations
            var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var programsPath = Path.Combine(startMenuPath, "Programs");

            var shortcutNames = new List<string>();

            if (installation.HasGenerals)
            {
                shortcutNames.AddRange(new[]
                {
                    "Command & Conquer Generals.lnk",
                    "Generals.lnk",
                    "C&C Generals.lnk",
                });
            }

            if (installation.HasZeroHour)
            {
                shortcutNames.AddRange(new[]
                {
                    "Command & Conquer Generals Zero Hour.lnk",
                    "Generals Zero Hour.lnk",
                    "C&C Zero Hour.lnk",
                    "Zero Hour.lnk",
                });
            }

            var foundShortcuts = 0;
            foreach (var shortcutName in shortcutNames)
            {
                var shortcutPath = Path.Combine(programsPath, shortcutName);
                if (File.Exists(shortcutPath))
                {
                    _logger.LogInformation("Found shortcut: {Shortcut}", shortcutName);
                    foundShortcuts++;
                }
            }

            return foundShortcuts > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for shortcuts");
            return false;
        }
    }
}
