using Avalonia.Headless.XUnit;
using Xunit;

namespace GenHub.Tests.Core.App;

/// <summary>
/// Defines a test collection that serializes headless Avalonia tests sharing the headless application instance.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class HeadlessTestCollectionDefinition
{
    /// <summary>
    /// The name of the headless test collection.
    /// </summary>
    public const string Name = "HeadlessTestCollection";
}
