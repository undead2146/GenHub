using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Features.GameProfiles.Services;
using GenHub.Features.GameProfiles.ViewModels;
using GenHub.Features.GameVersions.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GameProfiles.ViewModels
{
    public class GameProfileLauncherViewModelTests
    {
        private readonly Mock<ILogger<GameProfileLauncherViewModel>> _mockLogger;
        private readonly Mock<IGameLauncherService> _mockGameLauncherService;
        private readonly Mock<IGameVersionServiceFacade> _mockGameVersionService;
        private readonly Mock<IGameProfileManagerService> _mockProfileManagerService;
        private readonly Mock<GameDetectionFacade> _mockGameDetectionFacade;
        private readonly Mock<IGameExecutableLocator> _mockGameExecutableLocator;
        private readonly Mock<IGameProfileFactory> _mockGameProfileFactory;
        private readonly Mock<IDesktopShortcutServiceFacade> _mockShortcutService;
        private readonly GameProfileLauncherViewModel _viewModel;

        public GameProfileLauncherViewModelTests()
        {
            _mockLogger = new Mock<ILogger<GameProfileLauncherViewModel>>();
            _mockGameLauncherService = new Mock<IGameLauncherService>();
            _mockGameVersionService = new Mock<IGameVersionServiceFacade>();
            _mockProfileManagerService = new Mock<IGameProfileManagerService>();
            _mockGameDetectionFacade = new Mock<GameDetectionFacade>();
            _mockGameExecutableLocator = new Mock<IGameExecutableLocator>();
            _mockGameProfileFactory = new Mock<IGameProfileFactory>();
            _mockShortcutService = new Mock<IDesktopShortcutServiceFacade>();

            _viewModel = new GameProfileLauncherViewModel(
                _mockLogger.Object,
                _mockGameLauncherService.Object,
                _mockGameVersionService.Object,
                _mockProfileManagerService.Object,
                _mockGameDetectionFacade.Object,
                _mockGameExecutableLocator.Object,
                _mockGameProfileFactory.Object,
                _mockShortcutService.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesProperties()
        {
            // Assert
            _viewModel.Should().NotBeNull();
            _viewModel.StatusMessage.Should().Be(string.Empty);
            _viewModel.IsLaunching.Should().BeFalse();
            _viewModel.IsEditMode.Should().BeFalse();
            _viewModel.IsScanning.Should().BeFalse();
            _viewModel.IsLoading.Should().BeFalse();
            _viewModel.Profiles.Should().NotBeNull();
            _viewModel.Profiles.Should().BeEmpty();
            _viewModel.EditModeTracker.Should().NotBeNull();
            _viewModel.EditModeTracker.Should().HaveCount(1);
            _viewModel.EditModeTracker[0].Should().BeFalse();
        }

        [Fact]
        public void Constructor_SubscribesToProfilesUpdatedEvent()
        {
            // Arrange & Act - Constructor already called

            // Assert - Verify event subscription occurred
            _mockProfileManagerService.VerifyAdd(x => x.ProfilesUpdated += It.IsAny<EventHandler<IGameProfileManagerService.ProfilesUpdatedEventArgs>>(), Times.Once);
        }

        #endregion

        #region Property Tests

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
            _viewModel.StatusMessage = "Test message";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.StatusMessage.Should().Be("Test message");
        }

        [Fact]
        public void IsLaunching_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsLaunching))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.IsLaunching = true;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.IsLaunching.Should().BeTrue();
        }

        [Fact]
        public void IsEditMode_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsEditMode))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.IsEditMode = true;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.IsEditMode.Should().BeTrue();
        }

        [Fact]
        public void SelectedProfile_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var profile = new GameProfileItemViewModel(new GameProfile { Id = "test", Name = "Test Profile" });
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.SelectedProfile))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.SelectedProfile = profile;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _viewModel.SelectedProfile.Should().Be(profile);
        }

        #endregion

        #region Command Tests

        [Fact]
        public void ToggleEditModeCommand_WhenExecuted_TogglesEditMode()
        {
            // Arrange
            var initialEditMode = _viewModel.IsEditMode;

            // Act
            _viewModel.ToggleEditModeCommand.Execute(null);

            // Assert
            _viewModel.IsEditMode.Should().Be(!initialEditMode);
            _viewModel.EditModeTracker[0].Should().Be(!initialEditMode);
        }

        [Fact]
        public void ToggleEditModeCommand_WhenExecuted_LogsInformation()
        {
            // Arrange
            var initialEditMode = _viewModel.IsEditMode;

            // Act
            _viewModel.ToggleEditModeCommand.Execute(null);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Edit mode toggled to: {!initialEditMode}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeCommand_WhenExecuted_LoadsProfiles()
        {
            // Arrange
            var testProfiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile1", Name = "Profile 1" },
                new GameProfile { Id = "profile2", Name = "Profile 2" }
            };

            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testProfiles);

            // Act
            await _viewModel.InitializeCommand.ExecuteAsync(null);

            // Assert
            _viewModel.Profiles.Should().HaveCount(2);
            _viewModel.StatusMessage.Should().Be("Ready");
            _viewModel.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task InitializeCommand_WhenExecuted_SetsLoadingState()
        {
            // Arrange
            var loadingStates = new List<bool>();
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsLoading))
                    loadingStates.Add(_viewModel.IsLoading);
            };

            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            // Act
            await _viewModel.InitializeCommand.ExecuteAsync(null);

            // Assert
            loadingStates.Should().Contain(true); // Should have been set to true during loading
            _viewModel.IsLoading.Should().BeFalse(); // Should be false after completion
        }

        [Fact]
        public async Task InitializeCommand_WhenExceptionOccurs_SetsErrorMessage()
        {
            // Arrange
            var errorMessage = "Test error";
            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(errorMessage));

            // Act
            await _viewModel.InitializeCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().Be($"Error: {errorMessage}");
            _viewModel.IsLoading.Should().BeFalse();
        }

        #endregion

        #region Event Handling Tests

        [Fact]
        public void OnProfilesUpdated_WhenSourceIsThisViewModel_DoesNotReload()
        {
            // Arrange
            var eventArgs = new IGameProfileManagerService.ProfilesUpdatedEventArgs(_viewModel);
            var profilesLoadedCallCount = 0;

            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => profilesLoadedCallCount++)
                .ReturnsAsync(new List<IGameProfile>());

            // Act
            _mockProfileManagerService.Raise(x => x.ProfilesUpdated += null, _mockProfileManagerService.Object, eventArgs);

            // Assert - Give time for any async operations to complete
            Thread.Sleep(100);
            profilesLoadedCallCount.Should().Be(0);
        }

        [Fact]
        public void OnProfilesUpdated_WhenSourceIsExternal_ReloadsProfiles()
        {
            // Arrange
            var externalSource = new object();
            var eventArgs = new IGameProfileManagerService.ProfilesUpdatedEventArgs(externalSource);
            var profilesLoadedCallCount = 0;

            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => profilesLoadedCallCount++)
                .ReturnsAsync(new List<IGameProfile>());

            // Act
            _mockProfileManagerService.Raise(x => x.ProfilesUpdated += null, _mockProfileManagerService.Object, eventArgs);

            // Assert - Give time for the async operation to complete
            Thread.Sleep(100);
            profilesLoadedCallCount.Should().BeGreaterThan(0);
        }

        #endregion

        #region Collection Tests

        [Fact]
        public void Profiles_WhenItemsAdded_NotifiesCollectionChanged()
        {
            // Arrange
            var collectionChangedRaised = false;
            _viewModel.Profiles.CollectionChanged += (sender, args) => collectionChangedRaised = true;

            var profile = new GameProfileItemViewModel(new GameProfile { Id = "test", Name = "Test Profile" });

            // Act
            _viewModel.Profiles.Add(profile);

            // Assert
            collectionChangedRaised.Should().BeTrue();
            _viewModel.Profiles.Should().Contain(profile);
        }

        [Fact]
        public void Profiles_WhenCleared_NotifiesCollectionChanged()
        {
            // Arrange
            _viewModel.Profiles.Add(new GameProfileItemViewModel(new GameProfile { Id = "test", Name = "Test Profile" }));

            var collectionChangedRaised = false;
            _viewModel.Profiles.CollectionChanged += (sender, args) => collectionChangedRaised = true;

            // Act
            _viewModel.Profiles.Clear();

            // Assert
            collectionChangedRaised.Should().BeTrue();
            _viewModel.Profiles.Should().BeEmpty();
        }

        #endregion

        #region State Management Tests

        [Fact]
        public void EditModeTracker_InitializedWithSingleFalseValue()
        {
            // Assert
            _viewModel.EditModeTracker.Should().HaveCount(1);
            _viewModel.EditModeTracker[0].Should().BeFalse();
        }

        [Fact]
        public void EditModeTracker_UpdatesWithEditMode()
        {
            // Act
            _viewModel.IsEditMode = true;
            _viewModel.ToggleEditModeCommand.Execute(null);

            // Assert
            _viewModel.EditModeTracker[0].Should().Be(_viewModel.IsEditMode);
        }

        #endregion

        #region Validation Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsEditMode_AcceptsValidBooleanValues(bool value)
        {
            // Act
            _viewModel.IsEditMode = value;

            // Assert
            _viewModel.IsEditMode.Should().Be(value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsLaunching_AcceptsValidBooleanValues(bool value)
        {
            // Act
            _viewModel.IsLaunching = value;

            // Assert
            _viewModel.IsLaunching.Should().Be(value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsScanning_AcceptsValidBooleanValues(bool value)
        {
            // Act
            _viewModel.IsScanning = value;

            // Assert
            _viewModel.IsScanning.Should().Be(value);
        }

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

        #endregion

        #region Command Availability Tests

        [Fact]
        public void ToggleEditModeCommand_IsAlwaysAvailable()
        {
            // Assert
            _viewModel.ToggleEditModeCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void InitializeCommand_IsAlwaysAvailable()
        {
            // Assert
            _viewModel.InitializeCommand.CanExecute(null).Should().BeTrue();
        }

        #endregion

        #region Observable Property Tests

        [Fact]
        public void ObservableProperties_ImplementINotifyPropertyChanged()
        {
            // Assert
            _viewModel.Should().BeAssignableTo<INotifyPropertyChanged>();
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
            _viewModel.StatusMessage = "Test Status";
            _viewModel.IsLaunching = true;
            _viewModel.IsEditMode = true;
            _viewModel.IsScanning = true;
            _viewModel.IsLoading = true;
            _viewModel.SelectedProfile = new GameProfileItemViewModel(new GameProfile { Id = "test", Name = "Test" });

            // Assert
            propertyNames.Should().Contain(nameof(_viewModel.StatusMessage));
            propertyNames.Should().Contain(nameof(_viewModel.IsLaunching));
            propertyNames.Should().Contain(nameof(_viewModel.IsEditMode));
            propertyNames.Should().Contain(nameof(_viewModel.IsScanning));
            propertyNames.Should().Contain(nameof(_viewModel.IsLoading));
            propertyNames.Should().Contain(nameof(_viewModel.SelectedProfile));
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public void Dispose_UnsubscribesFromEvents()
        {
            // Act
            _viewModel.Dispose();

            // Assert
            _mockProfileManagerService.VerifyRemove(x => x.ProfilesUpdated -= It.IsAny<EventHandler<IGameProfileManagerService.ProfilesUpdatedEventArgs>>(), Times.Once);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullWorkflow_InitializeToggleEditModeSave_WorksCorrectly()
        {
            // Arrange
            var testProfiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile1", Name = "Profile 1" }
            };

            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testProfiles);

            // Act
            await _viewModel.InitializeCommand.ExecuteAsync(null);
            _viewModel.ToggleEditModeCommand.Execute(null);

            // Assert
            _viewModel.Profiles.Should().HaveCount(1);
            _viewModel.IsEditMode.Should().BeTrue();
            _viewModel.StatusMessage.Should().Be("Ready");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task InitializeCommand_WhenProfileServiceFails_HandlesErrorGracefully()
        {
            // Arrange
            _mockProfileManagerService.Setup(x => x.GetProfilesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Service unavailable"));

            // Act
            await _viewModel.InitializeCommand.ExecuteAsync(null);

            // Assert
            _viewModel.StatusMessage.Should().StartWith("Error:");
            _viewModel.IsLoading.Should().BeFalse();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error initializing dashboard")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
