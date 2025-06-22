using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GenHub.Tests.Core.Common;

public class AppLocatorTests
{
    [Fact]
    public void Services_ShouldInitializeAsNull()
    {
        // Arrange
        AppLocator.Services = null;

        // Act
        var services = AppLocator.Services;

        // Assert
        services.Should().BeNull();
    }

    [Fact]
    public void Services_ShouldBeSettable()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        AppLocator.Services = serviceProvider;

        // Assert
        AppLocator.Services.Should().BeSameAs(serviceProvider);
    }

    [Fact]
    public void Services_ShouldRetainSetValue()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<string>("test-service");
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        AppLocator.Services = serviceProvider;
        var retrievedServices = AppLocator.Services;

        // Assert
        retrievedServices.Should().BeSameAs(serviceProvider);
        retrievedServices.GetService<string>().Should().Be("test-service");
    }

    [Fact]
    public void Services_ShouldBeOverwritable()
    {
        // Arrange
        var firstServiceCollection = new ServiceCollection();
        var firstServiceProvider = firstServiceCollection.BuildServiceProvider();

        var secondServiceCollection = new ServiceCollection();
        var secondServiceProvider = secondServiceCollection.BuildServiceProvider();

        // Act
        AppLocator.Services = firstServiceProvider;
        AppLocator.Services = secondServiceProvider;

        // Assert
        AppLocator.Services.Should().BeSameAs(secondServiceProvider);
        AppLocator.Services.Should().NotBeSameAs(firstServiceProvider);
    }

    // Clean up after tests to avoid affecting other tests
    public void Dispose()
    {
        AppLocator.Services = null;
    }
}
