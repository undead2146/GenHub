namespace GenHub.Core;

public interface IGameDetector
{
    public List<IGameInstallation> Installations { get; }

    public void Detect();
}