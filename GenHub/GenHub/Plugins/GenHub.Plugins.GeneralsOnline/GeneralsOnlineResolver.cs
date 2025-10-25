using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Plugins.GeneralsOnline.Models;
using Microsoft.Extensions.Logging;

namespace GenHub.Plugins.GeneralsOnline;

/// <summary>
/// Resolves Generals Online search results into ContentManifests.
/// </summary>
public class GeneralsOnlineResolver : IContentResolver
{
    private readonly ILogger _logger;

    public GeneralsOnlineResolver(ILogger logger)
    {
        _logger = logger;
    }

    public string ResolverId => "GeneralsOnline";

    public async Task<OperationResult<ContentManifest>> ResolveAsync(
        ContentSearchResult searchResult,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving Generals Online manifest for: {Version}", searchResult.Version);

        try
        {
            var release = searchResult.GetData<GeneralsOnlineRelease>();
            if (release == null)
            {
                return OperationResult<ContentManifest>.CreateFailure(
                    "Release information not found in search result");
            }

            var manifest = GeneralsOnlineManifestFactory.CreateManifest(release);
            
            _logger.LogInformation("Successfully resolved Generals Online manifest");
            
            return await Task.FromResult(
                OperationResult<ContentManifest>.CreateSuccess(manifest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve Generals Online manifest");
            return OperationResult<ContentManifest>.CreateFailure(
                $"Resolution failed: {ex.Message}");
        }
    }
}
