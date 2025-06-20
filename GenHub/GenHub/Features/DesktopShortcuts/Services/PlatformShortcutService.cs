using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.DesktopShortcuts.Services
{
    /// <summary>
    /// Cross-platform shortcut service that delegates to platform-specific implementations
    /// </summary>
    public class PlatformShortcutService : IShortcutPlatformService
    {
        private readonly ILogger<PlatformShortcutService> _logger;
        private readonly IShortcutPlatformService _platformService;        public PlatformShortcutService(ILogger<PlatformShortcutService> logger, IShortcutCommandBuilder commandBuilder)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create platform-specific service using factory method
            _platformService = CreatePlatformService(_logger, commandBuilder);
        }private static IShortcutPlatformService CreatePlatformService(ILogger logger, IShortcutCommandBuilder commandBuilder)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    logger.LogDebug("Creating Windows shortcut service");
                    return new WindowsShortcutServiceInternal(logger, commandBuilder);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogDebug("Attempting to load Linux shortcut service");
                    
                    // Try to find the Linux assembly in the current app domain first
                    var linuxAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "GenHub.Linux");
                    
                    if (linuxAssembly == null)
                    {
                        // Try to load from file
                        var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GenHub.Linux.dll");
                        if (File.Exists(assemblyPath))
                        {
                            linuxAssembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
                            logger.LogDebug("Loaded Linux assembly from: {Path}", assemblyPath);
                        }
                        else
                        {
                            logger.LogWarning("GenHub.Linux.dll not found at: {Path}", assemblyPath);
                        }
                    }
                    
                    if (linuxAssembly != null)
                    {
                        var linuxServiceType = linuxAssembly.GetType("GenHub.Linux.DesktopShortcuts.LinuxShortcutService");
                        if (linuxServiceType != null)
                        {
                            logger.LogDebug("Found Linux shortcut service type, creating instance");
                            return (IShortcutPlatformService)Activator.CreateInstance(linuxServiceType, logger, commandBuilder)!;
                        }
                        else
                        {
                            logger.LogWarning("LinuxShortcutService type not found in Linux assembly");
                        }
                    }
                }
                else
                {
                    logger.LogWarning("Unsupported platform: {Platform}", RuntimeInformation.OSDescription);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating platform-specific shortcut service");
            }

            // Fallback to stub implementation
            logger.LogWarning("Falling back to stub shortcut service - platform-specific implementation not available");
            return new StubShortcutService(logger);
        }

        public Task<OperationResult> CreateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return _platformService.CreateShortcutAsync(configuration, cancellationToken);
        }

        public Task<OperationResult> RemoveShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return _platformService.RemoveShortcutAsync(configuration, cancellationToken);
        }

        public Task<OperationResult<ShortcutValidationResult>> ValidateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return _platformService.ValidateShortcutAsync(configuration, cancellationToken);
        }

        public Task<OperationResult> RepairShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return _platformService.RepairShortcutAsync(configuration, cancellationToken);
        }

        public string GetShortcutPath(ShortcutConfiguration configuration)
        {
            return _platformService.GetShortcutPath(configuration);
        }

        public bool SupportsShortcutType(ShortcutType shortcutType)
        {
            return _platformService.SupportsShortcutType(shortcutType);
        }

        public string[] GetSupportedExtensions()
        {
            return _platformService.GetSupportedExtensions();
        }
    }

    /// <summary>
    /// Stub implementation for unsupported platforms
    /// </summary>
    internal class StubShortcutService : IShortcutPlatformService
    {
        private readonly ILogger _logger;

        public StubShortcutService(ILogger logger)
        {
            _logger = logger;
        }

        public Task<OperationResult> CreateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Shortcut creation not supported on this platform");
            return Task.FromResult(OperationResult.Failed("Shortcut creation not supported on this platform"));
        }

        public Task<OperationResult> RemoveShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Shortcut removal not supported on this platform");
            return Task.FromResult(OperationResult.Failed("Shortcut removal not supported on this platform"));
        }

        public Task<OperationResult<ShortcutValidationResult>> ValidateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var result = ShortcutValidationResult.Failure(configuration, "Platform not supported");
            return Task.FromResult(OperationResult<ShortcutValidationResult>.Succeeded(result));
        }

        public Task<OperationResult> RepairShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Shortcut repair not supported on this platform");
            return Task.FromResult(OperationResult.Failed("Shortcut repair not supported on this platform"));
        }

        public string GetShortcutPath(ShortcutConfiguration configuration)
        {
            return string.Empty;
        }

        public bool SupportsShortcutType(ShortcutType shortcutType)
        {
            return false;
        }

        public string[] GetSupportedExtensions()
        {
            return Array.Empty<string>();
        }
    }
}
