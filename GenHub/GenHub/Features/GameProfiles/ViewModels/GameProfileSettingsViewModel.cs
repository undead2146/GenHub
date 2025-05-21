using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Models.GameProfiles;
using GenHub.Features.GameProfiles.Services;
using GenHub.Common.ViewModels;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Interfaces.UI;
using GenHub.Core.Models.UI;
using GenHub.Features.GameProfiles.Views;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;

namespace GenHub.Features.GameProfiles.ViewModels
{
    public partial class GameProfileSettingsViewModel : ViewModelBase
    {
        private readonly ILogger<GameProfileSettingsViewModel> _logger;
        private readonly IProfileSettingsDataProvider _dataProvider;
        private readonly IGameProfileManagerService _profileManager;
        private readonly GameProfileFactory _profileFactory;
        private readonly ProfileResourceService _resourceService;
        private readonly ProfileMetadataService _metadataService;
        private readonly IGameVersionServiceFacade _versionService;
        private readonly IGameLauncherService _launcherService;
        private readonly IDialogService _dialogService;

        private readonly Window? _ownerWindow;
        private CancellationTokenSource? _initializationCts;

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private string _executablePath = "";
        [ObservableProperty] private string _dataPath = "";
        [ObservableProperty] private string _iconPath = "";
        [ObservableProperty] private string _coverPath = "";
        [ObservableProperty] private string _commandLineArguments = "";
        [ObservableProperty] private string _colorValue = "#2A2A2A";
        [ObservableProperty] private bool _runAsAdmin;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private bool _isInitializing;
        [ObservableProperty] private bool _isNewProfile;
        [ObservableProperty] private bool _loadingError;
        [ObservableProperty] private GameVersion? _selectedVersion;
        [ObservableProperty] private bool _isProfileChanged;
        [ObservableProperty] private bool _isExecutableValid = true;
        [ObservableProperty] private bool _isDataPathValid = true;
        [ObservableProperty] private bool _canLaunchGame;
        [ObservableProperty] private bool _isTesting;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private ProfileResourceItem? _selectedIcon;
        [ObservableProperty] private ProfileResourceItem? _selectedCover;
        [ObservableProperty] private bool _hasGitHubMetadata;
        [ObservableProperty] private string _sourceTypeName = "Custom";
        [ObservableProperty] private bool _isValidInstallationType;

        [ObservableProperty] private ExecutablePathItem? _selectedExecutable;
        [ObservableProperty] private DataPathItem? _selectedDataPath;

        public ObservableCollection<GameVersion> AvailableVersions { get; } = new();
        public ObservableCollection<ProfileResourceItem> AvailableIcons { get; } = new();
        public ObservableCollection<ProfileResourceItem> AvailableCovers { get; } = new();
        public ObservableCollection<DataPathItem> AvailableDataPaths { get; } = new();
        public ObservableCollection<ExecutablePathItem> AvailableExecutables { get; } = new();
        public ObservableCollection<ProfileThemeColorItem> AvailableThemeColors { get; } = new();

        private readonly GameProfileItemViewModel? _originalProfile;
        private GameProfile? _editingProfile;
        private bool _initialized;
        public bool DialogHasCompleted { get; private set; }
        private Window? _dialogWindow;

        private readonly HashSet<GameInstallationType> _validInstallationTypes = new()
        {
            GameInstallationType.Steam,
            GameInstallationType.EaApp,
            GameInstallationType.Origin
        };

        // Add this field at the class level with other private fields (near line 44)
        private bool _isInitializationInProgress;

        // Add these flags at the class level to prevent recursive updates
        private bool _isUpdatingIconInternals;
        private bool _isUpdatingCoverInternals;
        private bool _isUpdatingExecutableInternals;
        private bool _isUpdatingDataPathInternals;

        public event EventHandler? DialogConfirmed;
        public event EventHandler? DialogCancelled;
        public Action? CloseAction { get; set; }

        public GameProfileSettingsViewModel()
        {
            _logger = AppLocator.GetService<ILogger<GameProfileSettingsViewModel>>();
            _dataProvider = AppLocator.GetService<IProfileSettingsDataProvider>();
            _profileManager = AppLocator.GetService<IGameProfileManagerService>();
            _profileFactory = AppLocator.GetService<GameProfileFactory>();
            _resourceService = AppLocator.GetService<ProfileResourceService>();
            _metadataService = AppLocator.GetService<ProfileMetadataService>();
            _versionService = AppLocator.GetService<IGameVersionServiceFacade>();
            _launcherService = AppLocator.GetService<IGameLauncherService>();
            _dialogService = AppLocator.GetService<IDialogService>();

            IsNewProfile = true;
            Name = "New Profile";
            ColorValue = "#2A2A2A";
            InitializeThemeColors();

            _logger.LogDebug("Created GameProfileSettingsViewModel (default constructor)");
            PropertyChanged += OnPropertyChanged;
        }

        public GameProfileSettingsViewModel(GameProfileItemViewModel? profile, Window? ownerWindow = null)
            : this()
        {
            _ownerWindow = ownerWindow;
            _originalProfile = profile;
            IsNewProfile = profile == null;

            if (profile != null)
            {
                Name = profile.Name;
                Description = profile.Description;
                ExecutablePath = profile.ExecutablePath;
                DataPath = profile.DataPath;
                IconPath = profile.IconPath;
                CoverPath = profile.CoverImagePath;
                CommandLineArguments = profile.CommandLineArguments;
                ColorValue = profile.ColorValue;
                RunAsAdmin = profile.RunAsAdmin;
                SourceTypeName = profile.SourceTypeName;
                HasGitHubMetadata = profile.GitHubMetadata != null;
                _logger.LogDebug("Created GameProfileSettingsViewModel for profile: {ProfileName}", Name);
            }
            else
            {
                Name = "New Profile";
                ColorValue = "#2A2A2A";
                _logger.LogDebug("Created GameProfileSettingsViewModel for new profile");
            }

            PropertyChanged += OnPropertyChanged;
            AvailableVersions.CollectionChanged += OnAvailableVersionsChanged;
        }

        private bool _isInitializingSelections;
        
        // Simplified initialization sequence
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            IsInitializing = true;
            _isInitializationInProgress = true; // Set the flag to prevent unnecessary UI updates during init
            LoadingError = false;
            _logger.LogInformation("InitializeAsync started for profile: {ProfileName}, IsNewProfile: {IsNewProfile}", Name, IsNewProfile);

