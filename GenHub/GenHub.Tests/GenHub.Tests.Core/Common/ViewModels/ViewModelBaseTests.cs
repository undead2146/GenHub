using FluentAssertions;
using GenHub.ViewModels;
using Xunit;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace GenHub.Tests.Core.Common.ViewModels;

public class ViewModelBaseTests
{
    [Fact]
    public void ViewModelBase_ShouldInheritFromObservableObject()
    {
        // Arrange & Act
        var viewModel = new ViewModelBase();

        // Assert
        viewModel.Should().BeAssignableTo<ObservableObject>();
        viewModel.Should().BeAssignableTo<INotifyPropertyChanged>();
    }

    [Fact]
    public void ViewModelBase_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange & Act
        var viewModel = new ViewModelBase();

        // Assert
        viewModel.Should().BeAssignableTo<INotifyPropertyChanged>();
    }

    [Fact]
    public void ViewModelBase_ShouldBeInstantiable()
    {
        // Arrange & Act
        var viewModel = new ViewModelBase();

        // Assert
        viewModel.Should().NotBeNull();
    }
}
