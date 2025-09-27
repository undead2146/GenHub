using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameClients;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameClients;

/// <summary>
/// Detects game clients from installations and directories.
/// </summary>
public class GameClientDetector(
    IManifestGenerationService manifestGenerationService,
    IContentManifestPool contentManifestPool,
    ILogger<GameClientDetector> logger) : IGameClientDetector
{
    private readonly IManifestGenerationService _manifestGenerationService = manifestGenerationService;
    private readonly IContentManifestPool _contentManifestPool = contentManifestPool;
    private readonly ILogger<GameClientDetector> _logger = logger;

    /// <inheritdoc/>
    public async Task<DetectionResult<GameClient>> DetectGameClientsFromInstallationsAsync(
        IEnumerable<GameInstallation> installations,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var gameClients = new List<GameClient>();

        foreach (var inst in installations)
        {
            if (inst.HasGenerals && !string.IsNullOrEmpty(inst.GeneralsPath) && Directory.Exists(inst.GeneralsPath))
            {
                var exePath = Path.Combine(inst.GeneralsPath, "generals.exe");
                if (File.Exists(exePath))
                {
                    var generalsVersion = new GameClient
                    {
                        Name = $"{inst.InstallationType} Generals",
                        Id = string.Empty, // Set later by manifest
                        Version = "1.04",
                        ExecutablePath = exePath,
                        GameType = GameType.Generals,
                        InstallationId = inst.Id,
                        WorkingDirectory = inst.GeneralsPath,
                    };
                    await GenerateClientManifestAndSetIdAsync(generalsVersion, inst.GeneralsPath, inst.InstallationType, GameType.Generals);
                    gameClients.Add(generalsVersion);
                }
                else
                {
                    _logger.LogWarning("Skipping Generals game client for {InstallationId}: generals.exe not found at {ExePath}", inst.Id, exePath);
                }
            }

            if (inst.HasZeroHour && !string.IsNullOrEmpty(inst.ZeroHourPath) && Directory.Exists(inst.ZeroHourPath))
            {
                var exePath = Path.Combine(inst.ZeroHourPath, "game.exe");
                if (File.Exists(exePath))
                {
                    var zeroHourVersion = new GameClient
                    {
                        Name = $"{inst.InstallationType} Zero Hour",
                        Id = string.Empty,
                        Version = "1.04",
                        ExecutablePath = exePath,
                        GameType = GameType.ZeroHour,
                        InstallationId = inst.Id,
                        WorkingDirectory = inst.ZeroHourPath,
                    };
                    await GenerateClientManifestAndSetIdAsync(zeroHourVersion, inst.ZeroHourPath, inst.InstallationType, GameType.ZeroHour);
                    gameClients.Add(zeroHourVersion);
                }
                else
                {
                    _logger.LogWarning("Skipping Zero Hour game client for {InstallationId}: game.exe not found at {ExePath}", inst.Id, exePath);
                }
            }
        }

        sw.Stop();
        _logger.LogInformation("Detected {Count} game clients from {InstallCount} installations in {ElapsedMs}ms", gameClients.Count, installations.Count(), sw.ElapsedMilliseconds);

        return DetectionResult<GameClient>.CreateSuccess(gameClients, sw.Elapsed);
    }

    /// <inheritdoc/>
    public async Task<DetectionResult<GameClient>> ScanDirectoryForGameClientsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!Directory.Exists(path))
        {
            sw.Stop();
            await Task.Delay(1, cancellationToken);
            return DetectionResult<GameClient>.CreateFailure("Directory does not exist");
        }

        var gameClients = new List<GameClient>();
        var exeFiles = Directory.EnumerateFiles(path, "*.exe", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Equals("generals.exe", StringComparison.OrdinalIgnoreCase));

        foreach (var exe in exeFiles)
        {
            var dir = Path.GetDirectoryName(exe);
            if (dir != null)
            {
                gameClients.Add(new GameClient
                {
                    Name = $"Scanned {Path.GetFileName(dir)}",
                    Id = string.Empty,
                    Version = "1.0",
                    ExecutablePath = exe,
                    GameType = GameType.Generals, // Default
                    InstallationId = string.Empty,
                });
            }
        }

        sw.Stop();
        await Task.Delay(1, cancellationToken);
        return DetectionResult<GameClient>.CreateSuccess(gameClients, sw.Elapsed);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateGameClientAsync(
        GameClient gameClient,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Ensure async

        return !string.IsNullOrEmpty(gameClient.ExecutablePath) && File.Exists(gameClient.ExecutablePath);
    }

    private async Task GenerateClientManifestAndSetIdAsync(GameClient gameClient, string clientPath, GameInstallationType installType, GameType gameType)
    {
        try
        {
            // Generate GameClient manifest
            var builder = await _manifestGenerationService.CreateGameClientManifestAsync(
                clientPath, gameType, gameClient.Name, gameClient.Version);

            var manifest = builder.Build();
            manifest.ContentType = ContentType.GameClient;

            // Generate ID using proper format to match validator
            var installTypeStr = installType.ToString().ToLowerInvariant();
            var gameTypeStr = gameType.ToString().ToLowerInvariant();
            var idString = $"1.0.{installTypeStr}.{gameTypeStr}-client";
            manifest.Id = ManifestId.Create(idString);

            // Add to pool
            var addResult = await _contentManifestPool.AddManifestAsync(manifest, clientPath);
            if (addResult.Success)
            {
                gameClient.Id = manifest.Id.ToString();
                _logger.LogDebug("Generated GameClient manifest ID {Id} for {VersionName}", gameClient.Id, gameClient.Name);
            }
            else
            {
                _logger.LogWarning("Failed to pool GameClient manifest for {VersionName}: {Errors}", gameClient.Name, string.Join(", ", addResult.Errors));
                gameClient.Id = Guid.NewGuid().ToString(); // Fallback
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate manifest for {VersionName}", gameClient.Name);
            gameClient.Id = Guid.NewGuid().ToString(); // Fallback
        }
    }
}