            try
            {
                _initializationCts = new CancellationTokenSource();
                var token = _initializationCts.Token;

                // Store initial values before loading anything
                string initialExecutablePath = ExecutablePath;
                string initialDataPath = DataPath;
                string initialIconPath = IconPath;
                string initialCoverPath = CoverPath;
                string versionId = _editingProfile?.VersionId;

                _logger.LogDebug("Initial paths - ExecutablePath: {path1}, DataPath: {path2}, IconPath: {path3}, CoverPath: {path4}, VersionId: {id}",
                    initialExecutablePath, initialDataPath, initialIconPath, initialCoverPath, versionId ?? "null");

                // Step 1: Load editing profile
                await LoadEditingProfileAsync(token);
                token.ThrowIfCancellationRequested();

                // Step 2: Load resources
                var (versions, icons, covers) = await LoadAvailableResourcesAsync(token);
                token.ThrowIfCancellationRequested();

                // Step 3: Populate collections
                await PopulateResourceCollectionsAsync(versions, icons, covers, token);
                token.ThrowIfCancellationRequested();

                // Step 4: Populate path collections
                await PopulatePathsFromVersions(versions);
                token.ThrowIfCancellationRequested();

                // Step 5: Set initial selections in the proper order
                await Dispatcher.UIThread.InvokeAsync(() => {
                    // First select the game version, which may update other paths
                    if (!string.IsNullOrEmpty(versionId))
                    {
                        SelectedVersion = AvailableVersions.FirstOrDefault(v => v.Id == versionId);
                        if (SelectedVersion != null)
                        {
                            _logger.LogDebug("Selected version from profile ID: {name}", SelectedVersion.Name);
                            UpdateUIForSelectedVersion();
                        }
                    }
                    
                    // If no version found or for new profiles, select default version
                    if (SelectedVersion == null && AvailableVersions.Any())
                    {
                        SelectedVersion = AvailableVersions.FirstOrDefault(v => _validInstallationTypes.Contains(v.SourceType)) 
                                       ?? AvailableVersions.FirstOrDefault();
                                       
                        if (SelectedVersion != null)
                        {
                            _logger.LogDebug("Selected default version: {name}", SelectedVersion.Name);
                            UpdateUIForSelectedVersion();
                        }
                    }

                    // Now restore the selections based on paths (either original or updated by version)
                    // For executable path
                    if (!string.IsNullOrEmpty(ExecutablePath))
                    {
                        _isUpdatingExecutableInternals = true;
                        try
                        {
                            SelectedExecutable = AvailableExecutables.FirstOrDefault(e => 
                                string.Equals(e.Path, ExecutablePath, StringComparison.OrdinalIgnoreCase));
                                
                            _logger.LogDebug("Selected executable for path {path}: {found}", 
                                ExecutablePath, SelectedExecutable != null ? "found" : "not found");
                        }
                        finally
                        {
                            _isUpdatingExecutableInternals = false;
                        }
                    }

                    // For data path
                    if (!string.IsNullOrEmpty(DataPath))
                    {
                        _isUpdatingDataPathInternals = true;
                        try
                        {
                            SelectedDataPath = AvailableDataPaths.FirstOrDefault(d => 
                                string.Equals(d.Path, DataPath, StringComparison.OrdinalIgnoreCase));
                                
                            _logger.LogDebug("Selected data path for path {path}: {found}", 
                                DataPath, SelectedDataPath != null ? "found" : "not found");
                        }
                        finally
                        {
                            _isUpdatingDataPathInternals = false;
                        }
                    }

                    // For icon
                    if (!string.IsNullOrEmpty(IconPath))
                    {
                        _isUpdatingIconInternals = true;
                        try
                        {
                            SelectedIcon = AvailableIcons.FirstOrDefault(i => 
                                string.Equals(i.Path, IconPath, StringComparison.OrdinalIgnoreCase));
                                
                            if (SelectedIcon == null)
                            {
                                // Try filename match as fallback
                                string iconFileName = Path.GetFileName(IconPath);
                                SelectedIcon = AvailableIcons.FirstOrDefault(i => 
                                    string.Equals(Path.GetFileName(i.Path), iconFileName, StringComparison.OrdinalIgnoreCase));
                            }
                                
                            _logger.LogDebug("Selected icon for path {path}: {found}", 
                                IconPath, SelectedIcon != null ? "found" : "not found");
                        }
                        finally
                        {
                            _isUpdatingIconInternals = false;
                        }
                    }

                    // For cover
                    if (!string.IsNullOrEmpty(CoverPath))
                    {
                        _isUpdatingCoverInternals = true;
                        try
                        {
                            SelectedCover = AvailableCovers.FirstOrDefault(c => 
                                string.Equals(c.Path, CoverPath, StringComparison.OrdinalIgnoreCase));
                                
                            if (SelectedCover == null)
                            {
                                // Try filename match as fallback
                                string coverFileName = Path.GetFileName(CoverPath);
                                SelectedCover = AvailableCovers.FirstOrDefault(c => 
                                    string.Equals(Path.GetFileName(c.Path), coverFileName, StringComparison.OrdinalIgnoreCase));
                            }
                                
                            _logger.LogDebug("Selected cover for path {path}: {found}", 
                                CoverPath, SelectedCover != null ? "found" : "not found");
                        }
                        finally
                        {
                            _isUpdatingCoverInternals = false;
                        }
                    }
                });
                token.ThrowIfCancellationRequested();

                // Step 6: Finalize
                await FinalizeInitializationAsync(token);
                token.ThrowIfCancellationRequested();

                _initialized = true;
                _logger.LogInformation("GameProfileSettingsViewModel initialization completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GameProfileSettingsViewModel initialization was cancelled for IsNewProfile={IsNewProfile}.", IsNewProfile);
                LoadingError = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing GameProfileSettingsViewModel for IsNewProfile={IsNewProfile}.", IsNewProfile);
                LoadingError = true;
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isInitializationInProgress = false;
                IsInitializing = false;
                _initializationCts?.Dispose();
                _initializationCts = null;
            }
        }

