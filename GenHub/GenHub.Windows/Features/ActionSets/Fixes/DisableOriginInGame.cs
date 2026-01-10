namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix that disables Origin in-game overlay for Generals and Zero Hour.
/// The Origin overlay can cause performance issues and conflicts with the game.
/// </summary>
public class DisableOriginInGame(ILogger<DisableOriginInGame> logger) : BaseActionSet(logger)
{
    private readonly ILogger<DisableOriginInGame> _logger = logger;

    /// <inheritdoc/>
    public override string Id => "DisableOriginInGame";

    /// <inheritdoc/>
    public override string Title => "Disable Origin In-Game Overlay";

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
            // Check if Origin is installed
            var originInstalled = IsOriginInstalled();

            if (!originInstalled)
            {
                // If Origin is not installed, consider this fix "applied"
                return Task.FromResult(true);
            }

            // Check if Origin in-game overlay is disabled
            var overlayDisabled = IsOriginOverlayDisabled();

            return Task.FromResult(overlayDisabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Origin overlay status");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        try
        {
            var originInstalled = IsOriginInstalled();

            if (!originInstalled)
            {
                _logger.LogInformation("Origin is not installed. No action needed.");
                return Task.FromResult(new ActionSetResult(true));
            }

            // Check if overlay is already disabled
            if (IsOriginOverlayDisabled())
            {
                _logger.LogInformation("Origin in-game overlay is already disabled.");
                return Task.FromResult(new ActionSetResult(true));
            }

            // Provide guidance for disabling Origin overlay
            _logger.LogWarning("Origin in-game overlay is enabled. This may cause performance issues.");
            _logger.LogInformation("To disable Origin in-game overlay:");
            _logger.LogInformation("1. Open Origin client");
            _logger.LogInformation("2. Go to 'Application Settings' (gear icon)");
            _logger.LogInformation("3. Select 'Origin In-Game'");
            _logger.LogInformation("4. Uncheck 'Enable Origin In-Game'");
            _logger.LogInformation("5. Click 'Save'");
            _logger.LogInformation(string.Empty);
            _logger.LogInformation("Alternatively, you can disable it per game:");
            _logger.LogInformation("1. Right-click on Generals or Zero Hour in Origin");
            _logger.LogInformation("2. Select 'Game Properties'");
            _logger.LogInformation("3. Uncheck 'Enable Origin In-Game for this game'");
            _logger.LogInformation("4. Click 'Save'");

            return Task.FromResult(new ActionSetResult(true, "Please manually disable Origin in-game overlay. See logs for details."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying Origin overlay disable fix");
            return Task.FromResult(new ActionSetResult(false, ex.Message));
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Disable Origin In-Game Fix is informational only. No undo action needed.");
        return Task.FromResult(new ActionSetResult(true));
    }

    private bool IsOriginInstalled()
    {
        try
        {
            // Check for Origin in registry
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Origin",
                false);

            if (key != null)
            {
                return true;
            }

            // Check for Origin processes
            var processes = Process.GetProcessesByName("Origin");
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for Origin installation");
            return false;
        }
    }

    private bool IsOriginOverlayDisabled()
    {
        try
        {
            // Check Origin configuration file
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var originConfigPath = Path.Combine(localAppData, "Origin", "Origin.ini");

            if (!File.Exists(originConfigPath))
            {
                return false;
            }

            var configContent = File.ReadAllText(originConfigPath);

            // Check if overlay is disabled
            // The setting is typically in the format: [General] OverlayEnabled=0
            return configContent.Contains("OverlayEnabled=0", StringComparison.OrdinalIgnoreCase) ||
                   configContent.Contains("OverlayEnabled=false", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Origin overlay configuration");
            return false;
        }
    }
}
