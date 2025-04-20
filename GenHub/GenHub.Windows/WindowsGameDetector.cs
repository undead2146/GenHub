using System;
using System.Collections.Generic;
using System.IO;
using GenHub.Core;
using GenHub.Windows.Installations;
using Microsoft.Win32;

namespace GenHub.Windows;

public class WindowsGameDetector : IGameDetector
{
    public List<IGameInstallation> Installations { get; private set; } = new();

    public void Detect()
    {
        Installations.Clear();

        Installations.Add(new SteamInstallation(true));
    }
}