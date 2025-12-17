namespace GenHub.Core.Constants;

/// <summary>
/// Build information constants populated at compile time via MSBuild.
/// These values are set during CI builds and remain empty for local development builds.
/// </summary>
public static class BuildInfoConstants
{
    /// <summary>
    /// Short git commit hash (7 chars). Empty for local/dev builds.
    /// Set via MSBuild property: -p:GitShortHash=abc1234.
    /// </summary>
    public const string GitShortHash = "";

    /// <summary>
    /// PR number if from a PR build, otherwise empty.
    /// Set via MSBuild property: -p:PullRequestNumber=123.
    /// </summary>
    public const string PullRequestNumber = "";

    /// <summary>
    /// Build channel identifier: "Release", "PR", "CI", or "Dev".
    /// Set via MSBuild property: -p:BuildChannel=PR.
    /// </summary>
    public const string BuildChannel = "Dev";

    /// <summary>
    /// Gets a value indicating whether this is a CI/CD build (has git hash).
    /// </summary>
    public static bool IsCiBuild => !string.IsNullOrEmpty(GitShortHash);

    /// <summary>
    /// Gets a value indicating whether this is a PR build.
    /// </summary>
    public static bool IsPrBuild => !string.IsNullOrEmpty(PullRequestNumber);

    /// <summary>
    /// Gets the PR number as an integer, or null if not a PR build.
    /// </summary>
    public static int? PullRequestNumberValue =>
        int.TryParse(PullRequestNumber, out var pr) ? pr : null;
}
