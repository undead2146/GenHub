using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using System.Diagnostics;
using Avalonia.Controls;

using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;

namespace GenHub.Features.GameProfiles.ViewModels
{
    /// <summary>
    /// View model for a game profile displayed in the launcher.
    /// This class wraps a GameProfile model and provides observable properties
    /// for UI binding, without duplicating any logic.
    /// </summary>
    public partial class GameProfileItemViewModel : ObservableObject, IGameProfile
    {
        // Inject the launcher service if we want to own the launch command
        private readonly IGameLauncherService? _launcherService;

        // Implement all IGameProfile properties
        [ObservableProperty] private string _id = string.Empty;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _executablePath = string.Empty;
        [ObservableProperty] private string _dataPath = string.Empty;
        [ObservableProperty] private string _iconPath = string.Empty;
        [ObservableProperty] private string _coverImagePath = string.Empty;
        [ObservableProperty] private string _colorValue = "#2A2A2A";
        [ObservableProperty] private string _commandLineArguments = string.Empty;
        [ObservableProperty] private string _versionId = string.Empty;
        [ObservableProperty] private bool _isDefaultProfile;
        [ObservableProperty] private bool _isCustomProfile = true;
        [ObservableProperty] private bool _isInstalled;
        [ObservableProperty] private bool _runAsAdmin;
        [ObservableProperty] private int _displayOrder;
        [ObservableProperty] private GameInstallationType _sourceType = GameInstallationType.Unknown;

        // GitHub-specific extended properties (for UI binding)
        [ObservableProperty] private int? _pullRequestNumber;
        [ObservableProperty] private int? _workflowNumber;
        [ObservableProperty] private string? _commitMessage;
        [ObservableProperty] private string? _commitSha;
        [ObservableProperty] private string? _buildPreset;
        [ObservableProperty] private GitHubBuild? _buildInfo;
        [ObservableProperty] private bool _hasWorkflowInfo;

        /// <summary>
        /// The source-specific metadata for this profile
        /// </summary>
        private BaseSourceMetadata? _sourceSpecificMetadata;

        /// <summary>
        /// Gets or sets the source-specific metadata for this profile
        /// </summary>
        public BaseSourceMetadata? SourceSpecificMetadata
        {
            get => _sourceSpecificMetadata;
            set => _sourceSpecificMetadata = value;
        }
        
        // Convenience Accessor
        /// <summary>
        /// Gets the GitHub-specific metadata for this game profile, if available.
        /// Returns null if SourceSpecificMetadata is not of type GitHubSourceMetadata.
        /// </summary>
        public GitHubSourceMetadata? GitHubMetadata => SourceSpecificMetadata as GitHubSourceMetadata;

        #region Commands

        // Define a RelayCommand for launching the profile
        // This allows the command to be initialized in constructors that have access to dependencies
        [ObservableProperty]
        private ICommand? _launchCommand;

        /// <summary>
        /// Creates the launch command using the provided launcher service
        /// </summary>
        public void InitializeLaunchCommand(IGameLauncherService launcherService)
        {
            if (launcherService != null)
            {
                LaunchCommand = new AsyncRelayCommand(async () => await LaunchProfileAsync(launcherService));
            }
        }

