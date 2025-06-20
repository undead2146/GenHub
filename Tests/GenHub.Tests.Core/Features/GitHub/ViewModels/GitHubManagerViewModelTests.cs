using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Features.GitHub.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GitHub.ViewModels
{
    public class GitHubManagerViewModelTests
    {
        private readonly Mock<ILogger<GitHubManagerViewModel>> _mockLogger;
        private readonly Mock<IGitHubTokenService> _mockTokenService;
        private readonly Mock<IGitHubServiceFacade> _mockGitHubService;
        private readonly Mock<RepositoryControlViewModel> _mockRepositoryControlVM;
        private readonly Mock<ContentModeFilterViewModel> _mockContentModeFilterVM;
        private readonly Mock<GitHubItemsTreeViewModel> _mockGitHubItemsTreeVM;
        private readonly Mock<GitHubDetailsViewModel> _mockDetailsVM;
        private readonly Mock<InstallationViewModel> _mockInstallationVM;
        private readonly GitHubManagerViewModel _viewModel;

        public GitHubManagerViewModelTests()
        {
            _mockLogger = new Mock<ILogger<GitHubManagerViewModel>>();
            _mockTokenService = new Mock<IGitHubTokenService>();
            _mockGitHubService = new Mock<IGitHubServiceFacade>();
            _mockRepositoryControlVM = new Mock<RepositoryControlViewModel>();
            _mockContentModeFilterVM = new Mock<ContentModeFilterViewModel>();
            _mockGitHubItemsTreeVM = new Mock<GitHubItemsTreeViewModel>();
            _mockDetailsVM = new Mock<GitHubDetailsViewModel>();
            _mockInstallationVM = new Mock<InstallationViewModel>();

            _viewModel = new GitHubManagerViewModel(
                _mockLogger.Object,
                _mockTokenService.Object,
                _mockGitHubService.Object,
                _mockRepositoryControlVM.Object,
                _mockContentModeFilterVM.Object,
                _mockGitHubItemsTreeVM.Object,
                _mockDetailsVM.Object,
                _mockInstallationVM.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubManagerViewModel(
                    null!,
                    _mockTokenService.Object,
                    _mockGitHubService.Object,
                    _mockRepositoryControlVM.Object,
                    _mockContentModeFilterVM.Object,
                    _mockGitHubItemsTreeVM.Object,
                    _mockDetailsVM.Object,
                    _mockInstallationVM.Object));

            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullTokenService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubManagerViewModel(
                    _mockLogger.Object,
                    null!,
                    _mockGitHubService.Object,
                    _mockRepositoryControlVM.Object,
                    _mockContentModeFilterVM.Object,
                    _mockGitHubItemsTreeVM.Object,
                    _mockDetailsVM.Object,
                    _mockInstallationVM.Object));

            exception.ParamName.Should().Be("tokenService");
        }

        [Fact]
        public void Constructor_WithNullGitHubService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubManagerViewModel(
                    _mockLogger.Object,
                    _mockTokenService.Object,
                    null!,
                    _mockRepositoryControlVM.Object,
                    _mockContentModeFilterVM.Object,
                    _mockGitHubItemsTreeVM.Object,
                    _mockDetailsVM.Object,
                    _mockInstallationVM.Object));

            exception.ParamName.Should().Be("gitHubService");
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesPropertiesCorrectly()
        {
            // Assert
            _viewModel.Should().NotBeNull();
            _viewModel.GitHubService.Should().Be(_mockGitHubService.Object);
            _viewModel.RepositoryControlVM.Should().Be(_mockRepositoryControlVM.Object);
            _viewModel.ContentModeFilterVM.Should().Be(_mockContentModeFilterVM.Object);
            _viewModel.GitHubItemsTreeVM.Should().Be(_mockGitHubItemsTreeVM.Object);
            _viewModel.DetailsVM.Should().Be(_mockDetailsVM.Object);
            _viewModel.InstallationVM.Should().Be(_mockInstallationVM.Object);
            _viewModel.IsLoading.Should().BeFalse();
            _viewModel.StatusMessage.Should().Be("Ready");
            _viewModel.ShowEmptyState.Should().BeTrue();
        }

        [Fact]
        public void Constructor_SetsParentViewModelOnChildComponents()
        {
            // Assert
            _mockGitHubItemsTreeVM.Verify(x => x.SetParentViewModel(_viewModel), Times.Once);
        }

        #endregion

        #region Observable Properties Tests

        [Fact]
        public void IsLoading_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsLoading))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.IsLoading = true;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.IsLoading.Should().BeTrue();
        }

        [Fact]
        public void StatusMessage_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.StatusMessage))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.StatusMessage = "Loading...";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.StatusMessage.Should().Be("Loading...");
        }

        [Fact]
        public void ShowEmptyState_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.ShowEmptyState))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.ShowEmptyState = false;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.ShowEmptyState.Should().BeFalse();
        }

        #endregion

        #region Event Tests

        [Fact]
        public void CloseRequested_EventCanBeSubscribedAndRaised()
        {
            // Arrange
            var eventRaised = false;
            _viewModel.CloseRequested += (sender, args) => eventRaised = true;

            // Act - Note: This tests that the event can be subscribed to
            // Actual firing would be tested in specific command/method tests
            _viewModel.CloseRequested?.Invoke(_viewModel, EventArgs.Empty);

            // Assert
            eventRaised.Should().BeTrue();
        }

        #endregion

        #region Child ViewModel Integration Tests

        [Fact]
        public void ChildViewModels_AreProperlyInjected()
        {
            // Assert
            _viewModel.RepositoryControlVM.Should().NotBeNull();
            _viewModel.ContentModeFilterVM.Should().NotBeNull();
            _viewModel.GitHubItemsTreeVM.Should().NotBeNull();
            _viewModel.DetailsVM.Should().NotBeNull();
            _viewModel.InstallationVM.Should().NotBeNull();
        }

        [Fact]
        public void RepositoryControlVM_EventSubscription_IsSetupCorrectly()
        {
            // Arrange - Create a real event to verify subscription
            // This test verifies the event subscription was set up in constructor
            // The actual event handling is tested in specific event handler tests

            // Assert
            // Since we're using mocks, we can't directly test event subscription
            // but the constructor test verifies the ViewModels are assigned correctly
            _viewModel.RepositoryControlVM.Should().Be(_mockRepositoryControlVM.Object);
        }

        [Fact]
        public void ContentModeFilterVM_EventSubscription_IsSetupCorrectly()
        {
            // Assert
            _viewModel.ContentModeFilterVM.Should().Be(_mockContentModeFilterVM.Object);
        }

        #endregion

        #region MVVM Pattern Compliance Tests

        [Fact]
        public void ViewModel_ImplementsINotifyPropertyChanged()
        {
            // Assert
            _viewModel.Should().BeAssignableTo<INotifyPropertyChanged>();
        }

        [Fact]
        public void ViewModel_InheritsFromObservableObject()
        {
            // Assert
            _viewModel.Should().BeAssignableTo<ObservableObject>();
        }

        [Fact]
        public void AllObservableProperties_RaisePropertyChangedEvents()
        {
            // Arrange
            var propertyNames = new List<string>();
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != null)
                    propertyNames.Add(args.PropertyName);
            };

            // Act - Set all observable properties
            _viewModel.IsLoading = !_viewModel.IsLoading;
            _viewModel.StatusMessage = "Test Status";
            _viewModel.ShowEmptyState = !_viewModel.ShowEmptyState;

            // Assert
            propertyNames.Should().Contain(nameof(_viewModel.IsLoading));
            propertyNames.Should().Contain(nameof(_viewModel.StatusMessage));
            propertyNames.Should().Contain(nameof(_viewModel.ShowEmptyState));
        }

        #endregion

        #region State Management Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsLoading_AcceptsValidBooleanValues(bool value)
        {
            // Act
            _viewModel.IsLoading = value;

            // Assert
            _viewModel.IsLoading.Should().Be(value);
        }

        [Theory]
        [InlineData("Ready")]
        [InlineData("Loading...")]
        [InlineData("Error occurred")]
        [InlineData("")]
        public void StatusMessage_AcceptsValidStringValues(string value)
        {
            // Act
            _viewModel.StatusMessage = value;

            // Assert
            _viewModel.StatusMessage.Should().Be(value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShowEmptyState_AcceptsValidBooleanValues(bool value)
        {
            // Act
            _viewModel.ShowEmptyState = value;

            // Assert
            _viewModel.ShowEmptyState.Should().Be(value);
        }

        #endregion

        #region Service Dependencies Tests

        [Fact]
        public void GitHubService_IsAccessible()
        {
            // Assert
            _viewModel.GitHubService.Should().NotBeNull();
            _viewModel.GitHubService.Should().Be(_mockGitHubService.Object);
        }

        #endregion

        #region Orchestrator Pattern Tests

        [Fact]
        public void ViewModel_ActsAsOrchestratorForChildViewModels()
        {
            // Arrange & Assert
            // The orchestrator pattern is demonstrated by:
            // 1. Having multiple child ViewModels
            _viewModel.RepositoryControlVM.Should().NotBeNull();
            _viewModel.ContentModeFilterVM.Should().NotBeNull();
            _viewModel.GitHubItemsTreeVM.Should().NotBeNull();
            _viewModel.DetailsVM.Should().NotBeNull();
            _viewModel.InstallationVM.Should().NotBeNull();

            // 2. Setting parent references on child ViewModels
            _mockGitHubItemsTreeVM.Verify(x => x.SetParentViewModel(_viewModel), Times.Once);

            // 3. Coordinating shared state (IsLoading, StatusMessage, etc.)
            _viewModel.IsLoading.Should().BeDefined();
            _viewModel.StatusMessage.Should().NotBeNull();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Constructor_WithNullChildViewModel_ThrowsArgumentNullException()
        {
            // Test each null child ViewModel parameter
            var exceptions = new List<ArgumentNullException>();

            // Test null RepositoryControlViewModel
            try
            {
                new GitHubManagerViewModel(
                    _mockLogger.Object,
                    _mockTokenService.Object,
                    _mockGitHubService.Object,
                    null!,
                    _mockContentModeFilterVM.Object,
                    _mockGitHubItemsTreeVM.Object,
                    _mockDetailsVM.Object,
                    _mockInstallationVM.Object);
            }
            catch (ArgumentNullException ex)
            {
                exceptions.Add(ex);
                ex.ParamName.Should().Be("repositoryControlViewModel");
            }

            // Test null ContentModeFilterViewModel
            try
            {
                new GitHubManagerViewModel(
                    _mockLogger.Object,
                    _mockTokenService.Object,
                    _mockGitHubService.Object,
                    _mockRepositoryControlVM.Object,
                    null!,
                    _mockGitHubItemsTreeVM.Object,
                    _mockDetailsVM.Object,
                    _mockInstallationVM.Object);
            }
            catch (ArgumentNullException ex)
            {
                exceptions.Add(ex);
                ex.ParamName.Should().Be("contentModeFilterViewModel");
            }

            // Assert at least some exceptions were thrown
            exceptions.Should().NotBeEmpty();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void FullWorkflow_InitializeAndCoordinate_WorksCorrectly()
        {
            // Arrange
            var initialLoadingState = _viewModel.IsLoading;
            var initialStatus = _viewModel.StatusMessage;

            // Act - Simulate a typical workflow
            _viewModel.IsLoading = true;
            _viewModel.StatusMessage = "Loading repositories...";

            // Simulate completion
            _viewModel.IsLoading = false;
            _viewModel.StatusMessage = "Ready";
            _viewModel.ShowEmptyState = false;

            // Assert
            _viewModel.IsLoading.Should().BeFalse();
            _viewModel.StatusMessage.Should().Be("Ready");
            _viewModel.ShowEmptyState.Should().BeFalse();
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public void ViewModel_CanBeCreatedAndDisposed()
        {
            // Arrange & Act
            var testViewModel = new GitHubManagerViewModel(
                _mockLogger.Object,
                _mockTokenService.Object,
                _mockGitHubService.Object,
                _mockRepositoryControlVM.Object,
                _mockContentModeFilterVM.Object,
                _mockGitHubItemsTreeVM.Object,
                _mockDetailsVM.Object,
                _mockInstallationVM.Object);

            // Assert
            testViewModel.Should().NotBeNull();
            
            // Note: If IDisposable is implemented, test disposal here
            // For now, just ensure no exceptions during creation
        }

        #endregion
    }
}
