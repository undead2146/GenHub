using FluentAssertions;
using GenHub.ViewModels;
using Xunit;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace GenHub.Tests.Core.Common;

public class ViewLocatorTests
{
    [Fact]
    public void ViewLocator_ShouldImplementIDataTemplate()
    {
        // Arrange & Act
        var viewLocator = new ViewLocator();

        // Assert
        viewLocator.Should().BeAssignableTo<IDataTemplate>();
    }

    [Fact]
    public void Build_ShouldReturnNull_WhenDataIsNull()
    {
        // Arrange
        var viewLocator = new ViewLocator();

        // Act
        var result = viewLocator.Build(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Build_ShouldReturnTextBlock_WhenViewTypeNotFound()
    {
        // Arrange
        var viewLocator = new ViewLocator();
        var mockViewModel = new MockViewModel();

        // Act
        var result = viewLocator.Build(mockViewModel);

        // Assert
        result.Should().BeOfType<TextBlock>();
        var textBlock = result as TextBlock;
        textBlock!.Text.Should().Contain("Couldn't find view: GenHub.Tests.Core.Common.MockView");
    }

    [Fact]
    public void Match_ShouldReturnTrue_WhenDataIsViewModelBase()
    {
        // Arrange
        var viewLocator = new ViewLocator();
        var viewModel = new ViewModelBase();

        // Act
        var result = viewLocator.Match(viewModel);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Match_ShouldReturnFalse_WhenDataIsNotViewModelBase()
    {
        // Arrange
        var viewLocator = new ViewLocator();
        var data = new object();

        // Act
        var result = viewLocator.Match(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Match_ShouldReturnFalse_WhenDataIsNull()
    {
        // Arrange
        var viewLocator = new ViewLocator();

        // Act
        var result = viewLocator.Match(null);

        // Assert
        result.Should().BeFalse();
    }
}

/// <summary>
/// Mock ViewModel for testing purposes that doesn't have a corresponding view
/// </summary>
public class MockViewModel : ViewModelBase
{
}
