using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Linq;
using System.Reflection;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Features.AppUpdate.Services;

namespace GenHub.Features.AppUpdate.Factories
{
    /// <summary>
    /// Factory responsible for creating the appropriate update installer for the current platform
    /// </summary>
    public class UpdateInstallerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UpdateInstallerFactory> _logger;

        public UpdateInstallerFactory(IServiceProvider serviceProvider, ILogger<UpdateInstallerFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates an appropriate update installer for the current platform
        /// </summary>
        public IUpdateInstaller CreateInstaller()
        {
            try
            {
                // First attempt to resolve the platform-specific installer directly by concrete type
                var platformInstaller = ResolvePlatformSpecificInstaller();
                if (platformInstaller != null)
                {
                    _logger.LogInformation("Using platform-specific update installer: {InstallerType}", 
                        platformInstaller.GetType().FullName);
                    return platformInstaller;
                }

                // If no platform installer is available, fall back to default
                _logger.LogWarning("No platform-specific installer found, using DefaultUpdateInstaller (simulation only)");
                return _serviceProvider.GetRequiredService<DefaultUpdateInstaller>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating update installer, falling back to DefaultUpdateInstaller");
                return _serviceProvider.GetRequiredService<DefaultUpdateInstaller>();
            }
        }

        private IUpdateInstaller? ResolvePlatformSpecificInstaller()
        {
            // Try to resolve a platform-specific installer by concrete type - avoid resolving IUpdateInstaller
            if (OperatingSystem.IsWindows())
            {
                _logger.LogDebug("Windows detected, looking for Windows update installer");
                
                // Look for WindowsUpdateInstaller by concrete type - NOT through the interface
                try
                {
                    // Try to find all relevant platform assemblies
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetName().Name?.Contains("Windows") == true)
                        .ToList();

                    foreach (var assembly in assemblies)
                    {
                        _logger.LogDebug("Checking assembly: {AssemblyName}", assembly.GetName().Name);
                        
                        // Look for types implementing IUpdateInstaller
                        var installerTypes = assembly.GetTypes()
                            .Where(t => t.Name.Contains("UpdateInstaller") && 
                                   typeof(IUpdateInstaller).IsAssignableFrom(t) &&
                                   !t.IsAbstract && !t.IsInterface)
                            .ToList();

                        foreach (var installerType in installerTypes)
                        {
                            _logger.LogDebug("Found installer type: {TypeName} in {AssemblyName}", 
                                installerType.FullName, assembly.GetName().Name);
                            
                            try
                            {
                                // Try to resolve the concrete type directly
                                var installer = _serviceProvider.GetService(installerType) as IUpdateInstaller;
                                if (installer != null)
                                {
                                    _logger.LogInformation("Successfully resolved {InstallerType}", installerType.FullName);
                                    return installer;
                                }
                                else
                                {
                                    _logger.LogDebug("Could not resolve {InstallerType} from DI container", installerType.FullName);
                                }
                                
                                // If direct resolution fails, try creating it manually
                                _logger.LogDebug("Attempting to create {InstallerType} manually", installerType.FullName);
                                
                                var ctors = installerType.GetConstructors()
                                    .OrderByDescending(c => c.GetParameters().Length)
                                    .ToList();
                                
                                foreach (var ctor in ctors)
                                {
                                    try
                                    {
                                        var parameters = ctor.GetParameters();
                                        var paramValues = new object[parameters.Length];
                                        
                                        _logger.LogDebug("Trying constructor with {ParamCount} parameters", parameters.Length);
                                        
                                        // Resolve constructor parameters
                                        bool canCreateAllParams = true;
                                        for (int i = 0; i < parameters.Length; i++)
                                        {
                                            var param = parameters[i];
                                            // Avoid resolving IUpdateInstaller to prevent circular dependency!
                                            if (param.ParameterType == typeof(IUpdateInstaller))
                                            {
                                                _logger.LogWarning("Constructor depends on IUpdateInstaller - circular dependency detected!");
                                                canCreateAllParams = false;
                                                break;
                                            }
                                            
                                            // Safely try to resolve the parameter
                                            try
                                            {
                                                paramValues[i] = _serviceProvider.GetService(param.ParameterType);
                                                if (paramValues[i] == null && !param.IsOptional)
                                                {
                                                    _logger.LogDebug("Could not resolve required parameter {ParamName} of type {ParamType}", 
                                                        param.Name, param.ParameterType.Name);
                                                    canCreateAllParams = false;
                                                    break;
                                                }
                                            }
                                            catch
                                            {
                                                canCreateAllParams = false;
                                                break;
                                            }
                                        }
                                        
                                        if (canCreateAllParams)
                                        {
                                            // Create the instance with resolved parameters
                                            var instance = ctor.Invoke(paramValues) as IUpdateInstaller;
                                            if (instance != null)
                                            {
                                                _logger.LogInformation("Successfully created {InstallerType} via reflection", 
                                                    installerType.FullName);
                                                return instance;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Failed to create instance using constructor");
                                        // Continue to next constructor
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Error resolving installer type {TypeName}", installerType.FullName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to find Windows update installer by scanning assemblies");
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                _logger.LogDebug("Linux detected, looking for Linux update installer");
                
                // Same approach for Linux - scan for concrete types directly
                try
                {
                    // Try to find all relevant platform assemblies
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetName().Name?.Contains("Linux") == true)
                        .ToList();

                    foreach (var assembly in assemblies)
                    {
                        _logger.LogDebug("Checking assembly: {AssemblyName}", assembly.GetName().Name);
                        
                        // Look for types implementing IUpdateInstaller
                        var installerTypes = assembly.GetTypes()
                            .Where(t => t.Name.Contains("UpdateInstaller") && 
                                   typeof(IUpdateInstaller).IsAssignableFrom(t) &&
                                   !t.IsAbstract && !t.IsInterface)
                            .ToList();

                        foreach (var installerType in installerTypes)
                        {
                            // Same implementation as for Windows - try to resolve or create the installer
                            // Implementation omitted for brevity
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to find Linux update installer by scanning assemblies");
                }
            }
            
            return null;
        }
    }
}
