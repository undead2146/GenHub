using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Windows.Features.ActionSets.Fixes;

/// <summary>
/// Installs the Zero Hour 1.04 official patch.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="logger">The logger instance.</param>
public class Patch104Fix(IHttpClientFactory httpClientFactory, ILogger<Patch104Fix> logger) : BaseActionSet(logger)
{
    /// <summary>
    /// Gets the description of the fix.
    /// </summary>
    public static string Description => "Official Zero Hour 1.04 patch - required for multiplayer and compatibility.";

    /// <inheritdoc/>
    public override string Id => "Patch104";

    /// <inheritdoc/>
    public override string Title => "Zero Hour 1.04 Patch";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // Only applicable for Zero Hour installations
        return Task.FromResult(installation.HasZeroHour);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        try
        {
            // Check if game.exe version is 1.04
            var gameExePath = Path.Combine(installation.ZeroHourPath, ActionSetConstants.FileNames.GameExe);
            if (!File.Exists(gameExePath))
            {
                return Task.FromResult(false);
            }

            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(gameExePath);
            var version = versionInfo.FileVersion;

            // 1.04 version should be 1.4.0.0 or similar
            if (version != null && version.StartsWith("1.4"))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check Zero Hour patch version");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        var details = new List<string>();

        try
        {
            details.Add("Starting Zero Hour 1.04 patch installation...");
            details.Add($"Target directory: {installation.ZeroHourPath}");

            var tempPath = Path.Combine(Path.GetTempPath(), "zh104_patch.zip");
            var extractPath = Path.Combine(Path.GetTempPath(), "zh104_extract");

            details.Add($"Download URL: {ExternalUrls.ZeroHour104PatchUrl}");
            details.Add("Downloading patch archive...");

            logger.LogInformation("Downloading Zero Hour 1.04 patch from {Url}", ExternalUrls.ZeroHour104PatchUrl);

            using var client = httpClientFactory.CreateClient("Downloader");
            using var response = await client.GetAsync(ExternalUrls.ZeroHour104PatchUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var fileSize = response.Content.Headers.ContentLength ?? 0;
            details.Add($"✓ Downloaded {fileSize / 1024 / 1024:F2} MB");

            using var fs = new FileStream(tempPath, FileMode.Create);
            await response.Content.CopyToAsync(fs, cancellationToken);
            fs.Close();

            details.Add("Extracting patch files...");
            logger.LogInformation("Extracting Zero Hour 1.04 patch...");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(tempPath, extractPath);

            var extractedFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
            details.Add($"✓ Extracted {extractedFiles.Length} files");

            // Copy files to game directory
            details.Add($"Installing to: {installation.ZeroHourPath}");
            logger.LogInformation("Copying patch files to {Path}", installation.ZeroHourPath);

            int copiedCount = 0;
            foreach (var file in extractedFiles)
            {
                var relativePath = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var destPath = Path.Combine(installation.ZeroHourPath, relativePath);

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(file, destPath, true);
                logger.LogDebug("Copied {File}", relativePath);
                copiedCount++;
            }

            details.Add($"✓ Installed {copiedCount} files");

            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            details.Add("✓ Cleanup completed");
            details.Add("✓ Zero Hour 1.04 patch installed successfully");

            logger.LogInformation("Zero Hour 1.04 patch installed successfully with {Count} actions", details.Count);
            return new ActionSetResult(true, null, details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install Zero Hour 1.04 patch");
            details.Add($"✗ Error: {ex.Message}");
            return new ActionSetResult(false, ex.Message, details);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        logger.LogWarning("Uninstalling Zero Hour 1.04 patch is not supported via GenHub.");
        return Task.FromResult(new ActionSetResult(true));
    }
}
