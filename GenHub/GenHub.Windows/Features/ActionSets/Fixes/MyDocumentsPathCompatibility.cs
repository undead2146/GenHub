namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.IO;
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
        _logger = logger;
    }

    private readonly ILogger<MyDocumentsPathCompatibility> _logger;
    private readonly string _markerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GenHub", "sub_markers", "MyDocumentsPathCompatibility.done");

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

        if (File.Exists(_markerPath)) return Task.FromResult(true);

        // If valid, return false so it shows as NOT APPLICABLE instead of APPLIED
        if (IsValidPath(documentsPath)) return Task.FromResult(false);
        return Task.FromResult(false); // If invalid, it's NOT applied yet.
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        // We cannot automatically move the Documents folder as it requires user interaction/OS configuration.
        // We return a failure with a descriptive message to prompt the user.
        // In the future, we might implement a symlink workaround similar to OneDriveFix here too,
        // but for now, we flag it.
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Since we can't auto-fix, if the user clicked Apply, we assume they saw the message.
        // We mark it as applied so it doesn't stay blue/red forever.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_markerPath)!);
            File.WriteAllText(_markerPath, DateTime.UtcNow.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create marker file for MyDocumentsPathCompatibility");
        }

        // We still return failure message to warn them, but next time it will be Green.
        // Actually, if we return Failure, the UI might show Red X.
        // But IsApplied will be true next check.
        return Task.FromResult(Failure($"Your 'Documents' path '{documentsPath}' contains incomplete characters. Please move your Documents folder manually. Marked as acknowledged."));
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
