using System;
using GenHub.Core;
using Microsoft.Win32;

namespace GenHub.Windows;

public class WindowsGameDetector : IGameDetector
{
    public string GamePath => "Mock Windows Path";

    #region Steam
    /// <summary>
    /// Checks if steam is installed by looking up the installation path of steam in the windows registry
    /// </summary>
    /// <returns><c>true</c> if the steam registry key was found</returns>
    private bool IsSteamInstalled()
    {
        using var key = GetSteamRegistryKey();
        return key != null;
    }

    /// <summary>
    /// Tries to fetch the installation path of steam from the windows registry.
    /// </summary>
    /// <param name="path">Returns the installation path if successful; otherwise, an empty string</param>
    /// <returns><c>true</c> if the installation path of steam was found</returns>
    private bool TryGetSteamPath(out string? path)
    {
        path = string.Empty;

        using var key = GetSteamRegistryKey();
        if (key == null)
            return false;

        // Find the steam path in current user registry
        path = key.GetValue("SteamPath") as string;

        if (string.IsNullOrEmpty(path))
        {
            // Find the steam path in local machine registry
            // This may or may not cause problems, e.g. when another user has steam installed but the current user
            // doesn't have access to it? - NH
            path = key.GetValue("InstallPath") as string;
            return !string.IsNullOrEmpty(path);
        }

        return true;
    }

    /// <summary>
    /// Returns a disposable RegistryKey. Caller is responsible for disposing it.
    /// </summary>
    /// <returns>Disposable RegistryKey</returns>
    private RegistryKey? GetSteamRegistryKey()
    {
        // Check current user
        var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (key != null)
            return key;

        // Check local machine if the current user doesn't have steam installed.
        // This may or may not cause problems, e.g. when another user has steam installed but the current user
        // doesn't have access to it? - NH
        key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam");
        return key;
    }
    #endregion

    #region EA App

    #endregion
}