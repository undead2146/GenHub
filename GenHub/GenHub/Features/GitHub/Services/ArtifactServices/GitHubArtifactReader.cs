using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for GitHub artifact operations
    /// </summary>
    public class GitHubArtifactReader : IGitHubArtifactReader
    {
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubCachingRepository _dataRepository;
        private readonly ILogger<GitHubArtifactReader> _logger;
        private readonly IGameVersionInstaller _versionInstaller;
        private readonly IGitHubWorkflowReader _workflowService;
        private readonly IGameVersionRepository _gameVersionRepository;
        private readonly IGitHubRepositoryManager _repoService;

        public GitHubArtifactReader(
            IGitHubApiClient apiClient,
            IGitHubCachingRepository dataRepository,
            ILogger<GitHubArtifactReader> logger,
            IGameVersionInstaller versionInstaller,
            IGitHubWorkflowReader workflowService,
            IGameVersionRepository gameVersionRepository,
            IGitHubRepositoryManager repoService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionInstaller = versionInstaller ?? throw new ArgumentNullException(nameof(versionInstaller));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _gameVersionRepository = gameVersionRepository ?? throw new ArgumentNullException(nameof(gameVersionRepository));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));

            _logger.LogInformation("GitHubArtifactReader initialized");
        }

        /// <summary>
        /// Gets artifacts for a specific workflow run
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubWorkflow run,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate run ID - if it's 0 or invalid, return empty list
                if (run == null || run.RunId <= 0)
                {
                    _logger.LogWarning("Invalid workflow run with ID {RunId} - cannot fetch artifacts",
                        run?.RunId ?? 0);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                _logger.LogInformation("Getting artifacts for run {RunId}", run.RunId);

                // Ensure repository info exists, creating it if needed
                if (run.RepositoryInfo == null)
                {
                    _logger.LogWarning("Workflow run {RunId} is missing repository info - using default repository", run.RunId);
                    run.RepositoryInfo = _repoService.GetDefaultRepository();
                }

                // Check if we already have cached artifacts for this run
                var repo = run.RepositoryInfo;
                string repoFullName = $"{repo.RepoOwner}/{repo.RepoName}";
                var cachedArtifacts = await _dataRepository.GetCachedArtifactsAsync(repoFullName, run.RunId, cancellationToken);

                if (cachedArtifacts != null && cachedArtifacts.Any())
                {
                    _logger.LogInformation("Using {Count} cached artifacts for run {RunId}", cachedArtifacts.Count(), run.RunId);
                    return cachedArtifacts;
                }

                // Get artifacts from the API with a timeout to prevent UI freezing
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    
                    // Direct API call returns the artifacts collection
                    var artifacts = await _apiClient.GetArtifactsForRunAsync(repo, run.RunId, linkedCts.Token);

                    // Process each artifact to add additional metadata
                    var processedArtifacts = artifacts.ToList();
                    foreach (var artifact in processedArtifacts)
                    {
                        // Ensure repository info is set for the artifact
                        if (artifact.RepositoryInfo == null)
                        {
                            artifact.RepositoryInfo = repo;
                        }

                        // Set workflow ID and run ID for the artifact
                        artifact.WorkflowId = run.WorkflowId;
                        artifact.RunId = run.RunId;

                        // Add additional metadata from the run
                        artifact.PullRequestNumber = run.PullRequestNumber;
                        artifact.PullRequestTitle = run.PullRequestTitle;
                        artifact.CommitSha = run.CommitSha;
                        artifact.CommitMessage = run.CommitMessage;
                        artifact.EventType = run.EventType;
                        artifact.CreatedAt = run.CreatedAt;

                        // Parse build info if needed
                        if (artifact.BuildInfo == null)
                        {
                            artifact.BuildInfo = ParseBuildInfo(artifact.Name);
                        }
                    }

                    _logger.LogInformation("Found {Count} artifacts for run {RunId}", processedArtifacts.Count, run.RunId);

                    // Cache the artifacts
                    if (processedArtifacts.Any())
                    {
                        await _dataRepository.CacheArtifactsAsync(
                            repoFullName,
                            run.RunId,
                            processedArtifacts,
                            cancellationToken);
                    }

                    return processedArtifacts;
                }
                catch (OperationCanceledException ex) when (ex is TaskCanceledException || cancellationToken.IsCancellationRequested)
                {
                    // Handle timeout or cancellation gracefully
                    _logger.LogWarning("Request for artifacts for run {RunId} was cancelled or timed out", run.RunId);
                    throw new GitHubServiceException($"Request for artifacts timed out. Please try again later.", ex);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Handle 404 errors gracefully - this is normal for runs with no artifacts
                    _logger.LogInformation("No artifacts found for run {RunId} (404 response)", run.RunId);
                    return Enumerable.Empty<GitHubArtifact>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for run {RunId}: {ErrorMessage}",
                    run?.RunId ?? 0, ex.Message);

                // Return empty collection instead of throwing
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Gets artifacts for a specific workflow run by run ID
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowAsync(
            long runId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Quick validation - if runId is 0 or negative, return empty list
                if (runId <= 0)
                {
                    _logger.LogWarning("Invalid workflow runId: {RunId}", runId);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                _logger.LogInformation("Getting artifacts for workflow {RunId}", runId);

                // Check cache first - try to find this run ID in all repositories
                var repos = _repoService.GetRepositories();
                foreach (var repo in repos)
                {
                    string repoFullName = $"{repo.RepoOwner}/{repo.RepoName}";
                    var cachedArtifacts = await _dataRepository.GetCachedArtifactsAsync(repoFullName, runId, cancellationToken);

                    if (cachedArtifacts != null && cachedArtifacts.Any())
                    {
                        _logger.LogInformation("Using {Count} cached artifacts for workflow {RunId} from repo {Repo}",
                            cachedArtifacts.Count(), runId, repoFullName);
                        return cachedArtifacts;
                    }
                }

                // If not in cache, get the workflow run first
                var run = await GetWorkflowRunAsync(runId, cancellationToken);
                if (run == null)
                {
                    _logger.LogWarning("Could not find workflow run {RunId}", runId);
                    return Enumerable.Empty<GitHubArtifact>();
                }

                // Now get artifacts for this run
                var artifacts = await GetArtifactsForRunAsync(run, cancellationToken);

                // Always ensure we return a non-null collection
                return artifacts ?? Enumerable.Empty<GitHubArtifact>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artifacts for workflow {RunId}: {ErrorMessage}", runId, ex.Message);
                return Enumerable.Empty<GitHubArtifact>();
            }
        }

        /// <summary>
        /// Get available workflow artifacts for the configured repository
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetArtifactsForRepositoryAsync(_repoService.GetDefaultRepository(), cancellationToken);
        }

        /// <summary>
        /// Get artifacts from a specific repository
        /// </summary>
        public async Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepositoryAsync(
            GitHubRepoSettings repoConfig,
            CancellationToken cancellationToken = default)
        {
            // Get workflow runs first
            var runs = await _workflowService.GetWorkflowRunsForRepositoryAsync(repoConfig, cancellationToken: cancellationToken);
            var artifacts = new List<GitHubArtifact>();

            // Fetch artifacts for each run (use already cached results when available)
            foreach (var run in runs)
            {
                try
                {
                    var runArtifacts = await GetArtifactsForRunAsync(run, cancellationToken);
                    artifacts.AddRange(runArtifacts);
                }
                catch (GitHubServiceException ex) when (ex.Message.Contains("rate limit"))
                {
                    // If we hit rate limits, just return what we have so far and log the error
                    _logger.LogWarning("Rate limit reached after loading {count} artifacts. Error: {message}", artifacts.Count, ex.Message);
                    break;
                }
            }

            return artifacts;
        }

        /// <summary>
        /// Download a specific artifact by ID
        /// </summary>
        public async Task<string> DownloadArtifactAsync(
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Downloading artifact: {ArtifactId}", artifactId);

                // Create the destination folder if it doesn't exist
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                // Get the default repository
                var repo = _repoService.GetDefaultRepository();

                // Download the artifact ZIP file from API
                var downloadUrl = $"repos/{repo.RepoOwner}/{repo.RepoName}/actions/artifacts/{artifactId}/zip";

                _logger.LogDebug("Using download URL: {URL}", downloadUrl);

                // Use a temporary file name for downloading
                string outputFile = Path.Combine(destinationFolder, $"artifact_{artifactId}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                // Stream directly to file instead of loading into memory
                var streamResult = await _apiClient.GetStreamAsync(repo, downloadUrl, cancellationToken);
                var contentStream = streamResult.Stream;
                var contentLength = streamResult.ContentLength;

                using (contentStream)
                using (var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    _logger.LogDebug("Streaming artifact download to: {OutputFile}", outputFile);

                    // Log the content length if available
                    if (contentLength.HasValue)
                    {
                        _logger.LogDebug("Download size: {Size} bytes", contentLength);
                    }
                    else
                    {
                        _logger.LogWarning("Could not determine artifact size - progress reporting may be inaccurate");
                    }

                    // Use a buffer to stream the content
                    var buffer = new byte[81920]; // 80KB buffer
                    var totalBytesRead = 0L;
                    var readCount = 0L;

                    while (true)
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();

                        // Read a chunk from the response stream
                        var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                            break;

                        // Write the chunk to file
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                        // Update progress
                        totalBytesRead += bytesRead;
                        readCount++;

                        // Report progress every 40 chunks (about 3.2MB) to avoid UI flooding
                        if (progress != null && contentLength.HasValue && readCount % 40 == 0)
                        {
                            var progressValue = (double)totalBytesRead / contentLength.Value;
                            progress.Report(progressValue);
                        }
                    }

                    // Ensure we flush all data to disk
                    await fileStream.FlushAsync(cancellationToken);
                }

                // Report 100% when done
                progress?.Report(1.0);

                _logger.LogInformation("Successfully downloaded artifact {ArtifactId} to {OutputFile}", artifactId, outputFile);
                return outputFile;
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                _logger.LogWarning("Download of artifact {ArtifactId} was cancelled", artifactId);
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error downloading artifact {ArtifactId}. Status code: {StatusCode}",
                    artifactId, ex.StatusCode);
                throw new GitHubDownloadException($"Error downloading artifact: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading artifact {ArtifactId}", artifactId);
                throw new GitHubDownloadException($"Error downloading artifact: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Download an artifact from a specific repository
        /// </summary>
        public async Task<string> DownloadArtifactAsync( // Renamed from DownloadArtifactFromRepositoryAsync
            GitHubRepoSettings repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(destinationFolder);
            var zipPath = Path.Combine(destinationFolder, $"artifact_{artifactId}.zip");

            try
            {
                // Use repository-specific URL to download the artifact
                var artifactUrl = $"https://api.github.com/repos/{repoConfig.RepoOwner}/{repoConfig.RepoName}/actions/artifacts/{artifactId}/zip";
                _logger.LogInformation("Downloading from: {url}", artifactUrl);

                // Get the response with the download URL
                var response = await _apiClient.GetAsync<HttpResponseMessage>(repoConfig, artifactUrl, cancellationToken);
                _logger.LogInformation("Download response status: {status}", response.StatusCode);

                // If we get a 403/401 error and don't have auth, give a better error message
                if ((response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                     response.StatusCode == System.Net.HttpStatusCode.Unauthorized))
                {
                    bool hasAuth = !string.IsNullOrEmpty(_apiClient.GetAuthToken());

                    if (!hasAuth)
                    {
                        // GitHub requires authentication for downloading build artifacts
                        // even if the repository is public
                        throw new GitHubServiceException(
                            "GitHub requires authentication to download build artifacts.\n\n" +
                            "Please add a GitHub personal access token in settings with 'workflow' permissions.\n" +
                            "This is required even for public repositories due to GitHub API limitations.");
                    }
                    else
                    {
                        // If we have auth and still get forbidden, the token might be invalid
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Error with token: {content}", errorContent);
                        throw new GitHubServiceException(
                            "Authentication failed with the provided token. The token may be invalid or lacks the required permissions.\n\n" +
                            "Please make sure your token has 'workflow' permissions and is not expired.");
                    }
                }

                // Handle other errors
                bool shouldContinue = await _apiClient.HandleRateLimiting(response);
                if (!shouldContinue)
                {
                    throw new GitHubServiceException("Rate limit exceeded. Please try again later.");
                }
                
                response.EnsureSuccessStatusCode();

                // Download the file with progress
                var total = response.Content.Headers.ContentLength ?? -1L;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

                // Buffer for reading data from the HTTP response
                var buffer = new byte[8192];
                long bytesRead = 0;
                int bytesReadThisChunk;

                // Read data in chunks and report progress
                while ((bytesReadThisChunk = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesReadThisChunk, cancellationToken);
                    bytesRead += bytesReadThisChunk;

                    // Report progress if total is known
                    if (total > 0 && progress != null)
                    {
                        progress.Report((double)bytesRead / total);
                    }
                }

                _logger.LogInformation("Successfully downloaded {bytes} bytes to {path}", bytesRead, zipPath);

                // Return the zip path to let GameVersionServiceFacade handle extraction
                return zipPath;
            }
            catch (GitHubServiceException)
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath); // Clean up partial file
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath); // Clean up partial file
                _logger.LogError(ex, "Unexpected error downloading artifact {artifactId}", artifactId);
                throw new GitHubServiceException($"Unexpected error downloading artifact {artifactId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Installs a GitHub artifact
        /// </summary>
        public async Task<GameVersion?> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "GenHub", "Downloads");
            Directory.CreateDirectory(tempPath);

            try
            {
                _logger.LogInformation("Installing artifact: {ArtifactName} ({ArtifactId})", artifact.Name, artifact.Id);


                // Download the artifact
                progress?.Report(new InstallProgress { Message = "Downloading artifact...", Percentage = 0 });

                // Create a progress converter that maps download progress (0-1) to installation progress (0-0.5)
                var downloadProgress = new Progress<double>(p =>
                    progress?.Report(new InstallProgress { Message = $"Downloading... {p:P0}", Percentage = p * 0.4 }));

                // Download the artifact zip file
                string downloadFile = await DownloadArtifactAsync(
                    artifact.Id,
                    tempPath,
                    downloadProgress,
                    cancellationToken);

                // Create extract options
                var options = new ExtractOptions
                {
                    DeleteZipAfterExtraction = true,
                    CustomInstallName = $"{DateTime.Now:yyyyMMdd}_{artifact.Name}"
                };

                // Convert the installation progress to our format
                var installProgress = new Progress<InstallProgress>(p =>
                {
                    var adjustedPercentage = 0.4 + (p.Percentage * 0.6); // Scale from 40% to 100%
                    progress?.Report(new InstallProgress
                    {
                        Message = p.Message,
                        Percentage = adjustedPercentage,
                        TotalFiles = p.TotalFiles,
                        Stage = p.Stage
                    });
                });

                // Install the artifact using the version installer service instead of GameVersionServiceFacade
                var result = await _versionInstaller.InstallVersionAsync(
                    artifact,
                    downloadFile,
                    options,
                    installProgress,
                    cancellationToken);

                // Properly access properties through the result.Data property
                if (result.Success && result.Data != null)
                {
                    var version = result.Data; // Store the GameVersion from Data property

                    // Update installed status using the GameVersion data
                    artifact.IsInstalled = true;

                    // Set additional properties on the artifact from the installed version
                    // If the installed version has GitHub metadata, update the artifact accordingly
                    if (version.GitHubMetadata?.AssociatedArtifact != null)
                    {
                        artifact.Id = version.GitHubMetadata.AssociatedArtifact.Id;
                        artifact.Name = version.GitHubMetadata.AssociatedArtifact.Name;
                        artifact.BuildInfo = version.GitHubMetadata.AssociatedArtifact.BuildInfo;
                        artifact.WorkflowId = version.GitHubMetadata.AssociatedArtifact.WorkflowId;
                        artifact.RunId = version.GitHubMetadata.AssociatedArtifact.RunId;
                        artifact.WorkflowNumber = version.GitHubMetadata.AssociatedArtifact.WorkflowNumber;
                        artifact.PullRequestNumber = version.GitHubMetadata.AssociatedArtifact.PullRequestNumber;
                        artifact.PullRequestTitle = version.GitHubMetadata.AssociatedArtifact.PullRequestTitle;
                        artifact.CommitSha = version.GitHubMetadata.AssociatedArtifact.CommitSha;
                        artifact.CommitMessage = version.GitHubMetadata.AssociatedArtifact.CommitMessage;
                        artifact.EventType = version.GitHubMetadata.AssociatedArtifact.EventType;
                        artifact.CreatedAt = version.GitHubMetadata.AssociatedArtifact.CreatedAt;
                        artifact.BuildPreset = version.GitHubMetadata.AssociatedArtifact.BuildPreset;
                        artifact.RepositoryInfo = version.GitHubMetadata.AssociatedArtifact.RepositoryInfo;
                    }

                    // Return the actual GameVersion data, not the OperationResult
                    return version;
                }
                else
                {
                    // Handle error case
                    _logger.LogError("Failed to install version: {ErrorMessage}", result.ErrorMessage);
                    throw new GitHubDownloadException($"Failed to install version: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Artifact installation was cancelled: {ArtifactId}", artifact.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact {ArtifactId}: {ErrorMessage}", artifact.Id, ex.Message);
                throw; // Re-throw to let calling code handle the error
            }
        }

        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default)
        {
            var artifacts = await GetAvailableArtifactsAsync(cancellationToken);
            return artifacts.Select(a => new GameVersion
            {
                Id = $"github-{a.Id}",
                Name = a.Name,
                SourceType = GameInstallationType.GitHubArtifact,
                SourceSpecificMetadata = new GitHubSourceMetadata
                {
                    AssociatedArtifact = a,
                    WorkflowDefinitionName = null,
                    WorkflowDefinitionPath = null,
                    WorkflowRunStatus = null,
                    WorkflowRunConclusion = null,
                    SourceControlBranch = null
                    // You can fill these if you have the info
                },
                IsValid = false,
                InstallDate = default
            });
        }

        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(GitHubRepoSettings repoConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                var artifacts = await GetArtifactsForRepositoryAsync(repoConfig, cancellationToken);
                if (artifacts == null || !artifacts.Any())
                {
                    return Enumerable.Empty<GameVersion>();
                }

                return artifacts.Select(a => new GameVersion
                {
                    Id = $"github-{a.Id}",
                    Name = a.Name,
                    SourceType = GameInstallationType.GitHubArtifact,
                    SourceSpecificMetadata = new GitHubSourceMetadata
                    {
                        AssociatedArtifact = a,
                        WorkflowDefinitionName = null,
                        WorkflowDefinitionPath = null,
                        WorkflowRunStatus = null,
                        WorkflowRunConclusion = null,
                        SourceControlBranch = null
                        // You can fill these if you have the info
                    },
                    IsValid = false,
                    InstallDate = default
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detected versions from GitHub: {ErrorMessage}", ex.Message);
                return Enumerable.Empty<GameVersion>();
            }
        }

        /// <summary>
        /// Parses build information from an artifact name
        /// </summary>
        public GitHubBuild ParseBuildInfo(string artifactName)
        {
            var info = new GitHubBuild();

            try
            {
                // Special handling for TheSuperHackers/GeneralsGameCode artifacts
                // Example: "Generals-vc6-debug+t+e" or "GeneralsMD-vc6-debug+t+e"
                string[] parts = artifactName.Split('-');
                if (parts.Length > 0)
                {
                    string gameVariantString = parts[0];
                    GameVariant gameVariant;

                    if (gameVariantString.Equals("GeneralsMD", StringComparison.OrdinalIgnoreCase))
                    {
                        gameVariant = GameVariant.ZeroHour;
                    }
                    else if (Enum.TryParse<GameVariant>(gameVariantString, true, out gameVariant))
                    {
                        gameVariant = GameVariant.Generals;
                    }
                    else
                    {
                        gameVariant = GameVariant.Unknown; // Or handle the default case as needed
                    }

                    info.GameVariant = gameVariant; // "Generals" or "ZeroHour"
                }


                if (parts.Length > 1)
                {
                    string compilerPart = parts[1]; // "vc6" or "win32" or "win32-vcpkg"
                    info.Compiler = compilerPart;

                    // Check if there's a configuration part
                    if (parts.Length > 2)
                    {
                        string configPart = parts[2]; // "debug", "profile", "internal", etc.

                        // Check for flags (usually +t+e at the end)
                        if (configPart.Contains("+"))
                        {
                            string[] flagParts = configPart.Split('+', StringSplitOptions.RemoveEmptyEntries);
                            info.Configuration = flagParts[0]; // "debug"

                            // Process flags
                            foreach (var flag in flagParts.Skip(1))
                            {
                                if (flag == "t") info.HasTFlag = true;
                                if (flag == "e") info.HasEFlag = true;
                            }
                        }
                        else
                        {
                            info.Configuration = configPart;
                        }
                    }
                    else if (compilerPart.Contains("+"))
                    {
                        // The flags are directly attached to the compiler part (e.g., "vc6+t+e")
                        string[] flagParts = compilerPart.Split('+', StringSplitOptions.RemoveEmptyEntries);
                        info.Compiler = flagParts[0];

                        foreach (var flag in flagParts.Skip(1))
                        {
                            if (flag == "t") info.HasTFlag = true;
                            if (flag == "e") info.HasEFlag = true;
                        }
                    }
                }
            }
            catch
            {
                // Default to returning what we have in case of parsing errors
            }

            return info;
        }

        // Helper method to parse artifacts from JSON response
        private IEnumerable<GitHubArtifact> ParseArtifactsFromJson(string jsonContent)
        {
            var artifacts = new List<GitHubArtifact>();

            try
            {
                var artDoc = JsonDocument.Parse(jsonContent);

                if (artDoc.RootElement.TryGetProperty("artifacts", out var artifactsElement))
                {
                    foreach (var art in artifactsElement.EnumerateArray())
                    {
                        try
                        {
                            var githubArtifact = new GitHubArtifact
                            {
                                Id = art.GetProperty("id").GetInt64(),
                                Name = art.GetProperty("name").GetString() ?? "Unknown Artifact",
                                ArchiveDownloadUrl = art.GetProperty("archive_download_url").GetString() ?? string.Empty,
                                SizeInBytes = art.GetProperty("size_in_bytes").GetInt64(),
                                CreatedAt = art.TryGetProperty("created_at", out var createdAtProp) && createdAtProp.TryGetDateTime(out var artifactDate)
                                    ? artifactDate
                                    : DateTime.MinValue,
                                CommitSha = TryExtractSha256Hash(art)
                            };

                            artifacts.Add(githubArtifact);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing artifact");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing artifacts from JSON");
            }

            return artifacts;
        }

        // Helper method to try extracting a SHA256 hash from artifact metadata
        private string TryExtractSha256Hash(JsonElement artifactElement)
        {
            try
            {
                if (artifactElement.TryGetProperty("digest", out var descProperty))
                {
                    var desc = descProperty.GetString() ?? string.Empty;
                    if (desc.Contains("sha256:"))
                    {
                        var parts = desc.Split(new[] { "sha256:" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            return parts[1].Trim().Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Helper method to get workflow run by ID - improved to handle errors better
        private async Task<GitHubWorkflow?> GetWorkflowRunAsync(long runId, CancellationToken cancellationToken)
        {
            try
            {
                if (runId <= 0)
                {
                    _logger.LogWarning("Invalid runId: {RunId}", runId);
                    return null;
                }

                // Try to find the run in all repositories (starting with default)
                var repositories = new List<GitHubRepoSettings> { _repoService.GetDefaultRepository() };
                repositories.AddRange(_repoService.GetRepositories().Where(r =>
                    r.RepoOwner != repositories[0].RepoOwner ||
                    r.RepoName != repositories[0].RepoName));

                foreach (var repo in repositories)
                {
                    try
                    {
                        _logger.LogDebug("Looking for workflow run {RunId} in repository {RepoOwner}/{RepoName}",
                            runId, repo.RepoOwner, repo.RepoName);

                        var workflow = await _workflowService.GetWorkflowRunAsync(
                            repo, runId, cancellationToken);

                        if (workflow != null)
                        {
                            // Make sure to attach repository information to the workflow
                            workflow.RepositoryInfo = repo;
                            return workflow;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        // Log but continue trying other repositories
                        _logger.LogWarning(ex, "Failed to get workflow run {RunId} from repo {Repo}/{Name}",
                            runId, repo.RepoOwner, repo.RepoName);
                    }
                }

                _logger.LogWarning("Could not find workflow run {RunId} in any repository", runId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow run {RunId}", runId);
                return null;
            }
        }

        // Helper method to parse an artifact from a JSON element
        private GitHubArtifact ParseArtifact(JsonElement artifactElement)
        {
            var artifact = new GitHubArtifact
            {
                Id = artifactElement.GetProperty("id").GetInt64(),
                Name = artifactElement.GetProperty("name").GetString() ?? string.Empty,
                SizeInBytes = artifactElement.GetProperty("size_in_bytes").GetInt64()
            };

            // Try to parse creation date
            if (artifactElement.TryGetProperty("created_at", out var createdAtProp) &&
                createdAtProp.ValueKind != JsonValueKind.Null)
            {
                if (DateTime.TryParse(createdAtProp.GetString(), out var createdAt))
                {
                    artifact.CreatedAt = createdAt;
                }
            }

            // Parse build info from the artifact name
            artifact.BuildInfo = ParseBuildInfo(artifact.Name);

            return artifact;
        }
    }
}
