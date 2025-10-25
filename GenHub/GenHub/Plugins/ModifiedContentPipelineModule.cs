// Modified ContentPipelineModule.cs showing plugin integration
public static class ContentPipelineModule
{
    public static IServiceCollection AddContentPipelineServices(this IServiceCollection services)
    {
        // ... existing code ...

        // Register built-in providers
        services.AddTransient<IContentProvider, GitHubContentProvider>();
        services.AddTransient<IContentProvider, CNCLabsContentProvider>();
        services.AddTransient<IContentProvider, ModDBContentProvider>();
        services.AddTransient<IContentProvider, LocalFileSystemContentProvider>();

        // Load and register plugin providers
        var pluginLoader = new PluginLoader(null); // Would use proper logging in real implementation
        var pluginProviders = pluginLoader.LoadPlugins();

        foreach (var pluginProvider in pluginProviders)
        {
            // Register plugin providers with transient lifetime
            services.AddTransient(_ => pluginProvider);
        }

        // ... rest of existing code ...

        return services;
    }
}
