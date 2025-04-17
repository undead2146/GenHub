using GenHub.Core;

namespace GenHub.Linux;

public class LinuxGameDetector : IGameDetector
{
    public bool IsVanillaInstalled => false;
    public string VanillaGamePath => "";
    public bool IsZeroHourInstalled => false;
    public string ZeroHourGamePath => "";
    public void Detect()
    {
        throw new System.NotImplementedException();
    }
}