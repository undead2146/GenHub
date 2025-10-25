// PluginLoader.cs - Would be added to GenHub.Core or Infrastructure
using GenHub.Core.Interfaces.Content;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;

namespace GenHub.Core.Services;

/// <summary>
/// Service responsible for loading and managing plugins from the Plugins directory.
/// </summary>
public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly string _pluginsDirectory;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
        // In real implementation, this would come from configuration
        _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }

    /// <summary>
    /// Loads all plugins from the Plugins directory and returns their IContentProvider implementations.
    /// </summary>
    public IEnumerable<IContentProvider> LoadPlugins()
    {
        var providers = new List<IContentProvider>();

        if (!Directory.Exists(_pluginsDirectory))
        {
            _logger.LogInformation("Plugins directory does not exist: {Path}", _pluginsDirectory);
            return providers;
        }

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            try
            {
                var pluginName = Path.GetFileName(pluginDir);
                _logger.LogInformation("Loading plugin: {PluginName}", pluginName);

                // Find the plugin DLL
                var dllPath = Directory.GetFiles(pluginDir, "*.dll").FirstOrDefault();
                if (dllPath == null)
                {
                    _logger.LogWarning("No DLL found in plugin directory: {Path}", pluginDir);
                    continue;
                }

                // Load the assembly
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                // Find all IContentProvider implementations
                var providerTypes = assembly.GetTypes()
                    .Where(t => typeof(IContentProvider).IsAssignableFrom(t) && !t.IsAbstract);

                foreach (var providerType in providerTypes)
                {
                    try
                    {
                        // Note: In real implementation, this would need proper DI container integration
                        // For now, this is a simplified example
                        _logger.LogInformation("Found provider type: {TypeName}", providerType.FullName);
                        // Provider instantiation would happen during DI registration
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load provider {TypeName} from plugin {PluginName}",
                            providerType.FullName, pluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from directory: {Path}", pluginDir);
            }
        }

        return providers;
    }
}
