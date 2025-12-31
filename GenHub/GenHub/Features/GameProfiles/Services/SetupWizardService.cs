using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.GameProfile;
using GenHub.Features.Content.Services.CommunityOutpost;
using GenHub.Features.Content.Services.GeneralsOnline;
using GenHub.Features.Content.Services.Publishers;
using GenHub.Features.GameProfiles.ViewModels.Wizard;
using GenHub.Features.GameProfiles.Views.Wizard;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameProfiles.Services;

/// <summary>
/// Service for running the Setup Wizard to handle detected game content.
/// </summary>
public class SetupWizardService(
    IGameClientProfileService gameClientProfileService,
    CommunityOutpostDiscoverer communityOutpostDiscoverer,
    GeneralsOnlineDiscoverer generalsOnlineDiscoverer,
    SuperHackersProvider superHackersProvider,
    ILogger<SetupWizardService> logger) : ISetupWizardService
{
    /// <inheritdoc/>
    public async Task<SetupWizardResult> RunSetupWizardAsync(IEnumerable<GameInstallation> installations, CancellationToken cancellationToken = default)
    {
        var installationsList = installations.ToList();
        var result = new SetupWizardResult();

        // 1. Determine Scenarios for each component across all installations
        var cpGlobal = installationsList.Select(inst => new { Inst = inst, Client = inst.AvailableGameClients.FirstOrDefault(c => c.PublisherType == CommunityOutpostConstants.PublisherType) }).Where(x => x.Client != null).ToList();
        var goGlobal = installationsList.Select(inst => new { Inst = inst, Client = inst.AvailableGameClients.FirstOrDefault(c => c.PublisherType == PublisherTypeConstants.GeneralsOnline) }).Where(x => x.Client != null).ToList();
        var shGlobal = installationsList.Select(inst => new { Inst = inst, Client = inst.AvailableGameClients.FirstOrDefault(c => c.PublisherType == PublisherTypeConstants.TheSuperHackers) }).Where(x => x.Client != null).ToList();

        // 2. Collection Phase: Build Wizard Items
        var wizardItems = new List<SetupWizardItemViewModel>();

        // Pre-fetch latest versions
        var cpLatestVersion = await GetLatestVersionAsync(CommunityOutpostConstants.PublisherType);
        var goLatestVersion = await GetLatestVersionAsync(PublisherTypeConstants.GeneralsOnline);
        var shLatestVersion = await GetLatestVersionAsync(PublisherTypeConstants.TheSuperHackers);

        // Community Patch Item
        if (cpGlobal.Count > 0)
        {
            bool profilesExist = false;
            foreach (var x in cpGlobal) if (await gameClientProfileService.ProfileExistsForGameClientAsync(x.Client!.Id, cancellationToken)) profilesExist = true;

            var detectedVersion = cpGlobal.Select(x => x.Client!.Version).FirstOrDefault() ?? GameClientConstants.UnknownVersion;
            var detectedVerStr = detectedVersion == GameClientConstants.UnknownVersion ? string.Empty : $" (Detected: v{detectedVersion})";

            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "Community Patch",
                Description = profilesExist
                    ? $"Update or reinstall existing Community Patch items{detectedVerStr}."
                    : $"Download & Install latest Community Patch v{cpLatestVersion}{detectedVerStr}.",
                IsSelected = true,
                Status = profilesExist ? "Installed" : "Detected",
                ActionLabel = profilesExist ? "Update / Reinstall" : "Download & Install",
                ActionType = profilesExist ? GameClientConstants.WizardActionTypes.Update : GameClientConstants.WizardActionTypes.CreateProfile,
                IconPath = CommunityOutpostConstants.LogoSource,
                Metadata = CommunityOutpostConstants.PublisherType,
                Version = detectedVersion == GameClientConstants.UnknownVersion ? cpLatestVersion : detectedVersion,
            });
        }
        else
        {
            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "Community Patch",
                Description = $"Download and install Community Patch v{cpLatestVersion} (Highly Recommended). Includes fixes, maps, and GenTool.",
                IsSelected = true,
                Status = "Missing",
                ActionLabel = "Download & Install",
                ActionType = GameClientConstants.WizardActionTypes.Install,
                IconPath = CommunityOutpostConstants.LogoSource,
                Metadata = CommunityOutpostConstants.PublisherType,
                Version = cpLatestVersion,
            });
        }

        // GeneralsOnline Item
        if (goGlobal.Count > 0)
        {
            bool profilesExist = false;
            foreach (var x in goGlobal) if (await gameClientProfileService.ProfileExistsForGameClientAsync(x.Client!.Id, cancellationToken)) profilesExist = true;

            var detectedVersion = goGlobal.Select(x => x.Client!.Version).FirstOrDefault() ?? GameClientConstants.UnknownVersion;
            var detectedVerStr = detectedVersion == GameClientConstants.UnknownVersion ? string.Empty : $" (Detected: v{detectedVersion})";

            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "Generals Online",
                Description = profilesExist
                    ? $"Update or reinstall existing Generals Online items{detectedVerStr}."
                    : $"Download & Install Generals Online v{goLatestVersion}{detectedVerStr}.",
                IsSelected = true,
                Status = profilesExist ? "Installed" : "Detected",
                ActionLabel = profilesExist ? "Update / Reinstall" : "Download & Install",
                ActionType = profilesExist ? GameClientConstants.WizardActionTypes.Update : GameClientConstants.WizardActionTypes.CreateProfile,
                IconPath = UriConstants.GeneralsOnlineLogoUri,
                Metadata = PublisherTypeConstants.GeneralsOnline,
                Version = detectedVersion == GameClientConstants.UnknownVersion ? goLatestVersion : detectedVersion,
            });
        }
        else
        {
            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "Generals Online",
                Description = $"Download and install Generals Online v{goLatestVersion} for multiplayer support.",
                IsSelected = false,
                Status = "Missing",
                ActionLabel = "Download & Install",
                ActionType = GameClientConstants.WizardActionTypes.Install,
                IconPath = UriConstants.GeneralsOnlineLogoUri,
                Metadata = PublisherTypeConstants.GeneralsOnline,
                Version = goLatestVersion,
            });
        }

        // SuperHackers Item
        if (shGlobal.Count > 0)
        {
            bool profilesExist = false;
            foreach (var x in shGlobal) if (await gameClientProfileService.ProfileExistsForGameClientAsync(x.Client!.Id, cancellationToken)) profilesExist = true;

            var detectedVersion = shGlobal.Select(x => x.Client!.Version).FirstOrDefault() ?? GameClientConstants.UnknownVersion;
            var detectedVerStr = detectedVersion == GameClientConstants.UnknownVersion ? string.Empty : $" (Detected: v{detectedVersion})";

            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "The Super Hackers",
                Description = profilesExist
                   ? $"Update or reinstall detected Super Hackers items{detectedVerStr}."
                   : $"Download & Install latest Super Hackers updates{detectedVerStr}.",
                IsSelected = true,
                Status = profilesExist ? "Installed" : "Detected",
                ActionLabel = profilesExist ? "Update / Reinstall" : "Download & Install",
                ActionType = profilesExist ? GameClientConstants.WizardActionTypes.Update : GameClientConstants.WizardActionTypes.CreateProfile,
                IconPath = UriConstants.SuperHackersLogoUri,
                Metadata = PublisherTypeConstants.TheSuperHackers,
                Version = detectedVersion == GameClientConstants.UnknownVersion ? shLatestVersion : detectedVersion,
            });
        }
        else
        {
            wizardItems.Add(new SetupWizardItemViewModel
            {
                Title = "The Super Hackers",
                Description = "Install The Super Hackers for advanced modding and features.",
                IsSelected = false,
                Status = "Missing",
                ActionLabel = "Install",
                ActionType = GameClientConstants.WizardActionTypes.Install,
                IconPath = UriConstants.SuperHackersLogoUri,
                Metadata = PublisherTypeConstants.TheSuperHackers,
            });
        }

        // 3. Presentation Phase: Show Wizard
        if (wizardItems.Count > 0)
        {
            var wizardVm = new SetupWizardViewModel(wizardItems);
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                var wizardView = new SetupWizardView
                {
                    DataContext = wizardVm,
                };

                await wizardView.ShowDialog(mainWindow);

                result.Confirmed = wizardVm.Confirmed;
            }
            else
            {
                logger.LogWarning("Could not resolve MainWindow for Setup Wizard.");
                result.Confirmed = false;
            }
        }

        // 4. Map decisions
        var cpItem = wizardItems.FirstOrDefault(x => x.Metadata as string == CommunityOutpostConstants.PublisherType);
        var goItem = wizardItems.FirstOrDefault(x => x.Metadata as string == PublisherTypeConstants.GeneralsOnline);
        var shItem = wizardItems.FirstOrDefault(x => x.Metadata as string == PublisherTypeConstants.TheSuperHackers);

        result.CommunityPatchAction = (result.Confirmed && cpItem?.IsSelected == true) ? cpItem.ActionType : GameClientConstants.WizardActionTypes.Decline;
        result.GeneralsOnlineAction = (result.Confirmed && goItem?.IsSelected == true) ? goItem.ActionType : GameClientConstants.WizardActionTypes.Decline;
        result.SuperHackersAction = (result.Confirmed && shItem?.IsSelected == true) ? shItem.ActionType : GameClientConstants.WizardActionTypes.Decline;

        return result;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private async Task<string> GetLatestVersionAsync(string publisher)
    {
        try
        {
            if (publisher == CommunityOutpostConstants.PublisherType)
            {
                var result = await communityOutpostDiscoverer.DiscoverAsync(new ContentSearchQuery());
                if (result.Success && result.Data != null)
                {
                    var version = result.Data.FirstOrDefault()?.Version;
                    if (!string.IsNullOrEmpty(version)) return version;
                }
            }
            else if (publisher == PublisherTypeConstants.GeneralsOnline)
            {
                var result = await generalsOnlineDiscoverer.DiscoverAsync(new ContentSearchQuery());
                if (result.Success && result.Data != null)
                {
                    var version = result.Data.FirstOrDefault()?.Version;
                    if (!string.IsNullOrEmpty(version)) return version;
                }
            }
            else if (publisher == PublisherTypeConstants.TheSuperHackers)
            {
                var query = new ContentSearchQuery
                {
                    AuthorName = SuperHackersConstants.GeneralsGameCodeOwner,
                    SearchTerm = SuperHackersConstants.GeneralsGameCodeRepo,
                };

                var result = await superHackersProvider.SearchAsync(query);
                if (result.Success && result.Data != null)
                {
                    var version = result.Data
                        .OrderByDescending(x => x.Version)
                        .FirstOrDefault()?.Version;
                    if (!string.IsNullOrEmpty(version)) return version;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch latest version for {Publisher}", publisher);
        }

        return GameClientConstants.UnknownVersion;
    }
}
