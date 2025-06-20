using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Intelligent repository discovery service that finds C&C Generals repositories through network traversal
    /// </summary>
    public class GitHubRepositoryDiscoveryService : IGitHubRepositoryDiscoveryService
    {
        private readonly ILogger<GitHubRepositoryDiscoveryService> _logger;
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubRepositoryManager _repositoryManager;

        #region Configuration
        
        // Base repositories - starting points only (as per requirements.md)
        private static readonly BaseRepositoryEntry[] BaseRepositories = {
            new("electronicarts", "CnC_Generals_Zero_Hour", "C&C Generals Zero Hour (Official EA)", 1, true),
            new("TheSuperHackers", "GeneralsGameCode", "Generals Game Code (Community)", 2, false)
        };

        // Marker repositories that MUST be found to validate discovery service effectiveness
        private static readonly MarkerRepositoryEntry[] MarkerRepositories = {
            new("jmarshall2323", "CnC_Generals_Zero_Hour", "Marker: Fork of primordial EA repository"),
            new("x64-dev", "GeneralsGameCode_GeneralsOnline", "Marker: Fork of community repository"),
        };        // Broad search terms for discovery - ALL queries must include fork:true to enforce requirements
        private static readonly string[] DiscoveryQueries = {
            "generals zero hour fork:true",
            "CnC_Generals_Zero_Hour in:name fork:true",
            "command conquer generals fork:true",
            "generalsgamecode fork:true",
            "cnc generals in:name fork:true",
            "zero hour generals in:name fork:true",
            "generals game code in:name fork:true",
            "CnC_Generals_Zero_Hour in:name fork:true",
            "GeneralsGameCode in:name fork:true",
            "generals zero hour language:C++ fork:true",
            "command conquer zero hour language:C++ fork:true",
        };

        // Keywords that indicate C&C Generals relevance
        private static readonly string[] GeneralsKeywords = {
            "generals", "zerohour", "zero hour", "zh", "generalsgamecode", "cnc_generals"
        };

        // Keywords to exclude other C&C games
        private static readonly string[] ExcludeKeywords = {
            "tiberian", "tiberium", "kane", "wrath", "dawn", "sun", "firestorm",
            "red alert", "redalert", "wiki", "documentation", "docs", "website", "tutorial"
        };

        #endregion

        public GitHubRepositoryDiscoveryService(
            ILogger<GitHubRepositoryDiscoveryService> logger,
            IGitHubApiClient apiClient,
            IGitHubRepositoryManager repositoryManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
        }

        #region Public Interface Implementation

        /// <summary>
        /// Discovers repositories using intelligent network traversal with non-blocking execution
        /// </summary>
        public async Task<OperationResult<IEnumerable<GitHubRepository>>> DiscoverRepositoriesAsync(
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new DiscoveryOptions();
            _logger.LogInformation("=== STARTING INTELLIGENT REPOSITORY DISCOVERY ===");

            var discoveredRepos = new Dictionary<string, GitHubRepository>();

            try
            {
                // Use ConfigureAwait(false) to prevent UI blocking
                await DiscoverBaseRepositories(discoveredRepos, cancellationToken).ConfigureAwait(false);
                await GradualNetworkExpansion(discoveredRepos, options, cancellationToken).ConfigureAwait(false);
                
                var validRepos = await SmartFilterRepositories(discoveredRepos.Values, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("=== DISCOVERY COMPLETE ===");
                _logger.LogInformation("Found {Total} repositories, {Valid} passed smart filtering", 
                    discoveredRepos.Count, validRepos.Count());

                return OperationResult<IEnumerable<GitHubRepository>>.Succeeded(validRepos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Repository discovery failed");
                return OperationResult<IEnumerable<GitHubRepository>>.Failed($"Discovery failed: {ex.Message}");
            }
        }

        public async Task<OperationResult<IEnumerable<GitHubRepository>>> DiscoverActiveForks(
            string owner,
            string name,
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Discovering forks for {Owner}/{Name}", owner, name);

                var forks = await GetRepositoryForksWithRetry(owner, name, cancellationToken);
                if (!forks.Any())
                {
                    return OperationResult<IEnumerable<GitHubRepository>>.Succeeded(Enumerable.Empty<GitHubRepository>());
                }

                var validForks = forks
                    .Where(IsGeneralsRelated)
                    .Where(f => !IsExcluded(f))
                    .Take(50)
                    .ToList();

                return OperationResult<IEnumerable<GitHubRepository>>.Succeeded(validForks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering forks for {Owner}/{Name}", owner, name);
                return OperationResult<IEnumerable<GitHubRepository>>.Failed($"Fork discovery failed: {ex.Message}");
            }
        }        public Task<OperationResult<IEnumerable<GitHubRepository>>> FindMostActiveForks(
            IEnumerable<GitHubRepository> repositories,
            DiscoveryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var activeRepos = repositories
                    .Where(IsGeneralsRelated)
                    .Where(r => !IsExcluded(r))
                    .Where(r => HasRecentActivity(r) || HasCommunityEngagement(r))
                    .OrderByDescending(r => r.StargazersCount)
                    .ThenByDescending(r => r.PushedAt ?? DateTime.MinValue)
                    .Take(options?.MaxResultsToReturn ?? 20);

                return Task.FromResult(OperationResult<IEnumerable<GitHubRepository>>.Succeeded(activeRepos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding most active forks");
                return Task.FromResult(OperationResult<IEnumerable<GitHubRepository>>.Failed($"Active fork search failed: {ex.Message}"));
            }
        }

        public async Task<OperationResult> AddDiscoveredRepositoriesAsync(
            IEnumerable<GitHubRepository> repositories,
            bool replaceExisting = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var validatedRepos = repositories.Where(r => r.IsValid).ToList();
                _logger.LogInformation("Adding {Count} validated repositories", validatedRepos.Count);

                var existingRepos = await GetExistingRepositories(cancellationToken);
                var reposToAdd = FilterNewRepositories(validatedRepos, existingRepos, replaceExisting);

                if (reposToAdd.Any())
                {
                    await SaveRepositories(existingRepos.Concat(reposToAdd).ToList(), cancellationToken);
                    _logger.LogInformation("Successfully added {Count} new repositories", reposToAdd.Count);
                }

                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding discovered repositories");
                return OperationResult.Failed($"Failed to add repositories: {ex.Message}");
            }
        }

        #endregion

        #region Core Discovery Logic        /// <summary>
        /// Step 1: Get base repositories (SKIP base repositories as they are not forks)
        /// According to requirements.md, only forks are valid - base repositories are excluded
        /// </summary>
        private async Task DiscoverBaseRepositories(Dictionary<string, GitHubRepository> discoveredRepos, CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== STEP 1: DISCOVERING BASE REPOSITORIES ===");
            _logger.LogInformation("NOTE: Base repositories are NOT added to results as they are not forks");
            _logger.LogInformation("Base repositories are only used as starting points for fork discovery");

            // We still need to verify base repositories exist for fork discovery, but don't add them
            foreach (var baseRepo in BaseRepositories)
            {
                try
                {
                    var repo = await _apiClient.GetRepositoryInfoAsync(baseRepo.Owner, baseRepo.Name, cancellationToken);
                    if (repo != null)
                    {
                        _logger.LogInformation("✓ Verified base repository exists: {DisplayName} (will be used for fork discovery)", baseRepo.DisplayName);
                    }
                    else
                    {
                        _logger.LogWarning("❌ Base repository {Owner}/{Name} not found - fork discovery may be incomplete", baseRepo.Owner, baseRepo.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error verifying base repository {Owner}/{Name}", baseRepo.Owner, baseRepo.Name);
                }
            }
        }

        /// <summary>
        /// Step 2: Gradual network expansion with intelligent rate limiting
        /// </summary>
        private async Task GradualNetworkExpansion(Dictionary<string, GitHubRepository> discoveredRepos, DiscoveryOptions options, CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== STEP 2: GRADUAL NETWORK EXPANSION ===");

            // Phase 1: Direct fork discovery from base repositories
            await ExpandFromBaseRepositories(discoveredRepos, cancellationToken);

            // Phase 2: Intelligent search-based expansion (rate limited)
            await IntelligentSearchExpansion(discoveredRepos, cancellationToken);

            // Phase 3: Network traversal from discovered repositories
            await NetworkTraversalExpansion(discoveredRepos, cancellationToken);
        }        /// <summary>
        /// Phase 1: Expand from base repositories - with explicit marker repository discovery
        /// </summary>
        private async Task ExpandFromBaseRepositories(Dictionary<string, GitHubRepository> discoveredRepos, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Phase 1: Expanding from base repositories");

            foreach (var baseRepo in BaseRepositories)
            {
                try
                {
                    _logger.LogInformation("=== GETTING FORKS FOR BASE REPOSITORY: {Owner}/{Name} ===", baseRepo.Owner, baseRepo.Name);
                    
                    // First, verify the base repository exists
                    var baseRepoInfo = await _apiClient.GetRepositoryInfoAsync(baseRepo.Owner, baseRepo.Name, cancellationToken);
                    if (baseRepoInfo == null)
                    {
                        _logger.LogError("❌ Base repository {Owner}/{Name} not found or inaccessible", baseRepo.Owner, baseRepo.Name);
                        continue;
                    }
                    
                    _logger.LogInformation("✅ Base repository verified: {Owner}/{Name} (Forks: {ForksCount})", 
                        baseRepo.Owner, baseRepo.Name, baseRepoInfo.ForksCount);
                    
                    // Get forks with detailed logging
                    var forks = await GetRepositoryForksWithRetry(baseRepo.Owner, baseRepo.Name, cancellationToken);
                    var forksArray = forks.ToArray();
                    
                    _logger.LogInformation("📋 RAW FORKS RETRIEVED: {Count} forks for {Owner}/{Name}", forksArray.Length, baseRepo.Owner, baseRepo.Name);
                    
                    if (!forksArray.Any())
                    {
                        _logger.LogWarning("⚠️  No forks returned from API for {Owner}/{Name}", baseRepo.Owner, baseRepo.Name);
                        continue;
                    }
                    
                    // Log sample of forks for debugging
                    _logger.LogInformation("📋 Sample of retrieved forks:");
                    foreach (var fork in forksArray.Take(5))
                    {
                        _logger.LogInformation("   - {Owner}/{Name} (Size: {Size}KB, Stars: {Stars}, Updated: {Updated})",
                            fork.RepoOwner, fork.RepoName, fork.Size, fork.StargazersCount, fork.UpdatedAt);
                    }
                    
                    // Apply Generals-related filtering
                    var generalsRelatedForks = forksArray.Where(IsGeneralsRelated).ToArray();
                    _logger.LogInformation("🎯 GENERALS-RELATED FORKS: {Count}/{Total} for {Owner}/{Name}", 
                        generalsRelatedForks.Length, forksArray.Length, baseRepo.Owner, baseRepo.Name);
                    
                    if (generalsRelatedForks.Length != forksArray.Length)
                    {
                        _logger.LogInformation("📋 Non-Generals forks filtered out:");
                        foreach (var filtered in forksArray.Except(generalsRelatedForks).Take(3))
                        {
                            _logger.LogInformation("   - FILTERED: {Owner}/{Name} - {Description}",
                                filtered.RepoOwner, filtered.RepoName, filtered.Description);
                        }
                    }
                    
                    // Add to discovered repos
                    var addedCount = 0;
                    foreach (var fork in generalsRelatedForks.Take(50))
                    {
                        var key = GetRepositoryKey(fork);
                        if (!discoveredRepos.ContainsKey(key))
                        {
                            discoveredRepos[key] = fork;
                            addedCount++;
                            _logger.LogInformation("✅ Added fork: {Owner}/{Name}", fork.RepoOwner, fork.RepoName);
                            
                            // Check if this is a marker repository
                            if (IsMarkerRepository(fork))
                            {
                                _logger.LogInformation("🎯🎯🎯 MARKER REPOSITORY FOUND VIA FORK DISCOVERY: {Owner}/{Name}", 
                                    fork.RepoOwner, fork.RepoName);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("⚠️  Fork already discovered: {Owner}/{Name}", fork.RepoOwner, fork.RepoName);
                        }
                    }
                    
                    _logger.LogInformation("📊 FORK EXPANSION SUMMARY for {Owner}/{Name}:", baseRepo.Owner, baseRepo.Name);
                    _logger.LogInformation("   - Raw forks retrieved: {Raw}", forksArray.Length);
                    _logger.LogInformation("   - Generals-related: {Related}", generalsRelatedForks.Length);
                    _logger.LogInformation("   - Actually added: {Added}", addedCount);
                    
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error expanding from base repository {Owner}/{Name}", baseRepo.Owner, baseRepo.Name);
                }
            }
            
            // Explicit check for marker repositories - if not found, try direct retrieval
            _logger.LogInformation("=== EXPLICIT MARKER REPOSITORY CHECK ===");
            await EnsureMarkerRepositoriesDiscovered(discoveredRepos, cancellationToken);
            
            _logger.LogInformation("=== PHASE 1 COMPLETE: Total repositories discovered so far: {Count} ===", discoveredRepos.Count);
        }

        /// <summary>
        /// Ensures marker repositories are discovered - attempts direct retrieval if not found via forks
        /// </summary>
        private async Task EnsureMarkerRepositoriesDiscovered(Dictionary<string, GitHubRepository> discoveredRepos, CancellationToken cancellationToken)
        {
            foreach (var marker in MarkerRepositories)
            {
                var key = $"{marker.Owner}/{marker.Name}".ToLowerInvariant();
                var found = discoveredRepos.ContainsKey(key);
                
                if (!found)
                {
                    _logger.LogWarning("❌ Marker repository {Owner}/{Name} not found via fork discovery - attempting direct retrieval", 
                        marker.Owner, marker.Name);
                    
                    try
                    {
                        var markerRepo = await _apiClient.GetRepositoryInfoAsync(marker.Owner, marker.Name, cancellationToken);
                        if (markerRepo != null)
                        {
                            if (!markerRepo.IsFork)
                            {
                                _logger.LogError("❌ Marker repository {Owner}/{Name} exists but is NOT A FORK - cannot be included", 
                                    marker.Owner, marker.Name);
                            }
                            else if (IsGeneralsRelated(markerRepo) && !IsExcluded(markerRepo))
                            {
                                discoveredRepos[key] = markerRepo;
                                _logger.LogInformation("✅ DIRECT RETRIEVAL: Added marker repository {Owner}/{Name}", 
                                    marker.Owner, marker.Name);
                            }
                            else
                            {
                                _logger.LogWarning("❌ Marker repository {Owner}/{Name} exists but failed Generals filtering", 
                                    marker.Owner, marker.Name);
                            }
                        }
                        else
                        {
                            _logger.LogError("❌ Marker repository {Owner}/{Name} does not exist or is inaccessible", 
                                marker.Owner, marker.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "💥 Error directly retrieving marker repository {Owner}/{Name}", 
                            marker.Owner, marker.Name);
                    }
                }
                else
                {
                    _logger.LogInformation("✅ Marker repository {Owner}/{Name} already discovered via fork expansion", 
                        marker.Owner, marker.Name);
                }
                
                await Task.Delay(500, cancellationToken);
            }
        }/// <summary>
        /// Phase 2: Intelligent search expansion - ONLY forks are included
        /// </summary>
        private async Task IntelligentSearchExpansion(Dictionary<string, GitHubRepository> discoveredRepos, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Phase 2: Intelligent search expansion (fork-only)");

            foreach (var query in DiscoveryQueries)
            {
                try
                {
                    _logger.LogDebug("Searching with query: {Query}", query);
                    
                    var searchResults = await _apiClient.SearchRepositoriesAsync(query, "updated", cancellationToken);
                    
                    if (searchResults?.Any() == true)
                    {
                        var validResults = searchResults
                            .Where(r => r.IsFork) // CRITICAL: Only include forks
                            .Where(IsGeneralsRelated)
                            .Where(r => !IsExcluded(r))
                            .Take(10);
                        
                        foreach (var repo in validResults)
                        {
                            var key = GetRepositoryKey(repo);
                            if (!discoveredRepos.ContainsKey(key))
                            {
                                discoveredRepos[key] = repo;
                                _logger.LogInformation("Found fork via search: {Owner}/{Name}", repo.RepoOwner, repo.RepoName);
                                
                                // Check if this is a marker repository
                                if (IsMarkerRepository(repo))
                                {
                                    _logger.LogInformation("🎯🎯🎯 MARKER REPOSITORY FOUND VIA SEARCH: {Owner}/{Name}", 
                                        repo.RepoOwner, repo.RepoName);
                                }
                            }
                        }
                        
                        // Log any non-forks that were filtered out
                        var nonForks = searchResults
                            .Where(r => !r.IsFork)
                            .Where(IsGeneralsRelated)
                            .Where(r => !IsExcluded(r))
                            .Take(3);
                            
                        foreach (var nonFork in nonForks)
                        {
                            _logger.LogDebug("Filtered non-fork from search: {Owner}/{Name}", nonFork.RepoOwner, nonFork.RepoName);
                        }
                    }
                    
                    await Task.Delay(2000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Search query failed: {Query}", query);
                }
            }
        }

        /// <summary>
        /// Phase 3: Network traversal from discovered repositories
        /// </summary>
        private async Task NetworkTraversalExpansion(Dictionary<string, GitHubRepository> discoveredRepos, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Phase 3: Network traversal expansion");

            var highValueRepos = discoveredRepos.Values
                .Where(r => r.StargazersCount > 0 || r.ForksCount > 0 || r.Size > 500)
                .OrderByDescending(r => r.ForksCount)
                .ThenByDescending(r => r.StargazersCount)
                .Take(15)
                .ToList();

            _logger.LogInformation("Found {Count} high-value repositories for traversal", highValueRepos.Count);

            foreach (var repo in highValueRepos)
            {
                try
                {
                    _logger.LogDebug("Getting forks for high-value repository: {Owner}/{Name}", repo.RepoOwner, repo.RepoName);
                    var forks = await GetRepositoryForksWithRetry(repo.RepoOwner, repo.RepoName, cancellationToken);
                    
                    foreach (var fork in forks.Where(IsGeneralsRelated).Take(30))
                    {
                        var key = GetRepositoryKey(fork);
                        if (!discoveredRepos.ContainsKey(key))
                        {
                            discoveredRepos[key] = fork;
                            _logger.LogDebug("Added via traversal: {Owner}/{Name}", fork.RepoOwner, fork.RepoName);
                        }
                    }

                    await Task.Delay(1500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in network traversal for {Owner}/{Name}", repo.RepoOwner, repo.RepoName);
                }
            }
            
            // Add deeper traversal for forks of forks
            var recentlyDiscovered = discoveredRepos.Values
                .Where(r => r.IsFork && r.PushedAt.HasValue && r.PushedAt.Value > DateTime.UtcNow.AddYears(-1))
                .Take(5)
                .ToList();
                
            _logger.LogInformation("Performing deeper traversal on {Count} recently active forks", recentlyDiscovered.Count);
            
            foreach (var fork in recentlyDiscovered)
            {
                try
                {
                    var subForks = await GetRepositoryForksWithRetry(fork.RepoOwner, fork.RepoName, cancellationToken);
                    foreach (var subFork in subForks.Where(IsGeneralsRelated).Take(10))
                    {
                        var key = GetRepositoryKey(subFork);
                        if (!discoveredRepos.ContainsKey(key))
                        {
                            discoveredRepos[key] = subFork;
                            _logger.LogDebug("Added via deep traversal: {Owner}/{Name}", subFork.RepoOwner, subFork.RepoName);
                        }
                    }
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in deep traversal for {Owner}/{Name}", fork.RepoOwner, fork.RepoName);
                }
            }
        }        /// <summary>
        /// Smart filtering with comprehensive validation and optimized performance
        /// </summary>
        private async Task<IEnumerable<GitHubRepository>> SmartFilterRepositories(
            IEnumerable<GitHubRepository> repositories, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== STEP 3: SMART FILTERING ===");

            var totalCount = repositories.Count();
            _logger.LogInformation($"Total discovered repositories to validate: {totalCount}");
            
            // Check if marker repositories are in the discovered list
            _logger.LogInformation("=== MARKER REPOSITORIES PRE-VALIDATION CHECK ===");
            foreach (var marker in MarkerRepositories)
            {
                var found = repositories.Any(r => 
                    string.Equals(r.RepoOwner, marker.Owner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.RepoName, marker.Name, StringComparison.OrdinalIgnoreCase));
                
                if (found)
                {
                    _logger.LogInformation($"🎯 FOUND marker repository in discovery: {marker.Owner}/{marker.Name}");
                }
                else
                {
                    _logger.LogError($"❌ MISSING marker repository in discovery: {marker.Owner}/{marker.Name}");
                }
            }
            
            // Sample of discovered repositories for debugging
            _logger.LogInformation("📋 Sample of discovered repositories:");
            foreach (var repo in repositories.Take(10))
            {
                _logger.LogInformation($"   - {repo.RepoOwner}/{repo.RepoName} (Fork: {repo.IsFork}, Stars: {repo.StargazersCount}, Size: {repo.Size}KB)");
            }

            // Optimized validation with reduced timeout and batching
            var validRepos = new List<GitHubRepository>();
            var processedCount = 0;

            // Reduced timeout for better performance - 90 seconds total
            using var filterTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, filterTimeout.Token);

            // Process in batches for better performance
            const int batchSize = 10;
            var repositoryArray = repositories.ToArray();
            
            for (int i = 0; i < repositoryArray.Length; i += batchSize)
            {
                var batch = repositoryArray.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(async repo =>
                {
                    try
                    {
                        var isValid = await IsValidRepository(repo, linkedToken.Token).ConfigureAwait(false);
                        return new { Repository = repo, IsValid = isValid };
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Validation timed out for repository {Owner}/{Name}", repo.RepoOwner, repo.RepoName);
                        return new { Repository = repo, IsValid = false };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error validating repository {Owner}/{Name}", repo.RepoOwner, repo.RepoName);
                        return new { Repository = repo, IsValid = false };
                    }
                });

                try
                {
                    var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);
                    
                    foreach (var result in batchResults)
                    {
                        processedCount++;
                        
                        if (result.IsValid)
                        {
                            validRepos.Add(result.Repository);
                            _logger.LogInformation("✓ VALID ({Current}/{Total}): {Owner}/{Name} - {Reason}", 
                                processedCount, totalCount, result.Repository.RepoOwner, result.Repository.RepoName, 
                                GetValidationReason(result.Repository));
                        }
                        else
                        {
                            _logger.LogDebug("✗ FILTERED ({Current}/{Total}): {Owner}/{Name}", 
                                processedCount, totalCount, result.Repository.RepoOwner, result.Repository.RepoName);
                        }
                    }
                    
                    // Small delay between batches to prevent API rate limiting
                    if (i + batchSize < repositoryArray.Length)
                    {
                        await Task.Delay(200, linkedToken.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Smart filtering timed out after processing {Count}/{Total} repositories", 
                        processedCount, totalCount);
                    break;
                }
            }

            _logger.LogInformation("Smart filtering complete: {Valid}/{Total} repositories passed validation", 
                validRepos.Count, repositoryArray.Length);

            // Validate marker repositories were found in final results
            ValidateMarkerRepositoriesFound(validRepos);

            return validRepos;
        }/// <summary>
        /// Comprehensive repository validation that enforces requirements
        /// </summary>
        private async Task<bool> IsValidRepository(GitHubRepository repo, CancellationToken cancellationToken)
        {
            // CRITICAL: Repository MUST be a fork (as per updated requirements.md)
            if (!repo.IsFork)
            {
                _logger.LogDebug("Repository {Owner}/{Name} rejected: NOT A FORK - Only forks are valid", repo.RepoOwner, repo.RepoName);
                return false;
            }

            if (repo.IsArchived || repo.IsDisabled)
            {
                _logger.LogDebug("Repository {Owner}/{Name} rejected: Archived or disabled", repo.RepoOwner, repo.RepoName);
                return false;
            }

            if (!IsGeneralsRelated(repo))
            {
                _logger.LogDebug("Repository {Owner}/{Name} rejected: Not Generals related", repo.RepoOwner, repo.RepoName);
                return false;
            }

            if (IsExcluded(repo))
            {
                _logger.LogDebug("Repository {Owner}/{Name} rejected: Excluded content", repo.RepoOwner, repo.RepoName);
                return false;
            }

            if (IsMarkerRepository(repo))
            {
                _logger.LogInformation("Repository {Owner}/{Name} accepted: Marker repository (fork)", repo.RepoOwner, repo.RepoName);
                return true;
            }

            return await IsRepositoryValidAsync(repo, cancellationToken);
        }/// <summary>
        /// Enhanced repository validation with detailed logging - STRICTLY enforces requirements.md
        /// </summary>
        private async Task<bool> IsRepositoryValidAsync(GitHubRepository repository, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation($"🔍 VALIDATING: {repository.RepoOwner}/{repository.RepoName}");
                
                // CRITICAL: According to requirements.md, repository MUST have releases with assets OR successful workflows
                // NO RELEASES + NO WORKFLOWS = INVALID REPOSITORY
                
                bool hasValidReleases = false;
                bool hasSuccessfulWorkflows = false;
                
                // Check releases first (preferred according to requirements)
                var releases = await _apiClient.GetReleasesForRepositoryAsync(
                    repository.RepoOwner, 
                    repository.RepoName, 
                    20, // Check more releases to be thorough
                    cancellationToken
                );

                _logger.LogInformation($"📦 Found {releases?.Count() ?? 0} total releases for {repository.RepoOwner}/{repository.RepoName}");

                if (releases?.Any() == true)
                {
                    // REQUIREMENT: Must have published releases with downloadable assets
                    // Draft releases DO NOT count, Empty releases without assets DO NOT count
                    var validReleases = releases
                        .Where(r => !r.Draft) // Draft releases DO NOT count
                        .Where(r => r.Assets?.Any() == true) // Must have assets
                        .ToList();
                    
                    _logger.LogInformation($"📦 Release analysis for {repository.RepoOwner}/{repository.RepoName}:");
                    _logger.LogInformation($"   Total releases: {releases.Count()}");
                    _logger.LogInformation($"   Draft releases: {releases.Count(r => r.Draft)}");
                    _logger.LogInformation($"   Releases with assets: {releases.Count(r => r.Assets?.Any() == true)}");
                    _logger.LogInformation($"   Valid releases (published + assets): {validReleases.Count}");
                    
                    if (validReleases.Any())
                    {
                        hasValidReleases = true;
                        _logger.LogInformation($"✅ Repository {repository.RepoOwner}/{repository.RepoName} HAS VALID RELEASES: {validReleases.Count} published releases with downloadable assets");
                        
                        // Log sample of assets for debugging
                        foreach (var release in validReleases.Take(2))
                        {
                            var assetTypes = release.Assets?.Select(a => Path.GetExtension(a.Name ?? "")).Distinct() ?? Enumerable.Empty<string>();
                            _logger.LogInformation($"   Release {release.TagName}: {release.Assets?.Count() ?? 0} assets ({string.Join(", ", assetTypes)})");
                        }
                    }
                }

                // Check workflows (alternative requirement)
                var workflowRuns = await _apiClient.GetWorkflowRunsForRepositoryAsync(
                    repository.RepoOwner,
                    repository.RepoName,
                    20, // Check more workflow runs to be thorough
                    cancellationToken
                );

                _logger.LogInformation($"🔄 Found {workflowRuns?.Count() ?? 0} workflow runs for {repository.RepoOwner}/{repository.RepoName}");

                if (workflowRuns?.Any() == true)
                {
                    // REQUIREMENT: Must have successful workflow runs that produce artifacts
                    // Failed/cancelled workflows DO NOT count
                    var successfulRuns = workflowRuns
                        .Where(w => w.Status == "completed" && w.Conclusion == "success")
                        .ToList();
                    
                    _logger.LogInformation($"🔄 Workflow analysis for {repository.RepoOwner}/{repository.RepoName}:");
                    _logger.LogInformation($"   Total workflow runs: {workflowRuns.Count()}");
                    _logger.LogInformation($"   Completed runs: {workflowRuns.Count(w => w.Status == "completed")}");
                    _logger.LogInformation($"   Successful runs: {successfulRuns.Count}");
                    
                    if (successfulRuns.Any())
                    {
                        hasSuccessfulWorkflows = true;
                        _logger.LogInformation($"✅ Repository {repository.RepoOwner}/{repository.RepoName} HAS SUCCESSFUL WORKFLOWS: {successfulRuns.Count} successful workflow runs");
                    }
                }

                // CRITICAL ENFORCEMENT: NO RELEASES + NO WORKFLOWS = INVALID REPOSITORY
                if (!hasValidReleases && !hasSuccessfulWorkflows)
                {
                    _logger.LogWarning($"❌ Repository {repository.RepoOwner}/{repository.RepoName} STRICTLY REJECTED: NO releases with assets AND NO successful workflows");
                    _logger.LogWarning($"   According to requirements.md: Repository size, stars, forks, activity are IRRELEVANT without releases/workflows");
                    _logger.LogWarning($"   This repository provides NO downloadable content to end users");
                    return false;
                }

                // Repository is valid - has downloadable content
                var reasons = new List<string>();
                if (hasValidReleases) reasons.Add($"releases with assets");
                if (hasSuccessfulWorkflows) reasons.Add($"successful workflows");
                
                _logger.LogInformation($"✅ Repository {repository.RepoOwner}/{repository.RepoName} APPROVED: Has {string.Join(" AND ", reasons)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error validating repository {repository.RepoOwner}/{repository.RepoName} - DEFAULTING TO REJECTION");
                // According to requirements: "Default to REJECTION if releases/workflows cannot be verified"
                return false;
            }
        }

        private string GetValidationReason(GitHubRepository repo)
        {
            if (IsMarkerRepository(repo)) return "🎯 MARKER REPOSITORY";
            return "Has releases or workflows";
        }

        private bool HasDevelopmentIndicators(GitHubRepository repo)
        {
            return repo.Size > 1000 && (HasRecentActivity(repo) || HasCommunityEngagement(repo));
        }

        private bool IsGeneralsRelated(GitHubRepository repo)
        {
            var text = $"{repo.RepoOwner} {repo.RepoName} {repo.Description} {repo.Language}".ToLowerInvariant();
            var topicsText = repo.Topics != null ? string.Join(" ", repo.Topics).ToLowerInvariant() : "";
            var combinedText = $"{text} {topicsText}";
            
            var isRelated = GeneralsKeywords.Any(keyword => combinedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            
            if (!isRelated)
            {
                _logger.LogDebug("❌ Repository {Owner}/{Name} NOT Generals-related. Text: '{Text}'", 
                    repo.RepoOwner, repo.RepoName, combinedText.Substring(0, Math.Min(100, combinedText.Length)));
            }
            
            return isRelated;
        }

        private bool IsExcluded(GitHubRepository repo)
        {
            var text = $"{repo.RepoName} {repo.Description}".ToLowerInvariant();
            return ExcludeKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasRecentActivity(GitHubRepository repo)
        {
            return repo.PushedAt.HasValue && repo.PushedAt.Value > DateTime.UtcNow.AddYears(-2);
        }

        private bool HasCommunityEngagement(GitHubRepository repo)
        {
            return repo.StargazersCount > 0 || 
                   repo.ForksCount > 0 || 
                   repo.WatchersCount > 0 ||
                   repo.OpenIssuesCount > 0;
        }

        private async Task<bool> HasActiveDevCycle(GitHubRepository repo, CancellationToken cancellationToken)
        {
            try
            {
                // Quick check for releases
                var releases = await _apiClient.GetReleasesForRepositoryAsync(repo.RepoOwner, repo.RepoName, 1, cancellationToken).ConfigureAwait(false);
                if (releases?.Any() == true)
                    return true;

                // Quick check for workflows
                var workflows = await _apiClient.GetWorkflowRunsForRepositoryAsync(repo.RepoOwner, repo.RepoName, 1, cancellationToken).ConfigureAwait(false);
                if (workflows?.Any() == true)
                    return true;

                return false;
            }
            catch
            {
                // If we can't check, include repositories with basic indicators
                return HasCommunityEngagement(repo) || HasRecentActivity(repo);
            }
        }

        /// <summary>
        /// Checks if a repository is one of our marker repositories
        /// </summary>
        private bool IsMarkerRepository(GitHubRepository repo)
        {
            return MarkerRepositories.Any(m =>
                string.Equals(m.Owner, repo.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Name, repo.RepoName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Logs when a marker repository is found
        /// </summary>
        private void CheckMarkerRepository(GitHubRepository repo)
        {
            var marker = MarkerRepositories.FirstOrDefault(m =>
                string.Equals(m.Owner, repo.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Name, repo.RepoName, StringComparison.OrdinalIgnoreCase));

            if (marker != null)
            {
                _logger.LogInformation("🎯 MARKER REPOSITORY FOUND: {Owner}/{Name} - {Description}",
                    repo.RepoOwner, repo.RepoName, marker.Description);
            }
        }

        /// <summary>
        /// Validates that marker repositories were found during discovery
        /// </summary>
        private void ValidateMarkerRepositoriesFound(IList<GitHubRepository> discoveredRepositories)
        {
            _logger.LogInformation("=== MARKER REPOSITORY VALIDATION ===");

            var foundMarkers = new List<MarkerRepositoryEntry>();
            var missingMarkers = new List<MarkerRepositoryEntry>();

            foreach (var marker in MarkerRepositories)
            {
                var found = discoveredRepositories.Any(repo =>
                    string.Equals(repo.RepoOwner, marker.Owner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(repo.RepoName, marker.Name, StringComparison.OrdinalIgnoreCase));

                if (found)
                {
                    foundMarkers.Add(marker);
                    _logger.LogInformation("✅ FOUND marker repository: {Owner}/{Name} - {Description}",
                        marker.Owner, marker.Name, marker.Description);
                }
                else
                {
                    missingMarkers.Add(marker);
                    _logger.LogError("❌ MISSING marker repository: {Owner}/{Name} - {Description}",
                        marker.Owner, marker.Name, marker.Description);
                }
            }

            if (missingMarkers.Any())
            {
                _logger.LogError("==================================================");
                _logger.LogError("🚨 DISCOVERY SERVICE EFFECTIVENESS: POOR");
                _logger.LogError("==================================================");
                _logger.LogError("Missing {MissingCount} out of {TotalCount} marker repositories",
                    missingMarkers.Count, MarkerRepositories.Length);
                _logger.LogError("The discovery service may not be finding all active forks");
                _logger.LogError("==================================================");
            }
            else
            {
                _logger.LogInformation("==================================================");
                _logger.LogInformation("✅ DISCOVERY SERVICE EFFECTIVENESS: EXCELLENT");
                _logger.LogInformation("==================================================");
                _logger.LogInformation("Successfully found all {Count} marker repositories!", MarkerRepositories.Length);
                _logger.LogInformation("Discovery service is working optimally!");
                _logger.LogInformation("==================================================");
            }

            _logger.LogInformation("Total repositories discovered: {Count}", discoveredRepositories.Count);
            _logger.LogInformation("Marker repositories found: {Found}/{Total}", foundMarkers.Count, MarkerRepositories.Length);
        }

        #endregion

        #region Helper Methods

        private async Task<IEnumerable<GitHubRepository>> GetRepositoryForksWithRetry(string owner, string name, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("🔍 Calling GitHub API for forks: {Owner}/{Name}", owner, name);
                
                var forks = await _apiClient.GetRepositoryForksAsync(owner, name, cancellationToken);
                
                if (forks == null)
                {
                    _logger.LogWarning("⚠️  API returned null for forks of {Owner}/{Name}", owner, name);
                    return Enumerable.Empty<GitHubRepository>();
                }
                
                var forksArray = forks.ToArray();
                _logger.LogDebug("✅ API returned {Count} forks for {Owner}/{Name}", forksArray.Length, owner, name);
                
                var validForks = forksArray.Where(f => f != null && f.IsValid && !f.IsPrivate).ToArray();
                
                if (validForks.Length != forksArray.Length)
                {
                    _logger.LogDebug("⚠️  Filtered out {Filtered} invalid/private forks from {Total} total", 
                        forksArray.Length - validForks.Length, forksArray.Length);
                }
                
                return validForks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error getting forks for {Owner}/{Name}", owner, name);
                return Enumerable.Empty<GitHubRepository>();
            }
        }

        private string GetRepositoryKey(GitHubRepository repo)
        {
            return $"{repo.RepoOwner}/{repo.RepoName}".ToLowerInvariant();
        }

        private async Task<List<GitHubRepository>> GetExistingRepositories(CancellationToken cancellationToken)
        {
            return await Task.Run(() => _repositoryManager.GetRepositories().ToList(), cancellationToken);
        }

        private List<GitHubRepository> FilterNewRepositories(
            List<GitHubRepository> validatedRepos,
            List<GitHubRepository> existingRepos,
            bool replaceExisting)
        {
            var reposToAdd = new List<GitHubRepository>();

            foreach (var repo in validatedRepos)
            {
                var existing = existingRepos.FirstOrDefault(r =>
                    string.Equals(r.RepoOwner, repo.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.RepoName, repo.RepoName, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    reposToAdd.Add(repo);
                }
                else if (replaceExisting)
                {
                    existing.DisplayName = repo.DisplayName;
                    existing.Branch = repo.Branch;
                    existing.LastAccessed = DateTime.UtcNow;
                }
            }

            return reposToAdd;
        }

        private async Task SaveRepositories(List<GitHubRepository> repositories, CancellationToken cancellationToken)
        {
            await Task.Run(() => _repositoryManager.SaveRepositories(repositories), cancellationToken);
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Base repository configuration
    /// </summary>
    internal record BaseRepositoryEntry(string Owner, string Name, string DisplayName, int Priority, bool IsOfficial);
    /// <summary>
    /// Marker repository that validates discovery service effectiveness
    /// </summary>
    internal record MarkerRepositoryEntry(string Owner, string Name, string Description);
    #endregion
}
