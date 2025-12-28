using System.Collections.Generic;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenHub.Features.Storage.Services;

/// <summary>
/// Resolves which CAS pool to use based on content type.
/// </summary>
public class CasPoolResolver(
    IOptions<CasConfiguration> config,
    ILogger<CasPoolResolver> logger) : ICasPoolResolver
{
    /// <summary>
    /// Content types that should use the installation pool (same drive as game).
    /// </summary>
    private static readonly HashSet<ContentType> InstallationPoolTypes =
    [
        ContentType.GameInstallation,
        ContentType.GameClient,
    ];

    private readonly CasConfiguration _config = config.Value;

    /// <inheritdoc/>
    public CasPoolType ResolvePool(ContentType contentType)
    {
        // GameInstallation and GameClient go to installation pool for hardlink support
        if (InstallationPoolTypes.Contains(contentType) && IsInstallationPoolAvailable())
        {
            logger.LogDebug("Resolved {ContentType} to Installation pool", contentType);
            return CasPoolType.Installation;
        }

        // All other content goes to primary pool
        logger.LogDebug("Resolved {ContentType} to Primary pool", contentType);
        return CasPoolType.Primary;
    }

    /// <inheritdoc/>
    public string GetPoolRootPath(CasPoolType poolType)
    {
        return poolType switch
        {
            CasPoolType.Installation when IsInstallationPoolAvailable()
                => _config.InstallationPoolRootPath,
            _ => _config.CasRootPath,
        };
    }

    /// <inheritdoc/>
    public string GetPoolRootPath(ContentType contentType)
    {
        var poolType = ResolvePool(contentType);
        return GetPoolRootPath(poolType);
    }

    /// <inheritdoc/>
    public bool IsInstallationPoolAvailable()
    {
        return !string.IsNullOrWhiteSpace(_config.InstallationPoolRootPath);
    }
}
