using System;

namespace GenHub;

/// <summary>
/// The AppLocator is needed to pass the service provider to the avalonia app without breaking the avalonia designer
/// There might be a more elegant solution, but this works for now - NH
/// </summary>
public static class AppLocator
{
    public static IServiceProvider? Services { get; set; }
}