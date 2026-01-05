namespace GenHub.Windows.Features.ActionSets.Fixes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Windows.Features.ActionSets.Infrastructure;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fix for EA App registry keys which are often missing or incorrect.
/// </summary>
/// <param name="registryService">The registry service.</param>
/// <param name="logger">The logger instance.</param>
public class EAAppRegistryFix(IRegistryService registryService, ILogger<EAAppRegistryFix> logger) : BaseActionSet(logger)
{
    private readonly IRegistryService _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));

    /// <inheritdoc/>
    public override string Id => "EAAppRegistryFix";

    /// <inheritdoc/>
    public override string Title => "EA App Registry Fix";

    /// <inheritdoc/>
    public override bool IsCoreFix => true;

    /// <inheritdoc/>
    public override bool IsCrucialFix => true;

    /// <inheritdoc/>
    public override Task<bool> IsApplicableAsync(GameInstallation installation)
    {
        // Strictly only for EA App or unknown types that we want to force-fix registry for.
        if (installation.InstallationType != GameInstallationType.EaApp && installation.InstallationType != GameInstallationType.Unknown)
        {
            return Task.FromResult(false);
        }

        // Applicable if keys are missing or point to wrong location
        bool fixNeeded = false;

        if (installation.HasGenerals)
        {
            var installPath = _registryService.GetStringValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.InstallPathValueName);
            var version = _registryService.GetIntValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.VersionValueName);
            var serial = _registryService.GetStringValue(RegistryConstants.EAAppGeneralsErgcKeyPath, string.Empty); // Default value name is empty string

            if (!string.Equals(installPath, installation.GeneralsPath, StringComparison.OrdinalIgnoreCase) ||
                version != RegistryConstants.GeneralsVersionDWord ||
                string.IsNullOrEmpty(serial))
            {
                fixNeeded = true;
            }
        }

        if (installation.HasZeroHour)
        {
            var installPath = _registryService.GetStringValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.InstallPathValueName);
            var version = _registryService.GetIntValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.VersionValueName);
            var serial = _registryService.GetStringValue(RegistryConstants.EAAppZeroHourErgcKeyPath, string.Empty);

            if (!string.Equals(installPath, installation.ZeroHourPath, StringComparison.OrdinalIgnoreCase) ||
                version != RegistryConstants.ZeroHourVersionDWord ||
                string.IsNullOrEmpty(serial))
            {
                fixNeeded = true;
            }
        }

        return Task.FromResult(fixNeeded);
    }

    /// <inheritdoc/>
    public override Task<bool> IsAppliedAsync(GameInstallation installation)
    {
        if (installation.HasGenerals)
        {
            var installPath = _registryService.GetStringValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.InstallPathValueName);
            var version = _registryService.GetIntValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.VersionValueName);
            var serial = _registryService.GetStringValue(RegistryConstants.EAAppGeneralsErgcKeyPath, string.Empty);

            if (!string.Equals(installPath, installation.GeneralsPath, StringComparison.OrdinalIgnoreCase) ||
                version != RegistryConstants.GeneralsVersionDWord ||
                string.IsNullOrEmpty(serial))
            {
                return Task.FromResult(false);
            }
        }

        if (installation.HasZeroHour)
        {
            var installPath = _registryService.GetStringValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.InstallPathValueName);
            var version = _registryService.GetIntValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.VersionValueName);
            var serial = _registryService.GetStringValue(RegistryConstants.EAAppZeroHourErgcKeyPath, string.Empty);

            if (!string.Equals(installPath, installation.ZeroHourPath, StringComparison.OrdinalIgnoreCase) ||
                version != RegistryConstants.ZeroHourVersionDWord ||
                string.IsNullOrEmpty(serial))
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    protected override Task<ActionSetResult> ApplyInternalAsync(GameInstallation installation, CancellationToken ct)
    {
        // Check if running as administrator - required for HKEY_LOCAL_MACHINE writes
        if (!_registryService.IsRunningAsAdministrator())
        {
            return Task.FromResult(Failure("Administrator privileges are required to modify registry keys. Please restart GenHub as administrator."));
        }

        try
        {
            bool allSucceeded = true;
            var failedOperations = new List<string>();

            if (installation.HasGenerals)
            {
                if (!_registryService.SetStringValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.InstallPathValueName, installation.GeneralsPath))
                {
                    allSucceeded = false;
                    failedOperations.Add($"{RegistryConstants.EAAppGeneralsKeyPath}\\{RegistryConstants.InstallPathValueName}");
                }

                if (!_registryService.SetIntValue(RegistryConstants.EAAppGeneralsKeyPath, RegistryConstants.VersionValueName, RegistryConstants.GeneralsVersionDWord))
                {
                    allSucceeded = false;
                    failedOperations.Add($"{RegistryConstants.EAAppGeneralsKeyPath}\\{RegistryConstants.VersionValueName}");
                }

                // Ensure serial key exists. If missing, we might need to prompt or set a placeholder.
                // For now, if missing, we just ensure the KEY exists, maybe with a specialized placeholder if totally absent?
                // Actually, without a valid serial, online play fails, but game *launches* usually.
                // We'll trust that the user has a serial or we define a placeholder if completely missing.
                var existingSerial = _registryService.GetStringValue(RegistryConstants.EAAppGeneralsErgcKeyPath, string.Empty);
                if (string.IsNullOrEmpty(existingSerial))
                {
                    // Placeholder or generic serial could go here, but for legal reasons usually we don't distribute serials.
                    // We will just create the key with an empty string if it's null, or leave it.
                    // Actually, the main issue is usually the KEY structure missing.
                    if (!_registryService.SetStringValue(RegistryConstants.EAAppGeneralsErgcKeyPath, string.Empty, existingSerial ?? string.Empty))
                    {
                        allSucceeded = false;
                        failedOperations.Add($"{RegistryConstants.EAAppGeneralsErgcKeyPath}\\(Default)");
                    }
                }
            }

            if (installation.HasZeroHour)
            {
                if (!_registryService.SetStringValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.InstallPathValueName, installation.ZeroHourPath))
                {
                    allSucceeded = false;
                    failedOperations.Add($"{RegistryConstants.EAAppZeroHourKeyPath}\\{RegistryConstants.InstallPathValueName}");
                }

                if (!_registryService.SetIntValue(RegistryConstants.EAAppZeroHourKeyPath, RegistryConstants.VersionValueName, RegistryConstants.ZeroHourVersionDWord))
                {
                    allSucceeded = false;
                    failedOperations.Add($"{RegistryConstants.EAAppZeroHourKeyPath}\\{RegistryConstants.VersionValueName}");
                }

                var existingSerial = _registryService.GetStringValue(RegistryConstants.EAAppZeroHourErgcKeyPath, string.Empty);
                if (string.IsNullOrEmpty(existingSerial))
                {
                    if (!_registryService.SetStringValue(RegistryConstants.EAAppZeroHourErgcKeyPath, string.Empty, existingSerial ?? string.Empty))
                    {
                        allSucceeded = false;
                        failedOperations.Add($"{RegistryConstants.EAAppZeroHourErgcKeyPath}\\(Default)");
                    }
                }
            }

            if (!allSucceeded)
            {
                return Task.FromResult(Failure($"Failed to write the following registry keys: {string.Join(", ", failedOperations)}. Ensure you are running as administrator."));
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
        // Undoing registry fixes is tricky - usually we don't want to revert to a broken state.
        return Task.FromResult(Success());
    }
}
