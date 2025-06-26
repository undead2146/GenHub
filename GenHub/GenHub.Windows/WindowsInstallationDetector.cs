using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;

namespace GenHub.Windows
{
    /// <summary>
    /// Example implementation of IGameInstallationDetector for Windows.
    /// </summary>
    public class WindowsInstallationDetector : IGameInstallationDetector
    {
        /// <summary>
        /// Gets the human-readable name for logs/UI.
        /// </summary>
        public string DetectorName => "Windows Retail Detector";

        /// <summary>
        /// Gets a value indicating whether this detector can run on the current OS/platform.
        /// </summary>
        public bool CanDetectOnCurrentPlatform => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Scan for base platform installations and return them.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="Task{DetectionResult{GameInstallation}}"/> representing the asynchronous operation.</returns>
        public Task<DetectionResult<GameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var installs = new List<GameInstallation>();
            var errors = new List<string>();

            try
            {
                // 1) Steam
                var steam = new GenHub.Windows.Installations.SteamInstallation(fetch: true);
                if (steam.IsSteamInstalled && (steam.IsVanillaInstalled || steam.IsZeroHourInstalled))
                    installs.Add(steam.ToGameInstallation());

                // 2) EA App
                var ea = new GenHub.Windows.Installations.EaAppInstallation(fetch: true);
                if (ea.IsEaAppInstalled && (ea.IsVanillaInstalled || ea.IsZeroHourInstalled))
                    installs.Add(ea.ToGameInstallation());

                // 3) Origin, TFD… (similar)
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            sw.Stop();
            return Task.FromResult(errors.Any()
                ? DetectionResult<GameInstallation>.Failed(string.Join("; ", errors))
                : DetectionResult<GameInstallation>.Succeeded(installs, sw.Elapsed));
        }
    }
}
