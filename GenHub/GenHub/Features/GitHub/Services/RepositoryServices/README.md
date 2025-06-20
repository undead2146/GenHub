# GitHub Repository Services

This directory contains services responsible for managing GitHub repositories, including discovery, validation, and management operations.

## Services Overview

### GitHubRepositoryDiscoveryService

**Purpose**: Intelligently discovers Command & Conquer: Generals Zero Hour repositories with downloadable content through fork network traversal.

**Key Features**:

- **Intelligent Fork Discovery**: Uses network traversal starting from known base repositories to find all active forks
- **Content Validation**: Ensures discovered repositories have releases with assets or successful workflow runs
- **Marker Repository Validation**: Uses known active repositories as markers to validate discovery effectiveness
- **Rate Limiting**: Implements intelligent delays to respect GitHub API limits
- **Non-blocking Execution**: Uses ConfigureAwait(false) to prevent UI blocking

**Discovery Process**:

1. **Base Repository Discovery**: Always includes official and community base repositories
2. **Network Expansion**: Traverses fork networks to discover all related repositories
3. **Content Validation**: Validates repositories for downloadable content (releases/workflows)
4. **Smart Filtering**: Applies secondary filters only after content validation

**Requirements Satisfied**:

- ✅ Discovers all forks of base repositories
- ✅ Validates repositories for releases or workflows
- ✅ Excludes repositories without downloadable content
- ✅ Uses generic search terms (no hardcoded marker names in queries)
- ✅ Comprehensive logging and error handling
- ✅ Follows SOLID and clean architecture principles

### GitHubRepositoryManager

**Purpose**: Manages the local repository list, providing CRUD operations for GitHub repositories.

**Key Features**:

- Repository storage and retrieval
- Repository validation and filtering
- Integration with caching layer
- Repository metadata management

## Configuration

The discovery service is configured through the `DiscoveryOptions` class:

```csharp
public class DiscoveryOptions
{
    public bool IncludeForks { get; set; } = true;
    public bool IncludeSearch { get; set; } = true;
    public int MaxForkDepth { get; set; } = 2;
    public int MaxForksPerRepository { get; set; } = 20;
    public int MaxForksToEvaluate { get; set; } = 100;
    public int MaxSearchResults { get; set; } = 50;
    public int RateLimitDelayMs { get; set; } = 1000;
    public bool RequireActionableContent { get; set; } = true;
    public int MaxResultsToReturn { get; set; } = 50;
}
```

## Usage Example

```csharp
// Inject the service
var discoveryService = serviceProvider.GetRequiredService<IGitHubRepositoryDiscoveryService>();

// Configure options
var options = new DiscoveryOptions
{
    MaxResultsToReturn = 30,
    RequireActionableContent = true,
    RateLimitDelayMs = 1500
};

// Discover repositories
var result = await discoveryService.DiscoverRepositoriesAsync(options, cancellationToken);

if (result.IsSuccess)
{
    var repositories = result.Data;
    // Process discovered repositories
    
    // Add to managed repository list
    await discoveryService.AddDiscoveredRepositoriesAsync(repositories, replaceExisting: false, cancellationToken);
}
```

## Base Repositories

The service starts discovery from these base repositories:

- **TheAssemblyArmada/Vanilla-Conquer**: Official C&C remaster source
- **TheSuperHackers/GeneralsGamePatch**: Community game patch
- **xezon/CnC_GeneralsGameCode**: Game code repository

## Marker Repositories

These repositories validate discovery effectiveness (not used in search queries):

- **DooMLoRD/Command-and-Conquer-Generals-ZeroHour-Linux**: Linux port
- **Various community forks**: Known active repositories with releases/workflows

## Architecture Notes

- **Facade Pattern**: `GitHubRepositoryDiscoveryService` coordinates multiple operations
- **Repository Pattern**: `GitHubRepositoryManager` implements repository pattern
- **Dependency Injection**: All services are registered via `GitHubModule`
- **Async/Await**: Non-blocking operations throughout
- **Error Handling**: Comprehensive error handling with detailed logging
- **Rate Limiting**: Built-in rate limiting to respect API limits

## Testing

Comprehensive test coverage includes:

- **Unit Tests**: `GitHubRepositoryDiscoveryServiceTests.cs`
- **Integration Tests**: `GitHubRepositoryDiscoveryServiceIntegrationTests.cs`
- **Scenarios Covered**:
  - Complete discovery workflow
  - Rate limiting behavior
  - Large fork networks
  - Network error handling
  - Filtering and validation
  - Cancellation support

## Logging

The service provides detailed logging at multiple levels:

- **Information**: Discovery progress, found repositories, validation results
- **Debug**: API call details, filtering decisions
- **Warning**: Network errors, missing repositories
- **Error**: Critical failures, missing marker repositories

## Performance Considerations

- **Non-blocking**: Uses ConfigureAwait(false) throughout
- **Rate Limited**: Intelligent delays between API calls
- **Batch Processing**: Processes repositories in batches
- **Timeout Handling**: Configurable timeouts for long operations
- **Memory Efficient**: Streams data rather than loading everything into memory

## Dependencies

- `IGitHubApiClient`: GitHub API communication
- `IGitHubRepositoryManager`: Repository management
- `ILogger<GitHubRepositoryDiscoveryService>`: Logging
- `Microsoft.Extensions.DependencyInjection`: Dependency injection

## Related Files

- `IGitHubRepositoryDiscoveryService.cs`: Service interface
- `requirements.md`: Detailed requirements specification
- `DiscoveryOptions.cs`: Configuration model
- Test files in `Tests/GenHub.Tests.Core/Features/GitHub/Services/`