        /// <summary>
        /// Launches the profile using the provided launcher service
        /// </summary>
        private async Task LaunchProfileAsync(IGameLauncherService launcherService)
        {
            if (launcherService == null)
                throw new ArgumentNullException(nameof(launcherService));

            try
            {
                Console.WriteLine($"Launching profile: {Name}");

                // Convert the ViewModel to a model and launch it
                var profileModel = ToModel();
                await launcherService.LaunchVersionAsync(profileModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching profile: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Gets a shortened version of the commit SHA (first 7 characters)
        /// </summary>
        public string ShortCommitSha =>
            !string.IsNullOrEmpty(CommitSha) && CommitSha.Length >= 7 ?
                CommitSha.Substring(0, 7) : CommitSha ?? string.Empty;

        /// <summary>
        /// Gets a display-friendly name for the installation source
        /// </summary>
        public string SourceTypeName => GetSourceTypeName(SourceType);

        /// <summary>
        /// Creates an empty profile view model
        /// </summary>
        public GameProfileItemViewModel() { }

        /// <summary>
        /// Creates a view model with launcher service injection for command ownership
        /// </summary>
        public GameProfileItemViewModel(IGameLauncherService? launcherService = null)
        {
            _launcherService = launcherService;

            // Initialize the launch command if we have a launcher service
            if (_launcherService != null)
            {
                InitializeLaunchCommand(_launcherService);
            }
        }

        /// <summary>
        /// Creates a view model from any IGameProfile implementation.
        /// Ensures all properties are properly copied, including extended ones.
        /// </summary>
        public GameProfileItemViewModel(IGameProfile profile, IGameLauncherService? launcherService = null)
        {
            if (profile == null) return;

            _launcherService = launcherService;

            // Initialize from profile
            UpdateFrom(profile);

            // Initialize the launch command if we have a launcher service
            if (_launcherService != null)
            {
                InitializeLaunchCommand(_launcherService);
            }
        }

        /// <summary>
        /// Updates this view model with properties from another profile.
        /// This centralizes the property copying logic to avoid duplication and errors.
        /// </summary>
        public void UpdateFrom(IGameProfile profile)
        {
            if (profile == null) return;

            // Copy standard properties
            Id = profile.Id;
            Name = profile.Name;
            Description = profile.Description;
            ExecutablePath = profile.ExecutablePath;
            DataPath = profile.DataPath;
            IconPath = profile.IconPath;
            CoverImagePath = profile.CoverImagePath;
            ColorValue = profile.ColorValue;
            CommandLineArguments = profile.CommandLineArguments;
            VersionId = profile.VersionId;
            IsDefaultProfile = profile.IsDefaultProfile;
            IsCustomProfile = profile.IsCustomProfile;
            IsInstalled = profile.IsInstalled;
            RunAsAdmin = profile.RunAsAdmin;
            SourceType = profile.SourceType;
            DisplayOrder = profile.DisplayOrder;

            // Store the original SourceSpecificMetadata for the explicit interface implementation
            _sourceSpecificMetadata = profile.SourceSpecificMetadata;

            // Copy extended properties if available
            CopyExtendedProperties(profile);
        }

        /// <summary>
        /// Copy extended properties from source profile
        /// </summary>
        private void CopyExtendedProperties(IGameProfile profile)
        {
            // Case 1: Source profile has GitHubSourceMetadata - most common case
            if (profile.SourceSpecificMetadata is GitHubSourceMetadata githubMetadata)
            {
                PullRequestNumber = githubMetadata.PullRequestNumber;
                WorkflowNumber = githubMetadata.WorkflowRunNumber;
                CommitMessage = githubMetadata.CommitMessage;
                CommitSha = githubMetadata.CommitSha;
                BuildPreset = githubMetadata.BuildPreset;
                BuildInfo = githubMetadata.BuildInfo;
                HasWorkflowInfo = githubMetadata.HasCompleteWorkflowContext;
            }
            // Case 2: Source profile is another GameProfileItemViewModel - copying between ViewModel instances
            else if (profile is GameProfileItemViewModel viewModel)
            {
                PullRequestNumber = viewModel.PullRequestNumber;
                WorkflowNumber = viewModel.WorkflowNumber;
                CommitMessage = viewModel.CommitMessage;
                CommitSha = viewModel.CommitSha;
                BuildPreset = viewModel.BuildPreset;
                HasWorkflowInfo = viewModel.HasWorkflowInfo;
                BuildInfo = viewModel.BuildInfo;
            }
            // Case 3: Source is a GameProfile with extended properties but not stored in metadata
            // This is a fallback for backward compatibility with older profiles
            else if (profile is GameProfile gameProfile)
            {
                // Access the properties directly from the GameProfile model
                // These fields exist in GameProfile but aren't part of IGameProfile interface
                var propTypes = gameProfile.GetType();

                // For backward compatibility - these are still directly on GameProfile
                if (propTypes.GetProperty("PullRequestNumber")?.GetValue(gameProfile) is int prNum)
                {
                    PullRequestNumber = prNum > 0 ? prNum : null;
                }

                if (propTypes.GetProperty("WorkflowNumber")?.GetValue(gameProfile) is int wfNum)
                {
                    WorkflowNumber = wfNum > 0 ? wfNum : null;
                }

                if (propTypes.GetProperty("CommitMessage")?.GetValue(gameProfile) is string commitMsg)
                {
                    CommitMessage = !string.IsNullOrEmpty(commitMsg) ? commitMsg : null;
                }

                if (propTypes.GetProperty("CommitSha")?.GetValue(gameProfile) is string sha)
                {
                    CommitSha = !string.IsNullOrEmpty(sha) ? sha : null;
                }

                if (propTypes.GetProperty("BuildPreset")?.GetValue(gameProfile) is string preset)
                {
                    BuildPreset = !string.IsNullOrEmpty(preset) ? preset : null;
                }

                if (propTypes.GetProperty("BuildInfo")?.GetValue(gameProfile) is GitHubBuild buildInfo)
                {
                    BuildInfo = buildInfo;
                }

                if (propTypes.GetProperty("HasWorkflowInfo")?.GetValue(gameProfile) is bool hasWf)
                {
                    HasWorkflowInfo = hasWf;
                }
            }
        }

        public void PopulateSourceSpecificMetadata(GameVersion? version = null)
        {
            if (version?.SourceSpecificMetadata is GitHubSourceMetadata githubMetadata)
            {
                WorkflowNumber = githubMetadata.WorkflowRunNumber;
                PullRequestNumber = githubMetadata.PullRequestNumber;
                CommitSha = githubMetadata.CommitSha;
                CommitMessage = githubMetadata.CommitMessage;
                BuildPreset = githubMetadata.BuildPreset;
                HasWorkflowInfo = githubMetadata.HasCompleteWorkflowContext;

                // Ensure BuildInfo is safely copied
                BuildInfo = githubMetadata.BuildInfo?.Clone() as GitHubBuild;

                SourceType = GameInstallationType.GitHubArtifact;

                // Update the underlying source-specific metadata with a clone
                _sourceSpecificMetadata = githubMetadata.Clone();
            }
            else
            {
                // Clear GitHub-specific metadata if not a GitHub version
                if (version?.SourceType != GameInstallationType.GitHubArtifact)
                {
                    WorkflowNumber = null;
                    PullRequestNumber = null;
                    CommitSha = null;
                    CommitMessage = null;
                    BuildPreset = null;
                    HasWorkflowInfo = false;
                    BuildInfo = null;
                }
            }

            // Always notify property changes to ensure UI updates
            OnPropertyChanged(nameof(WorkflowNumber));
            OnPropertyChanged(nameof(PullRequestNumber));
            OnPropertyChanged(nameof(CommitSha));
            OnPropertyChanged(nameof(CommitMessage));
            OnPropertyChanged(nameof(BuildPreset));
            OnPropertyChanged(nameof(HasWorkflowInfo));
            OnPropertyChanged(nameof(BuildInfo));
            OnPropertyChanged(nameof(ShortCommitSha));
        }

        /// <summary>
        /// Updates this profile with properties from a GameVersion
        /// </summary>
        public void UpdateFromVersion(GameVersion version)
        {
            if (version == null) return;

            VersionId = version.Id;
            ExecutablePath = version.ExecutablePath;
            IsInstalled = true;
            DataPath = version.InstallPath ?? string.Empty;
            SourceType = version.SourceType;

            // This will now correctly use SourceSpecificMetadata from GameVersion
            PopulateSourceSpecificMetadata(version);

            Console.WriteLine($"Updated profile from version: {version.Name}");
        }

        /// <summary>
        /// Converts this view model back to a data model
        /// </summary>
        public GameProfile ToModel()
        {
            // Ensure ID is set for new profiles
            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString();
            }

            var model = new GameProfile
            {
                Id = Id,
                Name = string.IsNullOrEmpty(Name) ? "New Profile" : Name,
                Description = string.IsNullOrEmpty(Description) ? "New game profile" : Description,
                ExecutablePath = ExecutablePath ?? string.Empty,
                DataPath = DataPath ?? string.Empty,
                IconPath = string.IsNullOrEmpty(IconPath) ? "avares://GenHub/Assets/Icons/genhub-Logo.png" : IconPath,
                CoverImagePath = string.IsNullOrEmpty(CoverImagePath) ? "avares://GenHub/Assets/Covers/generals-cover-2.png" : CoverImagePath,
                ColorValue = string.IsNullOrEmpty(ColorValue) ? "#2A2A2A" : ColorValue,
                CommandLineArguments = CommandLineArguments ?? string.Empty,
                VersionId = VersionId ?? string.Empty,
                IsDefaultProfile = IsDefaultProfile,
                IsCustomProfile = IsCustomProfile,
                IsInstalled = IsInstalled,
                RunAsAdmin = RunAsAdmin,
                SourceType = SourceType,
                DisplayOrder = DisplayOrder
            };

            try
            {
                // Handle source-specific metadata based on source type
                if (SourceType == GameInstallationType.GitHubArtifact)
                {
                    if (_sourceSpecificMetadata is GitHubSourceMetadata githubMeta)
                    {
                        // Use existing metadata if available
                        model.SourceSpecificMetadata = githubMeta.Clone();
                        
                        // Ensure BuildInfo exists
                        if (model.GitHubMetadata!.BuildInfo == null)
                        {
                            model.GitHubMetadata.BuildInfo = new GitHubBuild
                            {
                                Compiler = "Unknown",
                                Configuration = "Unknown",
                                Version = "Unknown"
                            };
                        }
                    }
                    else
                    {
                        // Create new metadata with valid BuildInfo for GitHub artifacts
                        var safeMetadata = new GitHubSourceMetadata();
                        safeMetadata.BuildInfo = new GitHubBuild
                        {
                            Compiler = "Unknown",
                            Configuration = "Unknown",
                            Version = "Unknown"
                        };
                        model.SourceSpecificMetadata = safeMetadata;
                    }
                }
                else if (_sourceSpecificMetadata != null)
                {
                    // For non-GitHub metadata types
                    try
                    {
                        model.SourceSpecificMetadata = _sourceSpecificMetadata.Clone();
                    }
                    catch (Exception ex)
                    {
                        // If cloning fails, set to null rather than crashing
                        model.SourceSpecificMetadata = null;
                    }
                }
                else
                {
                    // Important fix: explicitly set to null for new profiles with no metadata
                    model.SourceSpecificMetadata = null;
                }
            }
            catch (Exception ex)
            {
                // If all metadata handling fails, set to null rather than crashing
                model.SourceSpecificMetadata = null;
            }

            return model;
        }

        /// <summary>
        /// Gets a user-friendly name for the installation source type
        /// </summary>
        private string GetSourceTypeName(GameInstallationType sourceType)
        {
            return sourceType switch
            {
                GameInstallationType.Steam => "Steam",
                GameInstallationType.EaApp => "EA App",
                GameInstallationType.Origin => "Origin",
                GameInstallationType.TheFirstDecade => "First Decade",
                GameInstallationType.RGMechanics => "RG Mechanics",
                GameInstallationType.CDISO => "CD/ISO",
                GameInstallationType.GitHubArtifact => "GitHub",
                GameInstallationType.LocalZipFile => "Local",
                GameInstallationType.DirectoryImport => "Imported",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Gets a safe display version of the BuildInfo.Compiler property that handles null cases
        /// </summary>
        public string DisplayCompiler => BuildInfo?.Compiler ?? "N/A";

        /// <summary>
        /// Gets a safe display version of the BuildInfo.Configuration property that handles null cases
        /// </summary>
        public string DisplayConfiguration => BuildInfo?.Configuration ?? "N/A";

        // Add this method if you're manually handling property change notifications
        // If using [ObservableProperty] attribute from CommunityToolkit.Mvvm, this might not be needed
        private void OnBuildInfoChanged()
        {
            OnPropertyChanged(nameof(BuildInfo));
            OnPropertyChanged(nameof(DisplayCompiler));
            OnPropertyChanged(nameof(DisplayConfiguration));
        }
    }
}
