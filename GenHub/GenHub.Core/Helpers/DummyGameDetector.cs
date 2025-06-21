namespace GenHub.Core;

public class DummyGameDetector : IGameDetector
{
    public List<IGameInstallation> Installations => null;

    public void Detect()
    {
        throw new System.NotImplementedException();
    }
}