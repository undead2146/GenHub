namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix that prevents OneDrive from syncing game folders.
/// This fix creates desktop.ini files with ThisPCPolicy=DisableCloudSync
/// to prevent OneDrive from syncing game installation and user data folders.
/// </summary>
public class OneDriveFix(ILogger<OneDriveFix> logger) : BaseActionSet(logger)
{
    private readonly ILogger<OneDriveFix> _logger = logger;

    /// <inheritdoc/>
    public override string Id => "OneDriveFix";

    /// <inheritdoc/>
    public override string Title => "Prevent OneDrive Sync";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

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
            // Check if desktop.ini files exist with ThisPCPolicy=DisableCloudSync
            if (installation.HasGenerals)
            {
                if (!HasOneDriveProtection(installation.GeneralsPath))
                {
                    return Task.FromResult(false);
                }

                var userPath = GetUserDataPath(GameType.Generals);
                if (Directory.Exists(userPath) && !HasOneDriveProtection(userPath))
                {
                    return Task.FromResult(false);
                }
            }

            if (installation.HasZeroHour)
            {
                if (!HasOneDriveProtection(installation.ZeroHourPath))
                {
                    return Task.FromResult(false);
                }

                var userPath = GetUserDataPath(GameType.ZeroHour);
                if (Directory.Exists(userPath) && !HasOneDriveProtection(userPath))
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking OneDrive protection status");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        var details = new List<string>();

        try
        {
            details.Add("Starting OneDrive sync prevention...");
            details.Add("Creating desktop.ini files with ThisPCPolicy=DisableCloudSync");
            details.Add(string.Empty);

            int foldersProtected = 0;

            if (installation.HasGenerals)
            {
                details.Add($"Processing Generals: {installation.GeneralsPath}");
                await ApplyOneDriveProtectionAsync(installation.GeneralsPath, details, cancellationToken);
                foldersProtected++;

                var userPath = GetUserDataPath(GameType.Generals);
                if (Directory.Exists(userPath))
                {
                    details.Add($"Processing Generals user data: {userPath}");
                    await ApplyOneDriveProtectionAsync(userPath, details, cancellationToken);
                    foldersProtected++;
                }
            }

            if (installation.HasZeroHour)
            {
                details.Add($"Processing Zero Hour: {installation.ZeroHourPath}");
                await ApplyOneDriveProtectionAsync(installation.ZeroHourPath, details, cancellationToken);
                foldersProtected++;

                var userPath = GetUserDataPath(GameType.ZeroHour);
                if (Directory.Exists(userPath))
                {
                    details.Add($"Processing Zero Hour user data: {userPath}");
                    await ApplyOneDriveProtectionAsync(userPath, details, cancellationToken);
                    foldersProtected++;
                }
            }

            details.Add(string.Empty);
            details.Add($"✓ Protected {foldersProtected} folders from OneDrive sync");
            details.Add("✓ OneDrive sync prevention completed successfully");

            return new ActionSetResult(true, null, details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying OneDrive protection");
            details.Add($"✗ Error: {ex.Message}");
            return new ActionSetResult(false, ex.Message, details);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Undoing OneDrive protection is not recommended as it may cause sync issues.");
        return Task.FromResult(new ActionSetResult(true));
    }

    private static string GetUserDataPath(GameType gameType)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var folder = gameType == GameType.ZeroHour
            ? GameSettingsConstants.FolderNames.ZeroHour
            : GameSettingsConstants.FolderNames.Generals;
        return Path.Combine(documents, folder);
    }

    private bool HasOneDriveProtection(string path)
    {
        try
        {
            var desktopIniPath = Path.Combine(path, "desktop.ini");
            if (!File.Exists(desktopIniPath))
            {
                return false;
            }

            var content = File.ReadAllText(desktopIniPath);
            return content.Contains("ThisPCPolicy") && content.Contains("DisableCloudSync");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check OneDrive protection for {Path}", path);
            return false;
        }
    }

    private async Task ApplyOneDriveProtectionAsync(string path, List<string> details, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying OneDrive protection to: {Path}", path);

            var desktopIniPath = Path.Combine(path, "desktop.ini");

            // Create desktop.ini content with ThisPCPolicy=DisableCloudSync
            var content = new StringBuilder();
            content.AppendLine("[.ShellClassInfo]");
            content.AppendLine("ThisPCPolicy=DisableCloudSync");
            content.AppendLine("ConfirmFileOp=0");

            // Write desktop.ini file
            await File.WriteAllTextAsync(desktopIniPath, content.ToString(), cancellationToken);

            // Set desktop.ini as hidden and system file
            var attributes = File.GetAttributes(desktopIniPath);
            File.SetAttributes(desktopIniPath, attributes | FileAttributes.Hidden | FileAttributes.System);

            // Set folder as system folder (read-only bit indicates system folder)
            var dirInfo = new DirectoryInfo(path);
            var dirAttributes = dirInfo.Attributes;
            dirInfo.Attributes = dirAttributes | FileAttributes.System;

            details.Add($"  ✓ Created desktop.ini with DisableCloudSync policy");
            _logger.LogInformation("Successfully applied OneDrive protection to: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not apply OneDrive protection to {Path}", path);
            details.Add($"  ⚠ Failed to protect folder: {ex.Message}");
        }
    }
}
