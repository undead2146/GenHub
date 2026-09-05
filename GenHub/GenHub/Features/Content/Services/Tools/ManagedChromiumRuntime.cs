using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GenHub.Features.Content.Services.Tools;

/// <summary>
/// Owns the app-local Playwright Chromium runtime. Playwright's NuGet package supplies the
/// driver but deliberately does not ship browser binaries, so GenHub provisions Chromium under
/// its application-data directory instead of relying on a system browser installation.
/// System Chrome/Edge cannot satisfy this requirement — Playwright needs its own patched build.
/// </summary>
internal sealed class ManagedChromiumRuntime(
    string runtimeDirectory,
    Func<string[], int> installer,
    Func<string, Task<bool>> requestInstallConsentAsync,
    ILogger logger)
{
    /// <summary>
    /// Environment variable used by Playwright to locate app-owned browser binaries.
    /// </summary>
    internal const string BrowserPathEnvironmentVariable = "PLAYWRIGHT_BROWSERS_PATH";

    /// <summary>
    /// Environment variable used by Playwright to locate its driver binary (node.exe).
    /// </summary>
    internal const string DriverPathEnvironmentVariable = "PLAYWRIGHT_DRIVER_PATH";

    private string? _cachedDriverPath;

    /// <summary>
    /// Configures Playwright to resolve browsers only from GenHub's managed runtime directory.
    /// </summary>
    public void ConfigureEnvironment()
    {
        Directory.CreateDirectory(runtimeDirectory);
        Environment.SetEnvironmentVariable(BrowserPathEnvironmentVariable, runtimeDirectory);
        EnsureDriverEnvironmentVariable();
    }

    /// <summary>
    /// Installs Chromium exactly once when the app-owned executable is unavailable,
    /// after the user confirms the download via the standard confirmation dialog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for aborting the install.</param>
    /// <returns>True when the runtime is ready to launch; false if cancelled, refused, or failed.</returns>
    public async Task<bool> EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        ConfigureEnvironment();

        if (IsChromiumInstalled())
        {
            return true;
        }

        logger.LogDebug("Playwright Chromium runtime not found in {Dir}; requesting install consent", runtimeDirectory);

        var consent = await requestInstallConsentAsync(
            "GenHub uses an application-owned browser engine (Chromium, ~150 MB) to access protected mod repositories like ModDB.\n\nWould you like to install it now?");

        if (!consent)
        {
            logger.LogWarning("User declined Chromium runtime installation");
            return false;
        }

        logger.LogInformation("Installing Playwright Chromium into {Dir}...", runtimeDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var exitCode = await Task.Run(() => installer(["install", "chromium"]), cancellationToken);
            if (exitCode != 0)
            {
                logger.LogError("Playwright CLI installer returned exit code {Code}", exitCode);
                return false;
            }

            var verified = IsChromiumInstalled();
            if (verified)
            {
                logger.LogInformation("Playwright Chromium runtime verified at {Dir}", runtimeDirectory);
            }
            else
            {
                logger.LogError("Playwright installer succeeded but Chromium executable was not found in {Dir}", runtimeDirectory);
            }

            return verified;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run Playwright CLI installer");
            return false;
        }
    }

    /// <summary>
    /// Scans the managed directory for a Chromium executable (platform-appropriate name).
    /// </summary>
    /// <returns>True if at least one installed revision is present.</returns>
    public bool IsChromiumInstalled()
    {
        if (!Directory.Exists(runtimeDirectory))
        {
            return false;
        }

        var exeName = OperatingSystem.IsWindows()
            ? "chrome.exe"
            : OperatingSystem.IsMacOS()
                ? "Chromium"
                : "chrome";

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(runtimeDirectory, "chromium-*"))
            {
                var matches = Directory.GetFiles(dir, exeName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate Chromium directories in {Dir}", runtimeDirectory);
        }

        return false;
    }

    private static string GetPlatformFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win32_x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                ? "darwin-arm64"
                : "darwin-x64";
        }

        return "win32_x64";
    }

    private static string? FindDriverCandidateInDirectory(string dir, string platformFolder, string nodeBinaryName)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return null;
        }

        var candidate = Path.Combine(dir, ".playwright", "node", platformFolder, nodeBinaryName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var current = new DirectoryInfo(dir);
        for (var depth = 0; depth < 4 && current.Parent != null; depth++)
        {
            current = current.Parent;
            candidate = Path.Combine(current.FullName, ".playwright", "node", platformFolder, nodeBinaryName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void EnsureDriverEnvironmentVariable()
    {
        if (!string.IsNullOrWhiteSpace(_cachedDriverPath) && File.Exists(_cachedDriverPath))
        {
            Environment.SetEnvironmentVariable(DriverPathEnvironmentVariable, _cachedDriverPath);
            return;
        }

        var existingDriverPath = Environment.GetEnvironmentVariable(DriverPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(existingDriverPath) && File.Exists(existingDriverPath))
        {
            _cachedDriverPath = existingDriverPath;
            return;
        }

        var platformFolder = GetPlatformFolder();
        var nodeBinaryName = OperatingSystem.IsWindows() ? "node.exe" : "node";

        var searchDirectories = new[]
        {
            AppContext.BaseDirectory,
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(typeof(ManagedChromiumRuntime).Assembly.Location) ?? string.Empty,
            Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty,
        };

        foreach (var dir in searchDirectories)
        {
            var candidate = FindDriverCandidateInDirectory(dir, platformFolder, nodeBinaryName);
            if (candidate != null)
            {
                _cachedDriverPath = candidate;
                Environment.SetEnvironmentVariable(DriverPathEnvironmentVariable, candidate);
                logger.LogInformation("Resolved Playwright driver path: {DriverPath}", candidate);
                return;
            }
        }

        logger.LogWarning("Could not resolve Playwright driver node executable in any standard directory");
    }
}