        private async Task LoadEditingProfileAsync(CancellationToken token)
        {
            if (_originalProfile != null)
            {
                var profile = await _profileManager.GetProfileAsync(_originalProfile.Id, token);
                if (profile != null)
                {
                    _editingProfile = profile is GameProfile gameProfile ? gameProfile : new GameProfile(profile);
                }
                else
                {
                    _logger.LogWarning("Could not find profile with ID: {ProfileId}", _originalProfile.Id);
                    throw new InvalidOperationException($"Profile not found: {_originalProfile.Id}");
                }
            }
        }

        private async Task<(List<GameVersion> Versions, List<ProfileResourceItem> Icons, List<ProfileResourceItem> Covers)> LoadAvailableResourcesAsync(CancellationToken token)
        {
            _logger.LogDebug("Loading resources in parallel");
            var versionsTask = _dataProvider.GetAvailableVersionsAsync(token);
            var iconsTask = _dataProvider.GetAvailableIconsAsync(token);
            var coversTask = _dataProvider.GetAvailableCoverImagesAsync(token);

            await Task.WhenAll(versionsTask, iconsTask, coversTask);
            token.ThrowIfCancellationRequested();

            var versions = versionsTask.Result?.ToList() ?? new List<GameVersion>();
            var icons = iconsTask.Result?.ToList() ?? new List<ProfileResourceItem>();
            var covers = coversTask.Result?.ToList() ?? new List<ProfileResourceItem>();

            _logger.LogDebug("Loaded {versionCount} versions, {iconCount} icons, {coverCount} covers",
                versions.Count, icons.Count, covers.Count);
            return (versions, icons, covers);
        }

        private async Task PopulateResourceCollectionsAsync(IEnumerable<GameVersion> versions, IEnumerable<ProfileResourceItem> icons, IEnumerable<ProfileResourceItem> covers, CancellationToken token)
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                AvailableVersions.Clear();
                foreach (var version in versions) AvailableVersions.Add(version);
                token.ThrowIfCancellationRequested();

                AvailableIcons.Clear();
                foreach (var icon in icons) AvailableIcons.Add(icon);
                token.ThrowIfCancellationRequested();

