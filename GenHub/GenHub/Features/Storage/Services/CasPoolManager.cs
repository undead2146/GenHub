using System.Collections.Concurrent;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;

namespace GenHub.Features.Storage.Services;

/// <summary>
/// Manages multiple CAS storage pools for content-type-based routing.
/// </summary>
public class CasPoolManager : ICasPoolManager
{
    private readonly ICasPoolResolver _poolResolver;
    private readonly ILogger<CasPoolManager> _logger;
    private readonly IFileHashProvider _hashProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CasConfiguration _config;
    private readonly ConcurrentDictionary<CasPoolType, ICasStorage> _storages = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CasPoolManager"/> class.
    /// </summary>
    /// <param name="poolResolver">The pool resolver for routing decisions.</param>
    /// <param name="config">The CAS configuration.</param>
    /// <param name="hashProvider">The file hash provider.</param>
    /// <param name="loggerFactory">The logger factory for creating storage loggers.</param>
    /// <param name="logger">The logger instance.</param>
    public CasPoolManager(
        ICasPoolResolver poolResolver,
        IOptions<CasConfiguration> config,
        IFileHashProvider hashProvider,
        ILoggerFactory loggerFactory,
        ILogger<CasPoolManager> logger)
    {
        _poolResolver = poolResolver;
        _config = config.Value;
        _hashProvider = hashProvider;
        _loggerFactory = loggerFactory;
        _logger = logger;

        // Initialize primary pool
        InitializePool(CasPoolType.Primary);

        // Initialize installation pool if configured
        if (_poolResolver.IsInstallationPoolAvailable())
        {
            InitializePool(CasPoolType.Installation);
        }
    }

    /// <inheritdoc/>
    public ICasPoolResolver PoolResolver => _poolResolver;

    /// <inheritdoc/>
    public ICasStorage GetStorage(CasPoolType poolType)
    {
        if (_storages.TryGetValue(poolType, out var storage))
        {
            return storage;
        }

        // Fall back to primary pool if requested pool is not available
        if (poolType == CasPoolType.Installation && !_poolResolver.IsInstallationPoolAvailable())
        {
            _logger.LogDebug("Installation pool not available, falling back to primary pool");
            return _storages[CasPoolType.Primary];
        }

        // Initialize the pool on-demand if not already initialized
        InitializePool(poolType);
        return _storages[poolType];
    }

    /// <inheritdoc/>
    public ICasStorage GetStorage(ContentType contentType)
    {
        var poolType = _poolResolver.ResolvePool(contentType);
        return GetStorage(poolType);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ICasStorage> GetAllStorages()
    {
        return _storages.Values.ToList().AsReadOnly();
    }

    private void InitializePool(CasPoolType poolType)
    {
        if (_storages.ContainsKey(poolType))
        {
            return;
        }

        var rootPath = _poolResolver.GetPoolRootPath(poolType);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            _logger.LogWarning("Cannot initialize {PoolType} pool: root path is not configured", poolType);
            return;
        }

        // Create a configuration specific to this pool
        var poolConfig = new CasConfiguration
        {
            CasRootPath = rootPath,
            HashAlgorithm = _config.HashAlgorithm,
            GcGracePeriod = _config.GcGracePeriod,
            MaxCacheSizeBytes = _config.MaxCacheSizeBytes,
            AutoGcInterval = _config.AutoGcInterval,
            MaxConcurrentOperations = _config.MaxConcurrentOperations,
            VerifyIntegrity = _config.VerifyIntegrity,
            EnableAutomaticGc = _config.EnableAutomaticGc,
        };

        var poolConfigOptions = Options.Create(poolConfig);
        var storageLogger = _loggerFactory.CreateLogger<CasStorage>();

        var storage = new CasStorage(poolConfigOptions, storageLogger, _hashProvider);
        _storages[poolType] = storage;

        _logger.LogInformation("Initialized {PoolType} CAS pool at {RootPath}", poolType, rootPath);
    }
}
