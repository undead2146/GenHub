using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;

namespace GenHub.Features.GameVersions
{
    /// <summary>
    /// Orchestrates installation detection and version detection.
    /// </summary>
    public class GameVersionDetectionService : IGameVersionDetectionService
    {
        private readonly IGameInstallationDetectionService _instService;
        private readonly IGameVersionDetector _verDetector;

        public GameVersionDetectionService(
            IGameInstallationDetectionService instService,
            IGameVersionDetector verDetector)
        {
            _instService = instService;
            _verDetector = verDetector;
        }

        public async Task<DetectionResult<GameVersion>> DetectAllVersionsAsync(
            CancellationToken cancellationToken = default)
        {
            // 1) detect installations
            var instRes = await _instService.DetectAllInstallationsAsync(cancellationToken);
            if (!instRes.Success)
                return DetectionResult<GameVersion>.Failed(
                    "Install detection errors: " + string.Join("; ", instRes.Errors));

            // 2) detect versions from those installs
            return await _verDetector.DetectVersionsFromInstallationsAsync(
                instRes.Items, cancellationToken);
        }

        public async Task<List<GameVersion>> GetDetectedVersionsAsync(
            CancellationToken cancellationToken = default)
        {
            var vres = await DetectAllVersionsAsync(cancellationToken);
            return vres.Success ? vres.Items : new List<GameVersion>();
        }
    }
}
