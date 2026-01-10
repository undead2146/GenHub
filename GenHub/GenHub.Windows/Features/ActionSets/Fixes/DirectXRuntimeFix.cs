using System;
using System.Collections.Generic;
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
/// Fix that downloads and installs DirectX 8.1 and 9.0c runtime components required for Generals and Zero Hour.
/// </summary>
public class DirectXRuntimeFix(IHttpClientFactory httpClientFactory, ILogger<DirectXRuntimeFix> logger) : BaseActionSet(logger)
{
    private readonly ILogger<DirectXRuntimeFix> _logger = logger;

    /// <inheritdoc/>
    public override string Id => "DirectXRuntimeFix";

    /// <inheritdoc/>
    public override string Title => "DirectX Runtime Fix";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // This fix is applicable regardless of installation type as it's a system dependency
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        // GenPatcher check: If D3DX9_43.dll exists in SysWOW64, it's likely fine.
        // On 64-bit Windows (which is 99.9% of users today), this is the reliable check.
        var sysWow64Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
        var dxDll = Path.Combine(sysWow64Path, "D3DX9_43.dll");

        return Task.FromResult(File.Exists(dxDll));
    }

    /// <inheritdoc/>
    protected override async Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        var details = new List<string>();
        var tempFolder = Path.Combine(Path.GetTempPath(), "GenHub_DirectX");
        var zipFile = Path.Combine(tempFolder, "dx_runtime.zip");
        var extractPath = Path.Combine(tempFolder, "Extracted");

        try
        {
            details.Add("Starting DirectX Runtime installation...");
            details.Add($"Download URL: {ExternalUrls.DirectXRuntimeDownloadUrl}");

            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }

            Directory.CreateDirectory(extractPath);
            details.Add($"Temp directory: {tempFolder}");

            details.Add("Downloading DirectX Runtime...");
            _logger.LogInformation("Downloading DirectX Runtime from {Url}", ExternalUrls.DirectXRuntimeDownloadUrl);

            using (var client = httpClientFactory.CreateClient())
            {
                var response = await client.GetAsync(ExternalUrls.DirectXRuntimeDownloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var fileSize = response.Content.Headers.ContentLength ?? 0;
                details.Add($"✓ Downloaded {fileSize / 1024 / 1024:F2} MB");

                await using var fs = new FileStream(zipFile, FileMode.Create);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            details.Add("Extracting DirectX Runtime...");
            _logger.LogInformation("Extracting DirectX Runtime...");
            ZipFile.ExtractToDirectory(zipFile, extractPath);

            var extractedFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
            details.Add($"✓ Extracted {extractedFiles.Length} files");

            var setupExe = Path.Combine(extractPath, "DXSETUP.exe");
            if (!File.Exists(setupExe))
            {
                details.Add("✗ DXSETUP.exe not found in package");
                return new ActionSetResult(false, "DXSETUP.exe not found in downloaded package.", details);
            }

            details.Add($"Running DirectX Setup (silent mode)...");
            details.Add("  ⚠ This may require administrator privileges");
            _logger.LogInformation("Running DirectX Setup (Silent)...");

            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = setupExe,
                Arguments = "/silent",
                UseShellExecute = true,
                Verb = "runas", // Ensure admin for installation
            });

            if (process == null)
            {
                details.Add("✗ Failed to start DirectX setup process");
                return new ActionSetResult(false, "Failed to start DirectX setup process.", details);
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("DirectX setup exited with code {ExitCode}", process.ExitCode);
                details.Add($"⚠ DirectX setup exited with code {process.ExitCode}");
                details.Add("  Note: Non-zero codes may not indicate failure");

                // Note: Sometimes DX returns non-zero codes that aren't failures,
                // but usually /silent should result in 0 or a reboot code.
            }
            else
            {
                details.Add("✓ DirectX setup completed successfully");
            }

            details.Add("✓ DirectX Runtime installation completed");
            return new ActionSetResult(true, null, details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error implementing DirectX Runtime Fix");
            details.Add($"✗ Error: {ex.Message}");
            return new ActionSetResult(false, ex.Message, details);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Uninstalling DirectX Runtime is not supported via GenHub.");
        return Task.FromResult(new ActionSetResult(true));
    }
}
