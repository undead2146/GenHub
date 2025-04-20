using System.Collections.Generic;
using GenHub.Core;

namespace GenHub.Linux;

public class LinuxGameDetector : IGameDetector
{
    public List<IGameInstallation> Installations { get; private set; } = new();

    public void Detect()
    {
        throw new System.NotImplementedException();
    }
}