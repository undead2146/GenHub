namespace GenHub.Core;

public class DummyGameDetector : IGameDetector
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