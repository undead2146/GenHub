using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Models.GameProfile;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for a single game profile item.
/// </summary>
public partial class GameProfileItemViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets the name of the game profile.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Gets or sets the icon path.
    /// </summary>
    [ObservableProperty]
    private string _iconPath;

    /// <summary>
    /// Gets or sets the cover path.
    /// </summary>
    [ObservableProperty]
    private string _coverPath;

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    [ObservableProperty]
    private string _version;

    /// <summary>
    /// Gets or sets the executable path.
    /// </summary>
    [ObservableProperty]
    private string _executablePath;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [ObservableProperty]
    private string? _description;

    /// <summary>
    /// Gets or sets the color value.
    /// </summary>
    [ObservableProperty]
    private string? _colorValue;

    /// <summary>
    /// Gets or sets the cover image path.
    /// </summary>
    [ObservableProperty]
    private string? _coverImagePath;

    /// <summary>
    /// Gets or sets the profile ID.
    /// </summary>
    [ObservableProperty]
    private string _profileId;

    /// <summary>
    /// Gets or sets the version ID.
    /// </summary>
    [ObservableProperty]
    private string? _versionId;

    /// <summary>
    /// Gets or sets the source type name.
    /// </summary>
    [ObservableProperty]
    private string? _sourceTypeName;

    /// <summary>
    /// Gets or sets a value indicating whether workflow info is present.
    /// </summary>
    [ObservableProperty]
    private bool _hasWorkflowInfo;

    /// <summary>
    /// Gets or sets the workflow number.
    /// </summary>
    [ObservableProperty]
    private int _workflowNumber;

    /// <summary>
    /// Gets or sets the pull request number.
    /// </summary>
    [ObservableProperty]
    private int _pullRequestNumber;

    /// <summary>
    /// Gets or sets the commit SHA.
    /// </summary>
    [ObservableProperty]
    private string? _commitSha;

    /// <summary>
    /// Gets or sets the short commit SHA.
    /// </summary>
    [ObservableProperty]
    private string? _shortCommitSha;

    /// <summary>
    /// Gets or sets the build info.
    /// </summary>
    [ObservableProperty]
    private object? _buildInfo;

    /// <summary>
    /// Gets or sets the display compiler.
    /// </summary>
    [ObservableProperty]
    private string? _displayCompiler;

    /// <summary>
    /// Gets or sets the display configuration.
    /// </summary>
    [ObservableProperty]
    private string? _displayConfiguration;

    /// <summary>
    /// Gets or sets the build preset.
    /// </summary>
    [ObservableProperty]
    private string? _buildPreset;

    /// <summary>
    /// Gets or sets a value indicating whether to run as administrator.
    /// </summary>
    [ObservableProperty]
    private bool _runAsAdmin;

    /// <summary>
    /// Gets or sets the command line arguments.
    /// </summary>
    [ObservableProperty]
    private string? _commandLineArguments;

    /// <summary>
    /// Gets or sets the launch command.
    /// </summary>
    [ObservableProperty]
    private string? _launchCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfileItemViewModel"/> class.
    /// </summary>
    /// <param name="profileId">The profile ID.</param>
    /// <param name="profile">The game profile.</param>
    /// <param name="iconPath">The icon path.</param>
    /// <param name="coverPath">The cover path.</param>
    public GameProfileItemViewModel(string profileId, IGameProfile profile, string iconPath, string coverPath)
    {
        _profileId = profileId;
        _name = profile.Name;
        _version = profile.Version;
        _executablePath = profile.ExecutablePath;

        // Handle icon path with fallback
        _iconPath = !string.IsNullOrEmpty(iconPath)
            ? iconPath
            : "avares://GenHub/Assets/Icons/generalshub-icon.png";

        // Handle cover path with fallback to icon
        _coverPath = !string.IsNullOrEmpty(coverPath)
            ? coverPath
            : _iconPath;

        // Set cover image path (for UI binding)
        _coverImagePath = _coverPath;

        // Generate description with game type information
        var gameTypeName = profile.GameClient?.GameType.ToString() ?? "Unknown";
        _description = $"{gameTypeName} {profile.Version}";

        // Set color value with fallback - check if profile has ThemeColor property
        _colorValue = profile is GameProfile gameProfile && !string.IsNullOrEmpty(gameProfile.ThemeColor)
            ? gameProfile.ThemeColor
            : "#2A2A2A";

        // Set additional properties with safe defaults
        _sourceTypeName = profile.GameClient?.GameType.ToString() ?? "Unknown";
        _hasWorkflowInfo = false;
        _workflowNumber = 0;
        _pullRequestNumber = 0;
        _commitSha = string.Empty;
        _shortCommitSha = string.Empty;
        _buildInfo = null; // No build info for regular profiles
        _displayCompiler = string.Empty;
        _displayConfiguration = string.Empty;
        _buildPreset = string.Empty;
        _runAsAdmin = false;
        _commandLineArguments = string.Empty;
        _launchCommand = string.Empty;

        // Initialize BuildInfo and HasBuildInfo
        BuildInfo = profile.BuildInfo ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether this profile has build information.
    /// </summary>
    public bool HasBuildInfo => !string.IsNullOrEmpty(BuildInfo as string);
}
