// <auto-generated/>
namespace GenHub.Tests.Core.Features.AppUpdate.Services;

/// <summary>
/// Minimal stub interface for IRepositoriesClient for test purposes.
/// </summary>
public interface IRepositoriesClient
{
    /// <summary>
    /// Gets the releases client.
    /// </summary>
    IReleasesClient Release { get; }
}

/// <summary>
/// Test stub for IRepositoriesClient, only implements Release property for tests.
/// </summary>
public class RepositoriesClientStub : IRepositoriesClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoriesClientStub"/> class.
    /// </summary>
    /// <param name="releasesClient">The releases client to use.</param>
    public RepositoriesClientStub(IReleasesClient releasesClient)
    {
        Release = releasesClient;
    }

    /// <inheritdoc/>
    public IReleasesClient Release { get; }
}
