using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Service for managing game versions 
    /// </summary>
    public class GameVersionServiceFacade : IGameVersionServiceFacade
    {
        private readonly ILogger<GameVersionServiceFacade> _logger;
        private readonly IGameVersionManager _versionManager;
        private readonly IGameVersionDiscoveryService _discoveryService;
        private readonly IGameLauncherService _gameLauncherService;


        public GameVersionServiceFacade(
            ILogger<GameVersionServiceFacade> logger,
            IGameVersionManager versionManager,
            IGameVersionDiscoveryService discoveryService,
            IGameLauncherService gameLauncherService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versionManager = versionManager ?? throw new ArgumentNullException(nameof(versionManager));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
             _gameLauncherService = gameLauncherService ?? throw new ArgumentNullException(nameof(gameLauncherService));
            _logger.LogInformation("GameVersionServiceFacade initialized (facade)");
        }

        /// <summary>
        /// Gets the path where versions are stored - delegates to GameVersionManager
        /// </summary>
        public string GetVersionsStoragePath()
        {
            return _versionManager.GetVersionsStoragePath();
        }

        /// <summary>
        /// Gets all installed versions - delegates to GameVersionManager
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetInstalledVersionsAsync(CancellationToken cancellationToken = default)
        {
            return await _versionManager.GetInstalledVersionsAsync(cancellationToken);
        }

        /// <summary>
        /// Gets a version by ID - delegates to GameVersionManager
        /// </summary>
        public async Task<GameVersion?> GetVersionByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return await _versionManager.GetVersionByIdAsync(id, cancellationToken);
        }

        /// <summary>
        /// Discovers game versions - delegates to GameVersionDiscoveryService
        /// </summary>
        public async Task<IEnumerable<GameVersion>> DiscoverVersionsAsync(CancellationToken cancellationToken = default)
        {
            return await _discoveryService.DiscoverVersionsAsync(cancellationToken);
        }

        /// <summary>
        /// Saves a version - delegates to GameVersionManager
        /// </summary>
        public async Task<bool> SaveVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            return await _versionManager.SaveVersionAsync(version, cancellationToken);
        }

        /// <summary>
        /// Updates a version - delegates to GameVersionManager
        /// </summary>
        public async Task<bool> UpdateVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            return await _versionManager.UpdateVersionAsync(version, cancellationToken);
        }

        /// <summary>
        /// Deletes a version - delegates to GameVersionManager
        /// </summary>
        public async Task<bool> DeleteVersionAsync(string id, CancellationToken cancellationToken = default)
        {
            return await _versionManager.DeleteVersionAsync(id, cancellationToken);
        }

        /// <summary>
        /// Validates a version - delegates to GameVersionDiscoveryService
        /// </summary>
        public async Task<bool> ValidateVersionAsync(GameVersion version, CancellationToken cancellationToken = default)
        {
            return await _discoveryService.ValidateVersionAsync(version, cancellationToken);
        }

        /// <summary>
        /// Launches a version - delegates to GameVersionManager
        /// </summary>
        public async Task<OperationResult> LaunchVersionAsync(IGameProfile profile, CancellationToken cancellationToken = default)
        {
            return await _gameLauncherService.LaunchVersionAsync(profile);
        }

        /// <summary>
        /// Gets detected versions without saving them - delegates to GameVersionDiscoveryService
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default)
        {
            return await _discoveryService.GetDetectedVersionsAsync(cancellationToken);
        }

        /// <summary>
        /// Gets default game versions from standard installation locations - delegates to GameVersionDiscoveryService
        /// </summary>
        public async Task<IEnumerable<GameVersion>> GetDefaultGameVersionsAsync(CancellationToken cancellationToken = default)
        {
            return await _discoveryService.GetDefaultGameVersionsAsync(cancellationToken);
        }

        /// <summary>
        /// Scans a directory for game versions - delegates to GameVersionDiscoveryService
        /// </summary>
        public async Task<IEnumerable<GameVersion>> ScanDirectoryForVersionsAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            return await _discoveryService.ScanDirectoryForVersionsAsync(directoryPath, cancellationToken);
        }
    }
}