                AvailableCovers.Clear();
                foreach (var cover in covers) AvailableCovers.Add(cover);
            });
        }

        private async Task PopulatePathSelectionCollectionsAsync(IEnumerable<GameVersion> versions, CancellationToken token)
        {
            await PopulatePathsFromVersions(versions);
            token.ThrowIfCancellationRequested();
        }

        private async Task SetInitialSelectionsAndUIDefaultsAsync(IEnumerable<GameVersion> versions, CancellationToken token)
        {
            if (_editingProfile != null && !string.IsNullOrEmpty(_editingProfile.VersionId))
            {
                SelectedVersion = AvailableVersions.FirstOrDefault(v => v.Id == _editingProfile.VersionId);
            }
            if (SelectedVersion == null && AvailableVersions.Any())
            {
                SelectedVersion = AvailableVersions.FirstOrDefault(v => _validInstallationTypes.Contains(v.SourceType))
                                  ?? AvailableVersions.FirstOrDefault();
            }
            token.ThrowIfCancellationRequested();

            UpdateUIForSelectedVersion();
            token.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() => {
                if (!string.IsNullOrEmpty(ExecutablePath))
                    SelectedExecutable = AvailableExecutables.FirstOrDefault(e => e.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(DataPath))
                    SelectedDataPath = AvailableDataPaths.FirstOrDefault(d => d.Path.Equals(DataPath, StringComparison.OrdinalIgnoreCase));
                UpdateSelectedIcon();
                UpdateSelectedCover();
            });
        }

        private async Task FinalizeInitializationAsync(CancellationToken token)
        {
            await ValidatePathsAsync();
            token.ThrowIfCancellationRequested();
            UpdateCanLaunchState();

            if (_editingProfile?.GitHubMetadata != null)
            {
                HasGitHubMetadata = true;
                await ApplyGitHubMetadataAsync(_editingProfile.GitHubMetadata);
            }
        }

        private string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private void OnHeaderPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            _logger.LogTrace("Header property changed: {PropertyName}", propertyName);
        }

        private void InitializeThemeColors()
        {
            AvailableThemeColors.Clear();
            foreach (var color in ProfileThemeColor.GetDefaultColors())
            {
                AvailableThemeColors.Add(new ProfileThemeColorItem(color.Name, color.HexValue));
            }
            _logger.LogDebug("Initialized {count} theme colors.", AvailableThemeColors.Count);
        }

        private void UpdateSelectedIcon()
        {
            if (_isInitializationInProgress)
            {
                _logger.LogTrace("UpdateSelectedIcon skipped - initialization in progress");
                return;
            }

            _logger.LogDebug("UpdateSelectedIcon called with IconPath: {path}, AvailableIcons.Count: {count}",
                IconPath, AvailableIcons.Count);

            if (AvailableIcons.Count == 0)
            {
                _logger.LogWarning("Cannot update selected icon - AvailableIcons is empty");
                SelectedIcon = null;
                return;
            }

            string targetIconPath = IconPath;

            if (string.IsNullOrEmpty(targetIconPath) && IsNewProfile)
            {
                var gameType = SelectedVersion?.GameType ?? "Generals";
                targetIconPath = _dataProvider.GetIconPathForGameType(gameType);
                if (string.IsNullOrEmpty(targetIconPath) && AvailableIcons.Count > 0)
                {
                    targetIconPath = AvailableIcons[0].Path;
                    _logger.LogDebug("Set fallback icon path to first available: {path}", targetIconPath);
                }
            }

            if (string.IsNullOrEmpty(targetIconPath))
            {
                SelectedIcon = null;
                return;
            }

            ProfileResourceItem? newSelectedIcon = null;
            newSelectedIcon = AvailableIcons.FirstOrDefault(i =>
                i.Path.Equals(targetIconPath, StringComparison.OrdinalIgnoreCase));

            if (newSelectedIcon == null)
            {
                string normalizedPath = NormalizePath(targetIconPath);
                newSelectedIcon = AvailableIcons.FirstOrDefault(i =>
                    NormalizePath(i.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            }

            if (newSelectedIcon == null)
            {
                string iconFileName = Path.GetFileName(targetIconPath);
                newSelectedIcon = AvailableIcons.FirstOrDefault(i =>
                    Path.GetFileName(i.Path).Equals(iconFileName, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedIcon != newSelectedIcon)
            {
                SelectedIcon = newSelectedIcon;
            }

            _logger.LogDebug("UpdateSelectedIcon result: SelectedIcon='{selectedIconDisplay}', IconPath='{iconPath}'",
                SelectedIcon?.DisplayName ?? "null", IconPath);
        }

        private void UpdateSelectedCover()
        {
            if (_isInitializationInProgress)
            {
                _logger.LogTrace("UpdateSelectedCover skipped - initialization in progress");
                return;
            }

            _logger.LogDebug("UpdateSelectedCover called with CoverPath: {path}, AvailableCovers.Count: {count}",
                CoverPath, AvailableCovers.Count);

            if (AvailableCovers.Count == 0)
            {
                _logger.LogWarning("Cannot update selected cover - AvailableCovers is empty");
                SelectedCover = null;
                return;
            }

            string targetCoverPath = CoverPath;

            if (string.IsNullOrEmpty(targetCoverPath) && IsNewProfile)
            {
                var gameType = SelectedVersion?.GameType ?? "Generals";
                targetCoverPath = _dataProvider.GetCoverPathForGameType(gameType);
                if (string.IsNullOrEmpty(targetCoverPath) && AvailableCovers.Count > 0)
                {
                    targetCoverPath = AvailableCovers[0].Path;
                    _logger.LogDebug("Set fallback cover path to first available: {path}", targetCoverPath);
                }
            }

            if (string.IsNullOrEmpty(targetCoverPath))
            {
                SelectedCover = null;
                return;
            }

            ProfileResourceItem? newSelectedCover = null;
            newSelectedCover = AvailableCovers.FirstOrDefault(c =>
                c.Path.Equals(targetCoverPath, StringComparison.OrdinalIgnoreCase));

            if (newSelectedCover == null)
            {
                string normalizedPath = NormalizePath(targetCoverPath);
                newSelectedCover = AvailableCovers.FirstOrDefault(c =>
                    NormalizePath(c.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            }

            if (newSelectedCover == null)
            {
                string coverFileName = Path.GetFileName(targetCoverPath);
                newSelectedCover = AvailableCovers.FirstOrDefault(c =>
                    Path.GetFileName(c.Path).Equals(coverFileName, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedCover != newSelectedCover)
            {
                SelectedCover = newSelectedCover;
            }

            _logger.LogDebug("UpdateSelectedCover result: SelectedCover='{selectedCoverDisplay}', CoverPath='{coverPath}'",
                SelectedCover?.DisplayName ?? "null", CoverPath);
        }

        partial void OnIconPathChanged(string value)
        {
            if (_isUpdatingIconInternals || _isInitializationInProgress)
                return;

            _isUpdatingIconInternals = true;
            try
            {
                IsProfileChanged = true;
                UpdateSelectedIcon();
            }
            finally
            {
                _isUpdatingIconInternals = false;
            }
        }

        partial void OnSelectedIconChanged(ProfileResourceItem? value)
        {
            if (_isUpdatingIconInternals || _isInitializationInProgress)
                return;

            _isUpdatingIconInternals = true;
            try
            {
                IsProfileChanged = true;
                if (value != null && (string.IsNullOrEmpty(IconPath) || !IconPath.Equals(value.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("SelectedIcon changed, updating IconPath to: {path}", value.Path);
                    IconPath = value.Path;
                }
            }
            finally
            {
                _isUpdatingIconInternals = false;
            }
        }

        partial void OnCoverPathChanged(string value)
        {
            if (_isUpdatingCoverInternals || _isInitializationInProgress)
                return;

            _isUpdatingCoverInternals = true;
            try
            {
                IsProfileChanged = true;
                UpdateSelectedCover();
            }
            finally
            {
                _isUpdatingCoverInternals = false;
            }
        }

        partial void OnSelectedCoverChanged(ProfileResourceItem? value)
        {
            if (_isUpdatingCoverInternals || _isInitializationInProgress)
                return;

            _isUpdatingCoverInternals = true;
            try
            {
                IsProfileChanged = true;
                if (value != null && (string.IsNullOrEmpty(CoverPath) || !CoverPath.Equals(value.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("SelectedCover changed, updating CoverPath to: {path}", value.Path);
                    CoverPath = value.Path;
                }
            }
            finally
            {
                _isUpdatingCoverInternals = false;
            }
        }

        partial void OnExecutablePathChanged(string value)
        {
            if (_isUpdatingExecutableInternals || _isInitializationInProgress)
                return;

            _isUpdatingExecutableInternals = true;
            try
            {
                IsProfileChanged = true;
                // Update selected executable in dropdown if it matches the new path
                if (!string.IsNullOrEmpty(value))
                {
                    var matchingExecutable = AvailableExecutables.FirstOrDefault(e => 
                        e.Path.Equals(value, StringComparison.OrdinalIgnoreCase));
                        
                    if (matchingExecutable != null && 
                        (SelectedExecutable == null || !SelectedExecutable.Path.Equals(value, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogDebug("Setting SelectedExecutable from ExecutablePath: {path}", value);
                        SelectedExecutable = matchingExecutable;
                    }
                }
            }
            finally
            {
                _isUpdatingExecutableInternals = false;
            }
        }

        partial void OnSelectedExecutableChanged(ExecutablePathItem? value)
        {
            if (_isUpdatingExecutableInternals || _isInitializationInProgress)
                return;

            _isUpdatingExecutableInternals = true;
            try
            {
                IsProfileChanged = true;
                if (value != null && (string.IsNullOrEmpty(ExecutablePath) || !ExecutablePath.Equals(value.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("SelectedExecutable changed, updating ExecutablePath to: {path}", value.Path);
                    ExecutablePath = value.Path;
                }
            }
            finally
            {
                _isUpdatingExecutableInternals = false;
            }
        }

        partial void OnDataPathChanged(string value)
        {
            if (_isUpdatingDataPathInternals || _isInitializationInProgress)
                return;

            _isUpdatingDataPathInternals = true;
            try
            {
                IsProfileChanged = true;
                // Update selected data path in dropdown if it matches the new path
                if (!string.IsNullOrEmpty(value))
                {
                    var matchingDataPath = AvailableDataPaths.FirstOrDefault(d => 
                        d.Path.Equals(value, StringComparison.OrdinalIgnoreCase));
                        
                    if (matchingDataPath != null && 
                        (SelectedDataPath == null || !SelectedDataPath.Path.Equals(value, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogDebug("Setting SelectedDataPath from DataPath: {path}", value);
                        SelectedDataPath = matchingDataPath;
                    }
                }
            }
            finally
            {
                _isUpdatingDataPathInternals = false;
            }
        }

        partial void OnSelectedDataPathChanged(DataPathItem? value)
        {
            if (_isUpdatingDataPathInternals || _isInitializationInProgress)
                return;

            _isUpdatingDataPathInternals = true;
            try
            {
                if (value != null && (string.IsNullOrEmpty(DataPath) || !DataPath.Equals(value.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("SelectedDataPath changed, updating DataPath to: {path}", value.Path);
                    DataPath = value.Path;
                }
            }
            finally
            {
                _isUpdatingDataPathInternals = false;
            }
        }

        private async Task PopulatePathsFromVersions(IEnumerable<GameVersion> versions)
        {
            var executables = new List<ExecutablePathItem>();
            var dataPaths = new List<DataPathItem>();

            // First add any custom paths we already have
            if (!string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath) &&
                !executables.Any(e => e.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)))
            {
                executables.Add(new ExecutablePathItem(ExecutablePath, "Current Custom Path", "Custom"));
            }
            if (!string.IsNullOrEmpty(DataPath) && Directory.Exists(DataPath) &&
                !dataPaths.Any(d => d.Path.Equals(DataPath, StringComparison.OrdinalIgnoreCase)))
            {
                dataPaths.Add(new DataPathItem(DataPath, "Current Custom Path", "Custom", false));
            }

            // Then add paths from versions
            foreach (var version in versions)
            {
                if (!string.IsNullOrEmpty(version.ExecutablePath) && File.Exists(version.ExecutablePath))
                {
                    if (!executables.Any(e => e.Path.Equals(version.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        executables.Add(new ExecutablePathItem(
                            version.ExecutablePath, 
                            $"{version.Name} ({version.SourceTypeName})", 
                            version.GameType ?? "Unknown"));
                    }
                }
                if (!string.IsNullOrEmpty(version.InstallPath) && Directory.Exists(version.InstallPath))
                {
                    if (!dataPaths.Any(d => d.Path.Equals(version.InstallPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        dataPaths.Add(new DataPathItem(
                            version.InstallPath, 
                            $"{version.Name} ({version.SourceTypeName})", 
                            version.GameType ?? "Unknown", 
                            _validInstallationTypes.Contains(version.SourceType)));
                    }
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logger.LogDebug("Populating dropdown collections - executables: {execCount}, dataPaths: {dataCount}", 
                    executables.Count, dataPaths.Count);
                
                AvailableExecutables.Clear();
                foreach (var item in executables.OrderBy(e => e.DisplayName != "Current Custom Path").ThenBy(e => e.DisplayName))
                    AvailableExecutables.Add(item);

                AvailableDataPaths.Clear();
                foreach (var item in dataPaths.OrderBy(d => d.DisplayName != "Current Custom Path").ThenBy(d => d.DisplayName))
                    AvailableDataPaths.Add(item);
            });
        }

        private void UpdateCanLaunchState()
        {
            CanLaunchGame = IsExecutableValid && IsDataPathValid && !string.IsNullOrEmpty(ExecutablePath);
            _logger.LogTrace("CanLaunchGame updated to: {CanLaunch}", CanLaunchGame);
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isInitializationInProgress)
                return;
                
            switch (e.PropertyName)
            {
                case nameof(Name):
                case nameof(Description):
                case nameof(CommandLineArguments):
                case nameof(RunAsAdmin):
                case nameof(SelectedVersion):
                case nameof(ColorValue):
                    IsProfileChanged = true;
                    break;
                case nameof(ExecutablePath):
                    ValidateExecutablePath();
                    UpdateCanLaunchState();
                    
                    if (!string.IsNullOrEmpty(ExecutablePath) && !_isUpdatingExecutableInternals)
                    {
                        _isUpdatingExecutableInternals = true;
                        try
                        {
                            var matchingExecutable = AvailableExecutables.FirstOrDefault(e => 
                                e.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase));
                                
                            if (matchingExecutable != null && 
                                (SelectedExecutable == null || !SelectedExecutable.Path.Equals(matchingExecutable.Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogDebug("Setting SelectedExecutable from ExecutablePath: {path}", ExecutablePath);
                                SelectedExecutable = matchingExecutable;
                            }
                        }
                        finally
                        {
                            _isUpdatingExecutableInternals = false;
                        }
                    }
                    break;
                
                case nameof(DataPath):
                    ValidateDataPath();
                    UpdateCanLaunchState();
                    
                    if (!string.IsNullOrEmpty(DataPath) && !_isUpdatingDataPathInternals)
                    {
                        _isUpdatingDataPathInternals = true;
                        try
                        {
                            var matchingDataPath = AvailableDataPaths.FirstOrDefault(d => 
                                d.Path.Equals(DataPath, StringComparison.OrdinalIgnoreCase));
                                
                            if (matchingDataPath != null && 
                                (SelectedDataPath == null || !SelectedDataPath.Path.Equals(matchingDataPath.Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogDebug("Setting SelectedDataPath from DataPath: {path}", DataPath);
                                SelectedDataPath = matchingDataPath;
                            }
                        }
                        finally
                        {
                            _isUpdatingDataPathInternals = false;
                        }
                    }
                    break;
                case nameof(IconPath):
                    UpdateSelectedIcon();
                    break;
                case nameof(CoverPath):
                    UpdateSelectedCover();
                    break;
            }
        }

        private void OnAvailableVersionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && _editingProfile != null &&
                !string.IsNullOrEmpty(_editingProfile.VersionId) && SelectedVersion == null)
            {
                SelectedVersion = AvailableVersions.FirstOrDefault(v => v.Id == _editingProfile.VersionId);
                if (SelectedVersion != null)
                    UpdateUIForSelectedVersion();
            }
        }

        private void ValidateExecutablePath()
        {
            IsExecutableValid = !string.IsNullOrEmpty(ExecutablePath) &&
                                File.Exists(ExecutablePath) &&
                                Path.GetExtension(ExecutablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateDataPath()
        {
            IsDataPathValid = !string.IsNullOrEmpty(DataPath) && Directory.Exists(DataPath);
        }

        private async Task ValidatePathsAsync()
        {
            await Task.Run(() => {
                ValidateExecutablePath();
                ValidateDataPath();
            });
        }

        private void UpdateUIForSelectedVersion()
        {
            if (SelectedVersion == null)
            {
                if (IsNewProfile)
                {
                    IconPath = _dataProvider.GetIconPathForGameType("Generals");
                    CoverPath = _dataProvider.GetCoverPathForGameType("Generals");
                    ColorValue = Core.Models.GameProfiles.ProfileThemeColor.GetColorForGameType("Generals");
                }
                SourceTypeName = "Custom";
                IsValidInstallationType = false;
                HasGitHubMetadata = false;
            }
            else
            {
                if ((IsNewProfile || string.IsNullOrEmpty(ExecutablePath)) &&
                    !string.IsNullOrEmpty(SelectedVersion.ExecutablePath) &&
                    File.Exists(SelectedVersion.ExecutablePath))
                {
                    ExecutablePath = SelectedVersion.ExecutablePath;
                }

                if ((IsNewProfile || string.IsNullOrEmpty(DataPath)) &&
                    !string.IsNullOrEmpty(SelectedVersion.InstallPath) &&
                    Directory.Exists(SelectedVersion.InstallPath))
                {
                    DataPath = SelectedVersion.InstallPath;
                }
                IsValidInstallationType = _validInstallationTypes.Contains(SelectedVersion.SourceType);

                var gameType = SelectedVersion.GameType ?? (SelectedVersion.IsZeroHour ? "Zero Hour" : "Generals");
                if (IsNewProfile || string.IsNullOrEmpty(IconPath))
                {
                    IconPath = _dataProvider.GetIconPathForGameType(gameType);
                }
                if (IsNewProfile || string.IsNullOrEmpty(CoverPath))
                {
                    CoverPath = _dataProvider.GetCoverPathForGameType(gameType);
                }
                if (IsNewProfile || ColorValue == "#2A2A2A" || string.IsNullOrEmpty(ColorValue))
                {
                    ColorValue = Core.Models.GameProfiles.ProfileThemeColor.GetColorForGameType(gameType);
                }

                if (SelectedVersion.IsFromGitHub && SelectedVersion.GitHubMetadata != null)
                {
                    _ = ApplyGitHubMetadataAsync(SelectedVersion.GitHubMetadata);
                }
                else
                {
                    HasGitHubMetadata = false;
                }
                SourceTypeName = SelectedVersion.SourceTypeName;
            }
            UpdateCanLaunchState();
        }

        private async Task ApplyGitHubMetadataAsync(GitHubSourceMetadata? metadata)
        {
            if (metadata == null)
            {
                _logger.LogDebug("No GitHub metadata to apply.");
                return;
            }

            string repoName = "Unknown Repository";
            string artifactName = "Unknown Artifact";

            // Correctly access repository and artifact information through the nested objects
            if (metadata.RepositoryInfo != null)
            {
                repoName = metadata.RepositoryInfo.DisplayName;
                if (string.IsNullOrEmpty(repoName))
                {
                    repoName = $"{metadata.RepositoryInfo.RepoOwner}/{metadata.RepositoryInfo.RepoName}";
                }
            }
            
            if (metadata.AssociatedArtifact != null)
            {
                artifactName = metadata.AssociatedArtifact.Name;
            }

            _logger.LogInformation("Applying GitHub metadata: {RepoName}/{ArtifactName}", repoName, artifactName);

            if (IsNewProfile && !string.IsNullOrWhiteSpace(artifactName))
            {
                Name = artifactName;
            }

            StatusMessage = $"GitHub info: {repoName} - {artifactName}";
            await Task.CompletedTask;
        }

        public void SetDialogWindow(Window window)
        {
            _dialogWindow = window;
            _logger.LogDebug("Dialog window set for GameProfileSettingsViewModel.");
        }

        [RelayCommand]
        private void Cancel()
        {
            _logger.LogInformation("Profile settings cancelled by user.");
            CompleteWithCancel();
        }

        private void CompleteWithSuccess()
        {
            if (DialogHasCompleted) return;
            DialogHasCompleted = true;
            _logger.LogDebug("Dialog completed with success");
            DialogConfirmed?.Invoke(this, EventArgs.Empty);
            CloseAction?.Invoke();
        }

        private void CompleteWithCancel()
        {
            if (DialogHasCompleted) return;
            DialogHasCompleted = true;
            _logger.LogDebug("Dialog completed with cancel");
            DialogCancelled?.Invoke(this, EventArgs.Empty);
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void RandomizeColor()
        {
            var random = new Random();
            // Generate colors in the medium-to-bright range for better visibility
            byte r = (byte)random.Next(100, 240);
            byte g = (byte)random.Next(100, 240);
            byte b = (byte)random.Next(100, 240);
            
            ColorValue = $"#{r:X2}{g:X2}{b:X2}";
            
            // Force UI refresh
            OnHeaderPropertyChanged();
            IsProfileChanged = true;
            
            _logger.LogDebug("Generated random color: {ColorValue}", ColorValue);
        }

        [RelayCommand]
        private async Task BrowseCustomIcon()
        {
            try
            {
                if (_ownerWindow == null)
                {
                    _logger.LogWarning("Cannot browse for custom icon - no owner window provided");
                    return;
                }
                
                var dialog = new OpenFileDialog
                {
                    Title = "Select Custom Icon",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "Image Files", Extensions = { "png", "jpg", "jpeg", "ico" } },
                        new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                    }
                };
                
                var result = await dialog.ShowAsync(_ownerWindow);
                if (result != null && result.Length > 0)
                {
                    var iconFilePath = result[0];
                    
                    // Add the custom icon and get the resource path
                    var resourcePath = await _dataProvider.AddCustomIconAsync(iconFilePath);
                    
                    if (!string.IsNullOrEmpty(resourcePath))
                    {
                        IconPath = resourcePath;
                        
                        // Refresh icons to include the new one
                        var icons = await _dataProvider.GetAvailableIconsAsync();
                        
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            AvailableIcons.Clear();
                            foreach (var icon in icons)
                            {
                                AvailableIcons.Add(icon);
                            }
                            
                            // Select the new icon
                            UpdateSelectedIcon();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing for custom icon");
                StatusMessage = $"Error browsing for custom icon: {ex.Message}";
                
                await _dialogService.ShowMessageBoxAsync(
                    "Error",
                    $"Failed to add custom icon: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        [RelayCommand]
        private async Task BrowseCustomCover()
        {
            try
            {
                if (_ownerWindow == null)
                {
                    _logger.LogWarning("Cannot browse for custom cover - no owner window provided");
                    return;
                }
                
                var dialog = new OpenFileDialog
                {
                    Title = "Select Custom Cover Image",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "Image Files", Extensions = { "png", "jpg", "jpeg" } },
                        new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                    }
                };
                
                var result = await dialog.ShowAsync(_ownerWindow);
                if (result != null && result.Length > 0)
                {
                    var coverFilePath = result[0];
                    
                    // Add the custom cover and get the resource path
                    var resourcePath = await _dataProvider.AddCustomCoverAsync(coverFilePath);
                    
                    if (!string.IsNullOrEmpty(resourcePath))
                    {
                        CoverPath = resourcePath;
                        
                        // Refresh covers to include the new one
                        var covers = await _dataProvider.GetAvailableCoverImagesAsync();
                        
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            AvailableCovers.Clear();
                            foreach (var cover in covers)
                            {
                                AvailableCovers.Add(cover);
                            }
                            
                            // Select the new cover
                            UpdateSelectedCover();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing for custom cover");
                StatusMessage = $"Error browsing for custom cover: {ex.Message}";
                
                await _dialogService.ShowMessageBoxAsync(
                    "Error",
                    $"Failed to add custom cover: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        [RelayCommand]
        private void SelectIcon(ProfileResourceItem icon)
        {
            if (icon == null)
                return;
                
            IconPath = icon.Path;
            SelectedIcon = icon; // Set the selected item in the dropdown
            
            // Force UI refresh
            OnHeaderPropertyChanged();
            IsProfileChanged = true;
            
            _logger.LogDebug("Selected icon: {IconPath}", IconPath);
        }

        [RelayCommand]
        private void SelectCover(ProfileResourceItem cover)
        {
            if (cover == null)
                return;
                
            CoverPath = cover.Path;
            SelectedCover = cover; // Set the selected item in the dropdown
            
            // Force UI refresh
            OnHeaderPropertyChanged();
            IsProfileChanged = true;
            
            _logger.LogDebug("Selected cover: {CoverPath}", CoverPath);
        }

        [RelayCommand]
        private void SelectThemeColor(string colorHexString)
        {
            if (!string.IsNullOrEmpty(colorHexString))
            {
                // Update the color value which will update bindings immediately
                ColorValue = colorHexString;
                
                // Force UI refresh
                OnHeaderPropertyChanged();
                IsProfileChanged = true;
                
                _logger.LogDebug("Theme color selected: {ColorValue}", ColorValue);
            }
            else
            {
                _logger.LogWarning("SelectThemeColor called with null or empty color string.");
            }
        }

        [RelayCommand]
        private async Task ScanForVersions()
        {
            StatusMessage = "Scanning for game versions...";
            
            try
            {
                IsInitializing = true;
                
                // Clear and reload available versions
                var versions = await _dataProvider.GetAvailableVersionsAsync();
                
                await Dispatcher.UIThread.InvokeAsync(() => {
                    AvailableVersions.Clear();
                    foreach (var version in versions ?? Array.Empty<GameVersion>())
                    {
                        AvailableVersions.Add(version);
                    }
                });
                
                // Reload executable and data paths from versions
                await PopulatePathsFromVersions(versions);
                
                StatusMessage = $"Found {versions?.Count() ?? 0} game versions.";
                _logger.LogInformation("Scanned for game versions, found {0}", versions?.Count() ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for game versions");
                StatusMessage = $"Error scanning for versions: {ex.Message}";
                
                await _dialogService.ShowMessageBoxAsync(
                    "Error", 
                    $"Failed to scan for game versions: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                IsInitializing = false;
            }
        }

        [RelayCommand]
        private void SelectVersion(GameVersion version)
        {
            if (version == null)
                return;
                
            SelectedVersion = version;
            UpdateUIForSelectedVersion();
            
            _logger.LogDebug("Selected game version: {VersionId}", version.Id);
            StatusMessage = $"Selected version: {version.Name}";
        }

        [RelayCommand]
        private async Task BrowseExecutable()
        {
            try
            {
                if (_ownerWindow == null)
                {
                    _logger.LogWarning("Cannot browse for executable - no owner window provided");
                    return;
                }
                
                var dialog = new OpenFileDialog
                {
                    Title = "Select Game Executable",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "Executable Files", Extensions = { "exe" } },
                        new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                    }
                };
                
                // Set initial directory if we have a valid data path
                if (!string.IsNullOrEmpty(DataPath) && Directory.Exists(DataPath))
                {
                    dialog.Directory = DataPath;
                }
                else if (!string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath))
                {
                    dialog.Directory = Path.GetDirectoryName(ExecutablePath);
                }
                
                var result = await dialog.ShowAsync(_ownerWindow);
                if (result != null && result.Length > 0)
                {
                    ExecutablePath = result[0];
                    
                    // Also update data path if it's empty
                    if (string.IsNullOrEmpty(DataPath))
                    {
                        DataPath = Path.GetDirectoryName(ExecutablePath) ?? string.Empty;
                    }
                    
                    // Validate paths
                    await ValidatePathsAsync();
                    
                    // Update can launch state
                    UpdateCanLaunchState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing for executable");
                StatusMessage = $"Error browsing for executable: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task BrowseDataPath()
        {
            try
            {
                if (_ownerWindow == null)
                {
                    _logger.LogWarning("Cannot browse for data path - no owner window provided");
                    return;
                }
                
                var dialog = new OpenFolderDialog
                {
                    Title = "Select Game Data Directory"
                };
                
                // Set initial directory if we have a valid data path
                if (!string.IsNullOrEmpty(DataPath) && Directory.Exists(DataPath))
                {
                    dialog.Directory = DataPath;
                }
                else if (!string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath))
                {
                    dialog.Directory = Path.GetDirectoryName(ExecutablePath);
                }
                
                var result = await dialog.ShowAsync(_ownerWindow);
                if (!string.IsNullOrEmpty(result))
                {
                    DataPath = result;
                    
                    // Validate paths
                    await ValidatePathsAsync();
                    
                    // Update can launch state
                    UpdateCanLaunchState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing for data path");
                StatusMessage = $"Error browsing for data path: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ValidateProfile()
        {
            StatusMessage = "Validating profile...";
            
            try
            {
                // Validate paths
                await ValidatePathsAsync();
                
                // Update can launch state
                UpdateCanLaunchState();
                
                // Show validation result
                if (!IsExecutableValid)
                {
                    StatusMessage = "Invalid executable path.";
                    
                    await _dialogService.ShowMessageBoxAsync(
                        "Validation Error", 
                        "The executable path is invalid. Please select a valid .exe file.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else if (!IsDataPathValid)
                {
                    StatusMessage = "Invalid data directory path.";
                    
                    await _dialogService.ShowMessageBoxAsync(
                        "Validation Error", 
                        "The data directory path is invalid. Please select a valid directory.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    StatusMessage = "Profile validation succeeded.";
                    
                    await _dialogService.ShowMessageBoxAsync(
                        "Validation Success", 
                        "All paths are valid.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating profile");
                StatusMessage = $"Error during validation: {ex.Message}";
                
                await _dialogService.ShowMessageBoxAsync(
                    "Validation Error", 
                    $"Error during validation: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        [RelayCommand]
        private async Task TestLaunch()
        {
            if (IsTesting || !CanLaunchGame)
                return;
                
            IsTesting = true;
            StatusMessage = "Testing game launch...";
            
            try
            {
                // Create a temporary profile with current settings
                var tempProfile = _editingProfile?.Clone() ?? new GameProfile();
                
                // Update profile with current values
                tempProfile.Name = Name;
                tempProfile.ExecutablePath = ExecutablePath;
                tempProfile.DataPath = DataPath;
                tempProfile.CommandLineArguments = CommandLineArguments;
                tempProfile.RunAsAdmin = RunAsAdmin;
                
                // Update version ID if selected
                if (SelectedVersion != null)
                {
                    tempProfile.VersionId = SelectedVersion.Id;
                }
                
                // Launch the game
                var result = await _launcherService.LaunchVersionAsync(tempProfile);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully tested game launch");
                    StatusMessage = "Game launched successfully.";
                }
                else
                {
                    _logger.LogWarning("Game launch test failed: {Error}", result.ErrorMessage);
                    StatusMessage = $"Game launch failed: {result.ErrorMessage}";
                    
                    await _dialogService.ShowMessageBoxAsync(
                        "Launch Failed", 
                        $"Failed to launch game: {result.ErrorMessage}",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing game launch");
                StatusMessage = $"Error testing game launch: {ex.Message}";
                
                await _dialogService.ShowMessageBoxAsync(
                    "Launch Error", 
                    $"Error testing game launch: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (IsSaving)
                return;
                
            IsSaving = true;
            StatusMessage = "Saving profile...";
            
            try
            {
                // Validate name
                if (string.IsNullOrWhiteSpace(Name))
                {
                    await _dialogService.ShowMessageBoxAsync(
                        "Validation Error", 
                        "Profile name cannot be empty.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                        
                    IsSaving = false;
                    return;
                }
                
                // Validate executable exists
                if (!IsExecutableValid)
                {
                    // Ask user if they want to continue despite invalid executable
                    var result = await _dialogService.ShowMessageBoxAsync(
                        "Invalid Executable",
                        "The selected executable path is not valid. Do you want to save anyway?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                        
                    if (result != MessageBoxResult.Yes)
                    {
                        IsSaving = false;
                        return;
                    }
                }
                
                // Create a new profile or update existing one
                var profile = _editingProfile ?? new GameProfile();
                
                // Update profile with current values
                profile.Name = Name;
                profile.Description = Description;
                profile.ExecutablePath = ExecutablePath;
                profile.DataPath = DataPath;
                profile.IconPath = IconPath;
                profile.CoverImagePath = CoverPath;
                profile.CommandLineArguments = CommandLineArguments;
                profile.ColorValue = ColorValue;
                profile.RunAsAdmin = RunAsAdmin;
                
                // Update version ID if selected
                if (SelectedVersion != null)
                {
                    profile.VersionId = SelectedVersion.Id;
                    
                    // Update source type based on selected version
                    profile.SourceType = SelectedVersion.SourceType;
                    
                    // If this is a GitHub version, make sure metadata is transferred
                    if (SelectedVersion.IsFromGitHub && SelectedVersion.GitHubMetadata != null)
                    {
                        profile.SourceSpecificMetadata = SelectedVersion.GitHubMetadata.Clone();
                    }
                }
                
                // Set required fields if they're empty
                if (string.IsNullOrEmpty(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString();
                }
                
                if (string.IsNullOrEmpty(profile.IconPath))
                {
                    var gameType = SelectedVersion?.GameType ?? "Generals";
                    profile.IconPath = _dataProvider.GetIconPathForGameType(gameType);
                }
                
                if (string.IsNullOrEmpty(profile.CoverImagePath))
                {
                    var gameType = SelectedVersion?.GameType ?? "Generals";
                    profile.CoverImagePath = _dataProvider.GetCoverPathForGameType(gameType);
                }
                
                // Set profile as custom profile if it's not already marked
                profile.IsCustomProfile = true;
                
                // Extract GitHub metadata if available
                if (profile.GitHubMetadata == null)
                {
                    _metadataService.ExtractGitHubInfo(profile);
                }
                
                // Save the profile
                await _profileManager.SaveProfileAsync(profile);
                
                _logger.LogInformation("Profile saved successfully: {ProfileName}", profile.Name);
                StatusMessage = "Profile saved successfully.";
                
                // Signal success and close the window
                CompleteWithSuccess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profile");
                StatusMessage = $"Error saving profile: {ex.Message}";
                
                // Show error message
                await _dialogService.ShowMessageBoxAsync(
                    "Error", 
                    $"Failed to save profile: {ex.Message}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task ExecuteCancel()
        {
            _logger.LogInformation("Profile settings cancelled by user.");
            
            if (IsProfileChanged)
            {
                var result = await _dialogService.ShowConfirmationDialogAsync(
                    "Unsaved Changes",
                    "You have unsaved changes. Are you sure you want to cancel and discard them?",
                    "Discard", "Keep Editing");
                
                if (result == MessageBoxResult.Yes)
                {
                    CompleteWithCancel();
                }
            }
            else
            {
                CompleteWithCancel();
            }
        }

        public record ExecutablePathItem(string Path, string DisplayName, string GameType)
        {
            public override string ToString() => $"{DisplayName} ({Path})";
        }

        public record DataPathItem(string Path, string DisplayName, string GameType, bool IsValidSource)
        {
            public override string ToString() => $"{DisplayName} ({Path})";
        }
    }
}
