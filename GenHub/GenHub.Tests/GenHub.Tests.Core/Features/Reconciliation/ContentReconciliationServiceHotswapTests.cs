using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Launching;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Interfaces.Workspace;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Launching;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.Reconciliation;

/// <summary>
/// Unit tests verifying that <see cref="ContentReconciliationService"/> guards running profiles from workspace cleanup.
/// </summary>
public class ContentReconciliationServiceHotswapTests
{
    private readonly Mock<IGameProfileManager> _profileManagerMock = new();
    private readonly Mock<IWorkspaceManager> _workspaceManagerMock = new();
    private readonly Mock<IContentManifestPool> _manifestPoolMock = new();
    private readonly Mock<ICasReferenceTracker> _casReferenceTrackerMock = new();
    private readonly Mock<ICasLifecycleManager> _casLifecycleManagerMock = new();
    private readonly Mock<ILaunchRegistry> _launchRegistryMock = new();
    private readonly Mock<ILogger<ContentReconciliationService>> _loggerMock = new();
    private readonly ContentReconciliationService _reconciliationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReconciliationServiceHotswapTests"/> class.
    /// </summary>
    public ContentReconciliationServiceHotswapTests()
    {
        _reconciliationService = new ContentReconciliationService(
            _profileManagerMock.Object,
            _workspaceManagerMock.Object,
            _manifestPoolMock.Object,
            _casReferenceTrackerMock.Object,
            _casLifecycleManagerMock.Object,
            _loggerMock.Object,
            _launchRegistryMock.Object);
    }

