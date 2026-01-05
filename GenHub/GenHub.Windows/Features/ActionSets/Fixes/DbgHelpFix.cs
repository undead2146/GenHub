namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.GameInstallations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix for the dbghelp.dll which causes crashes on modern systems.
/// </summary>
public class DbgHelpFix(ILogger<DbgHelpFix> logger) : BaseActionSet(logger)
{
    // Use constants from GameClientConstants
    private const string DbgHelpDll = GameClientConstants.DbgHelpDll;
    private const string DbgHelpDllBak = GameClientConstants.DbgHelpDllBak;

    /// <inheritdoc/>
    public override string Id => "DbgHelpFix";

    /// <inheritdoc/>
    public override string Title => "Debug Help DLL Fix";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // Applicable if the file exists in either Generals or Zero Hour path
        // This fix is needed because the old dbghelp.dll causes crashes on modern Windows
        if (installation.HasGenerals && File.Exists(Path.Combine(installation.GeneralsPath, DbgHelpDll)))
        {
            return Task.FromResult(true);
        }

        if (installation.HasZeroHour && File.Exists(Path.Combine(installation.ZeroHourPath, DbgHelpDll)))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        // Considered applied if the DLL is missing (renamed) in all present installations
        bool generalsOk = !installation.HasGenerals || !File.Exists(Path.Combine(installation.GeneralsPath, DbgHelpDll));
        bool zeroHourOk = !installation.HasZeroHour || !File.Exists(Path.Combine(installation.ZeroHourPath, DbgHelpDll));

        return Task.FromResult(generalsOk && zeroHourOk);
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        try
        {
            if (installation.HasGenerals)
            {
                RenameDll(installation.GeneralsPath);
            }

            if (installation.HasZeroHour)
            {
                RenameDll(installation.ZeroHourPath);
            }

            return Task.FromResult(Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure(ex.Message));
        }
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> UndoInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        try
        {
            if (installation.HasGenerals)
            {
                RestoreDll(installation.GeneralsPath);
            }

            if (installation.HasZeroHour)
            {
                RestoreDll(installation.ZeroHourPath);
            }

            return Task.FromResult(Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure(ex.Message));
        }
    }

    private static void RenameDll(string path)
    {
        var dllPath = Path.Combine(path, DbgHelpDll);
        var bakPath = Path.Combine(path, DbgHelpDllBak);

        if (File.Exists(dllPath))
        {
            if (File.Exists(bakPath))
            {
                File.Delete(bakPath);
            }

            File.Move(dllPath, bakPath);
        }
    }

    private static void RestoreDll(string path)
    {
        var dllPath = Path.Combine(path, DbgHelpDll);
        var bakPath = Path.Combine(path, DbgHelpDllBak);

        if (File.Exists(bakPath))
        {
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
            }

            File.Move(bakPath, dllPath);
        }
    }
}
