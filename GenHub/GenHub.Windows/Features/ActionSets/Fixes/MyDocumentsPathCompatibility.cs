namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix for My Documents path compatibility issues (e.g. non-English characters or double backslashes).
/// </summary>
public class MyDocumentsPathCompatibility : BaseActionSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyDocumentsPathCompatibility"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public MyDocumentsPathCompatibility(ILogger<MyDocumentsPathCompatibility> logger)
        : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string Id => "MyDocumentsPathCompatibility";

    /// <inheritdoc/>
    public override string Title => "My Documents Path Compatibility";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // This fix applies to the User Profile / Documents path, not the game installation itself.
        // But we check it in context of an installation being present.
        if (!installation.HasGenerals && !installation.HasZeroHour)
        {
            return Task.FromResult(false);
        }

        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Task.FromResult(!IsValidPath(documentsPath));
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Task.FromResult(IsValidPath(documentsPath));
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        // We cannot automatically move the Documents folder as it requires user interaction/OS configuration.
        // We return a failure with a descriptive message to prompt the user.
        // In the future, we might implement a symlink workaround similar to OneDriveFix here too,
        // but for now, we flag it.
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Task.FromResult(Failure($"Your 'Documents' path '{documentsPath}' contains incomplete characters that crash the game. Please move your Documents folder to a path with only English characters (e.g. C:\\Users\\Name\\Documents)."));
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        return Task.FromResult(Success());
    }

    private bool IsValidPath(string path)
    {
        // Check for double backslashes (excluding the initial network share start if applicable, but usually strictly local)
        // AHK logic: if(InStr(Path, "\\")) return 0
        // C# Path.GetFullPath handles normalization, but if the string *source* has \\ it might be an issue for the game engine.
        if (path.Contains("\\\\"))
        {
            return false;
        }

        // Allowed chars: A-Z, 0-9, space, and specific symbols: `~!@#$%^&()_+-='{}.,;[]
        // AHK logic replaces these out and checks if anything remains.
        // We can use Regex to check if *any* character is NOT in the allowed set.
        // Note: Backslash \ and Colon : are allowed for drive paths e.g. C:\
        // Regex for disallowed characters: [^a-zA-Z0-9 `~!@#$%^&()_+\-='{}\.,;\[\]\:\\]
        // If match found, return false.
        return !Regex.IsMatch(path, @"[^a-zA-Z0-9 `~!@#$%^&()_+\-='{}\.,;\[\]\:\\]");
    }
}