    /// <summary>
    /// Verifies that ReconcileBulkManifestReplacementAsync updates the manifest reference while preserving the workspace for running profiles.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ReconcileBulkManifestReplacementAsync_WhenProfileRunning_PreservesWorkspaceAndUpdatesProfile()
    {
        // Arrange
        const string runningProfileId = "running-profile-1";
        const string oldManifestId = "1.0.0.mod.oldmod";
        const string newManifestId = "1.0.0.mod.newmod";

        var runningProfile = new GameProfile
        {
            Id = runningProfileId,
            Name = "Running Profile",
            ActiveWorkspaceId = "workspace-live-1",
            EnabledContentIds = [oldManifestId],
        };

        var newManifest = new ContentManifest
        {
            Id = ManifestId.Create(newManifestId),
            Name = "Updated Content",
        };

        _profileManagerMock.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<IReadOnlyList<GameProfile>>.CreateSuccess([runningProfile]));
        _profileManagerMock.Setup(p => p.UpdateProfileAsync(runningProfileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(runningProfile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(runningProfileId)]);

        _casReferenceTrackerMock.Setup(c => c.TrackManifestReferencesAsync(It.IsAny<string>(), It.IsAny<ContentManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.CreateSuccess());

        var replacements = new Dictionary<string, ContentManifest>
        {
            { oldManifestId, newManifest },
        };

        // Act
        var result = await _reconciliationService.ReconcileBulkManifestReplacementAsync(replacements);

        // Assert
        Assert.True(result.Success);
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _profileManagerMock.Verify(
            p => p.UpdateProfileAsync(
                runningProfileId,
                It.Is<UpdateProfileRequest>(r => r.ActiveWorkspaceId == "workspace-live-1" && r.EnabledContentIds!.Contains(newManifestId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that OrchestrateBulkRemovalAsync protects manifests from removal when active profiles are running.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OrchestrateBulkRemovalAsync_WhenProfileRunning_ProtectsManifestFromRemoval()
    {
        // Arrange
        const string runningProfileId = "running-profile-2";
        const string manifestId = "1.0.0.mod.deletedmod";

        var runningProfile = new GameProfile
        {
            Id = runningProfileId,
            Name = "Running Profile",
            ActiveWorkspaceId = "workspace-live-2",
            EnabledContentIds = [manifestId],
        };

        _profileManagerMock.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<IReadOnlyList<GameProfile>>.CreateSuccess([runningProfile]));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(runningProfileId)]);

        _casReferenceTrackerMock.Setup(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.CreateSuccess());
        _manifestPoolMock.Setup(m => m.RemoveManifestAsync(It.IsAny<ManifestId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        // Act
        var result = await _reconciliationService.OrchestrateBulkRemovalAsync([ManifestId.Create(manifestId)]);

        // Assert
        Assert.True(result.Success);
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _manifestPoolMock.Verify(m => m.RemoveManifestAsync(It.IsAny<ManifestId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _casReferenceTrackerMock.Verify(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that OrchestrateBulkRemovalAsync reconciles idle profiles while protecting manifest removal when mixed with a running profile.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OrchestrateBulkRemovalAsync_WhenMixedRunningAndIdleProfiles_ReconcilesIdleAndProtectsManifest()
    {
        // Arrange
        const string runningProfileId = "running-profile-mixed";
        const string idleProfileId = "idle-profile-mixed";
        const string manifestId = "1.0.0.mod.mixedmod";

        var runningProfile = new GameProfile
        {
            Id = runningProfileId,
            Name = "Running Profile",
            ActiveWorkspaceId = "workspace-live-mixed",
            EnabledContentIds = [manifestId],
        };

        var idleProfile = new GameProfile
        {
            Id = idleProfileId,
            Name = "Idle Profile",
            ActiveWorkspaceId = "workspace-idle-mixed",
            EnabledContentIds = [manifestId],
        };

        _profileManagerMock.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<IReadOnlyList<GameProfile>>.CreateSuccess([runningProfile, idleProfile]));

        _profileManagerMock.Setup(p => p.UpdateProfileAsync(idleProfileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(idleProfile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(runningProfileId)]);

        _workspaceManagerMock.Setup(w => w.CleanupWorkspaceAsync(idleProfile.ActiveWorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        // Act
        var result = await _reconciliationService.OrchestrateBulkRemovalAsync([ManifestId.Create(manifestId)]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.ProfilesUpdated);
        Assert.Equal(1, result.Data.WorkspacesInvalidated);
        Assert.Equal(1, result.Data.FailedProfilesCount);

        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync(idleProfile.ActiveWorkspaceId, It.IsAny<CancellationToken>()), Times.Once);
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync(runningProfile.ActiveWorkspaceId, It.IsAny<CancellationToken>()), Times.Never);
        _manifestPoolMock.Verify(m => m.RemoveManifestAsync(It.IsAny<ManifestId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _casReferenceTrackerMock.Verify(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that ReconcileManifestRemovalAsync returns failure and does not untrack CAS references when an active profile references the manifest.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ReconcileManifestRemovalAsync_WhenProfileRunning_ReturnsFailureAndProtectsManifest()
    {
        // Arrange
        const string runningProfileId = "running-profile-3";
        const string manifestId = "1.0.0.mod.runningmod";

        var runningProfile = new GameProfile
        {
            Id = runningProfileId,
            Name = "Running Profile 3",
            ActiveWorkspaceId = "workspace-live-3",
            EnabledContentIds = [manifestId],
        };

        _profileManagerMock.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<IReadOnlyList<GameProfile>>.CreateSuccess([runningProfile]));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(runningProfileId)]);

        // Act
        var result = await _reconciliationService.ReconcileManifestRemovalAsync(ManifestId.Create(manifestId));

        // Assert
        Assert.False(result.Success);
        Assert.Contains("active or failed reconciliation", result.FirstError, StringComparison.OrdinalIgnoreCase);
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _casReferenceTrackerMock.Verify(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that OrchestrateBulkRemovalAsync with mixed running and idle profiles updates the idle profile,
    /// skips workspace cleanup for the running profile, and protects the manifest from removal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task OrchestrateBulkRemovalAsync_WithMixedRunningAndIdleProfiles_UpdatesIdleProfileAndProtectsManifestFromRemoval()
    {
        // Arrange
        const string runningProfileId = "running-profile-mixed";
        const string idleProfileId = "idle-profile-mixed";
        const string manifestId = "1.0.0.mod.mixedmod";

        var runningProfile = new GameProfile
        {
            Id = runningProfileId,
            Name = "Running Profile",
            ActiveWorkspaceId = "workspace-running",
            EnabledContentIds = [manifestId],
        };

        var idleProfile = new GameProfile
        {
            Id = idleProfileId,
            Name = "Idle Profile",
            ActiveWorkspaceId = "workspace-idle",
            EnabledContentIds = [manifestId],
        };

        _profileManagerMock.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<IReadOnlyList<GameProfile>>.CreateSuccess([runningProfile, idleProfile]));
        _profileManagerMock.Setup(p => p.UpdateProfileAsync(idleProfileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(idleProfile));

        _workspaceManagerMock.Setup(w => w.CleanupWorkspaceAsync("workspace-idle", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(runningProfileId)]);

        _casReferenceTrackerMock.Setup(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.CreateSuccess());
        _manifestPoolMock.Setup(m => m.RemoveManifestAsync(It.IsAny<ManifestId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        // Act
        var result = await _reconciliationService.OrchestrateBulkRemovalAsync([ManifestId.Create(manifestId)]);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.ProfilesUpdated);
        Assert.Equal(1, result.Data.WorkspacesInvalidated);
        Assert.Equal(1, result.Data.FailedProfilesCount);

        // Idle profile's workspace cleaned up and profile updated
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync("workspace-idle", It.IsAny<CancellationToken>()), Times.Once);
        _workspaceManagerMock.Verify(w => w.CleanupWorkspaceAsync("workspace-running", It.IsAny<CancellationToken>()), Times.Never);

        // Manifest must NOT be untracked or removed because the running profile still references it
        _manifestPoolMock.Verify(m => m.RemoveManifestAsync(It.IsAny<ManifestId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _casReferenceTrackerMock.Verify(c => c.UntrackManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GameLaunchInfo CreateActiveLaunch(string profileId, string launchId = "launch-1", string workspaceId = "ws-1") => new()
    {
        LaunchId = launchId,
        ProfileId = profileId,
        WorkspaceId = workspaceId,
        ProcessInfo = new GameProcessInfo
        {
            ProcessId = 1234,
            ProcessName = "generals.exe",
            StartTime = DateTime.UtcNow,
        },
    };
}
