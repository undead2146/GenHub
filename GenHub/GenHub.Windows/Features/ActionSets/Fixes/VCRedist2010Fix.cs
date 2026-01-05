using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GenHub.Windows.Features.ActionSets.Fixes;

/// <summary>
/// Installs the Visual C++ 2010 Redistributable (x86) which is required for Generals/Zero Hour.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="logger">The logger instance.</param>
public class VCRedist2010Fix(IHttpClientFactory httpClientFactory, ILogger<VCRedist2010Fix> logger) : BaseActionSet(logger)
{
    /// <summary>
    /// Gets the description of the fix.
    /// </summary>
    public static string Description => "Mandatory dependency for C&C Generals and Zero Hour errors.";

    /// <inheritdoc/>
    public override string Id => "VCRedist2010";

    /// <inheritdoc/>
    public override string Title => "Visual C++ 2010 Runtime";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // This fix is applicable regardless of installation path as it's a system dependency
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        try
        {
            // Check specific registry key for VC++ 2010 x86
            using var key = Registry.LocalMachine.OpenSubKey(RegistryConstants.VCRedist2010x86Key);
            if (key != null)
            {
                var val = key.GetValue(RegistryConstants.InstalledValueName);
                if (val != null && (int)val == 1)
                {
                    return Task.FromResult(true);
                }
            }

            // Fallback check: try WOW6432Node
            using var key64 = Registry.LocalMachine.OpenSubKey(RegistryConstants.VCRedist2010x86KeyWow64);
            if (key64 != null)
            {
                var val = key64.GetValue(RegistryConstants.InstalledValueName);
                if (val != null && (int)val == 1)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check VCRedist registry status");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "vcredist_x86_2010.exe");

            logger.LogInformation("Downloading VCRedist 2010 from {Url}", ExternalUrls.VCRedist2010DownloadUrl);

            using var client = httpClientFactory.CreateClient("Downloader");
            using var response = await client.GetAsync(ExternalUrls.VCRedist2010DownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(tempPath, FileMode.Create);
            await response.Content.CopyToAsync(fs, cancellationToken);
            fs.Close();

            logger.LogInformation("Installing VCRedist 2010...");

            var psi = new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/q /norestart", // Silent install
                UseShellExecute = true,
                Verb = "runas", // Request elevation just in case
            };

            var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);

                // 3010 is restart required
                if (process.ExitCode != ProcessConstants.ExitCodeSuccess && process.ExitCode != 3010)
                {
                    logger.LogWarning("VCRedist install exited with code {Code}", process.ExitCode);
                    return Failure($"VCRedist install failed with code {process.ExitCode}");
                }
                else
                {
                    logger.LogInformation("VCRedist 2010 installed successfully");
                }
            }

            // Cleanup
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install VCRedist 2010");
            return Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        logger.LogWarning("Uninstalling VCRedist 2010 is not supported via GenHub.");
        return Task.FromResult(Success());
    }
}
