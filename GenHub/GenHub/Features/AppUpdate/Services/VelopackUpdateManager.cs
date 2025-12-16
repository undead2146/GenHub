using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models.Enums;
using GenHub.Features.AppUpdate.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Velopack;
using Velopack.Sources;

namespace GenHub.Features.AppUpdate.Services;

/// <summary>
/// Velopack-based update manager service with support for release and artifact update channels.
/// </summary>
public class VelopackUpdateManager(
    ILogger<VelopackUpdateManager> logger,
    IHttpClientFactory httpClientFactory,
    IGitHubTokenStorage gitHubTokenStorage,
    IUserSettingsService userSettingsService) : IVelopackUpdateManager, IDisposable
{
    /// <summary>
    /// Length of the git short hash used in versioning (7 characters).
    /// </summary>
    private const int GitShortHashLength = 7;

    private readonly GithubSource _githubSource = new(AppConstants.GitHubRepositoryUrl, string.Empty, true);
    private readonly UpdateManager? _updateManager = CreateUpdateManager(logger);

    private bool _hasUpdateFromGitHub;
    private string? _latestVersionFromGitHub;
    private ArtifactUpdateInfo? _latestArtifactUpdate;

    /// <inheritdoc/>
    public UpdateChannel CurrentChannel { get; set; } = userSettingsService.Get().UpdateChannel;

    /// <inheritdoc/>
    public bool HasArtifactUpdateAvailable => _latestArtifactUpdate != null;

    /// <inheritdoc/>
    public ArtifactUpdateInfo? LatestArtifactUpdate => _latestArtifactUpdate;

    /// <inheritdoc/>
    public int? SubscribedPrNumber { get; set; }

    /// <inheritdoc/>
    public bool IsPrMergedOrClosed { get; private set; }

    /// <inheritdoc/>
    public bool IsUpdatePendingRestart => _updateManager?.UpdatePendingRestart != null;

    /// <inheritdoc/>
    public bool HasUpdateAvailableFromGitHub
    {
        get
        {
            logger.LogDebug("HasUpdateAvailableFromGitHub property accessed: {Value}", _hasUpdateFromGitHub);
            return _hasUpdateFromGitHub;
        }
    }

    /// <inheritdoc/>
    public string? LatestVersionFromGitHub
    {
        get
        {
            logger.LogDebug("LatestVersionFromGitHub property accessed: '{Value}'", _latestVersionFromGitHub ?? "NULL");
            return _latestVersionFromGitHub;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_updateManager is IDisposable disposableUpdateManager)
        {
            disposableUpdateManager.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting GitHub release update check for: {Url}", AppConstants.GitHubRepositoryUrl);

        // Reset per-run state to avoid stale UI
        _hasUpdateFromGitHub = false;
        _latestVersionFromGitHub = null;

        try
        {
            var apiUrl = $"https://api.github.com/repos/{AppConstants.GitHubRepositoryOwner}/{AppConstants.GitHubRepositoryName}/releases";
            using var client = CreateConfiguredHttpClient();
            var response = await client.GetAsync(apiUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("GitHub API request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = JsonSerializer.Deserialize<JsonElement>(json);

            if (!releases.ValueKind.Equals(JsonValueKind.Array) || releases.GetArrayLength() == 0)
            {
                logger.LogWarning("No releases found on GitHub");
                return null;
            }

            if (!SemanticVersion.TryParse(AppConstants.AppVersion, out var currentVersion))
            {
                logger.LogError("Failed to parse current version: {Version}", AppConstants.AppVersion);
                return null;
            }

            SemanticVersion? latestVersion = null;
            foreach (var release in releases.EnumerateArray())
            {
                var tagName = release.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tagName))
                    continue;

                var versionString = tagName.TrimStart('v', 'V');
                if (!SemanticVersion.TryParse(versionString, out var releaseVersion))
                    continue;

                // Filter based on current channel
                if (CurrentChannel == UpdateChannel.Stable && releaseVersion.IsPrerelease)
                    continue;

                if (latestVersion == null || releaseVersion > latestVersion)
                {
                    latestVersion = releaseVersion;
                }
            }

            if (latestVersion == null || latestVersion <= currentVersion)
            {
                logger.LogInformation("No update available. Current: {Current}", currentVersion);
                return null;
            }

            logger.LogInformation("Update available: {Current} -> {Latest}", currentVersion, latestVersion);
            _hasUpdateFromGitHub = true;
            _latestVersionFromGitHub = latestVersion.ToString();

            // Try to get UpdateInfo from Velopack if available
            if (_updateManager != null)
            {
                try
                {
                    var updateInfo = await _updateManager.CheckForUpdatesAsync();
                    if (updateInfo != null)
                    {
                        logger.LogInformation("Velopack confirmed update available");
                        return updateInfo;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Velopack UpdateManager check failed");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check for updates");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ArtifactUpdateInfo?> CheckForArtifactUpdatesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Checking for artifact updates (requires PAT)");

        // Reset per-run state to avoid stale UI
        _latestArtifactUpdate = null;

        // Check if PAT is available
        if (!gitHubTokenStorage.HasToken())
        {
            logger.LogDebug("No GitHub PAT available, skipping artifact check");
            return null;
        }

        try
        {
            var token = await gitHubTokenStorage.LoadTokenAsync();
            if (token == null)
            {
                logger.LogWarning("Failed to load GitHub PAT");
                return null;
            }

            using var client = CreateConfiguredHttpClient(token);
            var owner = AppConstants.GitHubRepositoryOwner;
            var repo = AppConstants.GitHubRepositoryName;

            // Get workflow runs for CI
            var runsUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/runs?status=success&per_page=10";
            var runsResponse = await client.GetAsync(runsUrl, cancellationToken);

            if (!runsResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch workflow runs: {Status}", runsResponse.StatusCode);
                return null;
            }

            var runsJson = await runsResponse.Content.ReadAsStringAsync(cancellationToken);
            var runsData = JsonSerializer.Deserialize<JsonElement>(runsJson);

            if (!runsData.TryGetProperty("workflow_runs", out var runs) || runs.GetArrayLength() == 0)
            {
                logger.LogDebug("No successful workflow runs found");
                return null;
            }

            // Find the latest run with artifacts
            foreach (var run in runs.EnumerateArray())
            {
                var runId = run.GetProperty("id").GetInt64();
                var runUrl = run.GetProperty("html_url").GetString() ?? string.Empty;
                DateTime createdAt;
                try
                {
                    createdAt = run.GetProperty("created_at").GetDateTime();
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(ex, "Failed to parse created_at date from workflow run");
                    createdAt = DateTime.UtcNow;
                }

                var headSha = run.GetProperty("head_sha").GetString() ?? string.Empty;
                var shortHash = headSha.Length >= GitShortHashLength ? headSha[..GitShortHashLength] : headSha;

                // Check for PR number in the run
                int? prNumber = null;
                if (run.TryGetProperty("pull_requests", out var prs) && prs.GetArrayLength() > 0)
                {
                    prNumber = prs[0].GetProperty("number").GetInt32();
                }

                // Get artifacts for this run
                var artifactsUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/runs/{runId}/artifacts";
                var artifactsResponse = await client.GetAsync(artifactsUrl, cancellationToken);

                if (!artifactsResponse.IsSuccessStatusCode)
                    continue;

                var artifactsJson = await artifactsResponse.Content.ReadAsStringAsync(cancellationToken);
                var artifactsData = JsonSerializer.Deserialize<JsonElement>(artifactsJson);

                if (!artifactsData.TryGetProperty("artifacts", out var artifacts) || artifacts.GetArrayLength() == 0)
                    continue;

                // Look for Velopack artifacts
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    var artifactName = artifact.GetProperty("name").GetString() ?? string.Empty;
                    if (!artifactName.Contains("velopack", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var artifactId = artifact.GetProperty("id").GetInt64();

                    // Extract version from artifact name (format: genhub-velopack-windows-0.0.160-pr3)
                    var version = ExtractVersionFromArtifactName(artifactName) ?? "unknown";

                    var artifactInfo = new ArtifactUpdateInfo(
                        version: version,
                        gitHash: shortHash,
                        pullRequestNumber: prNumber,
                        workflowRunId: runId,
                        workflowRunUrl: runUrl,
                        artifactId: artifactId,
                        artifactName: artifactName,
                        createdAt: createdAt);

                    logger.LogInformation("Found artifact update: {Version} ({Hash})", version, shortHash);
                    _latestArtifactUpdate = artifactInfo;
                    return artifactInfo;
                }
            }

            logger.LogDebug("No Velopack artifacts found in recent workflow runs");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check for artifact updates");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching open pull requests with artifacts");

        // Reset merged/closed tracking
        IsPrMergedOrClosed = false;

        var results = new List<PullRequestInfo>();

        // Check if PAT is available
        if (!gitHubTokenStorage.HasToken())
        {
            logger.LogDebug("No GitHub PAT available, skipping PR list fetch");
            return results;
        }

        try
        {
            var token = await gitHubTokenStorage.LoadTokenAsync();
            if (token == null)
            {
                logger.LogWarning("Failed to load GitHub PAT");
                return results;
            }

            using var client = CreateConfiguredHttpClient(token);
            var owner = AppConstants.GitHubRepositoryOwner;
            var repo = AppConstants.GitHubRepositoryName;

            // Get open pull requests
            var prsUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls?state=open&per_page=30";
            var prsResponse = await client.GetAsync(prsUrl, cancellationToken);

            if (!prsResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch open PRs: {Status}", prsResponse.StatusCode);
                return results;
            }

            var prsJson = await prsResponse.Content.ReadAsStringAsync(cancellationToken);
            var prsData = JsonSerializer.Deserialize<JsonElement>(prsJson);

            if (!prsData.ValueKind.Equals(JsonValueKind.Array))
            {
                return results;
            }

            // Track if subscribed PR is still open
            bool subscribedPrFound = false;

            foreach (var pr in prsData.EnumerateArray())
            {
                var prNumber = pr.GetProperty("number").GetInt32();
                var title = pr.GetProperty("title").GetString() ?? "Unknown";
                var branchName = pr.TryGetProperty("head", out var head)
                    ? head.GetProperty("ref").GetString() ?? "unknown"
                    : "unknown";
                var author = pr.TryGetProperty("user", out var user)
                    ? user.GetProperty("login").GetString() ?? "unknown"
                    : "unknown";
                var state = pr.GetProperty("state").GetString() ?? "open";
                var updatedAt = pr.TryGetProperty("updated_at", out var updatedAtProp)
                    ? updatedAtProp.GetDateTimeOffset()
                    : (DateTimeOffset?)null;

                // Check if this is our subscribed PR
                if (SubscribedPrNumber == prNumber)
                {
                    subscribedPrFound = true;
                    IsPrMergedOrClosed = false;
                }

                // Find latest artifact for this PR by checking workflow runs
                ArtifactUpdateInfo? latestArtifact = null;

                var runsUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/runs?status=success&per_page=5";
                var runsResponse = await client.GetAsync(runsUrl, cancellationToken);

                if (runsResponse.IsSuccessStatusCode)
                {
                    var runsJson = await runsResponse.Content.ReadAsStringAsync(cancellationToken);
                    var runsData = JsonSerializer.Deserialize<JsonElement>(runsJson);

                    if (runsData.TryGetProperty("workflow_runs", out var runs))
                    {
                        foreach (var run in runs.EnumerateArray())
                        {
                            // Check if this run is for our PR
                            if (!run.TryGetProperty("pull_requests", out var runPrs))
                                continue;

                            bool isForThisPr = false;
                            foreach (var runPr in runPrs.EnumerateArray())
                            {
                                if (runPr.GetProperty("number").GetInt32() == prNumber)
                                {
                                    isForThisPr = true;
                                    break;
                                }
                            }

                            if (!isForThisPr)
                                continue;

                            // Get artifacts for this run
                            var runId = run.GetProperty("id").GetInt64();
                            var runUrl = run.GetProperty("html_url").GetString() ?? string.Empty;
                            DateTime createdAt;
                            try
                            {
                                createdAt = run.GetProperty("created_at").GetDateTime();
                            }
                            catch (FormatException ex)
                            {
                                logger.LogWarning(ex, "Failed to parse created_at date from workflow run");
                                createdAt = DateTime.UtcNow;
                            }

                            var headSha = run.GetProperty("head_sha").GetString() ?? string.Empty;
                            var shortHash = headSha.Length >= GitShortHashLength ? headSha[..GitShortHashLength] : headSha;

                            var artifactsUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/runs/{runId}/artifacts";
                            var artifactsResponse = await client.GetAsync(artifactsUrl, cancellationToken);

                            if (!artifactsResponse.IsSuccessStatusCode)
                                continue;

                            var artifactsJson = await artifactsResponse.Content.ReadAsStringAsync(cancellationToken);
                            var artifactsData = JsonSerializer.Deserialize<JsonElement>(artifactsJson);

                            if (!artifactsData.TryGetProperty("artifacts", out var artifacts))
                                continue;

                            // Collect all velopack artifacts, prefer Windows
                            ArtifactUpdateInfo? windowsArtifact = null;
                            ArtifactUpdateInfo? fallbackArtifact = null;

                            foreach (var artifact in artifacts.EnumerateArray())
                            {
                                var artifactName = artifact.GetProperty("name").GetString() ?? string.Empty;

                                // Look for the .nupkg artifact (genhub-velopack-windows-* or genhub-velopack-linux-*)
                                // Setup.exe and RELEASES are in separate artifacts to reduce download size
                                if (!artifactName.Contains("velopack", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var artifactId = artifact.GetProperty("id").GetInt64();
                                var version = ExtractVersionFromArtifactName(artifactName) ?? $"PR{prNumber}";

                                var artifactInfo = new ArtifactUpdateInfo(
                                    version: version,
                                    gitHash: shortHash,
                                    pullRequestNumber: prNumber,
                                    workflowRunId: runId,
                                    workflowRunUrl: runUrl,
                                    artifactId: artifactId,
                                    artifactName: artifactName,
                                    createdAt: createdAt);

                                // Prefer Windows artifact
                                if (artifactName.Contains("windows", StringComparison.OrdinalIgnoreCase))
                                {
                                    windowsArtifact = artifactInfo;
                                    break; // Got Windows, stop looking
                                }
                                else if (fallbackArtifact == null && !artifactName.Contains("linux", StringComparison.OrdinalIgnoreCase))
                                {
                                    fallbackArtifact = artifactInfo;
                                }
                            }

                            latestArtifact = windowsArtifact ?? fallbackArtifact;

                            if (latestArtifact != null)
                                break;
                        }
                    }
                }

                var prInfo = new PullRequestInfo
                {
                    Number = prNumber,
                    Title = title,
                    BranchName = branchName,
                    Author = author,
                    State = state,
                    UpdatedAt = updatedAt,
                    LatestArtifact = latestArtifact,
                };

                results.Add(prInfo);
            }

            // Update merged/closed status for subscribed PR
            if (SubscribedPrNumber.HasValue && !subscribedPrFound)
            {
                // PR is no longer in open PRs list - check if merged or closed
                var prStatusUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls/{SubscribedPrNumber}";
                var statusResponse = await client.GetAsync(prStatusUrl, cancellationToken);

                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
                    var statusData = JsonSerializer.Deserialize<JsonElement>(statusJson);
                    var state = statusData.GetProperty("state").GetString();

                    IsPrMergedOrClosed = state != null && !state.Equals("open", StringComparison.OrdinalIgnoreCase);
                    if (IsPrMergedOrClosed)
                    {
                        logger.LogInformation("Subscribed PR #{PrNumber} has been merged/closed", SubscribedPrNumber);
                    }
                }
            }

            logger.LogInformation("Found {Count} open PRs", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch open pull requests");
            return results;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadUpdatesAsync(UpdateInfo updateInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_updateManager == null)
        {
            throw new InvalidOperationException("UpdateManager not initialized");
        }

        ArgumentNullException.ThrowIfNull(updateInfo);

        try
        {
            logger.LogInformation("Downloading update {Version}...", updateInfo.TargetFullRelease.Version);

            Action<int>? velopackProgress = null;
            if (progress != null)
            {
                velopackProgress = percent =>
                {
                    progress.Report(new UpdateProgress
                    {
                        PercentComplete = percent,
                        Message = $"Downloading update... {percent}%",
                        Status = "Downloading",
                    });
                };
            }

            await _updateManager.DownloadUpdatesAsync(updateInfo, velopackProgress);

            progress?.Report(new UpdateProgress
            {
                PercentComplete = 100,
                Message = "Download complete",
                Status = "Downloaded",
                IsCompleted = true,
            });

            logger.LogInformation("Update downloaded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download updates");
            throw;
        }
    }

    /// <inheritdoc/>
    public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
    {
        if (_updateManager == null)
        {
            throw new InvalidOperationException("UpdateManager not initialized");
        }

        ArgumentNullException.ThrowIfNull(updateInfo);

        try
        {
            logger.LogInformation("Applying update {Version} and restarting...", updateInfo.TargetFullRelease.Version);
            logger.LogInformation("Update package: {Package}", updateInfo.TargetFullRelease.FileName);
            logger.LogInformation("Current app will exit and restart with new version");

            _updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);

            // If we reach here, restart might have failed
            logger.LogWarning("ApplyUpdatesAndRestart returned without exiting - this is unexpected");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply updates and restart. Attempting fallback to ApplyUpdatesAndExit...");

            // Try fallback to exit-only mode
            try
            {
                _updateManager.ApplyUpdatesAndExit(updateInfo.TargetFullRelease);
                logger.LogInformation("Fallback to ApplyUpdatesAndExit succeeded. Please restart the application manually.");
            }
            catch (Exception fallbackEx)
            {
                logger.LogError(fallbackEx, "Fallback to ApplyUpdatesAndExit also failed");
                throw new InvalidOperationException("Failed to apply update. Both restart and exit methods failed.", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyUpdatesAndExit(UpdateInfo updateInfo)
    {
        if (_updateManager == null)
        {
            throw new InvalidOperationException("UpdateManager not initialized");
        }

        ArgumentNullException.ThrowIfNull(updateInfo);

        try
        {
            logger.LogInformation("Applying update {Version} and exiting...", updateInfo.TargetFullRelease.Version);
            _updateManager.ApplyUpdatesAndExit(updateInfo.TargetFullRelease);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply updates and exit");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task InstallPrArtifactAsync(PullRequestInfo prInfo, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (prInfo.LatestArtifact == null)
        {
            throw new InvalidOperationException($"PR #{prInfo.Number} has no artifacts available");
        }

        if (!gitHubTokenStorage.HasToken())
        {
            throw new InvalidOperationException("GitHub PAT required to download PR artifacts");
        }

        SimpleHttpServer? server = null;
        string? tempDir = null;

        try
        {
            progress?.Report(new UpdateProgress { Status = "Downloading PR artifact...", PercentComplete = 0 });

            var token = await gitHubTokenStorage.LoadTokenAsync();
            if (token == null)
            {
                throw new InvalidOperationException("Failed to load GitHub PAT");
            }

            using var client = CreateConfiguredHttpClient(token);
            var owner = AppConstants.GitHubRepositoryOwner;
            var repo = AppConstants.GitHubRepositoryName;
            var artifactId = prInfo.LatestArtifact.ArtifactId;

            // GitHub Actions artifacts are returned as ZIP files via the API,
            // even if individual files were uploaded. This is a GitHub Actions platform limitation.
            // The /zip endpoint is the only way to download artifacts.
            var downloadUrl = $"https://api.github.com/repos/{owner}/{repo}/actions/artifacts/{artifactId}/zip";
            logger.LogInformation("Downloading PR #{Number} artifact from {Url}", prInfo.Number, downloadUrl);

            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Create temp directory
            tempDir = Path.Combine(Path.GetTempPath(), $"genhub-pr{prInfo.Number}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            var zipPath = Path.Combine(tempDir, "artifact.zip");
            using (var fileStream = File.Create(zipPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            progress?.Report(new UpdateProgress { Status = "Extracting artifact...", PercentComplete = 30 });

            // Extract the ZIP to access the .nupkg file
            // Note: The artifact now contains ONLY the .nupkg file (Setup.exe and RELEASES are separate artifacts)
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            // Find .nupkg file - should be the only file in the artifact
            var nupkgFiles = Directory.GetFiles(tempDir, "*.nupkg", SearchOption.AllDirectories);

            // Prefer platform-appropriate nupkg based on current runtime
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            var nupkgFile = isWindows
                ? nupkgFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("-win", StringComparison.OrdinalIgnoreCase))
                  ?? nupkgFiles.FirstOrDefault(f => !Path.GetFileName(f).Contains("-linux", StringComparison.OrdinalIgnoreCase))
                : isLinux
                ? nupkgFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("-linux", StringComparison.OrdinalIgnoreCase))
                  ?? nupkgFiles.FirstOrDefault()
                : nupkgFiles.FirstOrDefault();

            if (nupkgFile == null)
            {
                throw new FileNotFoundException("No .nupkg file found in PR artifact");
            }

            logger.LogInformation("Found nupkg: {File}", Path.GetFileName(nupkgFile));

            // Create releases.win.json (Velopack format)
            var releasesPath = Path.Combine(tempDir, "releases.win.json");
            var nupkgFileName = Path.GetFileName(nupkgFile);
            var fileInfo = new FileInfo(nupkgFile);
            var sha1 = CalculateSHA1(nupkgFile);
            var sha256 = CalculateSHA256(nupkgFile);

            // Extract version from nupkg filename (e.g., GenHub-0.0.160-pr3-full.nupkg -> 0.0.160-pr3)
            var versionMatch = System.Text.RegularExpressions.Regex.Match(nupkgFileName, @"GenHub-(.+)-full\.nupkg", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var fileVersion = versionMatch.Success ? versionMatch.Groups[1].Value : prInfo.LatestArtifact.Version;

            var releasesJson = new
            {
                Assets = new[]
                {
                    new
                    {
                        PackageId = "GenHub",
                        Version = fileVersion,
                        Type = "Full",
                        FileName = nupkgFileName,
                        SHA1 = sha1,
                        SHA256 = sha256,
                        Size = fileInfo.Length,
                    },
                },
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(releasesJson);
            await File.WriteAllTextAsync(releasesPath, jsonContent, cancellationToken);
            logger.LogInformation("Created releases.win.json with version {Version}", fileVersion);

            progress?.Report(new UpdateProgress { Status = "Starting local server...", PercentComplete = 50 });

            // Start HTTP server
            var port = FindAvailablePort();
            server = new SimpleHttpServer(nupkgFile, releasesPath, port, logger);
            server.Start();

            progress?.Report(new UpdateProgress { Status = "Preparing update...", PercentComplete = 60 });

            // For PR artifacts, we bypass Velopack's version check entirely
            // This allows users to "switch" to any PR build, even if it's technically "older"
            // We construct a VelopackAsset directly from the nupkg file
            logger.LogInformation("Bypassing version check for PR artifact installation");

            var asset = new VelopackAsset
            {
                PackageId = "GenHub",
                Version = NuGet.Versioning.SemanticVersion.Parse(fileVersion),
                Type = VelopackAssetType.Full,
                FileName = nupkgFileName,
                SHA1 = sha1,
                SHA256 = sha256,
                Size = fileInfo.Length,
            };

            progress?.Report(new UpdateProgress { Status = "Downloading update...", PercentComplete = 70 });

            // Point Velopack to localhost for download - use the secret token from the server for security
            var source = new SimpleWebSource($"http://localhost:{port}/{server.SecretToken}/");
            var localUpdateManager = new UpdateManager(source);

            try
            {
                // Check for updates - may return null if version check fails
                var updateInfo = await localUpdateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    // Version check failed - strip build metadata and try comparing base versions
                    var currentVersionStr = AppConstants.AppVersion.Split('+')[0]; // Strip build metadata
                    var targetVersionStr = fileVersion.Split('+')[0]; // Strip build metadata

                    logger.LogWarning("Cannot install PR artifact: current version ({Current}) >= target ({Target})",
                        currentVersionStr, targetVersionStr);
                    logger.LogInformation("Full versions: current={CurrentFull}, target={TargetFull}",
                        AppConstants.AppVersion, fileVersion);

                    // Check if they're actually the same version (ignoring build metadata)
                    if (currentVersionStr.Equals(targetVersionStr, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"PR build {fileVersion} is already installed (current: {AppConstants.AppVersion}). " +
                            $"This is the same version with different build metadata.");
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Cannot install PR build {fileVersion}: Current version ({AppConstants.AppVersion}) is newer. " +
                            $"To install this older PR build, uninstall GenHub first, then run Setup.exe from the PR artifact.");
                    }
                }

                // Download from localhost
                await localUpdateManager.DownloadUpdatesAsync(updateInfo, p =>
                {
                    progress?.Report(new UpdateProgress
                    {
                        Status = "Downloading update...",
                        PercentComplete = 70 + (int)(p * 0.2), // 70-90%
                    });
                });

                progress?.Report(new UpdateProgress { Status = "Installing update...", PercentComplete = 90 });

                logger.LogInformation("Applying PR #{Number} update and restarting", prInfo.Number);
                logger.LogInformation("Update version: {Version}", updateInfo.TargetFullRelease.Version);
                logger.LogInformation("Update package: {Package}", updateInfo.TargetFullRelease.FileName);

                try
                {
                    // Use ApplyUpdatesAndRestart to automatically restart the app
                    logger.LogInformation("Using ApplyUpdatesAndRestart for PR artifact installation");
                    localUpdateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);

                    // If we get here, the exit might not have happened immediately
                    logger.LogWarning("ApplyUpdatesAndExit returned without exiting - waiting for exit...");
                    await Task.Delay(5000, cancellationToken);

                    logger.LogError("Application did not exit after ApplyUpdatesAndExit. Update may have failed.");
                    throw new InvalidOperationException("Application did not exit after applying update");
                }
                catch (Exception restartEx)
                {
                    logger.LogError(restartEx, "Failed to apply PR artifact update");
                    logger.LogError("Update file: {File}", updateInfo.TargetFullRelease.FileName);
                    logger.LogError("Update version: {Version}", updateInfo.TargetFullRelease.Version);
                    throw;
                }
            }
            finally
            {
                // Note: UpdateManager doesn't implement IDisposable, so no cleanup needed
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install PR artifact");
            progress?.Report(new UpdateProgress { Status = "Installation failed", HasError = true, ErrorMessage = ex.Message });
            throw;
        }
        finally
        {
            // Cleanup
            server?.Dispose();

            if (tempDir != null && Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to cleanup temp directory: {Path}", tempDir);
                }
            }
        }
    }

    /// <summary>
    /// Creates the Velopack UpdateManager, or null if running in debug/uninstalled mode.
    /// </summary>
    private static UpdateManager? CreateUpdateManager(ILogger logger)
    {
        try
        {
            var source = new GithubSource(AppConstants.GitHubRepositoryUrl, string.Empty, true);
            var manager = new UpdateManager(source);
            logger.LogInformation("Velopack UpdateManager initialized for: {Repository}", AppConstants.GitHubRepositoryUrl);
            return manager;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Velopack UpdateManager not available (running from Debug)");
            return null;
        }
    }

    private static string CalculateSHA1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static string CalculateSHA256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static int FindAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Extracts version from artifact name.
    /// Expected format: genhub-velopack-{platform}-{version}
    /// Version format: 0.0.X or 0.0.X-prY (e.g., "0.0.150" or "0.0.150-pr42").
    /// </summary>
    private static string? ExtractVersionFromArtifactName(string artifactName)
    {
        // Try both windows and linux prefixes
        var prefixes = new[] { "genhub-velopack-windows-", "genhub-velopack-linux-" };

        foreach (var prefix in prefixes)
        {
            if (artifactName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var version = artifactName[prefix.Length..];
                return string.IsNullOrWhiteSpace(version) ? null : version;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts SecureString to plain string.
    /// </summary>
    private static string SecureStringToString(SecureString secureString)
    {
        var ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
        try
        {
            return Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    /// <summary>
    /// Creates an HttpClient with standard configuration.
    /// </summary>
    private HttpClient CreateConfiguredHttpClient(SecureString? token = null)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GenHub", AppConstants.AppVersion));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (token != null)
        {
            var plainText = SecureStringToString(token);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plainText);
        }

        return client;
    }
}
