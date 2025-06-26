using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Features.GameVersions
{
    /// <summary>
    /// Example implementation of IGameVersionDetector.
    /// </summary>
    public class GameExecutableLocator : IGameVersionDetector
    {
        public async Task<DetectionResult<GameVersion>> DetectVersionsFromInstallationsAsync(
            IEnumerable<GameInstallation> installations,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var versions = new List<GameVersion>();
            var errors = new List<string>();

            foreach (var inst in installations)
            {
                // For each installation, look for Generals.exe and GeneralsZH.exe
                var dir = inst.InstallationPath;
                var candidates = new[] { "generals.exe", "generalsv.exe", "generalszh.exe", "RTS.exe" };
                foreach (var exe in candidates)
                {
                    var path = Path.Combine(dir, exe);
                    if (File.Exists(path))
                    {
                        versions.Add(new GameVersion
                        {
                            Name = $"{inst.Id} - {(exe.Contains("zh") ? "Zero Hour" : "Generals")}",
                            ExecutablePath = path,
                            WorkingDirectory = dir,
                            GameType = exe.Contains("zh") ? "ZeroHour" : "Generals",
                            IsZeroHour = exe.Contains("zh"),
                            BaseInstallationId = inst.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            sw.Stop();
            return errors.Any()
                ? DetectionResult<GameVersion>.Failed(string.Join("; ", errors))
                : DetectionResult<GameVersion>.Succeeded(versions, sw.Elapsed);
        }

        public async Task<DetectionResult<GameVersion>> ScanDirectoryForVersionsAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            // Stub: implement directory scanning logic
            return DetectionResult<GameVersion>.Succeeded(new List<GameVersion>(), TimeSpan.Zero);
        }

        public async Task<bool> ValidateVersionAsync(
            GameVersion version,
            CancellationToken cancellationToken = default)
        {
            // Stub: implement validation logic
            return File.Exists(version.ExecutablePath);
        }
    }
}
